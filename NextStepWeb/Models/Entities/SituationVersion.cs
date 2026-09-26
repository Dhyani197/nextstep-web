using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStepWeb.Models.Entities
{
    public class SituationVersion
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SituationId { get; set; }

        [ForeignKey(nameof(SituationId))]
        public Situation? Situation { get; set; }

        public int VersionNumber { get; set; } = 1;

        [Required]
        public string SituationText { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ChangeSummary { get; set; }

        [Required]
        [MaxLength(64)]
        public string AnalysisMode { get; set; } = "Normal";

        public string? RawAiResponseJson { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public Assessment? Assessment { get; set; }
        public ICollection<Issue> Issues { get; set; } = new List<Issue>();
        public ICollection<ActionItem> ActionItems { get; set; } = new List<ActionItem>();
        public ICollection<ClarificationQuestion> ClarificationQuestions { get; set; } = new List<ClarificationQuestion>();
    }
}
