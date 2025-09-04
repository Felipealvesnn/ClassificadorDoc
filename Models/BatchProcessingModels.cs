using System.ComponentModel.DataAnnotations;
using ClassificadorDoc.Data;

namespace ClassificadorDoc.Models
{
    /// <summary>
    /// Entidade para controle de lotes de documentos processados
    /// Atende ao requisito do edital: II. Extração de entidades, classificação de documentos, 
    /// agrupamentos hierárquicos e redes neurais para categorização
    /// </summary>
    public class BatchProcessingHistory
    {
        public int Id { get; set; }

        /// <summary>
        /// Nome original do arquivo ZIP/lote enviado
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string BatchName { get; set; } = string.Empty;

        /// <summary>
        /// ID do usuário que processou o lote
        /// </summary>
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Nome do usuário para facilitar consultas
        /// </summary>
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Data/hora de início do processamento
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Data/hora de conclusão do processamento
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Total de documentos no lote
        /// </summary>
        public int TotalDocuments { get; set; }

        /// <summary>
        /// Documentos processados com sucesso
        /// </summary>
        public int SuccessfulDocuments { get; set; }

        /// <summary>
        /// Documentos com erro
        /// </summary>
        public int FailedDocuments { get; set; }

        /// <summary>
        /// Tempo total de processamento
        /// </summary>
        public TimeSpan? ProcessingDuration { get; set; }

        /// <summary>
        /// Tamanho do arquivo ZIP em bytes
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Método de processamento utilizado
        /// </summary>
        [MaxLength(20)]
        public string ProcessingMethod { get; set; } = "visual";

        /// <summary>
        /// Status do processamento
        /// </summary>
        [MaxLength(20)]
        public string Status { get; set; } = "Processing"; // Processing, Completed, Failed

        /// <summary>
        /// Classificação predominante do lote (mais comum)
        /// </summary>
        [MaxLength(50)]
        public string? PredominantDocumentType { get; set; }

        /// <summary>
        /// Confiança média do lote
        /// </summary>
        public double AverageConfidence { get; set; }

        /// <summary>
        /// Resumo das classificações encontradas (JSON)
        /// Ex: {"autuacao": 15, "defesa": 3, "notificacao": 2}
        /// </summary>
        public string? ClassificationSummary { get; set; }

        /// <summary>
        /// Palavras-chave mais encontradas no lote (JSON)
        /// </summary>
        public string? KeywordsSummary { get; set; }

        /// <summary>
        /// Mensagem de erro, se houver
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// IP de onde foi enviado o lote
        /// </summary>
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        /// <summary>
        /// User Agent do navegador
        /// </summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        // Relacionamento com documentos individuais
        public virtual ICollection<DocumentProcessingHistory> Documents { get; set; } = new List<DocumentProcessingHistory>();
    }

    /// <summary>
    /// Estatísticas de produtividade por lotes para relatórios
    /// </summary>
    public class BatchProductivityStats
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int TotalBatchesProcessed { get; set; }
        public int TotalDocumentsProcessed { get; set; }
        public double AverageSuccessRate { get; set; }
        public double AverageConfidence { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }
        public DateTime LastBatchProcessed { get; set; }
        public string MostCommonDocumentType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resumo de classificação para análise de agrupamentos hierárquicos
    /// </summary>
    public class ClassificationHierarchy
    {
        public string DocumentType { get; set; } = string.Empty;
        public int Count { get; set; }
        public double AverageConfidence { get; set; }
        public List<string> CommonKeywords { get; set; } = new();
        public List<string> RelatedBatches { get; set; } = new();
    }
}
