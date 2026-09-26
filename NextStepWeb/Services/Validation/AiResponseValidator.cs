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

            if (!string.IsNullOrWhiteSpace(response.Error))
            {
                result.IsValid = false;
                result.TechnicalError = $"API error: {response.Error} - {response.Message}";
                if (response.Error.Equals("rate_limited", StringComparison.OrdinalIgnoreCase))
                {
                    result.UserFriendlyError = "Analysis is temporarily busy. Your situation is saved and can be analysed again.";
                }
                else if (response.Error.Equals("timeout", StringComparison.OrdinalIgnoreCase))
                {
                    result.UserFriendlyError = "The analysis is taking longer than expected. Your situation has been saved. Try the analysis again.";
                }
                else
                {
                    result.UserFriendlyError = "The analysis returned incomplete information, so we did not show it. Your situation has been saved.";
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

            // Check for missing next_action in standard mode when issues exist
            if (mode == "standard" && (response.Issues != null && response.Issues.Any()))
            {
                if (response.NextAction == null || string.IsNullOrWhiteSpace(response.NextAction.Text))
                {
                    result.Warnings.Add("Recommended next action was not returned directly; falling back to top priority action.");
                    result.IsDegraded = true;
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
    }
}
