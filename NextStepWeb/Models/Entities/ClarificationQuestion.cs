using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStepWeb.Models.Entities
{
    public class ClarificationQuestion
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SituationVersionId { get; set; }

        [ForeignKey(nameof(SituationVersionId))]
        public SituationVersion? SituationVersion { get; set; }

        [Required]
        [MaxLength(300)]
        public string QuestionText { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Purpose { get; set; } = string.Empty;

        public string? AnswerText { get; set; }

        public bool IsAnswered { get; set; } = false;

        public bool IsSkipped { get; set; } = false;

        public DateTime? AnsweredAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
