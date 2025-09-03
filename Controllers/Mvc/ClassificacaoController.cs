using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Models;
using ClassificadorDoc.Services;
using System.IO.Compression;

namespace ClassificadorDoc.Controllers.Mvc
{
    [Authorize]
    public class ClassificacaoController : Controller
    {
        private readonly IClassificadorService _classificador;
        private readonly PdfExtractorService _pdfExtractor;
        private readonly ILogger<ClassificacaoController> _logger;

        public ClassificacaoController(
            IClassificadorService classificador,
            PdfExtractorService pdfExtractor,
            ILogger<ClassificacaoController> logger)
        {
            _classificador = classificador;
            _pdfExtractor = pdfExtractor;
            _logger = logger;
        }

        // GET: /Classificacao
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Classificacao/Historico
        public IActionResult Historico()
        {
            // Por enquanto, dados mockados. Futuramente pode vir de banco de dados
            var historico = new List<HistoricoClassificacao>
            {
                new HistoricoClassificacao
                {
                    Id = 1,
                    NomeArquivo = "exemplo_autuacao.pdf",
                    TipoClassificado = "autuacao",
                    DataClassificacao = DateTime.Now.AddDays(-1),
                    Confianca = 0.95m,
                    Usuario = User.Identity?.Name ?? "Usuário"
                },
                new HistoricoClassificacao
                {
                    Id = 2,
                    NomeArquivo = "lote_documentos.zip",
                    TipoClassificado = "lote",
                    DataClassificacao = DateTime.Now.AddDays(-2),
                    Confianca = 0.88m,
                    Usuario = User.Identity?.Name ?? "Usuário"
                }
            };

            return View(historico);
        }

        // POST: /Classificacao/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile arquivo, string metodo = "texto")
        {
            try
            {
                if (arquivo == null || arquivo.Length == 0)
                {
                    ViewBag.Erro = "Nenhum arquivo foi selecionado.";
                    return View("Index");
                }

                var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

                if (extensao == ".zip")
                {
                    return await ProcessarZip(arquivo, metodo);
                }
                else if (extensao == ".pdf")
                {
                    return await ProcessarPdfIndividual(arquivo, metodo);
                }
                else
                {
                    ViewBag.Erro = "Apenas arquivos PDF ou ZIP são aceitos.";
                    return View("Index");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar upload");
                ViewBag.Erro = $"Erro ao processar arquivo: {ex.Message}";
                return View("Index");
            }
        }

        private async Task<IActionResult> ProcessarPdfIndividual(IFormFile arquivo, string metodo)
        {
            try
            {
                // Por enquanto, só processamento visual está disponível
                using var memoryStream = new MemoryStream();
                await arquivo.CopyToAsync(memoryStream);
                var pdfBytes = memoryStream.ToArray();
                var classificacao = await _classificador.ClassificarDocumentoPdfAsync(arquivo.FileName, pdfBytes);

                var resultado = new ResultadoClassificacaoView
                {
                    Sucesso = true,
                    Metodo = "visual",
                    TotalDocumentos = 1,
                    Documentos = new List<DocumentoClassificacao> { classificacao }
                };

                return View("Resultado", resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar PDF individual");
                ViewBag.Erro = $"Erro ao processar PDF: {ex.Message}";
                return View("Index");
            }
        }

        private async Task<IActionResult> ProcessarZip(IFormFile arquivo, string metodo)
        {
            try
            {
                var documentos = new List<DocumentoClassificacao>();

                using var zipStream = arquivo.OpenReadStream();
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        // Por enquanto, só processamento visual está disponível
                        using var entryStream = entry.Open();
                        using var memoryStream = new MemoryStream();
                        await entryStream.CopyToAsync(memoryStream);
                        var pdfBytes = memoryStream.ToArray();
                        var classificacao = await _classificador.ClassificarDocumentoPdfAsync(entry.Name, pdfBytes);

                        documentos.Add(classificacao);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar {Arquivo}", entry.Name);
                        documentos.Add(new DocumentoClassificacao
                        {
                            NomeArquivo = entry.Name,
                            TipoDocumento = "erro",
                            ConfiancaClassificacao = 0,
                            ResumoConteudo = $"Erro: {ex.Message}",
                            ProcessadoComSucesso = false,
                            ErroProcessamento = ex.Message
                        });
                    }
                }

                var resultado = new ResultadoClassificacaoView
                {
                    Sucesso = true,
                    Metodo = "visual",
                    TotalDocumentos = documentos.Count,
                    Documentos = documentos
                };

                return View("Resultado", resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar ZIP");
                ViewBag.Erro = $"Erro ao processar ZIP: {ex.Message}";
                return View("Index");
            }
        }

        // GET: /Classificacao/TiposDocumento
        public IActionResult TiposDocumento()
        {
            var tipos = new
            {
                tipos = new[] { "autuacao", "defesa", "notificacao_penalidade", "outros" },
                descricoes = new
                {
                    autuacao = "Auto de Infração de Trânsito (AIT), Notificação de Autuação",
                    defesa = "Defesa de Autuação, Recurso JARI/CETRAN, Defesa Prévia, Indicação de Condutor",
                    notificacao_penalidade = "Notificação da Penalidade (NIP), Intimação para pagamento",
                    outros = "Outros documentos de trânsito"
                }
            };

            return Json(tipos);
        }
    }

    // Model para a view de resultado
    public class ResultadoClassificacaoView
    {
        public bool Sucesso { get; set; }
        public string Metodo { get; set; } = string.Empty;
        public int TotalDocumentos { get; set; }
        public List<DocumentoClassificacao> Documentos { get; set; } = new();
    }

    // Model para histórico
    public class HistoricoClassificacao
    {
        public int Id { get; set; }
        public string NomeArquivo { get; set; } = string.Empty;
        public string TipoClassificado { get; set; } = string.Empty;
        public DateTime DataClassificacao { get; set; }
        public decimal Confianca { get; set; }
        public string Usuario { get; set; } = string.Empty;
    }
}
