namespace ClassificadorDoc.Models
{
    public class ClassificacaoViewModel
    {
        public List<DocumentoClassificacao> Resultados { get; set; } = new();
        public string? ErroGeral { get; set; }
        public bool ProcessamentoCompleto { get; set; }
        public DateTime DataProcessamento { get; set; }
        public string? NomeArquivoZip { get; set; }
        public int TotalDocumentos { get; set; }
        public int DocumentosProcessados { get; set; }
    }
}
