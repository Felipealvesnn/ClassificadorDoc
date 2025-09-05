using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Models;
using ClassificadorDoc.Services;
using ClassificadorDoc.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using X.PagedList;
using X.PagedList.Extensions;

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
        public async Task<IActionResult> Historico(int? pagina)
        {
            var userId = _userManager.GetUserId(User);
            int paginaAtual = pagina ?? 1;
            int itensPorPagina = 20;

            // Buscar LOTES em vez de documentos individuais
            var lotes = await _context.BatchProcessingHistories
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.StartedAt)
                .Select(b => new HistoricoLoteView
                {
                    BatchId = b.Id,
                    NomeLote = b.BatchName,
                    DataProcessamento = b.StartedAt,
                    TotalDocumentos = b.TotalDocuments,
                    DocumentosSucesso = b.SuccessfulDocuments,
                    DocumentosErro = b.FailedDocuments,
                    TipoPredominante = b.PredominantDocumentType,
                    ConfiancaMedia = b.AverageConfidence,
                    Status = b.Status,
                    TempoProcessamento = b.ProcessingDuration,
                    TamanhoArquivo = b.FileSizeBytes
                })
                .ToListAsync();

            var lotesPaginados = lotes.ToPagedList(paginaAtual, itensPorPagina);

            return View(lotesPaginados);
        }

        // GET: /Classificacao/DetalhesLote/5
        public async Task<IActionResult> DetalhesLote(int id)
        {
            var userId = _userManager.GetUserId(User);

            // Buscar o lote
            var lote = await _context.BatchProcessingHistories
                .Where(b => b.Id == id && b.UserId == userId)
                .FirstOrDefaultAsync();

            if (lote == null)
            {
                return NotFound();
            }

            // Buscar documentos do lote
            var documentos = await _context.DocumentProcessingHistories
                .Where(d => d.BatchProcessingHistoryId == id)
                .OrderByDescending(d => d.ProcessedAt)
                .Select(d => new HistoricoDocumentoView
                {
                    Id = d.Id,
                    NomeArquivo = d.FileName,
                    TipoClassificado = d.DocumentType,
                    Confianca = (decimal)d.Confidence,
                    DataProcessamento = d.ProcessedAt,
                    Sucesso = d.IsSuccessful,
                    MensagemErro = d.ErrorMessage,
                    PalavrasChave = d.Keywords,
                    CaminhoResultado = null, // Para ser implementado futuramente
                    TamanhoBytes = d.FileSizeBytes
                })
                .ToListAsync();

            var viewModel = new DetalhesLoteView
            {
                Lote = new HistoricoLoteView
                {
                    BatchId = lote.Id,
                    NomeLote = lote.BatchName,
                    DataProcessamento = lote.StartedAt,
                    TotalDocumentos = lote.TotalDocuments,
                    DocumentosSucesso = lote.SuccessfulDocuments,
                    DocumentosErro = lote.FailedDocuments,
                    TipoPredominante = lote.PredominantDocumentType,
                    ConfiancaMedia = lote.AverageConfidence,
                    Status = lote.Status,
                    TempoProcessamento = lote.ProcessingDuration,
                    TamanhoArquivo = lote.FileSizeBytes
                },
                Documentos = documentos
            };

            return View(viewModel);
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
                var userName = User.Identity?.Name ?? "Usuário";
                var documentos = new List<DocumentoClassificacao>();
                var totalDocumentosProcessados = 0;
                var startTime = DateTime.UtcNow;

                // Criar registro do lote ANTES de processar
                var batchHistory = new BatchProcessingHistory
                {
                    BatchName = arquivo.FileName,
                    UserId = userId!,
                    UserName = userName,
                    StartedAt = startTime,
                    FileSizeBytes = arquivo.Length,
                    ProcessingMethod = metodo,
                    Status = "Processing",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = HttpContext.Request.Headers["User-Agent"].ToString()
                };

                _context.BatchProcessingHistories.Add(batchHistory);
                await _context.SaveChangesAsync(); // Salva para obter o ID

                using var zipStream = arquivo.OpenReadStream();
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                // Contar total de PDFs primeiro
                var pdfEntries = archive.Entries
                    .Where(e => e.FullName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                batchHistory.TotalDocuments = pdfEntries.Count;
                await _context.SaveChangesAsync();

                var classificacaoSummary = new Dictionary<string, int>();
                var keywordsSummary = new List<string>();
                var confidenceSum = 0.0;
                var sucessos = 0;

                foreach (var entry in pdfEntries)
                {
                    try
                    {
                        // Usar a API real de classificação
                        using var entryStream = entry.Open();
                        using var memoryStream = new MemoryStream();
                        await entryStream.CopyToAsync(memoryStream);
                        var pdfBytes = memoryStream.ToArray();
                        var classificacao = await _classificador.ClassificarDocumentoPdfAsync(entry.Name, pdfBytes);

                        documentos.Add(classificacao);

                        // Salvar no histórico para cada documento COM REFERÊNCIA AO LOTE
                        await SalvarHistoricoProcessamento(userId, entry.Name, classificacao, batchHistory.Id);
                        totalDocumentosProcessados++;

                        if (classificacao.ProcessadoComSucesso)
                        {
                            sucessos++;
                            confidenceSum += classificacao.ConfiancaClassificacao;

                            // Agregar estatísticas do lote
                            var tipo = classificacao.TipoDocumento;
                            classificacaoSummary[tipo] = classificacaoSummary.GetValueOrDefault(tipo, 0) + 1;

                            if (!string.IsNullOrEmpty(classificacao.PalavrasChaveEncontradas))
                            {
                                keywordsSummary.Add(classificacao.PalavrasChaveEncontradas);
                            }
                        }
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
                        await SalvarHistoricoProcessamento(userId, entry.Name, classificacaoErro, batchHistory.Id);
                        totalDocumentosProcessados++;
                    }
                }

                // Finalizar o lote com estatísticas
                var endTime = DateTime.UtcNow;
                batchHistory.CompletedAt = endTime;
                batchHistory.ProcessingDuration = endTime - startTime;
                batchHistory.SuccessfulDocuments = sucessos;
                batchHistory.FailedDocuments = totalDocumentosProcessados - sucessos;
                batchHistory.AverageConfidence = sucessos > 0 ? confidenceSum / sucessos : 0;
                batchHistory.Status = sucessos > 0 ? "Completed" : "Failed";

                // Determinar tipo predominante
                if (classificacaoSummary.Any())
                {
                    batchHistory.PredominantDocumentType = classificacaoSummary
                        .OrderByDescending(kvp => kvp.Value)
                        .First().Key;
                }

                // Serializar resumos como JSON
                batchHistory.ClassificationSummary = System.Text.Json.JsonSerializer.Serialize(classificacaoSummary);
                batchHistory.KeywordsSummary = System.Text.Json.JsonSerializer.Serialize(keywordsSummary.Take(50));

                await _context.SaveChangesAsync();

                // Atualizar produtividade do usuário com total de documentos
                await AtualizarProdutividadeUsuario(userId, totalDocumentosProcessados, sucessos > 0);

                // Registrar auditoria com informações do lote
                await RegistrarAuditoria(userId, "ClassificacaoLote",
                    $"Processou lote ID {batchHistory.Id}: {arquivo.FileName} com {totalDocumentosProcessados} documentos, {sucessos} sucessos, tipo predominante: {batchHistory.PredominantDocumentType}");

                // Retornar JSON para AJAX com informações completas do lote
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("application/json") == true)
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Lote processado com sucesso! {sucessos} de {totalDocumentosProcessados} documentos classificados.",
                        batchId = batchHistory.Id,
                        batchName = batchHistory.BatchName,
                        totalDocuments = totalDocumentosProcessados,
                        successfulDocuments = sucessos,
                        failedDocuments = totalDocumentosProcessados - sucessos,
                        successRate = totalDocumentosProcessados > 0 ? Math.Round((double)sucessos / totalDocumentosProcessados * 100, 1) : 0,
                        averageConfidence = sucessos > 0 ? Math.Round(confidenceSum / sucessos, 1) : 0,
                        predominantType = batchHistory.PredominantDocumentType,
                        processingTime = (DateTime.UtcNow - startTime).TotalSeconds,
                        redirectUrl = Url.Action("Lotes", "Relatorios", new { batchId = batchHistory.Id })
                    });
                }

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

                // Retornar JSON para AJAX com erro
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("application/json") == true)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Erro ao processar lote: {ex.Message}",
                        error = ex.Message
                    });
                }

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

        private async Task SalvarHistoricoProcessamento(string? userId, string fileName, DocumentoClassificacao classificacao, int? batchId = null)
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
                FileSizeBytes = 0, // Seria necessário passar como parâmetro
                BatchProcessingHistoryId = batchId // Novo campo para vincular ao lote
            };

            _context.DocumentProcessingHistories.Add(historico);
            await _context.SaveChangesAsync();
        }

        private async Task AtualizarProdutividadeUsuario(string? userId, int documentosProcessados, bool sucesso = true)
        {
            if (string.IsNullOrEmpty(userId)) return;

            // REFATORADO: UserProductivity agora só gerencia atividade na plataforma,
            // não mais documentos processados (isso vem do BatchProcessingHistory)

            // Atualizar contador no ApplicationUser (mantém por compatibilidade)
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.DocumentsProcessed += documentosProcessados;
                user.LastDocumentProcessedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Novo método específico para atividade na plataforma (logins, navegação)
        /// Separado do processamento de documentos
        /// </summary>
        private async Task RegistrarAtividadePlataforma(string? userId, string tipoAtividade = "PAGE_ACCESS")
        {
            if (string.IsNullOrEmpty(userId)) return;

            var hoje = DateTime.Today;

            // Buscar registro de produtividade de hoje
            var produtividade = await _context.UserProductivities
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Date.Date == hoje);

            if (produtividade == null)
            {
                // Criar novo registro focado em atividade da plataforma
                produtividade = new UserProductivity
                {
                    UserId = userId,
                    Date = hoje,
                    LoginCount = tipoAtividade == "LOGIN" ? 1 : 0,
                    TotalTimeOnline = TimeSpan.Zero,
                    PagesAccessed = tipoAtividade == "PAGE_ACCESS" ? 1 : 0,
                    FirstLogin = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                };
                _context.UserProductivities.Add(produtividade);
            }
            else
            {
                // Atualizar registro existente
                if (tipoAtividade == "LOGIN")
                {
                    produtividade.LoginCount += 1;
                }
                else if (tipoAtividade == "PAGE_ACCESS")
                {
                    produtividade.PagesAccessed += 1;
                }

                produtividade.LastActivity = DateTime.UtcNow;
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

    // Novos ViewModels para abordagem hierárquica
    public class HistoricoLoteView
    {
        public int BatchId { get; set; }
        public string NomeLote { get; set; } = string.Empty;
        public DateTime DataProcessamento { get; set; }
        public int TotalDocumentos { get; set; }
        public int DocumentosSucesso { get; set; }
        public int DocumentosErro { get; set; }
        public string? TipoPredominante { get; set; }
        public double ConfiancaMedia { get; set; }
        public string Status { get; set; } = string.Empty;
        public TimeSpan? TempoProcessamento { get; set; }
        public long TamanhoArquivo { get; set; }

        // Propriedades calculadas
        public decimal TaxaSucesso => TotalDocumentos > 0 ? Math.Round((decimal)DocumentosSucesso / TotalDocumentos * 100, 1) : 0;
        public string TamanhoFormatado => FormatarTamanhoArquivo(TamanhoArquivo);
        public string TempoFormatado => TempoProcessamento?.ToString(@"mm\:ss") ?? "N/A";

        private static string FormatarTamanhoArquivo(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class HistoricoDocumentoView
    {
        public int Id { get; set; }
        public string NomeArquivo { get; set; } = string.Empty;
        public string TipoClassificado { get; set; } = string.Empty;
        public decimal Confianca { get; set; }
        public DateTime DataProcessamento { get; set; }
        public bool Sucesso { get; set; }
        public string? MensagemErro { get; set; }
        public string? PalavrasChave { get; set; }
        public string? CaminhoResultado { get; set; }
        public long TamanhoBytes { get; set; }

        // Propriedades calculadas
        public string Status => Sucesso ? "Completed" : "Failed";
        public string TamanhoFormatado => FormatarTamanhoArquivo(TamanhoBytes);

        private static string FormatarTamanhoArquivo(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class DetalhesLoteView
    {
        public HistoricoLoteView Lote { get; set; } = new();
        public List<HistoricoDocumentoView> Documentos { get; set; } = new();
    }
}
