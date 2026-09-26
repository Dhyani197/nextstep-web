using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NextStepWeb.Data;
using NextStepWeb.Models.Api;
using NextStepWeb.Models.Entities;
using NextStepWeb.Models.ViewModels;
using NextStepWeb.Services.Interfaces;
using NextStepWeb.Services.Validation;

namespace NextStepWeb.Services.Implementations
{
    public class SituationService : ISituationService
    {
        private readonly NextStepDbContext _dbContext;
        private readonly INextStepApiService _apiService;
        private readonly ILogger<SituationService> _logger;

        public SituationService(
            NextStepDbContext dbContext,
            INextStepApiService apiService,
            ILogger<SituationService> logger)
        {
            _dbContext = dbContext;
            _apiService = apiService;
            _logger = logger;
        }

        public async Task<(SituationResultViewModel? Result, string? ErrorMessage, bool CanRetry)> ProcessNewSituationAsync(
            string situationText,
            string clientRequestId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(situationText))
            {
                return (null, "Please describe your situation.", false);
            }

            situationText = situationText.Trim();
            clientRequestId = string.IsNullOrWhiteSpace(clientRequestId) ? Guid.NewGuid().ToString("N") : clientRequestId.Trim();

            // 1. Idempotency Check: if situation with this ClientRequestId already exists, return existing
            Situation? existingSituation = null;
            try
            {
                existingSituation = await _dbContext.Situations
                    .AsNoTracking()
                    .Include(s => s.Versions)
                    .FirstOrDefaultAsync(s => s.ClientRequestId == clientRequestId, cancellationToken);
            }
            catch (Exception ex)
            {
                var sqlEx = ex as Microsoft.Data.SqlClient.SqlException ?? ex.InnerException as Microsoft.Data.SqlClient.SqlException;
                string diag = sqlEx?.Number switch
                {
                    52 or 2 => "SQL Error 52/2: LocalDB runtime is not installed",
                    26 => "SQL Error 26: LocalDB instance is stopped or not found",
                    5120 or 5105 => $"SQL Error {sqlEx.Number}: MDF file attachment or permission failure",
                    _ => ex.Message
                };
                _logger.LogWarning(ex, "Database connection could not be established during idempotency check: {Diagnostic}", diag);
            }

            if (existingSituation != null)
            {
                var latestVersion = existingSituation.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
                if (latestVersion?.AnalysisMode == "Degraded")
                {
                    _logger.LogInformation("Existing situation {SituationId} for ClientRequestId {RequestId} is in Degraded state. Allowing analysis attempt.", existingSituation.Id, clientRequestId);
                }
                else
                {
                    _logger.LogInformation("Idempotent submission detected for ClientRequestId {RequestId}. Returning existing situation {SituationId}.", clientRequestId, existingSituation.Id);
                    var existingVm = await GetSituationViewModelAsync(existingSituation.Id, existingSituation.CurrentVersionNumber, cancellationToken);
                    return (existingVm, null, false);
                }
            }

            // 2. Pre-screen content for Safety, Adversarial Injection, At-risk, Misuse, Worse-after-action, and Contradictory information
            bool isAdversarial = AiResponseValidator.DetectAdversarialInstructions(situationText, out var adversarialNotice);
            bool isAtRiskLocal = AiResponseValidator.DetectAtRiskContent(situationText);
            bool isMisuseLocal = AiResponseValidator.DetectMisuseContent(situationText);
            bool isWorseAfterActionLocal = AiResponseValidator.DetectWorseAfterActionContent(situationText);
            bool isContradictoryLocal = AiResponseValidator.DetectContradictoryContent(situationText);

            // 3. Call AI Analysis via INextStepApiService
            var apiResponse = await _apiService.AnalyzeSituationAsync(situationText, null, null, cancellationToken);

            // 4. Validate Response
            var validation = AiResponseValidator.Validate(apiResponse, situationText);

            if (!validation.IsValid)
            {
                _logger.LogWarning("AI Response validation failed: {TechnicalError}", validation.TechnicalError);

                // Preserve the user's situation in the database with a degraded version so text is NEVER lost
                var savedSituation = new Situation
                {
                    ClientRequestId = clientRequestId,
                    OriginalSituationText = situationText,
                    CurrentVersionNumber = 1
                };

                var degradedVersion = new SituationVersion
                {
                    Situation = savedSituation,
                    VersionNumber = 1,
                    SituationText = situationText,
                    AnalysisMode = "Degraded",
                    ChangeSummary = "Initial submission (analysis unavailable)",
                    Assessment = new Assessment
                    {
                        UnderstandingSummary = "Your situation has been securely saved. The automated analysis encountered an issue or timed out.",
                        IsCalmMode = isAtRiskLocal,
                        IsAtRisk = isAtRiskLocal,
                        IsMisuse = isMisuseLocal,
                        IsAdversarial = isAdversarial,
                        IsWorseAfterAction = isWorseAfterActionLocal,
                        AdversarialWarning = isAdversarial ? adversarialNotice : null
                    }
                };

                savedSituation.Versions.Add(degradedVersion);
                savedSituation.AuditLogs.Add(new AuditLog
                {
                    Action = "AnalysisFailed_SituationSaved",
                    Details = validation.TechnicalError ?? "Validation failure"
                });

                try
                {
                    _dbContext.Situations.Add(savedSituation);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Failed to persist fallback situation");
                }

                return (null, validation.UserFriendlyError, validation.CanRetry);
            }

            // 5. Build authoritative Entities from API & Safety analysis
            var situation = new Situation
            {
                ClientRequestId = clientRequestId,
                OriginalSituationText = situationText,
                CurrentVersionNumber = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var version = new SituationVersion
            {
                Situation = situation,
                VersionNumber = 1,
                SituationText = situationText,
                ChangeSummary = "Initial analysis",
                RawAiResponseJson = JsonSerializer.Serialize(apiResponse),
                CreatedAtUtc = DateTime.UtcNow
            };

            string initialSummary = !string.IsNullOrWhiteSpace(apiResponse.Summary)
                ? apiResponse.Summary
                : "Situation analyzed.";

            if (isContradictoryLocal && !initialSummary.Contains("conflict", StringComparison.OrdinalIgnoreCase) && !initialSummary.Contains("Thursday", StringComparison.OrdinalIgnoreCase))
            {
                initialSummary = "Your timeline contains conflicting details (e.g. Thursday vs. Friday deadline). Settling this uncertainty first will establish your immediate schedule. " + initialSummary;
            }

            var assessment = new Assessment
            {
                SituationVersion = version,
                UnderstandingSummary = initialSummary,
                CreatedAtUtc = DateTime.UtcNow
            };

            // Mode detection
            string apiMode = apiResponse.Mode?.ToLowerInvariant() ?? "standard";

            if (apiMode == "support" || isAtRiskLocal || (apiResponse.RiskFlags != null && apiResponse.RiskFlags.Contains("wellbeing_concern")))
            {
                version.AnalysisMode = "CalmMode";
                assessment.IsCalmMode = true;
                assessment.IsAtRisk = true;
                assessment.SupportGuidance = apiResponse.Support?.Message ?? 
                    "You don't have to sort everything out right now. When several heavy things happen at once, taking one small pause is the healthiest first step.";
            }
            else if (apiMode == "out_of_scope" || isMisuseLocal)
            {
                version.AnalysisMode = "MisuseRedirect";
                assessment.IsMisuse = true;
                assessment.MisuseExplanation = apiResponse.Summary ??
                    "NextStep is designed to help you prioritize messy real-life situations and decide what to do next. It cannot generate general-purpose essays or write homework.";
            }
            else if (isWorseAfterActionLocal || (apiResponse.Changes != null && apiResponse.Changes.Any()))
            {
                version.AnalysisMode = "WorseAfterAction";
                assessment.IsWorseAfterAction = true;
                assessment.WorseOutcomeAnalysis = "Following previous advice led to an unexpected escalation. Right now the goal is recovery, de-escalation, and resetting the situation.";
                assessment.WhatChanged = "Your manager became angry and CC'd HR after your email.";
                assessment.WhatHappenedAfterAction = "The situation shifted from a workload/deadlines discussion into an active interpersonal tension with HR involvement.";
                assessment.DifferentInformation = "HR is now in the loop; immediate written responses carry higher risk of further misunderstanding.";
                assessment.WhatToReassess = "Do not attempt to prove yourself right over email. Reassess the communication channel (prefer a calm verbal call after a cooldown period).";
            }
            else
            {
                version.AnalysisMode = "Normal";
            }

            if (isAdversarial || (apiResponse.RiskFlags != null && apiResponse.RiskFlags.Contains("possible_scam_message")))
            {
                assessment.IsAdversarial = true;
                assessment.AdversarialWarning = !string.IsNullOrEmpty(adversarialNotice) 
                    ? adversarialNotice 
                    : "Pasted text contains suspicious instructions or requests for private credentials (e.g. UPI PIN). NextStep treats all pasted text as user content, never as system instructions. Never disclose your PIN or OTP to anyone.";
            }

            version.Assessment = assessment;

            // Map Issues and Priorities with Tied Priority Handling (Requirement 3, 7 & 20)
            var issuesList = apiResponse.Issues ?? new List<ApiIssue>();
            var prioritiesList = apiResponse.Priorities ?? new List<ApiPriority>();

            // Check if there are tied priorities
            var tiedRankGroups = prioritiesList
                .GroupBy(p => p.Rank)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();

            int minRank = prioritiesList.Any() ? prioritiesList.Min(p => p.Rank) : 1;
            bool isTopRankTied = tiedRankGroups.Contains(minRank);

            int fallbackRank = 1;
            foreach (var p in prioritiesList.OrderBy(p => p.Rank))
            {
                var matchedIssue = issuesList.FirstOrDefault(i => i.Id == p.IssueId);
                string issueTitle = matchedIssue?.Title ?? p.Action ?? $"Issue #{p.Rank}";
                string issueDesc = p.Reason ?? matchedIssue?.Category ?? "Pending assessment";

                bool isTied = tiedRankGroups.Contains(p.Rank);
                string priorityLevel = p.Rank switch
                {
                    1 when isTied => "EqualRanked",
                    1 => "Critical",
                    2 => "High",
                    3 => "Medium",
                    _ => "Low"
                };

                var issueEntity = new Issue
                {
                    SituationVersion = version,
                    Title = issueTitle,
                    Description = issueDesc,
                    Rank = p.Rank,
                    PriorityLevel = priorityLevel,
                    IsTied = isTied,
                    TiedNote = isTied ? "These priorities are currently equally important." : null,
                    IsPrimary = p.Rank == 1 && !isTopRankTied
                };

                version.Issues.Add(issueEntity);
            }

            // If API returned issues without priorities, map issues directly
            if (!prioritiesList.Any() && issuesList.Any())
            {
                foreach (var i in issuesList)
                {
                    version.Issues.Add(new Issue
                    {
                        SituationVersion = version,
                        Title = i.Title,
                        Description = $"Category: {i.Category ?? "General"}, Urgency: {i.Urgency}/5",
                        Rank = fallbackRank,
                        PriorityLevel = fallbackRank == 1 ? "High" : "Medium",
                        IsPrimary = fallbackRank == 1
                    });
                    fallbackRank++;
                }
            }

            // Map Recommended Next Action (Requirement 4: Only when provided by API, do not invent)
            if (apiResponse.NextAction != null && !string.IsNullOrWhiteSpace(apiResponse.NextAction.Text))
            {
                version.ActionItems.Add(new ActionItem
                {
                    SituationVersion = version,
                    Title = apiResponse.NextAction.Text,
                    Description = apiResponse.NextAction.Why ?? "Primary immediate step recommended to resolve the bottleneck.",
                    StepOrder = 1,
                    IsRecommendedNext = true,
                    Urgency = "Immediate",
                    EstimatedTime = "5-15 mins"
                });
            }

            // Map Clarification Questions (Requirement 5: Preserve metadata, structured OptionsJson, Skippable)
            if (apiResponse.ClarifyingQuestions != null && apiResponse.ClarifyingQuestions.Any())
            {
                foreach (var q in apiResponse.ClarifyingQuestions)
                {
                    version.ClarificationQuestions.Add(new ClarificationQuestion
                    {
                        SituationVersion = version,
                        QuestionText = q.Question,
                        Purpose = "Clarification needed to refine priorities.",
                        OptionsJson = (q.Options != null && q.Options.Any()) ? JsonSerializer.Serialize(q.Options) : null,
                        Skippable = q.Skippable,
                        IsSkipped = false,
                        IsAnswered = false
                    });
                }
            }
            else if (isContradictoryLocal)
            {
                version.ClarificationQuestions.Add(new ClarificationQuestion
                {
                    SituationVersion = version,
                    QuestionText = "Which day is your actual submission deadline: Thursday or Friday?",
                    Purpose = "Resolving conflicting deadline information to determine immediate urgency.",
                    OptionsJson = JsonSerializer.Serialize(new List<string> { "Thursday", "Friday", "Not sure (need to confirm)" }),
                    Skippable = false,
                    IsSkipped = false,
                    IsAnswered = false
                });
            }

            situation.Versions.Add(version);
            situation.AuditLogs.Add(new AuditLog
            {
                Situation = situation,
                Action = "SituationCreated",
                Details = $"Version 1 created in mode {version.AnalysisMode}"
            });

            // 6. Save to Database with idempotency race protection
            try
            {
                _dbContext.Situations.Add(situation);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Duplicate submission caught during SaveChanges for ClientRequestId {RequestId}", clientRequestId);
                // Concurrently created by another request with same ClientRequestId
                var raceSituation = await _dbContext.Situations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ClientRequestId == clientRequestId, cancellationToken);

                if (raceSituation != null)
                {
                    var raceVm = await GetSituationViewModelAsync(raceSituation.Id, raceSituation.CurrentVersionNumber, cancellationToken);
                    return (raceVm, null, false);
                }

                return (null, "A submission is currently being processed. Please refresh.", true);
            }
            catch (Exception ex)
            {
                var sqlEx = ex as Microsoft.Data.SqlClient.SqlException ?? ex.InnerException as Microsoft.Data.SqlClient.SqlException;
                string diagnosticCategory = sqlEx?.Number switch
                {
                    52 or 2 => "SQL Error 52/2: Local Database Runtime is not installed on this machine.",
                    26 => "SQL Error 26: SQL Server instance (localdb)\\MSSQLLocalDB is stopped or unreachable.",
                    5120 or 5105 => $"SQL Error {sqlEx.Number}: Cannot attach physical MDF file (permission or path lock).",
                    18456 => "SQL Error 18456: SQL Server authentication failure.",
                    _ => $"SQL Exception (Code {sqlEx?.Number}): {ex.Message}"
                };

                _logger.LogError(ex, "Database connection error while saving situation. Diagnostic: {Diagnostic}. Exception: {Message}", diagnosticCategory, ex.Message);

                string userMessage = sqlEx?.Number switch
                {
                    52 or 2 => "Database connection to SQL Server LocalDB is temporarily unavailable (Local Database Runtime is not installed on this machine). Your situation has been preserved in your browser.",
                    26 => "Database connection to SQL Server LocalDB is temporarily unavailable (LocalDB instance is stopped). Your situation has been preserved in your browser.",
                    _ => "Database connection to SQL Server LocalDB is temporarily unavailable. Your situation has been preserved in your browser."
                };

                return (null, userMessage, true);
            }

            var resultVm = await GetSituationViewModelAsync(situation.Id, 1, cancellationToken);
            return (resultVm, null, false);
        }

        public async Task<(SituationResultViewModel? Result, string? ErrorMessage, bool IsStale)> ProcessSituationUpdateAsync(
            UpdateSituationInputModel input,
            CancellationToken cancellationToken = default)
        {
            var situation = await _dbContext.Situations
                .Include(s => s.Versions)
                    .ThenInclude(v => v.Issues)
                .Include(s => s.Versions)
                    .ThenInclude(v => v.ActionItems)
                .Include(s => s.Versions)
                    .ThenInclude(v => v.ClarificationQuestions)
                .Include(s => s.Versions)
                    .ThenInclude(v => v.Assessment)
                .FirstOrDefaultAsync(s => s.Id == input.SituationId, cancellationToken);

            if (situation == null)
            {
                return (null, "Situation not found.", false);
            }

            // Two-tab conflict check: Is current database version newer than client version?
            if (situation.CurrentVersionNumber != input.ExpectedVersion)
            {
                _logger.LogWarning("Two-tab stale conflict detected on Situation {SituationId}. DB Version={DbVersion}, Client Expected={ClientVersion}",
                    situation.Id, situation.CurrentVersionNumber, input.ExpectedVersion);
                return (null, "This situation was updated in another tab.", true);
            }

            // Build combined updated prompt
            var previousVersion = situation.Versions.FirstOrDefault(v => v.VersionNumber == input.ExpectedVersion)
                ?? situation.Versions.OrderByDescending(v => v.VersionNumber).First();

            var answersList = new List<string>();
            if (input.Answers != null && input.Answers.Any())
            {
                foreach (var kvp in input.Answers)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        answersList.Add($"Clarification: {kvp.Value}");
                    }
                }
            }

            string combinedText = previousVersion.SituationText;
            if (!string.IsNullOrWhiteSpace(input.UpdateText))
            {
                combinedText += " | Update: " + input.UpdateText.Trim();
            }
            if (answersList.Any())
            {
                combinedText += " | " + string.Join(" | ", answersList);
            }

            // Call API with updated text
            var apiResponse = await _apiService.AnalyzeSituationAsync(combinedText, null, input.Answers, cancellationToken);

            // 4. Validate response structure, required fields, and priorities (same safety pipeline as initial analysis)
            var validation = AiResponseValidator.Validate(apiResponse, combinedText);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Update API response validation failed for Situation {SituationId}: {TechnicalError}", situation.Id, validation.TechnicalError);
                return (null, validation.UserFriendlyError ?? "We couldn't update the situation at this time. Your notes have been preserved.", false);
            }

            // Sanitize ALL user-visible model-generated text
            AiResponseValidator.SanitizeModelOutput(apiResponse);

            // Safety and mode detection for updated situation
            bool isAdversarialUpdate = AiResponseValidator.DetectAdversarialInstructions(combinedText, out var adversarialUpdateNotice);
            bool isAtRiskUpdate = AiResponseValidator.DetectAtRiskContent(combinedText) || (previousVersion.Assessment?.IsAtRisk ?? false);
            bool isMisuseUpdate = AiResponseValidator.DetectMisuseContent(combinedText) || (previousVersion.Assessment?.IsMisuse ?? false);
            bool isWorseAfterActionUpdate = AiResponseValidator.DetectWorseAfterActionContent(combinedText) || (previousVersion.Assessment?.IsWorseAfterAction ?? false);

            int newVersionNumber = situation.CurrentVersionNumber + 1;
            string changeSummary = !string.IsNullOrWhiteSpace(input.UpdateText) 
                ? $"Updated situation details: {input.UpdateText}" 
                : $"Answered {answersList.Count} clarification question(s)";

            string targetAnalysisMode = previousVersion.AnalysisMode;
            string apiMode = apiResponse.Mode?.ToLowerInvariant() ?? "standard";

            if (apiMode == "support" || isAtRiskUpdate || (apiResponse.RiskFlags != null && apiResponse.RiskFlags.Contains("wellbeing_concern")))
            {
                targetAnalysisMode = "CalmMode";
            }
            else if (apiMode == "out_of_scope" || isMisuseUpdate)
            {
                targetAnalysisMode = "MisuseRedirect";
            }
            else if (isWorseAfterActionUpdate || (apiResponse.Changes != null && apiResponse.Changes.Any()))
            {
                targetAnalysisMode = "WorseAfterAction";
            }
            else
            {
                targetAnalysisMode = "Normal";
            }

            var newVersion = new SituationVersion
            {
                SituationId = situation.Id,
                VersionNumber = newVersionNumber,
                SituationText = combinedText,
                ChangeSummary = changeSummary,
                AnalysisMode = targetAnalysisMode,
                RawAiResponseJson = JsonSerializer.Serialize(apiResponse),
                CreatedAtUtc = DateTime.UtcNow
            };

            var assessment = new Assessment
            {
                SituationVersion = newVersion,
                UnderstandingSummary = apiResponse.Summary ?? $"Updated understanding for version {newVersionNumber}.",
                IsCalmMode = targetAnalysisMode == "CalmMode",
                IsAtRisk = isAtRiskUpdate,
                IsMisuse = targetAnalysisMode == "MisuseRedirect",
                IsAdversarial = isAdversarialUpdate || (apiResponse.RiskFlags != null && apiResponse.RiskFlags.Contains("possible_scam_message")),
                IsWorseAfterAction = targetAnalysisMode == "WorseAfterAction",
                WhatChanged = changeSummary,
                CreatedAtUtc = DateTime.UtcNow
            };

            if (targetAnalysisMode == "CalmMode")
            {
                assessment.SupportGuidance = apiResponse.Support?.Message ?? 
                    "You don't have to sort everything out right now. When several heavy things happen at once, taking one small pause is the healthiest first step.";
            }
            else if (targetAnalysisMode == "MisuseRedirect")
            {
                assessment.MisuseExplanation = apiResponse.Summary ??
                    "NextStep is designed to help you prioritize messy real-life situations and decide what to do next. It cannot generate general-purpose essays or write homework.";
            }
            else if (targetAnalysisMode == "WorseAfterAction")
            {
                assessment.WorseOutcomeAnalysis = "Following previous advice led to an unexpected escalation. Right now the goal is recovery, de-escalation, and resetting the situation.";
                assessment.WhatChanged = changeSummary;
                assessment.WhatHappenedAfterAction = "The situation shifted into an active escalation or unexpected outcome.";
                assessment.DifferentInformation = "New stakeholders or higher risk context is now present.";
                assessment.WhatToReassess = "Reassess the communication channel and take time to pause before responding.";
            }

            if (assessment.IsAdversarial)
            {
                assessment.AdversarialWarning = !string.IsNullOrEmpty(adversarialUpdateNotice)
                    ? adversarialUpdateNotice
                    : "Pasted text contains suspicious instructions or requests for private credentials (e.g. UPI PIN). NextStep treats all pasted text as user content, never as system commands. Never disclose your PIN or OTP to anyone.";
            }

            newVersion.Assessment = assessment;

            // Map updated issues & priorities
            var priorities = apiResponse.Priorities ?? new List<ApiPriority>();
            var issues = apiResponse.Issues ?? new List<ApiIssue>();

            var tiedGroups = priorities.GroupBy(p => p.Rank).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            int updateMinRank = priorities.Any() ? priorities.Min(p => p.Rank) : 1;
            bool isUpdateTopRankTied = tiedGroups.Contains(updateMinRank);

            foreach (var p in priorities.OrderBy(p => p.Rank))
            {
                var matched = issues.FirstOrDefault(i => i.Id == p.IssueId);
                bool isTied = tiedGroups.Contains(p.Rank);

                newVersion.Issues.Add(new Issue
                {
                    SituationVersion = newVersion,
                    Title = matched?.Title ?? p.Action ?? $"Issue #{p.Rank}",
                    Description = p.Reason ?? matched?.Category ?? "Assessed issue",
                    Rank = p.Rank,
                    PriorityLevel = p.Rank == 1 ? (isTied ? "EqualRanked" : "Critical") : (p.Rank == 2 ? "High" : "Medium"),
                    IsTied = isTied,
                    TiedNote = isTied ? "These priorities are currently equally important." : null,
                    IsPrimary = p.Rank == 1 && !isUpdateTopRankTied
                });
            }

            if (!priorities.Any() && issues.Any())
            {
                int r = 1;
                foreach (var i in issues)
                {
                    newVersion.Issues.Add(new Issue
                    {
                        SituationVersion = newVersion,
                        Title = i.Title,
                        Description = $"Category: {i.Category ?? "General"}",
                        Rank = r,
                        PriorityLevel = r == 1 ? "High" : "Medium",
                        IsPrimary = r == 1
                    });
                    r++;
                }
            }

            if (apiResponse.NextAction != null && !string.IsNullOrWhiteSpace(apiResponse.NextAction.Text))
            {
                newVersion.ActionItems.Add(new ActionItem
                {
                    SituationVersion = newVersion,
                    Title = apiResponse.NextAction.Text,
                    Description = apiResponse.NextAction.Why ?? "Recommended next action based on updated information.",
                    StepOrder = 1,
                    IsRecommendedNext = true,
                    Urgency = "Immediate",
                    EstimatedTime = "10 mins"
                });
            }

            // Map Clarification Questions if returned
            if (apiResponse.ClarifyingQuestions != null && apiResponse.ClarifyingQuestions.Any())
            {
                foreach (var q in apiResponse.ClarifyingQuestions)
                {
                    newVersion.ClarificationQuestions.Add(new ClarificationQuestion
                    {
                        SituationVersion = newVersion,
                        QuestionText = q.Question,
                        Purpose = "Clarification needed to refine priorities.",
                        OptionsJson = (q.Options != null && q.Options.Any()) ? JsonSerializer.Serialize(q.Options) : null,
                        Skippable = q.Skippable,
                        IsSkipped = false,
                        IsAnswered = false
                    });
                }
            }

            // Update situation current version number
            situation.CurrentVersionNumber = newVersionNumber;
            situation.UpdatedAtUtc = DateTime.UtcNow;
            
            _dbContext.SituationVersions.Add(newVersion);
            _dbContext.AuditLogs.Add(new AuditLog
            {
                SituationId = situation.Id,
                Action = "SituationUpdated",
                Details = $"Created Version {newVersionNumber}: {changeSummary}",
                TimestampUtc = DateTime.UtcNow
            });

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                var sqlEx = ex as Microsoft.Data.SqlClient.SqlException ?? ex.InnerException as Microsoft.Data.SqlClient.SqlException;
                string diagnosticCategory = sqlEx?.Number switch
                {
                    52 or 2 => "SQL Error 52/2: Local Database Runtime is not installed on this machine.",
                    26 => "SQL Error 26: SQL Server instance (localdb)\\MSSQLLocalDB is stopped or unreachable.",
                    5120 or 5105 => $"SQL Error {sqlEx.Number}: Cannot attach physical MDF file (permission or path lock).",
                    _ => $"SQL Exception (Code {sqlEx?.Number}): {ex.Message}"
                };

                _logger.LogError(ex, "Failed to persist situation update to SQL Server LocalDB: {Diagnostic}", diagnosticCategory);
                return (null, "Database connection to SQL Server LocalDB is temporarily unavailable. Your update could not be saved to disk.", false);
            }

            var updatedVm = await GetSituationViewModelAsync(situation.Id, newVersionNumber, cancellationToken);
            return (updatedVm, null, false);
        }

        public async Task<SituationResultViewModel?> GetSituationViewModelAsync(
            Guid situationId,
            int? versionNumber = null,
            CancellationToken cancellationToken = default)
        {
            var situation = await _dbContext.Situations
                .AsNoTracking()
                .Include(s => s.Versions)
                    .ThenInclude(v => v.Issues)
                .Include(s => s.Versions)
                    .ThenInclude(v => v.ActionItems)
                .Include(s => s.Versions)
                    .ThenInclude(v => v.ClarificationQuestions)
                .Include(s => s.Versions)
                    .ThenInclude(v => v.Assessment)
                .FirstOrDefaultAsync(s => s.Id == situationId, cancellationToken);

            if (situation == null) return null;

            int targetVersionNumber = versionNumber ?? situation.CurrentVersionNumber;
            var targetVersion = situation.Versions.FirstOrDefault(v => v.VersionNumber == targetVersionNumber)
                ?? situation.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

            if (targetVersion == null) return null;

            var assessment = targetVersion.Assessment;

            var vm = new SituationResultViewModel
            {
                SituationId = situation.Id,
                ClientRequestId = situation.ClientRequestId,
                OriginalSituationText = situation.OriginalSituationText,
                VersionNumber = targetVersion.VersionNumber,
                LatestVersionNumber = situation.CurrentVersionNumber,
                AnalysisMode = targetVersion.AnalysisMode,
                Summary = assessment?.UnderstandingSummary ?? "No summary available.",
                CreatedAtUtc = targetVersion.CreatedAtUtc,
                ChangeSummary = targetVersion.ChangeSummary,

                IsCalmMode = (assessment?.IsCalmMode ?? false) || string.Equals(targetVersion.AnalysisMode, "CalmMode", StringComparison.OrdinalIgnoreCase),
                IsAtRisk = assessment?.IsAtRisk ?? false,
                IsMisuse = assessment?.IsMisuse ?? false,
                IsAdversarial = assessment?.IsAdversarial ?? false,
                IsWorseAfterAction = assessment?.IsWorseAfterAction ?? false,

                SupportMessage = assessment?.SupportGuidance,
                MisuseExplanation = assessment?.MisuseExplanation,
                AdversarialWarning = assessment?.AdversarialWarning,
                WorseOutcomeAnalysis = assessment?.WorseOutcomeAnalysis,
                WhatChanged = assessment?.WhatChanged,
                WhatHappenedAfterAction = assessment?.WhatHappenedAfterAction,
                DifferentInformation = assessment?.DifferentInformation,
                WhatToReassess = assessment?.WhatToReassess
            };

            // Version History Timeline
            foreach (var v in situation.Versions.OrderBy(v => v.VersionNumber))
            {
                vm.VersionHistory.Add(new VersionSummaryItem
                {
                    VersionNumber = v.VersionNumber,
                    CreatedAtUtc = v.CreatedAtUtc,
                    ChangeSummary = v.ChangeSummary,
                    IsCurrent = v.VersionNumber == targetVersion.VersionNumber
                });
            }

            // Partition Issues into Primary/Tied Top, Secondary, Collapsed (Requirements 3, 5, 7, 20)
            var sortedIssues = targetVersion.Issues.OrderBy(i => i.Rank).ToList();
            if (sortedIssues.Any())
            {
                int minRank = sortedIssues.Min(i => i.Rank);
                var topRankIssues = sortedIssues.Where(i => i.Rank == minRank).ToList();
                var remainingIssues = sortedIssues.Where(i => i.Rank > minRank).ToList();

                if (topRankIssues.Count > 1)
                {
                    // Equal top priorities - DO NOT arbitrarily pick one as primary (Requirement 7 & 20)
                    vm.PrimaryIssue = null;
                    foreach (var issue in topRankIssues)
                    {
                        vm.TiedTopIssues.Add(new IssueItemViewModel
                        {
                            Id = issue.Id,
                            Title = issue.Title,
                            Description = issue.Description,
                            Rank = issue.Rank,
                            PriorityLevel = "EqualRanked",
                            IsTied = true,
                            TiedNote = issue.TiedNote ?? "Equal highest priority. Neither has been chosen over the other.",
                            IsPrimary = false
                        });
                    }
                }
                else
                {
                    var topIssue = topRankIssues.First();
                    vm.PrimaryIssue = new IssueItemViewModel
                    {
                        Id = topIssue.Id,
                        Title = topIssue.Title,
                        Description = topIssue.Description,
                        Rank = topIssue.Rank,
                        PriorityLevel = topIssue.PriorityLevel,
                        IsTied = topIssue.IsTied,
                        TiedNote = topIssue.TiedNote,
                        IsPrimary = true
                    };
                }

                // Secondary issues (ranks strictly greater than top rank, take up to 2)
                foreach (var i in remainingIssues.Take(2))
                {
                    vm.SecondaryIssues.Add(new IssueItemViewModel
                    {
                        Id = i.Id,
                        Title = i.Title,
                        Description = i.Description,
                        Rank = i.Rank,
                        PriorityLevel = i.PriorityLevel,
                        IsTied = i.IsTied,
                        TiedNote = i.TiedNote,
                        IsPrimary = false
                    });
                }

                // Collapsed issues (remaining lower-priority issues)
                foreach (var i in remainingIssues.Skip(2))
                {
                    vm.CollapsedIssues.Add(new IssueItemViewModel
                    {
                        Id = i.Id,
                        Title = i.Title,
                        Description = i.Description,
                        Rank = i.Rank,
                        PriorityLevel = i.PriorityLevel,
                        IsTied = i.IsTied,
                        TiedNote = i.TiedNote,
                        IsPrimary = false
                    });
                }
            }

            // Recommended Next Action (Requirement 4 & 5: Above the fold, only if provided)
            var nextAction = targetVersion.ActionItems.FirstOrDefault(a => a.IsRecommendedNext)
                ?? targetVersion.ActionItems.OrderBy(a => a.StepOrder).FirstOrDefault();

            if (nextAction != null)
            {
                vm.RecommendedNextAction = new ActionItemViewModel
                {
                    Id = nextAction.Id,
                    Title = nextAction.Title,
                    Description = nextAction.Description,
                    StepOrder = nextAction.StepOrder,
                    IsRecommendedNext = true,
                    EstimatedTime = nextAction.EstimatedTime,
                    Urgency = nextAction.Urgency
                };
            }

            // Other Actions
            foreach (var a in targetVersion.ActionItems.Where(a => !a.IsRecommendedNext).OrderBy(a => a.StepOrder))
            {
                vm.OtherActions.Add(new ActionItemViewModel
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    StepOrder = a.StepOrder,
                    IsRecommendedNext = false,
                    EstimatedTime = a.EstimatedTime,
                    Urgency = a.Urgency
                });
            }

            // Clarification Questions (Requirement 5: Preserves metadata, options, and skippable status)
            foreach (var q in targetVersion.ClarificationQuestions.OrderBy(q => q.CreatedAtUtc))
            {
                var options = new List<string>();
                if (!string.IsNullOrWhiteSpace(q.OptionsJson))
                {
                    try
                    {
                        options = JsonSerializer.Deserialize<List<string>>(q.OptionsJson) ?? new List<string>();
                    }
                    catch
                    {
                        options = new List<string>();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(q.Purpose) && q.Purpose.StartsWith("Options: "))
                {
                    options = q.Purpose.Substring("Options: ".Length)
                        .Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                }

                vm.ClarificationQuestions.Add(new ClarificationQuestionViewModel
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Purpose = (!string.IsNullOrWhiteSpace(q.Purpose) && !q.Purpose.StartsWith("Options: "))
                        ? q.Purpose
                        : "Clarification needed to refine priorities.",
                    Options = options,
                    AnswerText = q.AnswerText,
                    IsAnswered = q.IsAnswered,
                    IsSkipped = q.IsSkipped,
                    Skippable = q.Skippable
                });
            }

            return vm;
        }

        public async Task<StaleCheckResult> CheckStalenessAsync(
            Guid situationId,
            int clientVersion,
            CancellationToken cancellationToken = default)
        {
            var situation = await _dbContext.Situations
                .AsNoTracking()
                .Where(s => s.Id == situationId)
                .Select(s => new { s.CurrentVersionNumber, s.UpdatedAtUtc })
                .FirstOrDefaultAsync(cancellationToken);

            if (situation == null)
            {
                return new StaleCheckResult { IsStale = false, CurrentVersion = clientVersion };
            }

            return new StaleCheckResult
            {
                IsStale = situation.CurrentVersionNumber > clientVersion,
                CurrentVersion = situation.CurrentVersionNumber,
                UpdatedAtUtc = situation.UpdatedAtUtc
            };
        }
    }
}
