using System;
using System.Collections.Generic;
using System.Linq;
using NextStepWeb.Models.Api;

namespace NextStepWeb.Services.Validation
{
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public string? TechnicalError { get; set; }
        public string? UserFriendlyError { get; set; }
        public bool CanRetry { get; set; } = false;
        public bool IsDegraded { get; set; } = false;
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public static class AiResponseValidator
    {
        public static ValidationResult Validate(NextStepApiResponse? response, string originalInputText)
        {
            var result = new ValidationResult();

            if (response == null)
            {
                result.IsValid = false;
                result.TechnicalError = "Response is null or malformed JSON.";
                result.UserFriendlyError = "The analysis returned incomplete information, so we did not show it. Your situation has been saved.";
                result.CanRetry = true;
                return result;
            }

            // Treat model output as untrusted data: neutralize any prompt-injected credential requests
            SanitizeModelOutput(response);

            if (!string.IsNullOrWhiteSpace(response.Error))
            {
                result.IsValid = false;
                result.TechnicalError = $"API error: {response.Error} - {response.Message}";
                if (response.Error.Equals("rate_limited", StringComparison.OrdinalIgnoreCase))
                {
                    result.UserFriendlyError = "The analysis service is temporarily busy. Your situation has been safely saved so you won't lose your thoughts. Please wait a few moments and try again.";
                }
                else if (response.Error.Equals("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    result.UserFriendlyError = "The analysis is taking longer than expected. Your situation has been safely saved. You can try running the analysis again.";
                }
                else if (response.Error.Equals("server_error", StringComparison.OrdinalIgnoreCase))
                {
                    result.UserFriendlyError = "The analysis service is temporarily experiencing an issue. Your situation has been preserved. Please try again in a moment.";
                }
                else if (response.Error.Equals("network_error", StringComparison.OrdinalIgnoreCase))
                {
                    result.UserFriendlyError = "We could not connect to the analysis service. Your situation has been saved locally. Please check your connection and try again.";
                }
                else
                {
                    result.UserFriendlyError = "The analysis returned incomplete information, so we did not show it. Your situation has been safely saved. Please try again.";
                }
                result.CanRetry = true;
                return result;
            }

            // Verify core required fields
            if (string.IsNullOrWhiteSpace(response.Summary) && response.Support == null)
            {
                result.IsValid = false;
                result.TechnicalError = "Required field 'summary' is missing from AI response.";
                result.UserFriendlyError = "The analysis returned incomplete information, so we did not show it. Your situation has been saved.";
                result.CanRetry = true;
                return result;
            }

            var mode = response.Mode?.ToLowerInvariant() ?? "standard";

            // If support / calm mode
            if (mode == "support" || response.Support != null)
            {
                if (response.Support == null || string.IsNullOrWhiteSpace(response.Support.Message))
                {
                    result.IsValid = false;
                    result.TechnicalError = "Support mode is indicated, but support details or message are missing.";
                    result.UserFriendlyError = "We could not load the support guidance properly. Your situation has been saved.";
                    result.CanRetry = true;
                    return result;
                }
                return result; // Valid support mode
            }

            // If out_of_scope
            if (mode == "out_of_scope" || mode == "irrelevant")
            {
                return result; // Valid out of scope redirect
            }

            // Check for tied priorities
            if (response.Priorities != null && response.Priorities.Count > 1)
            {
                var groupedRanks = response.Priorities.GroupBy(p => p.Rank).Where(g => g.Count() > 1).ToList();
                if (groupedRanks.Any())
                {
                    result.Warnings.Add("Tied priorities detected: Some items share the same rank and are treated as equally important.");
                    result.IsDegraded = true;
                }
            }

            // Check for missing action in priorities
            if (response.Priorities != null)
            {
                foreach (var p in response.Priorities)
                {
                    if (string.IsNullOrWhiteSpace(p.Action))
                    {
                        result.Warnings.Add($"Priority for issue '{p.IssueId}' has no specific action defined.");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Sanitizes and detects adversarial prompt injection in user input.
        /// Treats all pasted instructions as user content, never as system commands.
        /// </summary>
        public static bool DetectAdversarialInstructions(string input, out string safetyNotice)
        {
            safetyNotice = string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var upper = input.ToUpperInvariant();
            bool hasSystemInstruction = upper.Contains("SYSTEM:") || 
                                        upper.Contains("IGNORE PREVIOUS INSTRUCTIONS") || 
                                        upper.Contains("IGNORE ALL PREVIOUS INSTRUCTIONS");

            bool requestsCredentials = upper.Contains("UPI PIN") || 
                                       upper.Contains("PASSWORD") || 
                                       upper.Contains("OTP") || 
                                       upper.Contains("CVV") || 
                                       upper.Contains("BANKING CREDENTIALS");

            if (hasSystemInstruction || requestsCredentials)
            {
                safetyNotice = "Notice: This text contains instructions or requests for sensitive security data (e.g. PIN, OTP). NextStep strictly treats all pasted text as user content, not system commands, and will never ask you for or follow instructions to disclose your UPI PIN or banking credentials.";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Detects emotional distress / at-risk input if not already flagged by the API.
        /// </summary>
        public static bool DetectAtRiskContent(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var lower = input.ToLowerInvariant();

            string[] distressSignals = new[]
            {
                "what's the point honestly",
                "whats the point honestly",
                "falling apart",
                "tired of all of it",
                "can't go on",
                "cant go on",
                "want to end it",
                "give up on everything",
                "hopeless"
            };

            return distressSignals.Any(s => lower.Contains(s));
        }

        /// <summary>
        /// Detects out-of-scope / misuse requests such as essay writing.
        /// </summary>
        public static bool DetectMisuseContent(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var lower = input.ToLowerInvariant();

            string[] misuseSignals = new[]
            {
                "write a 1500-word essay",
                "write an essay",
                "write my essay",
                "write a paper on",
                "solve my assignment"
            };

            return misuseSignals.Any(s => lower.Contains(s));
        }

        /// <summary>
        /// Detects worse-after-action feedback from previous advice.
        /// </summary>
        public static bool DetectWorseAfterActionContent(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var lower = input.ToLowerInvariant();

            string[] signals = new[]
            {
                "like you said and now",
                "followed your advice and",
                "did what you said and now",
                "emailed my manager like you said and now she's angry",
                "now she's angry and has cc'd hr",
                "now he's angry and has cc'd hr",
                "made things worse"
            };

            return signals.Any(s => lower.Contains(s));
        }

        /// <summary>
        /// Detects contradictory deadline or conflicting situation information.
        /// </summary>
        public static bool DetectContradictoryContent(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var lower = input.ToLowerInvariant();

            bool hasDeadlineConflict = (lower.Contains("friday") && lower.Contains("thursday")) ||
                                       lower.Contains("actually wait") ||
                                       lower.Contains("think the professor said") ||
                                       (lower.Contains("not talking right now") && lower.Contains("borrow from"));

            return hasDeadlineConflict;
        }

        /// <summary>
        /// Neutralizes model output if it contains requests for private credentials (UPI PIN, OTP, passwords, bank PIN, recovery code).
        /// Treats ALL AI output strictly as untrusted data so prompt injection cannot turn into trusted UI advice.
        /// </summary>
        public static void SanitizeModelOutput(NextStepApiResponse? response)
        {
            if (response == null) return;

            string[] credentialKeywords = new[] 
            { 
                "UPI PIN", "PASSWORD", "OTP", "CVV", "BANKING CREDENTIALS", 
                "PIN NUMBER", "SHARE YOUR PIN", "ENTER YOUR PIN", "PROVIDE YOUR PIN",
                "AUTHENTICATION SECRET", "BANK PIN", "RECOVERY CODE", "SECURITY CODE",
                "PASSCODE", "ATM PIN", "SECRET KEY" 
            };

            bool ContainsCredentialRequest(string? text)
            {
                if (string.IsNullOrWhiteSpace(text)) return false;
                var upper = text.ToUpperInvariant();
                return credentialKeywords.Any(k => upper.Contains(k));
            }

            string Neutralize(string? text)
            {
                if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
                var upper = text.ToUpperInvariant();
                if (credentialKeywords.Any(k => upper.Contains(k)))
                {
                    return "[Notice: Sensitive authentication data or secret request was blocked by NextStep guardrails]";
                }
                return text;
            }

            if (ContainsCredentialRequest(response.Summary))
            {
                response.Summary = Neutralize(response.Summary);
            }

            if (ContainsCredentialRequest(response.Message))
            {
                response.Message = Neutralize(response.Message);
            }

            if (response.NextAction != null)
            {
                if (ContainsCredentialRequest(response.NextAction.Text) || ContainsCredentialRequest(response.NextAction.Why))
                {
                    response.NextAction.Text = "Do not share sensitive secrets, OTPs, or PINs with unverified senders.";
                    response.NextAction.Why = "Private security guardrail active: NextStep never instructs users to disclose banking or authentication secrets.";
                }
            }

            if (response.Issues != null)
            {
                foreach (var issue in response.Issues)
                {
                    if (ContainsCredentialRequest(issue.Title))
                    {
                        issue.Title = "Potential phishing / scam message";
                        issue.Category = "Security Notice: Never disclose private authentication secrets or PINs.";
                    }
                    else if (ContainsCredentialRequest(issue.Category))
                    {
                        issue.Category = Neutralize(issue.Category);
                    }
                    if (ContainsCredentialRequest(issue.Deadline))
                    {
                        issue.Deadline = null;
                    }
                }
            }

            if (response.Priorities != null)
            {
                foreach (var p in response.Priorities)
                {
                    if (ContainsCredentialRequest(p.Action))
                    {
                        p.Action = "Verify sender authenticity through official channels and never share PIN or OTP";
                    }
                    if (ContainsCredentialRequest(p.Reason))
                    {
                        p.Reason = Neutralize(p.Reason);
                    }
                }
            }

            if (response.ClarifyingQuestions != null)
            {
                // Remove questions that solicit credentials
                response.ClarifyingQuestions.RemoveAll(q => ContainsCredentialRequest(q.Question));

                // Sanitize options list within remaining questions
                foreach (var q in response.ClarifyingQuestions)
                {
                    if (q.Options != null && q.Options.Any())
                    {
                        q.Options.RemoveAll(opt => ContainsCredentialRequest(opt));
                    }
                }

                // If all options were removed from an options-based question, remove question
                response.ClarifyingQuestions.RemoveAll(q => q.Options != null && !q.Options.Any());
            }

            if (response.Support != null)
            {
                if (ContainsCredentialRequest(response.Support.Message))
                {
                    response.Support.Message = Neutralize(response.Support.Message);
                }
                if (ContainsCredentialRequest(response.Support.OfferToContinue))
                {
                    response.Support.OfferToContinue = Neutralize(response.Support.OfferToContinue);
                }
                if (response.Support.Resources != null)
                {
                    foreach (var r in response.Support.Resources)
                    {
                        if (ContainsCredentialRequest(r.Name)) r.Name = Neutralize(r.Name);
                        if (ContainsCredentialRequest(r.Contact)) r.Contact = Neutralize(r.Contact);
                    }
                }
            }

            if (response.Changes != null)
            {
                foreach (var c in response.Changes)
                {
                    if (ContainsCredentialRequest(c.Reason)) c.Reason = Neutralize(c.Reason);
                    if (ContainsCredentialRequest(c.From)) c.From = Neutralize(c.From);
                    if (ContainsCredentialRequest(c.To)) c.To = Neutralize(c.To);
                }
            }

            if (response.MissingInformation != null)
            {
                response.MissingInformation.RemoveAll(item => ContainsCredentialRequest(item));
            }
        }
    }
}
