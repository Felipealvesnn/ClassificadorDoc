namespace ClassificadorDoc.ViewModels
{
    /// <summary>
    /// ViewModel que combina dados de UserProductivity (atividade) + BatchProcessingHistory (documentos)
    /// Evita redundância entre as tabelas mantendo separação de responsabilidades
    /// </summary>
    public class CombinedProductivityViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        // DADOS ÚNICOS DA PLATAFORMA (UserProductivity)
        /// <summary>Número de logins realizados no dia</summary>
        public int LoginCount { get; set; }

        /// <summary>Tempo total online na plataforma</summary>
        public TimeSpan TotalTimeOnline { get; set; }

        /// <summary>Número de páginas acessadas</summary>
        public int PagesAccessed { get; set; }

        /// <summary>Horário do primeiro login</summary>
        public DateTime? FirstLogin { get; set; }

        /// <summary>Horário da última atividade</summary>
        public DateTime? LastActivity { get; set; }

        // DADOS DE PROCESSAMENTO (BatchProcessingHistory agregados)
        /// <summary>Número total de lotes processados</summary>
        public int TotalBatches { get; set; }

        /// <summary>Total de documentos processados em todos os lotes</summary>
        public int DocumentsProcessed { get; set; }

        /// <summary>Documentos classificados com sucesso</summary>
        public int SuccessfulDocuments { get; set; }

        /// <summary>Documentos com falha na classificação</summary>
        public int FailedDocuments { get; set; }

        /// <summary>Confiança média das classificações</summary>
        public double AverageConfidence { get; set; }

        /// <summary>Tempo total de processamento</summary>
        public TimeSpan TotalProcessingTime { get; set; }

        // PROPRIEDADES CALCULADAS
        /// <summary>Taxa de sucesso nas classificações (%)</summary>
        public double SuccessRate { get; set; }

        /// <summary>Produtividade por hora (documentos/hora)</summary>
        public double DocumentsPerHour => TotalTimeOnline.TotalHours > 0 ?
            DocumentsProcessed / TotalTimeOnline.TotalHours : 0;

        /// <summary>Tempo médio por documento</summary>
        public TimeSpan AverageTimePerDocument => DocumentsProcessed > 0 ?
            TimeSpan.FromSeconds(TotalProcessingTime.TotalSeconds / DocumentsProcessed) : TimeSpan.Zero;

        /// <summary>Indicador de atividade principal (navegação vs processamento)</summary>
        public string PrimaryActivity => DocumentsProcessed > 0 ? "Processamento" : "Navegação";

        /// <summary>Score de produtividade geral (0-100)</summary>
        public int ProductivityScore
        {
            get
            {
                var score = 0;

                // Pontos por atividade na plataforma (0-30)
                if (LoginCount > 0) score += Math.Min(LoginCount * 5, 15);
                if (TotalTimeOnline.TotalHours > 0) score += Math.Min((int)(TotalTimeOnline.TotalHours * 2), 15);

                // Pontos por processamento de documentos (0-50)
                if (DocumentsProcessed > 0) score += Math.Min(DocumentsProcessed, 30);
                if (SuccessRate > 80) score += 20;
                else if (SuccessRate > 60) score += 10;

                // Pontos por eficiência (0-20)
                if (DocumentsPerHour > 10) score += 20;
                else if (DocumentsPerHour > 5) score += 10;

                return Math.Min(score, 100);
            }
        }
    }
}
