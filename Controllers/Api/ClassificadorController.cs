using Microsoft.AspNetCore.Mvc;
using ClassificadorDoc.Models;
using ClassificadorDoc.Services;
using System.IO.Compression;
using System.Diagnostics;

namespace ClassificadorDoc.Controllers.Api
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

                // Processa os PDFs sequencialmente para evitar problemas de stream concorrente
                const int batchSize = 1; // Processamento sequencial para máxima estabilidade

                for (int i = 0; i < entradaPdfs.Count; i += batchSize)
                {
                    var batch = entradaPdfs.Skip(i).Take(batchSize);

                    // Processamento sequencial para evitar conflitos de stream
                    foreach (var entrada in batch)
                    {
                        var resultado_individual = await ProcessarPdfVisual(entrada);
                        resultado.Documentos.Add(resultado_individual);

                        // Pequeno delay entre arquivos para evitar sobrecarga
                        await Task.Delay(100);
                    }

                    _logger.LogInformation("Processado item {ItemNumber}/{Total}",
                        i + 1, entradaPdfs.Count);
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
                    // Estratégia de isolamento total - nova instância a cada tentativa
                    byte[] pdfBytes;

                    using (var pdfStream = entrada.Open())
                    using (var memoryStream = new MemoryStream())
                    {
                        // Reset de posição para garantir leitura do início
                        if (pdfStream.CanSeek)
                        {
                            pdfStream.Position = 0;
                        }

                        // Cópia completa do stream com buffer maior
                        await pdfStream.CopyToAsync(memoryStream, bufferSize: 81920); // 80KB buffer
                        pdfBytes = memoryStream.ToArray();
                    }

                    // Validação básica do arquivo PDF
                    if (pdfBytes.Length < 4 || !VerificarHeaderPdf(pdfBytes))
                    {
                        throw new InvalidOperationException("Arquivo não é um PDF válido");
                    }

                    _logger.LogDebug("Tentativa {Tentativa}: PDF {NomeArquivo} carregado com {Bytes} bytes",
                        tentativa, entrada.Name, pdfBytes.Length);

                    // Cria uma cópia independente dos bytes para evitar referências compartilhadas
                    var pdfBytesCopia = new byte[pdfBytes.Length];
                    Array.Copy(pdfBytes, pdfBytesCopia, pdfBytes.Length);

                    var resultado = await _classificador.ClassificarDocumentoPdfAsync(entrada.Name, pdfBytesCopia);

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
