using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NextStepWeb.Models.Api;

namespace NextStepWeb.Models.ViewModels
{
    public class SituationInputViewModel
    {
        [Required(ErrorMessage = "Please describe what is happening.")]
        [MinLength(5, ErrorMessage = "Please provide a bit more detail about your situation.")]
        [Display(Name = "Describe your situation")]
        public string SituationText { get; set; } = string.Empty;

        public string ClientRequestId { get; set; } = Guid.NewGuid().ToString("N");

        public string? SelectedScenarioId { get; set; }

        public string? ErrorMessage { get; set; }
        public string? WarningMessage { get; set; }
        public bool CanRetry { get; set; }
    }

    public class SituationResultViewModel
    {
        public Guid SituationId { get; set; }
        public string ClientRequestId { get; set; } = string.Empty;
        public string OriginalSituationText { get; set; } = string.Empty;
        public int VersionNumber { get; set; } = 1;
        public int LatestVersionNumber { get; set; } = 1;
        public List<VersionSummaryItem> VersionHistory { get; set; } = new List<VersionSummaryItem>();

        public string AnalysisMode { get; set; } = "Normal";
        public string Summary { get; set; } = string.Empty;

        // Special modes
        public bool IsCalmMode { get; set; }
        public bool IsAtRisk { get; set; }
        public bool IsMisuse { get; set; }
        public bool IsAdversarial { get; set; }
        public bool IsWorseAfterAction { get; set; }

        public string? SupportMessage { get; set; }
        public List<ApiSupportResource>? SupportResources { get; set; }
        public string? SupportOfferToContinue { get; set; }

        public string? MisuseExplanation { get; set; }
        public string? AdversarialWarning { get; set; }
        public string? WorseOutcomeAnalysis { get; set; }
        public string? WhatChanged { get; set; }
        public string? WhatHappenedAfterAction { get; set; }
        public string? DifferentInformation { get; set; }
        public string? WhatToReassess { get; set; }

        // Information Hierarchy: Primary, Secondary, Collapsed
        public IssueItemViewModel? PrimaryIssue { get; set; }
        public ActionItemViewModel? RecommendedNextAction { get; set; }
        public List<IssueItemViewModel> SecondaryIssues { get; set; } = new List<IssueItemViewModel>();
        public List<IssueItemViewModel> CollapsedIssues { get; set; } = new List<IssueItemViewModel>();
        public List<ActionItemViewModel> OtherActions { get; set; } = new List<ActionItemViewModel>();

        public List<ClarificationQuestionViewModel> ClarificationQuestions { get; set; } = new List<ClarificationQuestionViewModel>();
        public List<string> MissingInformation { get; set; } = new List<string>();
        public List<string> RiskFlags { get; set; } = new List<string>();
        public string? ConfidenceLevel { get; set; }
        public List<string> ConfidenceReasons { get; set; } = new List<string>();

        public string? ChangeSummary { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        // Quality & Error states
        public bool IsDegraded { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public string? ErrorMessage { get; set; }
        public bool IsStaleVersion { get; set; }
    }

    public class IssueItemViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Rank { get; set; }
        public string PriorityLevel { get; set; } = "High";
        public bool IsTied { get; set; }
        public string? TiedNote { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class ActionItemViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public bool IsRecommendedNext { get; set; }
        public string? EstimatedTime { get; set; }
        public string? Urgency { get; set; }
    }

    public class ClarificationQuestionViewModel
    {
        public Guid Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
        public string? AnswerText { get; set; }
        public bool IsAnswered { get; set; }
        public bool IsSkipped { get; set; }
        public bool Skippable { get; set; } = true;
    }

    public class VersionSummaryItem
    {
        public int VersionNumber { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string? ChangeSummary { get; set; }
        public bool IsCurrent { get; set; }
    }

    public class UpdateSituationInputModel
    {
        [Required]
        public Guid SituationId { get; set; }

        [Required]
        public int ExpectedVersion { get; set; }

        public string? UpdateText { get; set; }

        public Dictionary<string, string> Answers { get; set; } = new Dictionary<string, string>();
    }

    public class StaleCheckResult
    {
        public bool IsStale { get; set; }
        public int CurrentVersion { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
