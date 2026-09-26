using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStepWeb.Models.Entities
{
    public class Assessment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SituationVersionId { get; set; }

        [ForeignKey(nameof(SituationVersionId))]
        public SituationVersion? SituationVersion { get; set; }

        [Required]
        public string UnderstandingSummary { get; set; } = string.Empty;

        public string? SupportGuidance { get; set; }

        public string? MisuseExplanation { get; set; }

        public string? AdversarialWarning { get; set; }

        public string? WorseOutcomeAnalysis { get; set; }

        public string? WhatChanged { get; set; }

        public string? WhatHappenedAfterAction { get; set; }

        public string? DifferentInformation { get; set; }

        public string? WhatToReassess { get; set; }

        public bool IsCalmMode { get; set; } = false;

        public bool IsAtRisk { get; set; } = false;

        public bool IsMisuse { get; set; } = false;

        public bool IsAdversarial { get; set; } = false;

        public bool IsWorseAfterAction { get; set; } = false;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
