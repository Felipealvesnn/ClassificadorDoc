using System.ComponentModel.DataAnnotations;

namespace ClassificadorDoc.Models
{
    /// <summary>
    /// Entidade para auditoria completa do sistema conforme requisitos do edital
    /// Armazena logs de acesso, atividades e operações por no mínimo 12 meses
    /// </summary>
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty; // LOGIN, LOGOUT, CREATE_USER, CLASSIFY_DOC, ACCESS_PAGE, etc.

        [Required]
        [StringLength(100)]
        public string Resource { get; set; } = string.Empty; // Página/funcionalidade acessada

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;

        [StringLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        [StringLength(500)]
        public string UserAgent { get; set; } = string.Empty;

        public string? Details { get; set; } // JSON com detalhes específicos da ação

        [Required]
        [StringLength(10)]
        public string Result { get; set; } = string.Empty; // SUCCESS, FAILED, BLOCKED

        [StringLength(500)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Campo para categorizar o tipo de auditoria
        /// ACCESS: Acesso a páginas e recursos
        /// SECURITY: Login, logout, tentativas de acesso
        /// BUSINESS: Operações de negócio (classificação, upload)
        /// ADMIN: Operações administrativas
        /// </summary>
        [StringLength(20)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Nível de criticidade da ação para auditoria
        /// LOW: Navegação normal
        /// MEDIUM: Operações de negócio
        /// HIGH: Alterações de dados críticos
        /// CRITICAL: Operações administrativas, falhas de segurança
        /// </summary>
        [StringLength(10)]
        public string Severity { get; set; } = "LOW";
    }

    /// <summary>
    /// ViewModel para relatórios de auditoria
    /// </summary>
    public class AuditReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? UserId { get; set; }
        public string? Action { get; set; }
        public string? Category { get; set; }
        public string? Result { get; set; }
        public List<AuditLog> Logs { get; set; } = new();
        public int TotalRecords { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Estatísticas de auditoria para dashboard
    /// </summary>
    public class AuditStatsViewModel
    {
        public int TotalLogins { get; set; }
        public int FailedLogins { get; set; }
        public int DocumentsProcessed { get; set; }
        public int AdminActions { get; set; }
        public int SecurityEvents { get; set; }
        public DateTime OldestLog { get; set; }
        public DateTime NewestLog { get; set; }
        public List<DailyAuditSummary> DailySummary { get; set; } = new();
    }

    public class DailyAuditSummary
    {
        public DateTime Date { get; set; }
        public int TotalActions { get; set; }
        public int UniqueUsers { get; set; }
        public int SecurityEvents { get; set; }
    }
}
