using EmailAutomation.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace EmailAutomation.Services;

public class PdfGeneratorService : IPdfGeneratorService
{
    private readonly EmailReportOptions _options;
    private readonly IConfiguration _puppeter;
    private readonly ILogger<PdfGeneratorService> _logger;

    public PdfGeneratorService(IOptions<EmailReportOptions> options, ILogger<PdfGeneratorService> logger, IConfiguration puppeter)
    {
        _options = options.Value;
        _logger = logger;
        _puppeter = puppeter;
    }

    public async Task<byte[]> GeneratePdfAsync(string htmlContent)
    {
        try
        {
            _logger.LogInformation("Starting PDF generation process...");

            var configPath = _puppeter.GetValue<string>("PathPuppeter:ExecutablePath");
            var fullPath = Path.IsPathRooted(configPath)
                ? configPath
                : Path.Combine(AppContext.BaseDirectory, configPath ?? string.Empty);

            var options = new LaunchOptions
            {
                Headless = true,
                ExecutablePath = fullPath,
                Args = new[] {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage"
                }
            };

            _logger.LogInformation("Launching browser using local executable...");

            // Menggunakan browser dan page sekali saja di sini
            using (var browser = await Puppeteer.LaunchAsync(options))
            using (var page = await browser.NewPageAsync())
            {
                await page.SetContentAsync(htmlContent);
                await page.WaitForNetworkIdleAsync();

                // Setup format kertas A4 dan Margin tetap dipertahankan di sini
                var pdfOptions = new PdfOptions
                {
                    Format = PaperFormat.A4,
                    Landscape = false, // Balikin ke Portrait
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

                // Fitur simpan backup file PDF tetap berjalan jika konfigurasinya ada
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during PDF generation using PuppeteerSharp.");
            throw;
        }
    }
}