using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStepWeb.Models.Entities
{
    public class ActionItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SituationVersionId { get; set; }

        [ForeignKey(nameof(SituationVersionId))]
        public SituationVersion? SituationVersion { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public int StepOrder { get; set; } = 1;

        public bool IsRecommendedNext { get; set; } = false;

        [MaxLength(64)]
        public string? EstimatedTime { get; set; }

        [MaxLength(64)]
        public string? Urgency { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
