using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ClassificadorDoc.Models;

namespace ClassificadorDoc.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets para suas entidades customizadas
        public DbSet<DocumentProcessingHistory> DocumentProcessingHistories { get; set; }
        public DbSet<ClassificationSession> ClassificationSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configurações customizadas
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.FullName).HasMaxLength(100);
                entity.Property(e => e.Department).HasMaxLength(50);
            });

            builder.Entity<ApplicationRole>(entity =>
            {
                entity.Property(e => e.Description).HasMaxLength(200);
            });

            builder.Entity<DocumentProcessingHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.DocumentType).HasMaxLength(50);
                entity.Property(e => e.UserId).IsRequired();

                // Relacionamento com usuário
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ClassificationSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SessionId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.ProcessingMethod).HasMaxLength(20);
                entity.Property(e => e.UserId).IsRequired();

                // Relacionamento com usuário
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }

    // Modelos para histórico e sessões
    public class DocumentProcessingHistory
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public string UserId { get; set; } = string.Empty;
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Keywords { get; set; }
        public int FileSizeBytes { get; set; }
    }

    public class ClassificationSession
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public int TotalDocuments { get; set; }
        public int ProcessedDocuments { get; set; }
        public int SuccessfulDocuments { get; set; }
        public string ProcessingMethod { get; set; } = string.Empty; // "text" ou "visual"
        public string UserId { get; set; } = string.Empty;
        public TimeSpan? ProcessingDuration { get; set; }
    }
}
