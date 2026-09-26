using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NextStepWeb.Models.Api
{
    public class NextStepApiRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("situation_id")]
        public string? SituationId { get; set; }

        [JsonPropertyName("answers")]
        public Dictionary<string, string>? Answers { get; set; }
    }

    public class NextStepApiResponse
    {
        [JsonPropertyName("situation_id")]
        public string? SituationId { get; set; }

        [JsonPropertyName("version")]
        public int? Version { get; set; }

        [JsonPropertyName("server_time")]
        public string? ServerTime { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("issues")]
        public List<ApiIssue>? Issues { get; set; }

        [JsonPropertyName("priorities")]
        public List<ApiPriority>? Priorities { get; set; }

        [JsonPropertyName("next_action")]
        public ApiNextAction? NextAction { get; set; }

        [JsonPropertyName("clarifying_questions")]
        public List<ApiClarifyingQuestion>? ClarifyingQuestions { get; set; }

        [JsonPropertyName("missing_information")]
        public List<string>? MissingInformation { get; set; }

        [JsonPropertyName("risk_flags")]
        public List<string>? RiskFlags { get; set; }

        [JsonPropertyName("confidence")]
        public ApiConfidence? Confidence { get; set; }

        [JsonPropertyName("changes")]
        public List<ApiChange>? Changes { get; set; }

        [JsonPropertyName("support")]
        public ApiSupport? Support { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }
    }

    public class ApiIssue
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("urgency")]
        public int Urgency { get; set; } = 3;

        [JsonPropertyName("deadline")]
        public string? Deadline { get; set; }

        [JsonPropertyName("depends_on")]
        public List<string>? DependsOn { get; set; }
    }

    public class ApiPriority
    {
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("issue_id")]
        public string? IssueId { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("estimated_minutes")]
        public int? EstimatedMinutes { get; set; }
    }

    public class ApiNextAction
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("issue_id")]
        public string? IssueId { get; set; }

        [JsonPropertyName("why")]
        public string? Why { get; set; }
    }

    public class ApiClarifyingQuestion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;

        [JsonPropertyName("options")]
        public List<string>? Options { get; set; }

        [JsonPropertyName("skippable")]
        public bool Skippable { get; set; } = true;
    }

    public class ApiConfidence
    {
        [JsonPropertyName("level")]
        public string? Level { get; set; }

        [JsonPropertyName("reasons")]
        public List<string>? Reasons { get; set; }
    }

    public class ApiChange
    {
        [JsonPropertyName("field")]
        public string? Field { get; set; }

        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    public class ApiSupport
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("resources")]
        public List<ApiSupportResource>? Resources { get; set; }

        [JsonPropertyName("offer_to_continue")]
        public string? OfferToContinue { get; set; }
    }

    public class ApiSupportResource
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("contact")]
        public string? Contact { get; set; }

        [JsonPropertyName("hours")]
        public string? Hours { get; set; }
    }
}
