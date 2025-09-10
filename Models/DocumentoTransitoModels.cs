using ClassificadorDoc.Data;

namespace ClassificadorDoc.Models
{
    // Entidade para armazenar documentos de trânsito processados
    public class DocumentoTransito
    {
        public int Id { get; set; }
        public string NomeArquivo { get; set; } = string.Empty;
        public string TipoDocumento { get; set; } = string.Empty; // autuacao, defesa, notificacao_autuacao, notificacao_penalidade, outros
        public double ConfiancaClassificacao { get; set; }
        public string ResumoConteudo { get; set; } = string.Empty;
        public string? PalavrasChaveEncontradas { get; set; }
        public string TextoCompleto { get; set; } = string.Empty; // NOVO: texto completo extraído do PDF
        public bool ProcessadoComSucesso { get; set; }
        public string? ErroProcessamento { get; set; }
        public DateTime ProcessadoEm { get; set; } = DateTime.UtcNow;
        public string ProcessadoPor { get; set; } = string.Empty; // UserId

        // Campos específicos para documentos de trânsito
        public string? NumeroAIT { get; set; } // Número do Auto de Infração de Trânsito
        public string? PlacaVeiculo { get; set; } // Placa do veículo
        public string? NomeCondutor { get; set; } // Nome do condutor (para indicação de condutor)
        public string? NumeroCNH { get; set; } // Número da CNH do condutor
        public string? TextoDefesa { get; set; } // Texto completo da defesa (para defesas)
        public DateTime? DataInfracao { get; set; } // Data da infração (extraída do documento)
        public string? LocalInfracao { get; set; } // Local da infração
        public string? CodigoInfracao { get; set; } // Código CTB da infração
        public decimal? ValorMulta { get; set; } // Valor da multa (para notificações de penalidade)
        public string? OrgaoAutuador { get; set; } // Órgão que aplicou a multa

        // Relacionamento com o usuário que processou
        public ApplicationUser? ProcessadoPorUsuario { get; set; }
    }

    // Modelo para exibição/edição dos dados extraídos
    public class DocumentoTransitoViewModel
    {
        public int Id { get; set; }
        public string NomeArquivo { get; set; } = string.Empty;
        public string TipoDocumento { get; set; } = string.Empty;
        public double ConfiancaClassificacao { get; set; }
        public string ResumoConteudo { get; set; } = string.Empty;
        public string? PalavrasChaveEncontradas { get; set; }
        public string TextoCompleto { get; set; } = string.Empty;
        public bool ProcessadoComSucesso { get; set; }
        public string? ErroProcessamento { get; set; }
        public DateTime ProcessadoEm { get; set; }
        public string ProcessadoPor { get; set; } = string.Empty;

        // Campos específicos editáveis
        public string? NumeroAIT { get; set; }
        public string? PlacaVeiculo { get; set; }
        public string? NomeCondutor { get; set; }
        public string? NumeroCNH { get; set; }
        public string? TextoDefesa { get; set; }
        public DateTime? DataInfracao { get; set; }
        public string? LocalInfracao { get; set; }
        public string? CodigoInfracao { get; set; }
        public decimal? ValorMulta { get; set; }
        public string? OrgaoAutuador { get; set; }

        // Campos helper para validação
        public bool PrecisaNumeroAIT => TipoDocumento != "outros";
        public bool PrecisaPlacaVeiculo => TipoDocumento != "outros";
        public bool PrecisaNomeCondutor => TipoDocumento == "indicacao_condutor";
        public bool PrecisaNumeroCNH => TipoDocumento == "indicacao_condutor";
        public bool PrecisaTextoDefesa => TipoDocumento == "defesa";
    }

    // Modelo para resposta da classificação com campos específicos
    public class DocumentoClassificacaoExtendida : DocumentoClassificacao
    {
        // Campos específicos extraídos pelo Gemini
        public string? NumeroAIT { get; set; }
        public string? PlacaVeiculo { get; set; }
        public string? NomeCondutor { get; set; }
        public string? NumeroCNH { get; set; }
        public string? TextoDefesa { get; set; }
        public DateTime? DataInfracao { get; set; }
        public string? LocalInfracao { get; set; }
        public string? CodigoInfracao { get; set; }
        public decimal? ValorMulta { get; set; }
        public string? OrgaoAutuador { get; set; }
    }
}
