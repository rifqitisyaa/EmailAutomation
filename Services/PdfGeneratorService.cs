using EmailAutomation.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace EmailAutomation.Services;

public class PdfGeneratorService : IPdfGeneratorService
{
    private readonly EmailReportOptions _options;
    private readonly ILogger<PdfGeneratorService> _logger;

    public PdfGeneratorService(IOptions<EmailReportOptions> options, ILogger<PdfGeneratorService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<byte[]> GeneratePdfAsync(string htmlContent)
    {
        try
        {
            _logger.LogInformation("Starting PDF generation process...");
            
            var browserFetcher = new BrowserFetcher();
            _logger.LogInformation("Checking for Chromium updates...");
            await browserFetcher.DownloadAsync();

            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });

            await using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);
            await page.WaitForNetworkIdleAsync();

            var pdfOptions = new PdfOptions
            {
                Format = PaperFormat.A4,
                MarginOptions = new MarginOptions
                {
                    Top = "1cm",
                    Bottom = "1cm",
                    Left = "1cm",
                    Right = "1cm"
                },
                PrintBackground = true
            };

            var pdfBytes = await page.PdfDataAsync(pdfOptions);
            _logger.LogInformation("PDF generated successfully. Size: {Size} bytes", pdfBytes.Length);

            if (!string.IsNullOrEmpty(_options.PdfOutputPath))
            {
                if (!Directory.Exists(_options.PdfOutputPath))
                {
                    Directory.CreateDirectory(_options.PdfOutputPath);
                }

                var fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var filePath = Path.Combine(_options.PdfOutputPath, fileName);
                await File.WriteAllBytesAsync(filePath, pdfBytes);
                _logger.LogInformation("PDF backup saved to: {Path}", filePath);
            }

            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during PDF generation using PuppeteerSharp.");
            throw;
        }
    }
}
