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
        private readonly IWebHostEnvironment _hostEnvironment;

        public ClassificacaoController(
            IClassificadorService classificador,
            PdfExtractorService pdfExtractor,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ClassificacaoController> logger,
            IWebHostEnvironment hostEnvironment)
        {
            _classificador = classificador;
            _pdfExtractor = pdfExtractor;
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _hostEnvironment = hostEnvironment;
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
        public async Task<IActionResult> Historico(int? pagina, string? status, string? tipo, DateTime? dataInicio, DateTime? dataFim, int? confiancaMinima)
        {
            var userId = _userManager.GetUserId(User);
            int paginaAtual = pagina ?? 1;
            int itensPorPagina = 10;

            // Query base para LOTES
            var query = _context.BatchProcessingHistories
                .Where(b => b.UserId == userId);

            // Aplicar filtros
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(b => b.Status == status);
            }

            if (!string.IsNullOrEmpty(tipo))
            {
                query = query.Where(b => b.PredominantDocumentType == tipo);
            }

            if (dataInicio.HasValue)
            {
                query = query.Where(b => b.StartedAt.Date >= dataInicio.Value.Date);
            }

            if (dataFim.HasValue)
            {
                query = query.Where(b => b.StartedAt.Date <= dataFim.Value.Date);
            }

            if (confiancaMinima.HasValue)
            {
                var confiancaDecimal = confiancaMinima.Value / 100.0;
                query = query.Where(b => b.AverageConfidence >= confiancaDecimal);
            }

            // Buscar LOTES em vez de documentos individuais
            var lotes = await query
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
                    TamanhoArquivo = b.FileSizeBytes,
                    NomeUsuario = b.UserName
                })
                .ToListAsync();

            var lotesPaginados = lotes.ToPagedList(paginaAtual, itensPorPagina);

            // Passar filtros para a view para manter estado
            ViewBag.StatusFiltro = status;
            ViewBag.TipoFiltro = tipo;
            ViewBag.DataInicioFiltro = dataInicio?.ToString("yyyy-MM-dd");
            ViewBag.DataFimFiltro = dataFim?.ToString("yyyy-MM-dd");
            ViewBag.ConfiancaFiltro = confiancaMinima;

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
                    CaminhoResultado = d.CaminhoArquivo, // 📁 USAR O NOVO CAMPO CaminhoArquivo
                    TamanhoBytes = d.FileSizeBytes,

                    // Campos específicos extraídos
                    TextoCompleto = d.TextoCompleto,
                    NumeroAIT = d.NumeroAIT,
                    PlacaVeiculo = d.PlacaVeiculo,
                    NomeCondutor = d.NomeCondutor,
                    NumeroCNH = d.NumeroCNH,
                    TextoDefesa = d.TextoDefesa,
                    DataInfracao = d.DataInfracao,
                    LocalInfracao = d.LocalInfracao,
                    CodigoInfracao = d.CodigoInfracao,
                    ValorMulta = d.ValorMulta,
                    OrgaoAutuador = d.OrgaoAutuador
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

        [HttpGet]
        public async Task<IActionResult> DetalhesDocumento(int id)
        {
            try
            {
                var documento = await _context.DocumentProcessingHistories
                    .Where(d => d.Id == id)
                    .Select(d => new
                    {
                        Id = d.Id,
                        NomeArquivo = d.FileName,
                        TipoClassificado = d.DocumentType,
                        Confianca = d.Confidence,
                        DataProcessamento = d.ProcessedAt,
                        Sucesso = d.IsSuccessful,
                        MensagemErro = d.ErrorMessage,
                        PalavrasChave = d.Keywords,
                        TamanhoBytes = d.FileSizeBytes,

                        // Dados específicos
                        TextoCompleto = d.TextoCompleto,
                        NumeroAIT = d.NumeroAIT,
                        PlacaVeiculo = d.PlacaVeiculo,
                        NomeCondutor = d.NomeCondutor,
                        NumeroCNH = d.NumeroCNH,
                        TextoDefesa = d.TextoDefesa,
                        DataInfracao = d.DataInfracao,
                        LocalInfracao = d.LocalInfracao,
                        CodigoInfracao = d.CodigoInfracao,
                        ValorMulta = d.ValorMulta,
                        OrgaoAutuador = d.OrgaoAutuador,

                        // NOVOS CAMPOS PARA INDICAÇÃO DE CONDUTOR
                        RequerenteNome = d.RequerenteNome,
                        RequerenteCPF = d.RequerenteCPF,
                        RequerenteRG = d.RequerenteRG,
                        RequerenteEndereco = d.RequerenteEndereco,
                        IndicacaoNome = d.IndicacaoNome,
                        IndicacaoCPF = d.IndicacaoCPF,
                        IndicacaoRG = d.IndicacaoRG,
                        IndicacaoCNH = d.IndicacaoCNH,

                        // Campos calculados
                        Status = d.IsSuccessful ? "Completed" : "Failed",
                        ConfiancaPercentual = Math.Round(d.Confidence * 100, 1),
                        TamanhoFormatado = FormatarTamanhoArquivo(d.FileSizeBytes),
                        DataProcessamentoFormatada = d.ProcessedAt.ToString("dd/MM/yyyy HH:mm:ss")
                    })
                    .FirstOrDefaultAsync();

                if (documento == null)
                {
                    return NotFound();
                }

                return Json(documento);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar detalhes do documento {DocumentoId}", id);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> VisualizarDocumento(int id)
        {
            try
            {
                var documento = await _context.DocumentProcessingHistories
                    .Where(d => d.Id == id)
                    .Select(d => new { d.FileName, d.CaminhoArquivo })
                    .FirstOrDefaultAsync();

                if (documento == null)
                {
                    return NotFound("Documento não encontrado");
                }

                if (string.IsNullOrEmpty(documento.CaminhoArquivo))
                {
                    return NotFound("Caminho do arquivo não disponível");
                }

                // O caminho já é absoluto (pasta Documents), então usar diretamente
                var caminhoCompleto = documento.CaminhoArquivo;

                // Se por algum motivo for relativo, combinar com ContentRoot
                if (!Path.IsPathRooted(caminhoCompleto))
                {
                    caminhoCompleto = Path.Combine(_hostEnvironment.ContentRootPath, documento.CaminhoArquivo);
                }

                if (!System.IO.File.Exists(caminhoCompleto))
                {
                    return NotFound("Arquivo não encontrado no disco");
                }

                var bytes = await System.IO.File.ReadAllBytesAsync(caminhoCompleto);
                var extensao = Path.GetExtension(documento.FileName).ToLowerInvariant();

                string contentType = extensao switch
                {
                    ".pdf" => "application/pdf",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".tiff" or ".tif" => "image/tiff",
                    _ => "application/octet-stream"
                };

                // Para todos os arquivos, retornar inline para visualização no navegador
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{documento.FileName}\"";

                return File(bytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao visualizar documento {DocumentoId}", id);
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        // GET: /Classificacao/BaixarDocumento/5
        [HttpGet]
        public async Task<IActionResult> BaixarDocumento(int id)
        {
            try
            {
                var userId = _userManager.GetUserId(User);

                // Buscar documento no histórico com verificação de propriedade
                var documento = await _context.DocumentProcessingHistories
                    .Where(d => d.Id == id && d.UserId == userId)
                    .Select(d => new { d.CaminhoArquivo, d.FileName })
                    .FirstOrDefaultAsync();

                if (documento == null)
                {
                    return NotFound("Documento não encontrado ou você não tem permissão para acessá-lo.");
                }

                if (string.IsNullOrEmpty(documento.CaminhoArquivo))
                {
                    return NotFound("Caminho do arquivo não disponível.");
                }

                // O caminho já é absoluto (pasta Documents), usar diretamente
                var caminhoCompleto = documento.CaminhoArquivo;

                // Se for relativo (compatibilidade), combinar com diretório atual
                if (!Path.IsPathRooted(caminhoCompleto))
                {
                    caminhoCompleto = Path.Combine(Environment.CurrentDirectory, documento.CaminhoArquivo);
                }

                if (!System.IO.File.Exists(caminhoCompleto))
                {
                    return NotFound("Arquivo físico não encontrado no sistema.");
                }

                // Retornar arquivo para download
                var fileBytes = await System.IO.File.ReadAllBytesAsync(caminhoCompleto);
                var contentType = "application/pdf"; // Assumindo que são PDFs

                return File(fileBytes, contentType, documento.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao baixar documento {DocumentoId}", id);
                return StatusCode(500, "Erro interno do servidor ao baixar o documento.");
            }
        }

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
                var startTime = DateTime.Now;

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

                        // 📁 NOVO: Salvar arquivo organizado por tipo
                        var caminhoSalvo = await SalvarArquivoOrganizado(entry.Name, pdfBytes, classificacao.TipoDocumento, batchHistory.Id);

                        // Salvar no histórico para cada documento COM REFERÊNCIA AO LOTE E CAMINHO
                        await SalvarHistoricoProcessamento(userId, entry.Name, classificacao, batchHistory.Id, caminhoSalvo, pdfBytes.Length);
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

                        _logger.LogInformation("✅ Processado: {Arquivo} → {Tipo} → {Caminho}",
                            entry.Name, classificacao.TipoDocumento, caminhoSalvo);
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

                        // Ainda salvar arquivo de erro organizado
                        using var entryStream = entry.Open();
                        using var memoryStream = new MemoryStream();
                        await entryStream.CopyToAsync(memoryStream);
                        var pdfBytes = memoryStream.ToArray();
                        var caminhoErro = await SalvarArquivoOrganizado(entry.Name, pdfBytes, "erro", batchHistory.Id);

                        await SalvarHistoricoProcessamento(userId, entry.Name, classificacaoErro, batchHistory.Id, caminhoErro, pdfBytes.Length);
                        totalDocumentosProcessados++;

                        _logger.LogWarning("❌ Erro processado: {Arquivo} → {Caminho}", entry.Name, caminhoErro);
                    }
                }

                // Finalizar o lote com estatísticas
                var endTime = DateTime.Now;
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

        private async Task<string> SalvarArquivoOrganizado(string nomeArquivo, byte[] arquivoBytes, string tipoDocumento, int batchId, string baseDirectory = "DocumentosProcessados")
        {
            try
            {
                // Usar pasta Documents do usuário
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var baseFolder = Path.Combine(documentsPath, "ClassificadorDoc", baseDirectory);

                                // Criar estrutura de pastas: Documents/ClassificadorDoc/DocumentosProcessados/Lote_XXXXXX/TipoDocumento/
                var batchFolder = Path.Combine(baseFolder, $"Lote_{batchId:000000}");
                var tipoFolder = Path.Combine(batchFolder, NormalizarNomeTipo(tipoDocumento));

                // Garantir que as pastas existem
                Directory.CreateDirectory(tipoFolder);

                // Caminho final do arquivo
                var caminhoArquivo = Path.Combine(tipoFolder, nomeArquivo);

                // Evitar conflitos de nome
                var contador = 1;
                var nomeBase = Path.GetFileNameWithoutExtension(nomeArquivo);
                var extensao = Path.GetExtension(nomeArquivo);

                while (System.IO.File.Exists(caminhoArquivo))
                {
                    var novoNome = $"{nomeBase}_{contador:000}{extensao}";
                    caminhoArquivo = Path.Combine(tipoFolder, novoNome);
                    contador++;
                }

                // Salvar o arquivo
                await System.IO.File.WriteAllBytesAsync(caminhoArquivo, arquivoBytes);

                _logger.LogDebug("📁 Arquivo salvo em Documents: {Caminho}", caminhoArquivo);

                // Retornar caminho absoluto (Documents é mais confiável que caminhos relativos)
                return caminhoArquivo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao salvar arquivo {NomeArquivo} do tipo {Tipo}", nomeArquivo, tipoDocumento);
                return string.Empty;
            }
        }

        private static string NormalizarNomeTipo(string tipoDocumento)
        {
            return tipoDocumento.ToLowerInvariant() switch
            {
                "notificacao_autuacao" => "01_Autuacoes",
                "autuacao" => "01_Autuacoes",
                "defesa" => "02_Defesas",
                "indicacao_condutor" => "03_Indicacoes_Condutor",
                "notificacao_penalidade" => "04_Notificacoes_Penalidade",
                "erro" => "99_Erros",
                _ => "05_Outros"
            };
        }

        private async Task SalvarHistoricoProcessamento(string? userId, string fileName, DocumentoClassificacao classificacao, int? batchId = null, string? caminhoArquivo = null, long tamanhoBytes = 0)
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
                FileSizeBytes = (int)Math.Min(tamanhoBytes, int.MaxValue), // Converter para int com limite
                BatchProcessingHistoryId = batchId, // Novo campo para vincular ao lote
                CaminhoArquivo = caminhoArquivo, // 📁 NOVO CAMPO para salvar caminho do arquivo

                // NOVOS CAMPOS ESPECÍFICOS PARA DOCUMENTOS DE TRÂNSITO
                TextoCompleto = classificacao.TextoExtraido,
                NumeroAIT = classificacao.NumeroAIT,
                PlacaVeiculo = classificacao.PlacaVeiculo,
                NomeCondutor = classificacao.NomeCondutor,
                NumeroCNH = classificacao.NumeroCNH,
                TextoDefesa = classificacao.TextoDefesa,
                DataInfracao = classificacao.DataInfracao,
                LocalInfracao = classificacao.LocalInfracao,
                CodigoInfracao = classificacao.CodigoInfracao,
                ValorMulta = classificacao.ValorMulta,
                OrgaoAutuador = classificacao.OrgaoAutuador,

                // NOVOS CAMPOS PARA INDICAÇÃO DE CONDUTOR
                RequerenteNome = classificacao.RequerenteNome,
                RequerenteCPF = classificacao.RequerenteCPF,
                RequerenteRG = classificacao.RequerenteRG,
                RequerenteEndereco = classificacao.RequerenteEndereco,
                IndicacaoNome = classificacao.IndicacaoNome,
                IndicacaoCPF = classificacao.IndicacaoCPF,
                IndicacaoRG = classificacao.IndicacaoRG,
                IndicacaoCNH = classificacao.IndicacaoCNH
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
        public string NomeUsuario { get; set; } = string.Empty;

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

        // Campos específicos extraídos
        public string? TextoCompleto { get; set; }
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

        // Propriedades calculadas
        public string Status => Sucesso ? "Completed" : "Failed";
        public string TamanhoFormatado => FormatarTamanhoArquivo(TamanhoBytes);

        // Resumo dos dados específicos baseado no tipo
        public string DadosEspecificosResumo
        {
            get
            {
                if (string.IsNullOrEmpty(TipoClassificado))
                    return string.Empty;

                var dados = new List<string>();

                // Para INDICAÇÃO DE CONDUTOR: AIT, Placa, Nome Condutor, CNH
                if (TipoClassificado.Contains("indicacao"))
                {
                    if (!string.IsNullOrEmpty(NumeroAIT))
                        dados.Add($"AIT: {NumeroAIT}");
                    if (!string.IsNullOrEmpty(PlacaVeiculo))
                        dados.Add($"Placa: {PlacaVeiculo}");
                    if (!string.IsNullOrEmpty(NomeCondutor))
                        dados.Add($"Condutor: {NomeCondutor}");
                    if (!string.IsNullOrEmpty(NumeroCNH))
                        dados.Add($"CNH: {NumeroCNH}");
                }
                // Para DEFESA: AIT, Placa, Texto da Defesa
                else if (TipoClassificado.Contains("defesa"))
                {
                    if (!string.IsNullOrEmpty(NumeroAIT))
                        dados.Add($"AIT: {NumeroAIT}");
                    if (!string.IsNullOrEmpty(PlacaVeiculo))
                        dados.Add($"Placa: {PlacaVeiculo}");
                    if (!string.IsNullOrEmpty(TextoDefesa))
                    {
                        var textoLimitado = TextoDefesa.Length > 50
                            ? TextoDefesa.Substring(0, 50) + "..."
                            : TextoDefesa;
                        dados.Add($"Defesa: {textoLimitado}");
                    }
                }
                // Para AUTUAÇÃO: AIT, Placa, Valor, Data
                else if (TipoClassificado.Contains("autuacao"))
                {
                    if (!string.IsNullOrEmpty(NumeroAIT))
                        dados.Add($"AIT: {NumeroAIT}");
                    if (!string.IsNullOrEmpty(PlacaVeiculo))
                        dados.Add($"Placa: {PlacaVeiculo}");
                    if (ValorMulta.HasValue && ValorMulta > 0)
                        dados.Add($"Valor: R$ {ValorMulta:F2}");
                    if (DataInfracao.HasValue)
                        dados.Add($"Data: {DataInfracao.Value:dd/MM/yyyy}");
                }
                // Para outros tipos: Dados básicos disponíveis
                else
                {
                    if (!string.IsNullOrEmpty(NumeroAIT))
                        dados.Add($"AIT: {NumeroAIT}");
                    if (!string.IsNullOrEmpty(PlacaVeiculo))
                        dados.Add($"Placa: {PlacaVeiculo}");
                }

                return string.Join(" | ", dados);
            }
        }

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
