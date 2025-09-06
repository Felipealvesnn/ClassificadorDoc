using Microsoft.AspNetCore.Mvc;
using FastReport.Web;

namespace ClassificadorDoc.Controllers
{
    public class TestReportController : Controller
    {
        private readonly IWebHostEnvironment _hostEnvironment;

        public TestReportController(IWebHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Preview(string templateName = "ClassificacaoTemplate")
        {
            try
            {
                // Armazenar parâmetros na sessão para o WebReport
                HttpContext.Session.SetString("ReportId", Guid.NewGuid().ToString());
                HttpContext.Session.SetString("TemplateName", templateName);

                // Dados de exemplo para o relatório
                var dadosExemplo = new[]
                {
                    new {
                        Id = 1,
                        Documento = "Contrato_001.pdf",
                        Categoria = "Contrato",
                        DataProcessamento = DateTime.Now.AddDays(-1),
                        Status = "Processado",
                        Confianca = 95.5
                    },
                    new {
                        Id = 2,
                        Documento = "Fatura_123.pdf",
                        Categoria = "Fatura",
                        DataProcessamento = DateTime.Now.AddDays(-2),
                        Status = "Processado",
                        Confianca = 89.2
                    },
                    new {
                        Id = 3,
                        Documento = "RG_Scan.pdf",
                        Categoria = "Documento Pessoal",
                        DataProcessamento = DateTime.Now,
                        Status = "Em Análise",
                        Confianca = 78.9
                    }
                };

                HttpContext.Session.SetString("ReportData", System.Text.Json.JsonSerializer.Serialize(dadosExemplo));

                ViewBag.TemplateName = templateName;
                ViewBag.ReportId = HttpContext.Session.GetString("ReportId");

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Erro ao preparar relatório: {ex.Message}";
                return View();
            }
        }

        public IActionResult WebReport()
        {
            try
            {
                var reportId = HttpContext.Session.GetString("ReportId");
                var templateName = HttpContext.Session.GetString("TemplateName") ?? "ClassificacaoTemplate";

                if (string.IsNullOrEmpty(reportId))
                {
                    return BadRequest("Sessão expirada. Gere o relatório novamente.");
                }

                var templatePath = Path.Combine(_hostEnvironment.WebRootPath ?? _hostEnvironment.ContentRootPath,
                    "Reports", "Templates", $"{templateName}.frx");

                if (!System.IO.File.Exists(templatePath))
                {
                    return BadRequest($"Template {templateName} não encontrado em {templatePath}");
                }

                var webReport = new WebReport();
                webReport.Report.Load(templatePath);

                // Configurar dados do relatório
                var jsonData = HttpContext.Session.GetString("ReportData");
                if (!string.IsNullOrEmpty(jsonData))
                {
                    var dados = System.Text.Json.JsonSerializer.Deserialize<object[]>(jsonData);
                    webReport.Report.RegisterData(dados, "Dados");
                }

                // Configurar toolbar do WebReport
                webReport.Toolbar.Show = true;
                webReport.Toolbar.ShowPrint = true;
                webReport.Toolbar.ShowRefresh = true;
                webReport.Toolbar.ShowExports = true;
                webReport.Toolbar.ShowPrevPage = true;
                webReport.Toolbar.ShowNextPage = true;
                webReport.Toolbar.ShowZoom = true;

                // Configurar exportações disponíveis
                webReport.Toolbar.Exports.ShowPreparedReport = true;
                webReport.Toolbar.Exports.ShowPdf = true;
                webReport.Toolbar.Exports.ShowExcel = true;
                webReport.Toolbar.Exports.ShowWord = true;
                webReport.Toolbar.Exports.ShowHtml = true;

                webReport.Width = "100%";
                webReport.Height = "700px";

                return View(webReport);
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao gerar WebReport: {ex.Message}");
            }
        }
    }
}
