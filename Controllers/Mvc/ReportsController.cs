using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Services;
using Microsoft.AspNetCore.Authorization;
using System.Data;

namespace ClassificadorDoc.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(IReportService reportService, ILogger<ReportsController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        /// <summary>
        /// Página principal de relatórios com filtros
        /// </summary>
        public IActionResult Index()
        {
            ViewBag.TiposRelatorio = new[]
            {
                new { Value = "auditoria", Text = "Relatório de Auditoria" },
                new { Value = "produtividade", Text = "Relatório de Produtividade" },
                new { Value = "classificacao", Text = "Relatório de Classificação" },
                new { Value = "lotes", Text = "Relatório de Lotes" },
                new { Value = "consolidado", Text = "Relatório Consolidado" },
                new { Value = "lgpd", Text = "Relatório LGPD" }
            };

            ViewBag.FormatosExport = new[]
            {
                new { Value = "pdf", Text = "PDF" },
                new { Value = "excel", Text = "Excel (XLSX)" }
            };

            return View();
        }

        /// <summary>
        /// Gera relatório baseado nos filtros selecionados
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GerarRelatorio(
            string tipoRelatorio,
            DateTime? dataInicio,
            DateTime? dataFim,
            string? categoria,
            string? status,
            string formato = "pdf")
        {
            try
            {
                var inicioData = dataInicio ?? DateTime.Now.AddDays(-30);
                var fimData = dataFim ?? DateTime.Now;

                _logger.LogInformation("Gerando relatório {Tipo} em formato {Formato} para período: {StartDate} - {EndDate}",
                    tipoRelatorio, formato, inicioData, fimData);

                byte[] fileBytes;
                string fileName;
                string contentType;

                // Usar os métodos do ReportService que já utilizam FastReport
                if (formato.ToLower() == "excel")
                {
                    // FastReport pode exportar para Excel nativamente
                    fileBytes = await GerarRelatorioExcel(tipoRelatorio, inicioData, fimData, categoria, status);
                    fileName = $"relatorio_{tipoRelatorio}_{inicioData:yyyyMMdd}_{fimData:yyyyMMdd}.xlsx";
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                }
                else
                {
                    // Gerar PDF usando FastReport
                    fileBytes = await GerarRelatorioPDF(tipoRelatorio, inicioData, fimData, categoria, status);
                    fileName = $"relatorio_{tipoRelatorio}_{inicioData:yyyyMMdd}_{fimData:yyyyMMdd}.pdf";
                    contentType = "application/pdf";
                }

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar relatório {Tipo}", tipoRelatorio);
                TempData["Error"] = $"Erro ao gerar relatório: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Visualizar relatório na tela (preview)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> VisualizarRelatorio(
            string tipoRelatorio,
            DateTime? dataInicio,
            DateTime? dataFim,
            string? categoria,
            string? status)
        {
            try
            {
                var inicioData = dataInicio ?? DateTime.Now.AddDays(-30);
                var fimData = dataFim ?? DateTime.Now;

                var dataTable = await ObterDadosRelatorio(tipoRelatorio, inicioData, fimData, categoria, status);

                var model = new RelatorioPreviewModel
                {
                    TipoRelatorio = ObterNomeRelatorio(tipoRelatorio),
                    DataInicio = inicioData,
                    DataFim = fimData,
                    Categoria = categoria,
                    Status = status,
                    Dados = dataTable
                };

                return View("Preview", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao visualizar relatório {Tipo}", tipoRelatorio);
                TempData["Error"] = $"Erro ao visualizar relatório: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        private async Task<byte[]> GerarRelatorioPDF(string tipoRelatorio, DateTime dataInicio, DateTime dataFim, string? categoria, string? status)
        {
            return tipoRelatorio.ToLower() switch
            {
                "auditoria" => await _reportService.GerarRelatorioAuditoria(dataInicio, dataFim),
                "produtividade" => await _reportService.GerarRelatorioProdutividade(dataInicio, dataFim),
                "classificacao" => await _reportService.GerarRelatorioClassificacao(dataInicio, dataFim, categoria),
                "lotes" => await _reportService.GerarRelatorioLotes(dataInicio, dataFim, status),
                "consolidado" => await _reportService.GerarRelatorioConsolidado(dataInicio, dataFim),
                "lgpd" => await _reportService.GerarRelatorioLGPD(dataInicio, dataFim),
                _ => throw new ArgumentException($"Tipo de relatório inválido: {tipoRelatorio}")
            };
        }

        private async Task<byte[]> GerarRelatorioExcel(string tipoRelatorio, DateTime dataInicio, DateTime dataFim, string? categoria, string? status)
        {
            // Por enquanto, Excel será igual ao PDF até implementarmos exportação específica
            return await GerarRelatorioPDF(tipoRelatorio, dataInicio, dataFim, categoria, status);
        }

        private async Task<DataTable> ObterDadosRelatorio(string tipoRelatorio, DateTime dataInicio, DateTime dataFim, string? categoria, string? status)
        {
            // Por enquanto, dados de exemplo. Depois pode conectar com o banco real
            return tipoRelatorio.ToLower() switch
            {
                "auditoria" => await ObterDadosAuditoria(dataInicio, dataFim),
                "produtividade" => await ObterDadosProdutividade(dataInicio, dataFim),
                "classificacao" => await ObterDadosClassificacao(dataInicio, dataFim),
                "lotes" => await ObterDadosLotes(dataInicio, dataFim),
                "consolidado" => await ObterDadosConsolidado(dataInicio, dataFim),
                "lgpd" => await ObterDadosLGPD(dataInicio, dataFim),
                _ => throw new ArgumentException($"Tipo de relatório inválido: {tipoRelatorio}")
            };
        }

        private string ObterNomeRelatorio(string tipo)
        {
            return tipo.ToLower() switch
            {
                "auditoria" => "Relatório de Auditoria",
                "produtividade" => "Relatório de Produtividade",
                "classificacao" => "Relatório de Classificação",
                "lotes" => "Relatório de Lotes",
                "consolidado" => "Relatório Consolidado",
                "lgpd" => "Relatório LGPD",
                _ => "Relatório"
            };
        }

        // Métodos auxiliares para obter dados (implementação simplificada para preview)
        private async Task<DataTable> ObterDadosAuditoria(DateTime dataInicio, DateTime dataFim)
        {
            var dt = new DataTable();
            dt.Columns.Add("Data/Hora", typeof(string));
            dt.Columns.Add("Usuário", typeof(string));
            dt.Columns.Add("Ação", typeof(string));
            dt.Columns.Add("Categoria", typeof(string));
            dt.Columns.Add("IP", typeof(string));
            dt.Columns.Add("Detalhes", typeof(string));

            dt.Rows.Add(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "Admin", "Login", "SECURITY", "192.168.1.1", "Login realizado com sucesso");
            dt.Rows.Add(DateTime.Now.AddHours(-1).ToString("dd/MM/yyyy HH:mm"), "User1", "Classificar", "DOCUMENT", "192.168.1.100", "Documento classificado como 'Contrato'");

            return await Task.FromResult(dt);
        }

        private async Task<DataTable> ObterDadosProdutividade(DateTime dataInicio, DateTime dataFim)
        {
            var dt = new DataTable();
            dt.Columns.Add("Usuário", typeof(string));
            dt.Columns.Add("Logins", typeof(int));
            dt.Columns.Add("Tempo Online", typeof(string));
            dt.Columns.Add("Páginas Acessadas", typeof(int));
            dt.Columns.Add("Última Atividade", typeof(string));
            dt.Columns.Add("Eficiência", typeof(string));

            dt.Rows.Add("Admin", 25, "40h 30m", 150, DateTime.Now.AddDays(-1).ToString("dd/MM/yyyy"), "Alta");
            dt.Rows.Add("User1", 18, "32h 15m", 120, DateTime.Now.AddDays(-2).ToString("dd/MM/yyyy"), "Média");

            return await Task.FromResult(dt);
        }

        private async Task<DataTable> ObterDadosClassificacao(DateTime dataInicio, DateTime dataFim)
        {
            var dt = new DataTable();
            dt.Columns.Add("Documento", typeof(string));
            dt.Columns.Add("Categoria", typeof(string));
            dt.Columns.Add("Confiança", typeof(string));
            dt.Columns.Add("Processado", typeof(string));
            dt.Columns.Add("Status", typeof(string));
            dt.Columns.Add("Tempo (ms)", typeof(int));

            dt.Rows.Add("documento1.pdf", "Contratos", "95%", DateTime.Now.ToString("dd/MM/yyyy"), "Sucesso", 1250);
            dt.Rows.Add("documento2.pdf", "Faturas", "88%", DateTime.Now.AddHours(-2).ToString("dd/MM/yyyy"), "Sucesso", 890);

            return await Task.FromResult(dt);
        }

        private async Task<DataTable> ObterDadosLotes(DateTime dataInicio, DateTime dataFim)
        {
            var dt = new DataTable();
            dt.Columns.Add("Lote", typeof(string));
            dt.Columns.Add("Status", typeof(string));
            dt.Columns.Add("Iniciado", typeof(string));
            dt.Columns.Add("Concluído", typeof(string));
            dt.Columns.Add("Total", typeof(int));
            dt.Columns.Add("Processados", typeof(int));
            dt.Columns.Add("Taxa Sucesso", typeof(string));

            dt.Rows.Add("Lote_001", "Concluído", DateTime.Now.AddDays(-2).ToString("dd/MM/yyyy"), DateTime.Now.AddDays(-1).ToString("dd/MM/yyyy"), 100, 98, "98%");
            dt.Rows.Add("Lote_002", "Em Processamento", DateTime.Now.AddHours(-3).ToString("dd/MM/yyyy"), "", 50, 35, "70%");

            return await Task.FromResult(dt);
        }

        private async Task<DataTable> ObterDadosConsolidado(DateTime dataInicio, DateTime dataFim)
        {
            var dt = new DataTable();
            dt.Columns.Add("Métrica", typeof(string));
            dt.Columns.Add("Valor", typeof(string));
            dt.Columns.Add("Descrição", typeof(string));

            dt.Rows.Add("Usuários Ativos", "15", "Total de usuários ativos no sistema");
            dt.Rows.Add("Documentos Processados", "1,250", "Documentos processados no período");
            dt.Rows.Add("Taxa de Sucesso", "96.5%", "Percentual de processamentos bem-sucedidos");
            dt.Rows.Add("Lotes Concluídos", "45", "Lotes finalizados no período");

            return await Task.FromResult(dt);
        }

        private async Task<DataTable> ObterDadosLGPD(DateTime dataInicio, DateTime dataFim)
        {
            var dt = new DataTable();
            dt.Columns.Add("Data/Hora", typeof(string));
            dt.Columns.Add("Evento", typeof(string));
            dt.Columns.Add("Titular", typeof(string));
            dt.Columns.Add("Tipo Dados", typeof(string));
            dt.Columns.Add("Base Legal", typeof(string));
            dt.Columns.Add("Finalidade", typeof(string));
            dt.Columns.Add("Status", typeof(string));

            dt.Rows.Add(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "Coleta", "João Silva", "Nome e Email", "Consentimento", "Cadastro no sistema", "Conforme");
            dt.Rows.Add(DateTime.Now.AddHours(-1).ToString("dd/MM/yyyy HH:mm"), "Consulta", "Maria Santos", "Dados Pessoais", "Interesse Legítimo", "Processamento de documentos", "Conforme");

            return await Task.FromResult(dt);
        }
    }

    // Modelo para preview
    public class RelatorioPreviewModel
    {
        public string TipoRelatorio { get; set; } = string.Empty;
        public DateTime DataInicio { get; set; }
        public DateTime DataFim { get; set; }
        public string? Categoria { get; set; }
        public string? Status { get; set; }
        public DataTable Dados { get; set; } = new DataTable();
    }
}
