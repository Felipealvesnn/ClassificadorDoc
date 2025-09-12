using System.ComponentModel.DataAnnotations;

namespace ClassificadorDoc.Models
{
    /// <summary>
    /// Modelo para configurações do sistema
    /// </summary>
    public class Configuracao
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Chave { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Valor { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Descricao { get; set; }

        [StringLength(50)]
        public string Categoria { get; set; } = "Geral";

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public DateTime DataAtualizacao { get; set; } = DateTime.UtcNow;

        [StringLength(450)]
        public string? UsuarioAtualizacao { get; set; }

        public bool Ativo { get; set; } = true;
    }

    /// <summary>
    /// Constantes para chaves de configuração
    /// </summary>
    public static class ChavesConfiguracao
    {
        public const string CAMINHO_SALVAMENTO_DOCUMENTOS = "CAMINHO_SALVAMENTO_DOCUMENTOS";
        public const string DIRETORIO_BASE_DOCUMENTOS = "DIRETORIO_BASE_DOCUMENTOS";
        public const string NOME_PASTA_CLASSIFICADOR = "NOME_PASTA_CLASSIFICADOR";
        public const string ESTRUTURA_PASTAS_HABILITADA = "ESTRUTURA_PASTAS_HABILITADA";
    }

    /// <summary>
    /// ViewModel para configurações
    /// </summary>
    public class ConfiguracaoViewModel
    {
        public string CaminhoSalvamento { get; set; } = string.Empty;
        public string DiretorioBase { get; set; } = "DocumentosProcessados";
        public string NomePastaClassificador { get; set; } = "ClassificadorDoc";
        public bool EstruturaPastasHabilitada { get; set; } = true;
    }
}
