using System.ComponentModel.DataAnnotations;

namespace ClassificadorDoc.Models
{
    /// <summary>
    /// Controle de produtividade dos usuários - Requisito 4.2.7.4
    /// REFATORADO: Removidos campos redundantes com BatchProcessingHistory
    /// Mantém apenas dados específicos de atividade na plataforma
    /// </summary>
    public class UserProductivity
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        // === DADOS ÚNICOS (não disponíveis em BatchProcessingHistory) ===

        /// <summary>
        /// Número de logins no dia (independente de processar documentos)
        /// </summary>
        public int LoginCount { get; set; }

        /// <summary>
        /// Tempo total online na plataforma (sessão ativa)
        /// </summary>
        public TimeSpan TotalTimeOnline { get; set; }

        /// <summary>
        /// Páginas acessadas na plataforma (navegação)
        /// </summary>
        public int PagesAccessed { get; set; }

        /// <summary>
        /// Primeiro login do dia
        /// </summary>
        public DateTime FirstLogin { get; set; }

        /// <summary>
        /// Última atividade registrada
        /// </summary>
        public DateTime LastActivity { get; set; }

        // === REMOVIDOS (redundantes com BatchProcessingHistory) ===
        // DocumentsProcessed -> Calculado via BatchProcessingHistory.Sum(TotalDocuments)
        // ErrorCount -> Calculado via BatchProcessingHistory.Sum(FailedDocuments)  
        // SuccessRate -> Calculado via BatchProcessingHistory aggregate
    }

    /// <summary>
    /// Usuários conectados em tempo real - Requisito 4.2.7.5
    /// </summary>
    public class ActiveUserSession
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; }
        public DateTime LastActivity { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string CurrentPage { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string Role { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }

    /// <summary>
    /// Metadados para mineração de dados - Requisito 4.2.3.V
    /// </summary>
    public class DataMiningMetadata
    {
        public int Id { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsSearchable { get; set; }
        public bool IsAnalyzable { get; set; }
        public string? DefaultValue { get; set; }
        public string? ValidationRules { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Séries temporais para análise e previsão - Requisito 4.2.3.IV
    /// </summary>
    public class TimeSeriesData
    {
        public int Id { get; set; }
        public string SeriesName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Tags { get; set; } // JSON
        public string DataSource { get; set; } = string.Empty;
        public string? PredictionModel { get; set; }
        public double? PredictedValue { get; set; }
        public double? Confidence { get; set; }
    }

    /// <summary>
    /// Alertas automáticos programáveis - Requisito 4.2.6
    /// </summary>
    public class AutomatedAlert
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty; // JSON com condições
        public string AlertType { get; set; } = string.Empty; // EMAIL, SYSTEM, SMS
        public string Recipients { get; set; } = string.Empty; // JSON com destinatários
        public bool IsActive { get; set; } = true;
        public string Priority { get; set; } = "MEDIUM"; // LOW, MEDIUM, HIGH, CRITICAL
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? LastTriggered { get; set; }
        public int TriggerCount { get; set; }
        public string? LastResult { get; set; }
    }

    /// <summary>
    /// Dashboards e relatórios interativos - Requisito 4.2.3.VI
    /// </summary>
    public class DashboardWidget
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string WidgetType { get; set; } = string.Empty; // CHART, TABLE, METRIC, MAP
        public string DataSource { get; set; } = string.Empty;
        public string Configuration { get; set; } = string.Empty; // JSON
        public int OrderIndex { get; set; }
        public bool IsVisible { get; set; } = true;
        public string UserRole { get; set; } = string.Empty; // Qual role pode ver
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Conformidade LGPD - Requisito 4.2.7.6
    /// </summary>
    public class LGPDCompliance
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty; // PERSONAL, SENSITIVE, PROFESSIONAL
        public string Action { get; set; } = string.Empty; // COLLECT, PROCESS, SHARE, DELETE
        public string LegalBasis { get; set; } = string.Empty; // CONSENT, LEGITIMATE_INTEREST, etc.
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Description { get; set; } = string.Empty;
        public bool ConsentGiven { get; set; }
        public DateTime? ConsentDate { get; set; }
        public DateTime? RetentionUntil { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string? ProcessorInfo { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    /// <summary>
    /// Exportação de dados em formatos abertos - Requisito 4.2.5
    /// </summary>
    public class DataExport
    {
        public int Id { get; set; }
        public string ExportName { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty; // CSV, XML, XLSX
        public string DataType { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = "PENDING"; // PENDING, PROCESSING, COMPLETED, FAILED
        public string? FilePath { get; set; }
        public long? FileSizeBytes { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int RecordCount { get; set; }
    }
}
