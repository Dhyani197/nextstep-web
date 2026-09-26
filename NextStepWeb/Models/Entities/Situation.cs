using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NextStepWeb.Models.Entities
{
    public class Situation
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(128)]
        public string ClientRequestId { get; set; } = string.Empty;

        [Required]
        public string OriginalSituationText { get; set; } = string.Empty;

        public int CurrentVersionNumber { get; set; } = 1;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public ICollection<SituationVersion> Versions { get; set; } = new List<SituationVersion>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }
}
