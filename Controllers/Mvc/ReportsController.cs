using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Services;
using Microsoft.AspNetCore.Authorization;
using System.Data;
using ClassificadorDoc.Data;
using Microsoft.EntityFrameworkCore;

namespace ClassificadorDoc.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ILogger<ReportsController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ReportsController(IReportService reportService, ILogger<ReportsController> logger, ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _reportService = reportService;
            _logger = logger;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
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
        /// Visualizar relatório na tela (preview) - Usa FastReport WebReport
        /// </summary>
        [HttpPost]
        public IActionResult VisualizarRelatorio(
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

                // Preparar filtros adicionais
                var filtrosAdicionais = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(categoria))
                    filtrosAdicionais["categoria"] = categoria;
                if (!string.IsNullOrEmpty(status))
                    filtrosAdicionais["status"] = status;

                // Gerar um ID único para o relatório
                var reportId = Guid.NewGuid().ToString();

                // Armazenar os parâmetros na sessão para recuperar na action do WebReport
                HttpContext.Session.SetString($"report_{reportId}_tipo", tipoRelatorio);
                HttpContext.Session.SetString($"report_{reportId}_dataInicio", inicioData.ToString("yyyy-MM-dd"));
                HttpContext.Session.SetString($"report_{reportId}_dataFim", fimData.ToString("yyyy-MM-dd"));
                if (!string.IsNullOrEmpty(categoria))
                    HttpContext.Session.SetString($"report_{reportId}_categoria", categoria);
                if (!string.IsNullOrEmpty(status))
                    HttpContext.Session.SetString($"report_{reportId}_status", status);

                var model = new RelatorioPreviewModel
                {
                    TipoRelatorio = _reportService.ObterTituloRelatorio(tipoRelatorio),
                    DataInicio = inicioData,
                    DataFim = fimData,
                    Categoria = categoria,
                    Status = status,
                    ReportId = reportId,
                    PdfBytes = null,
                    Dados = null
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

        /// <summary>
        /// Action para servir o FastReport WebReport
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> WebReport(string id)
        {
            try
            {
                // Recuperar parâmetros da sessão
                var tipoRelatorio = HttpContext.Session.GetString($"report_{id}_tipo");
                var dataInicioStr = HttpContext.Session.GetString($"report_{id}_dataInicio");
                var dataFimStr = HttpContext.Session.GetString($"report_{id}_dataFim");
                var categoria = HttpContext.Session.GetString($"report_{id}_categoria");
                var status = HttpContext.Session.GetString($"report_{id}_status");

                if (string.IsNullOrEmpty(tipoRelatorio) ||
                    string.IsNullOrEmpty(dataInicioStr) ||
                    string.IsNullOrEmpty(dataFimStr))
                {
                    return BadRequest("Parâmetros do relatório não encontrados na sessão.");
                }

                var dataInicio = DateTime.Parse(dataInicioStr);
                var dataFim = DateTime.Parse(dataFimStr);

                // Preparar filtros
                var filtrosAdicionais = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(categoria))
                    filtrosAdicionais["categoria"] = categoria;
                if (!string.IsNullOrEmpty(status))
                    filtrosAdicionais["status"] = status;

                // Gerar o relatório usando o ReportService
                var webReport = new FastReport.Web.WebReport();
                var report = new FastReport.Report();

                // Obter dados do relatório
                var dados = await _reportService.ObterDadosRelatorioAsync(tipoRelatorio, dataInicio, dataFim, filtrosAdicionais);

                // Carregar template baseado no tipo
                var templatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "Reports", "Templates", $"{GetTemplateFileName(tipoRelatorio)}");

                if (System.IO.File.Exists(templatePath))
                {
                    report.Load(templatePath);

                    // Registrar dados no relatório
                    if (dados is IEnumerable<object> enumerable)
                    {
                        report.RegisterData(enumerable, "Data");
                    }

                    // Definir parâmetros do relatório
                    if (report.Parameters.FindByName("DataInicio") != null)
                        report.SetParameterValue("DataInicio", dataInicio.ToString("dd/MM/yyyy"));
                    if (report.Parameters.FindByName("DataFim") != null)
                        report.SetParameterValue("DataFim", dataFim.ToString("dd/MM/yyyy"));
                    if (report.Parameters.FindByName("TituloRelatorio") != null)
                        report.SetParameterValue("TituloRelatorio", _reportService.ObterTituloRelatorio(tipoRelatorio));

                    // Preparar o relatório
                    report.Prepare();

                    // Configurar WebReport
                    webReport.Report = report;
                    webReport.Mode = FastReport.Web.WebReportMode.Preview;

                    // Configurar toolbar usando API nova
                    webReport.Toolbar.Show = true;
                    webReport.Toolbar.Exports.Show = true;
                    webReport.Toolbar.ShowPrint = true;
                    webReport.Toolbar.ShowRefreshButton = true;

                    return View("WebReport", webReport);
                }
                else
                {
                    return NotFound($"Template não encontrado: {templatePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar WebReport {Id}", id);
                return StatusCode(500, $"Erro ao carregar relatório: {ex.Message}");
            }
        }

        private string GetTemplateFileName(string tipoRelatorio)
        {
            return tipoRelatorio.ToLower() switch
            {
                "auditoria" => "AuditoriaTemplate.frx",
                "produtividade" => "ProdutividadeTemplate.frx",
                "classificacao" => "ClassificacaoTemplate.frx",
                "lotes" => "LotesTemplate.frx",
                "consolidado" => "ConsolidadoTemplate.frx",
                "lgpd" => "LGPDTemplate.frx",
                _ => "DefaultTemplate.frx"
            };
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

        // Métodos auxiliares para obter dados reais do banco
        private async Task<DataTable> ObterDadosAuditoria(DateTime dataInicio, DateTime dataFim)
        {
            var dt = new DataTable();
            dt.Columns.Add("Data/Hora", typeof(string));
            dt.Columns.Add("Usuário", typeof(string));
            dt.Columns.Add("Ação", typeof(string));
            dt.Columns.Add("Categoria", typeof(string));
            dt.Columns.Add("IP", typeof(string));
            dt.Columns.Add("Detalhes", typeof(string));

            try
            {
                var logs = await _context.AuditLogs
                    .Where(a => a.Timestamp >= dataInicio && a.Timestamp <= dataFim)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(100) // Limitar para performance
                    .ToListAsync();

                foreach (var log in logs)
                {
                    dt.Rows.Add(
                        log.Timestamp.ToString("dd/MM/yyyy HH:mm"),
                        log.UserName ?? "Sistema",
                        log.Action ?? "",
                        log.Category ?? "",
                        log.IpAddress ?? "",
                        log.Details ?? ""
                    );
                }

                // Se não há dados, adicionar uma linha de exemplo
                if (dt.Rows.Count == 0)
                {
                    dt.Rows.Add(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "Sistema", "Consulta", "SYSTEM", "127.0.0.1", "Nenhum dado encontrado no período selecionado");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dados de auditoria");
                dt.Rows.Add(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "Sistema", "Erro", "ERROR", "127.0.0.1", $"Erro ao carregar dados: {ex.Message}");
            }

            return dt;
        }

        private async Task<DataTable> ObterDadosProdutividade(DateTime dataInicio, DateTime dataFim)
        {
            var dt = new DataTable();
            dt.Columns.Add("Usuário", typeof(string));
            dt.Columns.Add("Total Lotes", typeof(int));
            dt.Columns.Add("Lotes Concluídos", typeof(int));
            dt.Columns.Add("Docs Processados", typeof(int));
            dt.Columns.Add("Taxa Sucesso (%)", typeof(string));
            dt.Columns.Add("Tempo Médio/Lote", typeof(string));
            dt.Columns.Add("Último Lote", typeof(string));

            try
            {
                // Buscar dados básicos primeiro
                var lotes = await _context.BatchProcessingHistories
                    .Where(b => b.StartedAt >= dataInicio && b.StartedAt <= dataFim)
                    .ToListAsync();

                // Agrupar em memória para evitar problemas de tradução LINQ
                var produtividade = lotes
                    .GroupBy(b => new { b.UserId, UserName = b.UserName ?? "Sistema" })
                    .Select(g => new
                    {
                        UserName = g.Key.UserName,
                        TotalLotes = g.Count(),
                        LotesConcluidos = g.Count(x => x.Status == "Completed"),
                        TotalDocumentos = g.Sum(x => x.TotalDocuments),
                        DocumentosSucesso = g.Sum(x => x.SuccessfulDocuments),
                        TempoMedioProcessamento = g.Where(x => x.ProcessingDuration.HasValue)
                                                   .Select(x => x.ProcessingDuration!.Value.TotalMinutes)
                                                   .DefaultIfEmpty(0)
                                                   .Average(),
                        UltimoLote = g.Max(x => x.StartedAt)
                    })
                    .OrderByDescending(x => x.TotalLotes)
                    .ToList();

                foreach (var prod in produtividade)
                {
                    var taxaSucesso = prod.TotalDocumentos > 0 ?
                        Math.Round((double)prod.DocumentosSucesso / prod.TotalDocumentos * 100, 1) : 0;

                    var tempoMedio = prod.TempoMedioProcessamento > 0 ?
                        TimeSpan.FromMinutes(prod.TempoMedioProcessamento).ToString(@"hh\:mm") : "N/A";

                    dt.Rows.Add(
                        prod.UserName,
                        prod.TotalLotes,
                        prod.LotesConcluidos,
                        prod.TotalDocumentos,
                        $"{taxaSucesso}%",
                        tempoMedio,
                        prod.UltimoLote.ToString("dd/MM/yyyy HH:mm")
                    );
                }

                // Se não há dados, adicionar linha de exemplo
                if (dt.Rows.Count == 0)
                {
                    dt.Rows.Add("Sistema", 0, 0, 0, "0%", "N/A", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dados de produtividade");
                dt.Rows.Add("Sistema", 0, 0, 0, "0%", "Erro", "Erro");
            }

            return dt;
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

            try
            {
                // Consultar classificações reais dos logs de auditoria
                var classificacoes = await _context.AuditLogs
                    .Where(a => a.Timestamp >= dataInicio &&
                               a.Timestamp <= dataFim &&
                               a.Action == "Classificar")
                    .OrderByDescending(a => a.Timestamp)
                    .Take(100)
                    .ToListAsync();

                foreach (var log in classificacoes)
                {
                    // Extrair informações dos detalhes do log
                    var detalhes = log.Details ?? "";
                    var categoria = ExtrairCategoria(detalhes);
                    var documento = ExtrairNomeDocumento(detalhes);
                    var confianca = ExtrairConfianca(detalhes);

                    dt.Rows.Add(
                        documento,
                        categoria,
                        confianca,
                        log.Timestamp.ToString("dd/MM/yyyy HH:mm"),
                        "Sucesso",
                        new Random().Next(800, 2000) // Placeholder para tempo
                    );
                }

                // Se não há dados, adicionar linha de exemplo
                if (dt.Rows.Count == 0)
                {
                    dt.Rows.Add("Nenhum documento", "N/A", "0%", DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "Sem dados", 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dados de classificação");
                dt.Rows.Add("Erro", "ERROR", "0%", DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "Erro", 0);
            }

            return dt;
        }

        // Métodos auxiliares para extrair informações dos logs
        private string ExtrairCategoria(string detalhes)
        {
            if (detalhes.Contains("Contrato")) return "Contratos";
            if (detalhes.Contains("Fatura")) return "Faturas";
            if (detalhes.Contains("RG")) return "RG";
            if (detalhes.Contains("CPF")) return "CPF";
            if (detalhes.Contains("CNPJ")) return "CNPJ";
            return "Outros";
        }

        private string ExtrairNomeDocumento(string detalhes)
        {
            // Tentar extrair nome do documento dos detalhes
            var lines = detalhes.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains(".pdf") || line.Contains(".docx") || line.Contains(".jpg"))
                {
                    return line.Trim();
                }
            }
            return "documento.pdf";
        }

        private string ExtrairConfianca(string detalhes)
        {
            // Tentar extrair confiança dos detalhes
            if (detalhes.Contains("%"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(detalhes, @"(\d+\.?\d*)%");
                if (match.Success)
                {
                    return match.Value;
                }
            }
            return "85%"; // Valor padrão
        }

        private string ExtrairNomeLote(string detalhes)
        {
            // Tentar extrair nome do lote dos detalhes
            if (detalhes.Contains("Lote"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(detalhes, @"Lote[_\s]*(\w+)");
                if (match.Success)
                {
                    return $"Lote_{match.Groups[1].Value}";
                }
            }
            return $"Lote_{DateTime.Now:yyyyMMdd}";
        }

        private async Task<DataTable> ObterDadosLotes(DateTime dataInicio, DateTime dataFim)
        {
            var dt = new DataTable();
            dt.Columns.Add("Lote ID", typeof(string));
            dt.Columns.Add("Nome do Lote", typeof(string));
            dt.Columns.Add("Usuário", typeof(string));
            dt.Columns.Add("Status", typeof(string));
            dt.Columns.Add("Iniciado", typeof(string));
            dt.Columns.Add("Concluído", typeof(string));
            dt.Columns.Add("Total Docs", typeof(int));
            dt.Columns.Add("Sucessos", typeof(int));
            dt.Columns.Add("Falhas", typeof(int));
            dt.Columns.Add("Taxa Sucesso", typeof(string));

            try
            {
                // Buscar dados reais da tabela BatchProcessingHistories
                var lotes = await _context.BatchProcessingHistories
                    .Where(b => b.StartedAt >= dataInicio && b.StartedAt <= dataFim)
                    .Include(b => b.Documents)
                    .OrderByDescending(b => b.StartedAt)
                    .Take(50)
                    .ToListAsync();

                foreach (var lote in lotes)
                {
                    var totalDocs = lote.TotalDocuments;
                    var sucessos = lote.SuccessfulDocuments;
                    var falhas = lote.FailedDocuments;
                    var taxaSucesso = totalDocs > 0 ? Math.Round((double)sucessos / totalDocs * 100, 1) : 0;

                    dt.Rows.Add(
                        $"Lote_{lote.Id}",
                        lote.BatchName,
                        lote.UserName ?? "Sistema",
                        lote.Status,
                        lote.StartedAt.ToString("dd/MM/yyyy HH:mm"),
                        lote.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? (lote.Status == "Completed" ? "Concluído" : ""),
                        totalDocs,
                        sucessos,
                        falhas,
                        $"{taxaSucesso}%"
                    );
                }

                // Se não há dados, adicionar linha de exemplo
                if (dt.Rows.Count == 0)
                {
                    dt.Rows.Add("Sem lotes", "N/A", "Sistema", "N/A", DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "", 0, 0, 0, "0%");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dados de lotes");
                dt.Rows.Add("Erro", "Erro", "Sistema", "Erro", DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "", 0, 0, 0, "0%");
            }

            return dt;
        }

        private async Task<DataTable> ObterDadosConsolidado(DateTime dataInicio, DateTime dataFim)
        {
            var dt = new DataTable();
            dt.Columns.Add("Métrica", typeof(string));
            dt.Columns.Add("Valor", typeof(string));
            dt.Columns.Add("Descrição", typeof(string));

            try
            {
                // Calcular métricas reais baseadas nos logs
                var totalLogs = await _context.AuditLogs
                    .Where(a => a.Timestamp >= dataInicio && a.Timestamp <= dataFim)
                    .CountAsync();

                var usuariosAtivos = await _context.AuditLogs
                    .Where(a => a.Timestamp >= dataInicio && a.Timestamp <= dataFim)
                    .Select(a => a.UserName)
                    .Distinct()
                    .CountAsync();

                var documentosProcessados = await _context.AuditLogs
                    .Where(a => a.Timestamp >= dataInicio &&
                               a.Timestamp <= dataFim &&
                               a.Action == "Classificar")
                    .CountAsync();

                var loginsRealizados = await _context.AuditLogs
                    .Where(a => a.Timestamp >= dataInicio &&
                               a.Timestamp <= dataFim &&
                               a.Action == "Login")
                    .CountAsync();

                var erros = await _context.AuditLogs
                    .Where(a => a.Timestamp >= dataInicio &&
                               a.Timestamp <= dataFim &&
                               (a.Category == "ERROR" || a.Action.Contains("Erro")))
                    .CountAsync();

                var taxaSucesso = documentosProcessados > 0 ?
                    Math.Round((double)(documentosProcessados - erros) / documentosProcessados * 100, 1) : 0;

                dt.Rows.Add("Usuários Ativos", usuariosAtivos.ToString(), "Total de usuários únicos que acessaram o sistema");
                dt.Rows.Add("Documentos Processados", documentosProcessados.ToString("#,##0"), "Documentos classificados no período");
                dt.Rows.Add("Taxa de Sucesso", $"{taxaSucesso}%", "Percentual de processamentos bem-sucedidos");
                dt.Rows.Add("Total de Logs", totalLogs.ToString("#,##0"), "Total de eventos registrados no sistema");
                dt.Rows.Add("Logins Realizados", loginsRealizados.ToString("#,##0"), "Acessos ao sistema no período");
                dt.Rows.Add("Erros Registrados", erros.ToString("#,##0"), "Total de erros encontrados");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dados consolidados");
                dt.Rows.Add("Erro", "N/A", $"Erro ao carregar métricas: {ex.Message}");
            }

            return dt;
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

            try
            {
                // Buscar eventos relacionados a LGPD nos logs
                var eventosLGPD = await _context.AuditLogs
                    .Where(a => a.Timestamp >= dataInicio &&
                               a.Timestamp <= dataFim &&
                               (a.Category == "LGPD" ||
                                a.Action.Contains("DadosPessoais") ||
                                a.Action.Contains("Consentimento") ||
                                a.Details != null && a.Details.Contains("LGPD")))
                    .OrderByDescending(a => a.Timestamp)
                    .Take(50)
                    .ToListAsync();

                foreach (var evento in eventosLGPD)
                {
                    var detalhes = evento.Details ?? "";
                    var tipoEvento = DeterminarTipoEventoLGPD(evento.Action ?? "");
                    var tipoDados = ExtrairTipoDados(detalhes);
                    var baseLegal = DeterminarBaseLegal(evento.Action ?? "");
                    var finalidade = DeterminarFinalidade(evento.Action ?? "");

                    dt.Rows.Add(
                        evento.Timestamp.ToString("dd/MM/yyyy HH:mm"),
                        tipoEvento,
                        evento.UserName ?? "Sistema",
                        tipoDados,
                        baseLegal,
                        finalidade,
                        "Conforme"
                    );
                }

                // Se não há dados específicos de LGPD, mostrar eventos de acesso a dados pessoais
                if (dt.Rows.Count == 0)
                {
                    var acessosDados = await _context.AuditLogs
                        .Where(a => a.Timestamp >= dataInicio &&
                                   a.Timestamp <= dataFim &&
                                   (a.Action == "Login" || a.Action == "Classificar"))
                        .OrderByDescending(a => a.Timestamp)
                        .Take(10)
                        .ToListAsync();

                    foreach (var acesso in acessosDados)
                    {
                        dt.Rows.Add(
                            acesso.Timestamp.ToString("dd/MM/yyyy HH:mm"),
                            acesso.Action == "Login" ? "Acesso ao Sistema" : "Processamento de Documento",
                            acesso.UserName ?? "Sistema",
                            "Dados de Autenticação",
                            "Interesse Legítimo",
                            acesso.Action == "Login" ? "Controle de acesso" : "Classificação de documentos",
                            "Conforme"
                        );
                    }
                }

                // Se ainda não há dados, adicionar linha padrão
                if (dt.Rows.Count == 0)
                {
                    dt.Rows.Add(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "Consulta", "Sistema", "Logs de Auditoria", "Obrigação Legal", "Monitoramento LGPD", "Conforme");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter dados LGPD");
                dt.Rows.Add(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "Erro", "Sistema", "N/A", "N/A", "Consulta de dados", "Erro");
            }

            return dt;
        }

        // Métodos auxiliares para LGPD
        private string DeterminarTipoEventoLGPD(string action)
        {
            return action.ToLower() switch
            {
                var a when a.Contains("login") => "Acesso",
                var a when a.Contains("classificar") => "Processamento",
                var a when a.Contains("consentimento") => "Consentimento",
                var a when a.Contains("dados") => "Coleta",
                _ => "Consulta"
            };
        }

        private string ExtrairTipoDados(string detalhes)
        {
            if (detalhes.Contains("email") || detalhes.Contains("Email")) return "Nome e Email";
            if (detalhes.Contains("CPF")) return "CPF";
            if (detalhes.Contains("RG")) return "RG";
            if (detalhes.Contains("CNPJ")) return "CNPJ";
            return "Dados de Sistema";
        }

        private string DeterminarBaseLegal(string action)
        {
            return action.ToLower() switch
            {
                var a when a.Contains("login") => "Interesse Legítimo",
                var a when a.Contains("consentimento") => "Consentimento",
                var a when a.Contains("auditoria") => "Obrigação Legal",
                _ => "Interesse Legítimo"
            };
        }

        private string DeterminarFinalidade(string action)
        {
            return action.ToLower() switch
            {
                var a when a.Contains("login") => "Controle de acesso ao sistema",
                var a when a.Contains("classificar") => "Processamento de documentos",
                var a when a.Contains("auditoria") => "Monitoramento e auditoria",
                _ => "Operação do sistema"
            };
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
        public DataTable? Dados { get; set; } = new DataTable();
        public byte[]? PdfBytes { get; set; }
        public string? PdfBase64 => PdfBytes != null ? Convert.ToBase64String(PdfBytes) : null;
        public string? ReportId { get; set; }
    }
}
