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
            try
            {
                using var memoryStream = new MemoryStream();
                await pdfStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var pdfDocument = new PdfDocument(new PdfReader(memoryStream));
                var textoCompleto = string.Empty;

                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                {
                    var pagina = pdfDocument.GetPage(i);
                    var textoPagina = PdfTextExtractor.GetTextFromPage(pagina);
                    textoCompleto += textoPagina + "\n";
                }

                return textoCompleto.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair texto do PDF");
                throw new InvalidOperationException("Não foi possível extrair o texto do PDF", ex);
            }
        }
    }
}
