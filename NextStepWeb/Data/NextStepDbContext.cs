using Microsoft.EntityFrameworkCore;
using NextStepWeb.Models.Entities;

namespace NextStepWeb.Data
{
    public class NextStepDbContext : DbContext
    {
        public NextStepDbContext(DbContextOptions<NextStepDbContext> options)
            : base(options)
        {
        }

        public DbSet<Situation> Situations => Set<Situation>();
        public DbSet<SituationVersion> SituationVersions => Set<SituationVersion>();
        public DbSet<Assessment> Assessments => Set<Assessment>();
        public DbSet<Issue> Issues => Set<Issue>();
        public DbSet<ActionItem> ActionItems => Set<ActionItem>();
        public DbSet<ClarificationQuestion> ClarificationQuestions => Set<ClarificationQuestion>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Situation
            modelBuilder.Entity<Situation>(entity =>
            {
                entity.HasKey(s => s.Id);

                // Enforce idempotency unique constraint on ClientRequestId
                entity.HasIndex(s => s.ClientRequestId)
                    .IsUnique()
                    .HasDatabaseName("IX_Situations_ClientRequestId");

                entity.HasIndex(s => s.CreatedAtUtc)
                    .HasDatabaseName("IX_Situations_CreatedAtUtc");
            });

            // SituationVersion
            modelBuilder.Entity<SituationVersion>(entity =>
            {
                entity.HasKey(v => v.Id);

                entity.HasIndex(v => new { v.SituationId, v.VersionNumber })
                    .IsUnique()
                    .HasDatabaseName("IX_SituationVersions_SituationId_VersionNumber");

                entity.HasOne(v => v.Situation)
                    .WithMany(s => s.Versions)
                    .HasForeignKey(v => v.SituationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Assessment (1-to-1 with SituationVersion)
            modelBuilder.Entity<Assessment>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.HasIndex(a => a.SituationVersionId)
                    .IsUnique()
                    .HasDatabaseName("IX_Assessments_SituationVersionId");

                entity.HasOne(a => a.SituationVersion)
                    .WithOne(v => v.Assessment)
                    .HasForeignKey<Assessment>(a => a.SituationVersionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Issue
            modelBuilder.Entity<Issue>(entity =>
            {
                entity.HasKey(i => i.Id);

                entity.HasIndex(i => new { i.SituationVersionId, i.Rank })
                    .HasDatabaseName("IX_Issues_SituationVersionId_Rank");

                entity.HasOne(i => i.SituationVersion)
                    .WithMany(v => v.Issues)
                    .HasForeignKey(i => i.SituationVersionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ActionItem
            modelBuilder.Entity<ActionItem>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.HasIndex(a => new { a.SituationVersionId, a.StepOrder })
                    .HasDatabaseName("IX_ActionItems_SituationVersionId_StepOrder");

                entity.HasOne(a => a.SituationVersion)
                    .WithMany(v => v.ActionItems)
                    .HasForeignKey(a => a.SituationVersionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ClarificationQuestion
            modelBuilder.Entity<ClarificationQuestion>(entity =>
            {
                entity.HasKey(q => q.Id);

                entity.HasIndex(q => q.SituationVersionId)
                    .HasDatabaseName("IX_ClarificationQuestions_SituationVersionId");

                entity.HasOne(q => q.SituationVersion)
                    .WithMany(v => v.ClarificationQuestions)
                    .HasForeignKey(q => q.SituationVersionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // AuditLog
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(l => l.Id);

                entity.HasIndex(l => l.SituationId)
                    .HasDatabaseName("IX_AuditLogs_SituationId");

                entity.HasIndex(l => l.TimestampUtc)
                    .HasDatabaseName("IX_AuditLogs_TimestampUtc");

                entity.HasOne(l => l.Situation)
                    .WithMany(s => s.AuditLogs)
                    .HasForeignKey(l => l.SituationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
