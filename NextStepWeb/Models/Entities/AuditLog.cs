using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStepWeb.Models.Entities
{
    public class AuditLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? SituationId { get; set; }

        [ForeignKey(nameof(SituationId))]
        public Situation? Situation { get; set; }

        [Required]
        [MaxLength(64)]
        public string Action { get; set; } = string.Empty;

        [Required]
        public string Details { get; set; } = string.Empty;

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
