using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Models;
using ClassificadorDoc.Services;
using ClassificadorDoc.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace ClassificadorDoc.Controllers.Mvc
{
    [Authorize]
    public class ClassificacaoController : Controller
    {
        private readonly IClassificadorService _classificador;
        private readonly PdfExtractorService _pdfExtractor;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ClassificacaoController> _logger;

        public ClassificacaoController(
            IClassificadorService classificador,
            PdfExtractorService pdfExtractor,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ClassificacaoController> logger)
        {
            _classificador = classificador;
            _pdfExtractor = pdfExtractor;
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: /Classificacao
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Classificacao/Upload
        public IActionResult Upload()
        {
            return View();
        }

        // GET: /Classificacao/Historico
        public async Task<IActionResult> Historico()
        {
            var userId = _userManager.GetUserId(User);
            var userName = User.Identity?.Name ?? "Usuário";

            // Buscar histórico real do banco de dados
            var historico = await _context.DocumentProcessingHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.ProcessedAt)
                .ToListAsync();

            var resultado = historico.Select(h => new HistoricoClassificacao
            {
                Id = h.Id,
                NomeArquivo = h.FileName,
                TipoClassificado = h.DocumentType,
                DataClassificacao = h.ProcessedAt,
                Confianca = (decimal)h.Confidence,
                Usuario = userName
            }).Take(100).ToList();

            return View(resultado);
        }

        // POST: /Classificacao/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(100_000_000)] // 100MB para ZIP
        public async Task<IActionResult> Upload(IFormFile arquivo, string metodo = "visual")
        {
            try
            {
                if (arquivo == null || arquivo.Length == 0)
                {
                    ViewBag.Erro = "Nenhum arquivo foi selecionado.";
                    return View("Index");
                }

                var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

                // Aceitar apenas ZIP para processamento em lote
                if (extensao == ".zip")
                {
                    return await ProcessarZip(arquivo, metodo);
                }
                else
                {
                    ViewBag.Erro = "Apenas arquivos ZIP são aceitos. O sistema processa lotes de documentos.";
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

        private async Task<IActionResult> ProcessarZip(IFormFile arquivo, string metodo)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var documentos = new List<DocumentoClassificacao>();
                var totalDocumentosProcessados = 0;

                using var zipStream = arquivo.OpenReadStream();
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (!entry.FullName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        // Usar a API real de classificação
                        using var entryStream = entry.Open();
                        using var memoryStream = new MemoryStream();
                        await entryStream.CopyToAsync(memoryStream);
                        var pdfBytes = memoryStream.ToArray();
                        var classificacao = await _classificador.ClassificarDocumentoPdfAsync(entry.Name, pdfBytes);

                        documentos.Add(classificacao);

                        // Salvar no histórico para cada documento
                        await SalvarHistoricoProcessamento(userId, entry.Name, classificacao);
                        totalDocumentosProcessados++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar {Arquivo}", entry.Name);
                        var classificacaoErro = new DocumentoClassificacao
                        {
                            NomeArquivo = entry.Name,
                            TipoDocumento = "erro",
                            ConfiancaClassificacao = 0,
                            ResumoConteudo = $"Erro: {ex.Message}",
                            ProcessadoComSucesso = false,
                            ErroProcessamento = ex.Message
                        };

                        documentos.Add(classificacaoErro);
                        await SalvarHistoricoProcessamento(userId, entry.Name, classificacaoErro);
                        totalDocumentosProcessados++;
                    }
                }

                // Atualizar produtividade do usuário com total de documentos
                var sucessos = documentos.Count(d => d.ProcessadoComSucesso);
                await AtualizarProdutividadeUsuario(userId, totalDocumentosProcessados, sucessos > 0);

                // Registrar auditoria
                await RegistrarAuditoria(userId, "ClassificacaoLote",
                    $"Processou lote ZIP: {arquivo.FileName} com {totalDocumentosProcessados} documentos, {sucessos} sucessos");

                var resultado = new ResultadoClassificacaoView
                {
                    Sucesso = documentos.Any(d => d.ProcessadoComSucesso),
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

        #region Métodos Auxiliares para Produtividade e Auditoria

        private async Task SalvarHistoricoProcessamento(string? userId, string fileName, DocumentoClassificacao classificacao)
        {
            if (string.IsNullOrEmpty(userId)) return;

            var historico = new DocumentProcessingHistory
            {
                UserId = userId,
                FileName = fileName,
                DocumentType = classificacao.TipoDocumento,
                Confidence = classificacao.ConfiancaClassificacao,
                ProcessedAt = DateTime.UtcNow,
                IsSuccessful = classificacao.ProcessadoComSucesso,
                ErrorMessage = classificacao.ErroProcessamento,
                Keywords = classificacao.PalavrasChaveEncontradas,
                FileSizeBytes = 0 // Seria necessário passar como parâmetro
            };

            _context.DocumentProcessingHistories.Add(historico);
            await _context.SaveChangesAsync();
        }

        private async Task AtualizarProdutividadeUsuario(string? userId, int documentosProcessados, bool sucesso = true)
        {
            if (string.IsNullOrEmpty(userId)) return;

            var hoje = DateTime.Today;

            // Buscar registro de produtividade de hoje
            var produtividade = await _context.UserProductivities
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Date.Date == hoje);

            if (produtividade == null)
            {
                // Criar novo registro
                produtividade = new UserProductivity
                {
                    UserId = userId,
                    Date = hoje,
                    DocumentsProcessed = documentosProcessados,
                    LoginCount = 0,
                    TotalTimeOnline = TimeSpan.Zero,
                    ErrorCount = sucesso ? 0 : 1,
                    SuccessRate = sucesso ? 100.0 : 0.0,
                    PagesAccessed = 0,
                    FirstLogin = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                };
                _context.UserProductivities.Add(produtividade);
            }
            else
            {
                // Atualizar registro existente
                produtividade.DocumentsProcessed += documentosProcessados;
                if (!sucesso)
                {
                    produtividade.ErrorCount += 1;
                }

                // Recalcular taxa de sucesso
                var totalProcessados = produtividade.DocumentsProcessed;
                var sucessos = totalProcessados - produtividade.ErrorCount;
                produtividade.SuccessRate = totalProcessados > 0 ? (double)sucessos / totalProcessados * 100 : 0;
                produtividade.LastActivity = DateTime.UtcNow;
            }

            // Atualizar contador no ApplicationUser
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.DocumentsProcessed += documentosProcessados;
                user.LastDocumentProcessedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }

            await _context.SaveChangesAsync();
        }

        private async Task RegistrarAuditoria(string? userId, string action, string details)
        {
            if (string.IsNullOrEmpty(userId)) return;

            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                Resource = "ClassificacaoController",
                Timestamp = DateTime.UtcNow,
                Details = details,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                Result = "SUCCESS",
                Category = "BUSINESS"
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        #endregion
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
