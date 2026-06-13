using iTextSharp.text.pdf;
using Microsoft.Extensions.Logging;

namespace EmailAutomation.Services;

public class PdfCompressionService : IPdfCompressionService
{
    private readonly ILogger<PdfCompressionService> _logger;

    public PdfCompressionService(ILogger<PdfCompressionService> logger)
    {
        _logger = logger;
    }

    public byte[] CompressPdf(byte[] pdfBytes)
    {
        try
        {
            _logger.LogInformation("Starting PDF compression. Original size: {Size} bytes", pdfBytes.Length);

            using (var ms = new MemoryStream())
            {
                var reader = new PdfReader(pdfBytes);
                var stamper = new PdfStamper(reader, ms);

                stamper.SetFullCompression();
                
                reader.RemoveUnusedObjects();
                
                stamper.Close();
                reader.Close();

                var compressedBytes = ms.ToArray();
                _logger.LogInformation("PDF compression finished. Compressed size: {Size} bytes", compressedBytes.Length);
                
                return compressedBytes;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during PDF compression.");
            return pdfBytes;
        }
    }
}
