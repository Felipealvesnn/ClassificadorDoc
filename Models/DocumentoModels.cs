namespace ClassificadorDoc.Models
{
    public class DocumentoClassificacao
    {
        public string NomeArquivo { get; set; } = string.Empty;
        public string TipoDocumento { get; set; } = string.Empty;
        public double ConfiancaClassificacao { get; set; }
        public string ResumoConteudo { get; set; } = string.Empty;
        public string? PalavrasChaveEncontradas { get; set; }
        public string TextoExtraido { get; set; } = string.Empty;
        public bool ProcessadoComSucesso { get; set; }
        public string? ErroProcessamento { get; set; }
    }

    public class ResultadoClassificacao
    {
        public int TotalDocumentos { get; set; }
        public int DocumentosProcessados { get; set; }
        public int DocumentosComErro { get; set; }
        public List<DocumentoClassificacao> Documentos { get; set; } = new();
        public TimeSpan TempoProcessamento { get; set; }
    }

    public enum TipoDocumentoTransito
    {
        Autuacao,           // AIT, Notificação de Autuação
        Defesa,             // Defesa de Autuação, Recurso JARI/CETRAN
        NotificacaoPenalidade, // NIP, Intimação de pagamento
        Outros
    }
}
