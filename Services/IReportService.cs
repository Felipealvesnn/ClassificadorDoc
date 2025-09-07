using System.Data;

namespace ClassificadorDoc.Services
{
    public interface IReportService
    {
        // Métodos originais para compatibilidade

        // Novos métodos para suporte MVC com múltiplos formatos
        /// <summary>
        /// Gera relatório em PDF usando FastReport

        /// Obtém dados para visualização prévia do relatório
        /// </summary>
        Task<dynamic> ObterDadosRelatorioAsync(string tipoRelatorio, DateTime? dataInicio, DateTime? dataFim, Dictionary<string, string>? filtrosAdicionais = null);

      

        // Métodos auxiliares para obtenção de dados específicos
        Task<IEnumerable<dynamic>> ObterDadosAuditoriaAsync(DateTime? dataInicio, DateTime? dataFim);
        Task<IEnumerable<dynamic>> ObterDadosProdutividadeAsync(DateTime? dataInicio, DateTime? dataFim);
        Task<IEnumerable<dynamic>> ObterDadosClassificacaoAsync(DateTime? dataInicio, DateTime? dataFim, string? categoria = null, string? status = null);
        Task<IEnumerable<dynamic>> ObterDadosLotesAsync(DateTime? dataInicio, DateTime? dataFim, string? status = null);
        Task<dynamic> ObterDadosConsolidadoAsync(DateTime? dataInicio, DateTime? dataFim);
        Task<IEnumerable<dynamic>> ObterDadosLgpdAsync(DateTime? dataInicio, DateTime? dataFim);
    }
}
