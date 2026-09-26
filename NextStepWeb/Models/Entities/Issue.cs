using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStepWeb.Models.Entities
{
    public class Issue
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

        public int Rank { get; set; } = 1;

        [Required]
        [MaxLength(32)]
        public string PriorityLevel { get; set; } = "High"; // Critical, High, Medium, Low, EqualRanked

        public bool IsTied { get; set; } = false;

        [MaxLength(250)]
        public string? TiedNote { get; set; }

        public bool IsPrimary { get; set; } = false;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
