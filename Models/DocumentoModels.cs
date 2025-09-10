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

        // NOVOS CAMPOS ESPECÍFICOS PARA DOCUMENTOS DE TRÂNSITO
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
