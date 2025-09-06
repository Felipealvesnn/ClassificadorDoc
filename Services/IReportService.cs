using System.Data;

namespace ClassificadorDoc.Services
{
    public interface IReportService
    {
        // Métodos originais para compatibilidade
        Task<byte[]> GerarRelatorioAuditoria(DateTime startDate, DateTime endDate);
        Task<byte[]> GerarRelatorioProdutividade(DateTime startDate, DateTime endDate);
        Task<byte[]> GerarRelatorioClassificacao(DateTime startDate, DateTime endDate, string? categoria = null);
        Task<byte[]> GerarRelatorioLotes(DateTime startDate, DateTime endDate, string? status = null);
        Task<byte[]> GerarRelatorioConsolidado(DateTime startDate, DateTime endDate);
        Task<byte[]> GerarRelatorioLGPD(DateTime startDate, DateTime endDate);

        // Novos métodos para suporte MVC com múltiplos formatos
        /// <summary>
        /// Gera relatório em PDF usando FastReport
        /// </summary>
        Task<byte[]> GerarRelatorioPdfAsync(string tipoRelatorio, DateTime? dataInicio, DateTime? dataFim, Dictionary<string, string>? filtrosAdicionais = null);

        /// <summary>
        /// Gera relatório em Excel usando FastReport
        /// </summary>
        Task<byte[]> GerarRelatorioExcelAsync(string tipoRelatorio, DateTime? dataInicio, DateTime? dataFim, Dictionary<string, string>? filtrosAdicionais = null);

        /// <summary>
        /// Obtém dados para visualização prévia do relatório
        /// </summary>
        Task<dynamic> ObterDadosRelatorioAsync(string tipoRelatorio, DateTime? dataInicio, DateTime? dataFim, Dictionary<string, string>? filtrosAdicionais = null);

        /// <summary>
        /// Valida se o tipo de relatório é suportado
        /// </summary>
        bool ValidarTipoRelatorio(string tipoRelatorio);

        /// <summary>
        /// Obtém o título descritivo do relatório
        /// </summary>
        string ObterTituloRelatorio(string tipoRelatorio);

        // Métodos auxiliares para obtenção de dados específicos
        Task<IEnumerable<dynamic>> ObterDadosAuditoriaAsync(DateTime? dataInicio, DateTime? dataFim);
        Task<IEnumerable<dynamic>> ObterDadosProdutividadeAsync(DateTime? dataInicio, DateTime? dataFim);
        Task<IEnumerable<dynamic>> ObterDadosClassificacaoAsync(DateTime? dataInicio, DateTime? dataFim, string? categoria = null, string? status = null);
        Task<IEnumerable<dynamic>> ObterDadosLotesAsync(DateTime? dataInicio, DateTime? dataFim, string? status = null);
        Task<dynamic> ObterDadosConsolidadoAsync(DateTime? dataInicio, DateTime? dataFim);
        Task<IEnumerable<dynamic>> ObterDadosLgpdAsync(DateTime? dataInicio, DateTime? dataFim);
    }
}
