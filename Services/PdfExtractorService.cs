using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace ClassificadorDoc.Services
{
    public interface IPdfExtractorService
    {
        Task<string> ExtrairTextoAsync(Stream pdfStream);
    }

    public class PdfExtractorService : IPdfExtractorService
    {
        private readonly ILogger<PdfExtractorService> _logger;

        public PdfExtractorService(ILogger<PdfExtractorService> logger)
        {
            _logger = logger;
        }

        public async Task<string> ExtrairTextoAsync(Stream pdfStream)
        {
            // Primeira tentativa: cópia completa do stream para evitar problemas de posição
            byte[] pdfBytes;
            try
            {
                // Se o stream for seekable, garante que está na posição inicial
                if (pdfStream.CanSeek)
                {
                    pdfStream.Position = 0;
                }

                using var tempMemoryStream = new MemoryStream();
                await pdfStream.CopyToAsync(tempMemoryStream);
                pdfBytes = tempMemoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao copiar stream do PDF");
                throw new InvalidOperationException("Não foi possível ler o stream do PDF", ex);
            }

            // Segunda tentativa: processamento com retry para lidar com problemas de concorrência
            const int maxTentativas = 3;
            Exception? ultimaExcecao = null;

            for (int tentativa = 1; tentativa <= maxTentativas; tentativa++)
            {
                try
                {
                    // Cria um novo MemoryStream para cada tentativa para evitar problemas de estado
                    using var memoryStream = new MemoryStream(pdfBytes);
                    using var pdfReader = new PdfReader(memoryStream);
                    using var pdfDocument = new PdfDocument(pdfReader);

                    var textoCompleto = string.Empty;

                    for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                    {
                        var pagina = pdfDocument.GetPage(i);
                        var textoPagina = PdfTextExtractor.GetTextFromPage(pagina);
                        textoCompleto += textoPagina + "\n";
                    }

                    var resultado = textoCompleto.Trim();

                    if (tentativa > 1)
                    {
                        _logger.LogInformation("Extração de texto bem-sucedida na tentativa {Tentativa}", tentativa);
                    }

                    return resultado;
                }
                catch (Exception ex) when (tentativa < maxTentativas &&
                    (ex.Message.Contains("inner stream position") ||
                     ex.Message.Contains("stream") ||
                     ex.Message.Contains("position") ||
                     ex.Message.Contains("concurrency") ||
                     ex.Message.Contains("thread")))
                {
                    ultimaExcecao = ex;
                    _logger.LogWarning("Tentativa {Tentativa} de extração falhou: {Erro}. Tentando novamente...",
                        tentativa, ex.Message);

                    // Aguarda um tempo crescente antes de tentar novamente
                    await Task.Delay(TimeSpan.FromMilliseconds(tentativa * 500));
                    continue;
                }
                catch (Exception ex)
                {
                    ultimaExcecao = ex;
                    break; // Erro não transitório, para imediatamente
                }
            }

            _logger.LogError(ultimaExcecao, "Erro ao extrair texto do PDF após {MaxTentativas} tentativas", maxTentativas);
            throw new InvalidOperationException("Não foi possível extrair o texto do PDF após múltiplas tentativas", ultimaExcecao);
        }
    }
}
