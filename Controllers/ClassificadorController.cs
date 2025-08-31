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
        private readonly IClassificadorService _classificador;
        private readonly ILogger<ClassificadorController> _logger;

        public ClassificadorController(
            IClassificadorService classificador,
            ILogger<ClassificadorController> logger)
        {
            _classificador = classificador;
            _logger = logger;
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

        private async Task<DocumentoClassificacao> ProcessarPdfVisual(ZipArchiveEntry entrada)
        {
            const int maxTentativas = 3;
            Exception? ultimaExcecao = null;

            for (int tentativa = 1; tentativa <= maxTentativas; tentativa++)
            {
                try
                {
                    // Cria uma nova instância do stream a cada tentativa para evitar problemas de estado
                    using var pdfStream = entrada.Open();
                    using var memoryStream = new MemoryStream();

                    // Cópia completa do stream
                    await pdfStream.CopyToAsync(memoryStream);
                    var pdfBytes = memoryStream.ToArray();

                    // Validação básica do arquivo PDF
                    if (pdfBytes.Length < 4 || !VerificarHeaderPdf(pdfBytes))
                    {
                        throw new InvalidOperationException("Arquivo não é um PDF válido");
                    }

                    var resultado = await _classificador.ClassificarDocumentoPdfAsync(entrada.Name, pdfBytes);

                    if (tentativa > 1)
                    {
                        _logger.LogInformation("Processamento de PDF visual bem-sucedido na tentativa {Tentativa} para {NomeArquivo}",
                            tentativa, entrada.Name);
                    }

                    return resultado;
                }
                catch (Exception ex) when (tentativa < maxTentativas &&
                    (ex.Message.Contains("inner stream position") ||
                     ex.Message.Contains("stream") ||
                     ex.Message.Contains("position") ||
                     ex.Message.Contains("concurrency") ||
                     ex.Message.Contains("thread") ||
                     ex.Message.Contains("timeout") ||
                     ex.Message.Contains("network")))
                {
                    ultimaExcecao = ex;
                    _logger.LogWarning("Tentativa {Tentativa} falhou para PDF visual {NomeArquivo}: {Erro}. Tentando novamente...",
                        tentativa, entrada.Name, ex.Message);

                    // Aguarda um tempo crescente antes de tentar novamente
                    await Task.Delay(TimeSpan.FromSeconds(tentativa * 2));
                    continue;
                }
                catch (Exception ex)
                {
                    ultimaExcecao = ex;
                    break; // Erro não transitório
                }
            }

            _logger.LogError(ultimaExcecao, "Erro ao processar PDF visual {NomeArquivo} após {MaxTentativas} tentativas",
                entrada.Name, maxTentativas);

            return new DocumentoClassificacao
            {
                NomeArquivo = entrada.Name,
                TipoDocumento = "Erro",
                TextoExtraido = string.Empty,
                ProcessadoComSucesso = false,
                ErroProcessamento = ultimaExcecao?.Message ?? "Erro desconhecido após múltiplas tentativas"
            };
        }

        private static bool VerificarHeaderPdf(byte[] bytes)
        {
            // Verifica se os primeiros bytes correspondem ao header PDF (%PDF)
            return bytes.Length >= 4 &&
                   bytes[0] == 0x25 && // %
                   bytes[1] == 0x50 && // P
                   bytes[2] == 0x44 && // D
                   bytes[3] == 0x46;   // F
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
