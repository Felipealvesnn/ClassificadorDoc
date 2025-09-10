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
        public DbSet<AuditLog> AuditLogs { get; set; }

        // DbSet para documentos de trânsito processados
        public DbSet<DocumentoTransito> DocumentosTransito { get; set; }

        // DbSets para controle de lotes (atende requisito edital)
        public DbSet<BatchProcessingHistory> BatchProcessingHistories { get; set; }

        // DbSets para conformidade com edital
        public DbSet<UserProductivity> UserProductivities { get; set; }
        public DbSet<ActiveUserSession> ActiveUserSessions { get; set; }
        public DbSet<DataMiningMetadata> DataMiningMetadata { get; set; }
        public DbSet<TimeSeriesData> TimeSeriesData { get; set; }
        public DbSet<AutomatedAlert> AutomatedAlerts { get; set; }
        public DbSet<DashboardWidget> DashboardWidgets { get; set; }
        public DbSet<LGPDCompliance> LGPDCompliances { get; set; }
        public DbSet<DataExport> DataExports { get; set; }
        public DbSet<SystemNotification> SystemNotifications { get; set; }

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

                // Relacionamento com lote (novo)
                entity.HasOne(e => e.BatchProcessingHistory)
                    .WithMany(b => b.Documents)
                    .HasForeignKey(e => e.BatchProcessingHistoryId)
                    .OnDelete(DeleteBehavior.NoAction);

                // CONFIGURAÇÕES PARA NOVOS CAMPOS ESPECÍFICOS
                entity.Property(e => e.TextoCompleto).HasColumnType("nvarchar(max)");
                entity.Property(e => e.NumeroAIT).HasMaxLength(50);
                entity.Property(e => e.PlacaVeiculo).HasMaxLength(10);
                entity.Property(e => e.NomeCondutor).HasMaxLength(100);
                entity.Property(e => e.NumeroCNH).HasMaxLength(20);
                entity.Property(e => e.TextoDefesa).HasColumnType("nvarchar(max)");
                entity.Property(e => e.LocalInfracao).HasMaxLength(200);
                entity.Property(e => e.CodigoInfracao).HasMaxLength(20);
                entity.Property(e => e.OrgaoAutuador).HasMaxLength(100);
                entity.Property(e => e.ValorMulta).HasColumnType("decimal(10,2)");

                // Índices para busca eficiente
                entity.HasIndex(e => e.DocumentType);
                entity.HasIndex(e => e.NumeroAIT);
                entity.HasIndex(e => e.PlacaVeiculo);
                entity.HasIndex(e => new { e.UserId, e.ProcessedAt });
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

            builder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Resource).HasMaxLength(100).IsRequired();
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.UserName).HasMaxLength(100);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.Result).HasMaxLength(10).IsRequired();
                entity.Property(e => e.ErrorMessage).HasMaxLength(500);
                entity.Property(e => e.Category).HasMaxLength(20);
                entity.Property(e => e.Severity).HasMaxLength(10);

                // Índices para performance em consultas de auditoria
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => new { e.Timestamp, e.Category });
            });

            // Configurações para conformidade com edital
            builder.Entity<UserProductivity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.Date }).IsUnique();
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
            });

            builder.Entity<ActiveUserSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SessionId).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.IsActive);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.UserName).HasMaxLength(100);
                entity.Property(e => e.SessionId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.CurrentPage).HasMaxLength(200);
                entity.Property(e => e.Role).HasMaxLength(50);
                entity.Property(e => e.Department).HasMaxLength(50);
            });

            builder.Entity<DataMiningMetadata>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.EntityName, e.PropertyName }).IsUnique();
                entity.Property(e => e.EntityName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.PropertyName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.DataType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Category).HasMaxLength(50);
            });

            builder.Entity<TimeSeriesData>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.SeriesName, e.Timestamp });
                entity.HasIndex(e => e.Category);
                entity.Property(e => e.SeriesName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Category).HasMaxLength(50);
                entity.Property(e => e.DataSource).HasMaxLength(100);
                entity.Property(e => e.PredictionModel).HasMaxLength(50);
            });

            builder.Entity<AutomatedAlert>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.IsActive);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.AlertType).HasMaxLength(20);
                entity.Property(e => e.Priority).HasMaxLength(10);
                entity.Property(e => e.CreatedBy).HasMaxLength(450);
            });

            builder.Entity<DashboardWidget>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserRole);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.WidgetType).HasMaxLength(20);
                entity.Property(e => e.DataSource).HasMaxLength(100);
                entity.Property(e => e.UserRole).HasMaxLength(50);
                entity.Property(e => e.CreatedBy).HasMaxLength(450);
                entity.Property(e => e.UpdatedBy).HasMaxLength(450);
            });

            builder.Entity<LGPDCompliance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.DataType);
                entity.HasIndex(e => e.Timestamp);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.DataType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
                entity.Property(e => e.LegalBasis).HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Purpose).HasMaxLength(200);
            });

            builder.Entity<DataExport>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);
                entity.Property(e => e.ExportName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Format).HasMaxLength(10).IsRequired();
                entity.Property(e => e.DataType).HasMaxLength(50);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.Property(e => e.FilePath).HasMaxLength(500);
            });

            // Configurações para controle de lotes (requisito edital)
            builder.Entity<BatchProcessingHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.StartedAt);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.PredominantDocumentType);
                entity.HasIndex(e => new { e.UserId, e.StartedAt });

                entity.Property(e => e.BatchName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.UserName).HasMaxLength(100);
                entity.Property(e => e.ProcessingMethod).HasMaxLength(20);
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.Property(e => e.PredominantDocumentType).HasMaxLength(50);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);

                // Relacionamento com usuário
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configurações para documentos de trânsito
            builder.Entity<DocumentoTransito>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProcessadoPor);
                entity.HasIndex(e => e.ProcessadoEm);
                entity.HasIndex(e => e.TipoDocumento);
                entity.HasIndex(e => e.NumeroAIT);
                entity.HasIndex(e => e.PlacaVeiculo);
                entity.HasIndex(e => new { e.ProcessadoPor, e.ProcessadoEm });

                entity.Property(e => e.NomeArquivo).HasMaxLength(255).IsRequired();
                entity.Property(e => e.TipoDocumento).HasMaxLength(50).IsRequired();
                entity.Property(e => e.ResumoConteudo).HasMaxLength(1000);
                entity.Property(e => e.PalavrasChaveEncontradas).HasMaxLength(500);
                entity.Property(e => e.TextoCompleto).HasColumnType("nvarchar(max)");
                entity.Property(e => e.ErroProcessamento).HasMaxLength(1000);
                entity.Property(e => e.ProcessadoPor).HasMaxLength(450).IsRequired();

                // Campos específicos de trânsito
                entity.Property(e => e.NumeroAIT).HasMaxLength(50);
                entity.Property(e => e.PlacaVeiculo).HasMaxLength(10);
                entity.Property(e => e.NomeCondutor).HasMaxLength(100);
                entity.Property(e => e.NumeroCNH).HasMaxLength(20);
                entity.Property(e => e.TextoDefesa).HasColumnType("nvarchar(max)");
                entity.Property(e => e.LocalInfracao).HasMaxLength(200);
                entity.Property(e => e.CodigoInfracao).HasMaxLength(20);
                entity.Property(e => e.OrgaoAutuador).HasMaxLength(100);
                entity.Property(e => e.ValorMulta).HasColumnType("decimal(10,2)");

                // Relacionamento com usuário
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.ProcessadoPor)
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
        public string? CaminhoArquivo { get; set; } // Caminho onde o arquivo foi salvo

        // Relacionamento com lote (novo campo)
        public int? BatchProcessingHistoryId { get; set; }
        public BatchProcessingHistory? BatchProcessingHistory { get; set; }

        // NOVOS CAMPOS ESPECÍFICOS PARA DOCUMENTOS DE TRÂNSITO
        public string? TextoCompleto { get; set; } // Texto extraído do PDF
        public string? NumeroAIT { get; set; } // Número do Auto de Infração
        public string? PlacaVeiculo { get; set; } // Placa do veículo
        public string? NomeCondutor { get; set; } // Nome do condutor (para indicação de condutor)
        public string? NumeroCNH { get; set; } // Número da CNH do condutor
        public string? TextoDefesa { get; set; } // Texto completo da defesa (para defesas)
        public DateTime? DataInfracao { get; set; } // Data da infração
        public string? LocalInfracao { get; set; } // Local da infração
        public string? CodigoInfracao { get; set; } // Código CTB da infração
        public decimal? ValorMulta { get; set; } // Valor da multa
        public string? OrgaoAutuador { get; set; } // Órgão que aplicou a multa

        // NOVOS CAMPOS PARA INDICAÇÃO DE CONDUTOR
        // Dados do REQUERENTE (proprietário do veículo)
        public string? RequerenteNome { get; set; } // Nome completo do requerente
        public string? RequerenteCPF { get; set; } // CPF do requerente
        public string? RequerenteRG { get; set; } // RG do requerente
        public string? RequerenteEndereco { get; set; } // Endereço do requerente

        // Dados da INDICAÇÃO (condutor real no momento da infração)
        public string? IndicacaoNome { get; set; } // Nome do condutor indicado
        public string? IndicacaoCPF { get; set; } // CPF do condutor indicado
        public string? IndicacaoRG { get; set; } // RG do condutor indicado
        public string? IndicacaoCNH { get; set; } // CNH do condutor indicado
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
