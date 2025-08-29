using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Models;
using ClassificadorDoc.Services;
using System.IO.Compression;
using System.Diagnostics;

namespace ClassificadorDoc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClassificadorController : ControllerBase
    {
        private readonly IPdfExtractorService _pdfExtractor;
        private readonly IClassificadorService _classificador;
        private readonly ILogger<ClassificadorController> _logger;

        public ClassificadorController(
            IPdfExtractorService pdfExtractor,
            IClassificadorService classificador,
            ILogger<ClassificadorController> logger)
        {
            _pdfExtractor = pdfExtractor;
            _classificador = classificador;
            _logger = logger;
        }

        [HttpPost("classificar-zip")]
        [RequestSizeLimit(100_000_000)] // 100MB limit
        public async Task<ActionResult<ResultadoClassificacao>> ClassificarDocumentosZip(IFormFile arquivo)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (arquivo == null || arquivo.Length == 0)
                {
                    return BadRequest("Nenhum arquivo foi enviado.");
                }

                if (!arquivo.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("O arquivo deve ter extensão .zip");
                }

                var resultado = new ResultadoClassificacao();

                using var arquivoStream = arquivo.OpenReadStream();
                using var zip = new ZipArchive(arquivoStream, ZipArchiveMode.Read);

                var entradaPdfs = zip.Entries
                    .Where(e => e.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                resultado.TotalDocumentos = entradaPdfs.Count;

                if (entradaPdfs.Count == 0)
                {
                    return BadRequest("Nenhum arquivo PDF encontrado no ZIP.");
                }

                _logger.LogInformation("Processando {Count} arquivos PDF", entradaPdfs.Count);

                // Processa os PDFs em batches para evitar sobrecarga
                var tasks = new List<Task<DocumentoClassificacao>>();
                const int batchSize = 5; // Processa 5 PDFs por vez

                for (int i = 0; i < entradaPdfs.Count; i += batchSize)
                {
                    var batch = entradaPdfs.Skip(i).Take(batchSize);
                    var batchTasks = batch.Select(ProcessarPdf);

                    var batchResults = await Task.WhenAll(batchTasks);
                    resultado.Documentos.AddRange(batchResults);

                    _logger.LogInformation("Processado batch {BatchNumber}/{TotalBatches}",
                        (i / batchSize) + 1,
                        (entradaPdfs.Count + batchSize - 1) / batchSize);
                }

                resultado.DocumentosProcessados = resultado.Documentos.Count(d => d.ProcessadoComSucesso);
                resultado.DocumentosComErro = resultado.Documentos.Count(d => !d.ProcessadoComSucesso);

                stopwatch.Stop();
                resultado.TempoProcessamento = stopwatch.Elapsed;

                _logger.LogInformation("Processamento concluído: {Processados}/{Total} documentos em {Tempo}s",
                    resultado.DocumentosProcessados,
                    resultado.TotalDocumentos,
                    resultado.TempoProcessamento.TotalSeconds);

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar arquivo ZIP");
                return StatusCode(500, $"Erro interno: {ex.Message}");
            }
        }

        [HttpPost("classificar-zip-visual")]
        [RequestSizeLimit(100_000_000)] // 100MB limit
        public async Task<ActionResult<ResultadoClassificacao>> ClassificarDocumentosZipVisual(IFormFile arquivo)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (arquivo == null || arquivo.Length == 0)
                {
                    return BadRequest("Nenhum arquivo foi enviado.");
                }

                if (!arquivo.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("O arquivo deve ter extensão .zip");
                }

                var resultado = new ResultadoClassificacao();

                using var arquivoStream = arquivo.OpenReadStream();
                using var zip = new ZipArchive(arquivoStream, ZipArchiveMode.Read);

                var entradaPdfs = zip.Entries
                    .Where(e => e.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                resultado.TotalDocumentos = entradaPdfs.Count;

                if (entradaPdfs.Count == 0)
                {
                    return BadRequest("Nenhum arquivo PDF encontrado no ZIP.");
                }

                _logger.LogInformation("Processando {Count} arquivos PDF com análise visual", entradaPdfs.Count);

                // Processa os PDFs em batches menores para análise visual (mais pesada)
                const int batchSize = 3; // Reduzido para análise visual

                for (int i = 0; i < entradaPdfs.Count; i += batchSize)
                {
                    var batch = entradaPdfs.Skip(i).Take(batchSize);
                    var batchTasks = batch.Select(ProcessarPdfVisual);

                    var batchResults = await Task.WhenAll(batchTasks);
                    resultado.Documentos.AddRange(batchResults);

                    _logger.LogInformation("Processado batch visual {BatchNumber}/{TotalBatches}",
                        (i / batchSize) + 1,
                        (entradaPdfs.Count + batchSize - 1) / batchSize);
                }

                resultado.DocumentosProcessados = resultado.Documentos.Count(d => d.ProcessadoComSucesso);
                resultado.DocumentosComErro = resultado.Documentos.Count(d => !d.ProcessadoComSucesso);

                stopwatch.Stop();
                resultado.TempoProcessamento = stopwatch.Elapsed;

                _logger.LogInformation("Processamento visual concluído: {Processados}/{Total} documentos em {Tempo}s",
                    resultado.DocumentosProcessados,
                    resultado.TotalDocumentos,
                    resultado.TempoProcessamento.TotalSeconds);

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar arquivo ZIP visual");
                return StatusCode(500, $"Erro interno: {ex.Message}");
            }
        }

        private async Task<DocumentoClassificacao> ProcessarPdf(ZipArchiveEntry entrada)
        {
            try
            {
                using var pdfStream = entrada.Open();
                var texto = await _pdfExtractor.ExtrairTextoAsync(pdfStream);

                if (string.IsNullOrWhiteSpace(texto))
                {
                    return new DocumentoClassificacao
                    {
                        NomeArquivo = entrada.Name,
                        TipoDocumento = "Erro",
                        TextoExtraido = string.Empty,
                        ProcessadoComSucesso = false,
                        ErroProcessamento = "Não foi possível extrair texto do PDF"
                    };
                }

                return await _classificador.ClassificarDocumentoAsync(entrada.Name, texto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar PDF {NomeArquivo}", entrada.Name);
                return new DocumentoClassificacao
                {
                    NomeArquivo = entrada.Name,
                    TipoDocumento = "Erro",
                    TextoExtraido = string.Empty,
                    ProcessadoComSucesso = false,
                    ErroProcessamento = ex.Message
                };
            }
        }

        private async Task<DocumentoClassificacao> ProcessarPdfVisual(ZipArchiveEntry entrada)
        {
            try
            {
                using var pdfStream = entrada.Open();
                using var memoryStream = new MemoryStream();
                await pdfStream.CopyToAsync(memoryStream);
                var pdfBytes = memoryStream.ToArray();

                return await _classificador.ClassificarDocumentoPdfAsync(entrada.Name, pdfBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar PDF visual {NomeArquivo}", entrada.Name);
                return new DocumentoClassificacao
                {
                    NomeArquivo = entrada.Name,
                    TipoDocumento = "Erro",
                    TextoExtraido = string.Empty,
                    ProcessadoComSucesso = false,
                    ErroProcessamento = ex.Message
                };
            }
        }

        [HttpGet("tipos-documento")]
        public ActionResult<object> ObterTiposDocumento()
        {
            return Ok(new
            {
                tipos = new[] { "autuacao", "defesa", "notificacao_penalidade", "outros" },
                descricoes = new
                {
                    autuacao = "Auto de Infração de Trânsito (AIT), Notificação de Autuação",
                    defesa = "Defesa de Autuação, Recurso JARI/CETRAN, Defesa Prévia, Indicação de Condutor",
                    notificacao_penalidade = "Notificação da Penalidade (NIP), Intimação para pagamento",
                    outros = "Outros documentos de trânsito"
                }
            });
        }

        [HttpGet("status")]
        public ActionResult<object> Status()
        {
            return Ok(new
            {
                status = "ativo",
                versao = "1.0.0",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
