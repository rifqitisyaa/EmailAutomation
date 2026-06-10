using Microsoft.Extensions.Logging;

namespace EmailAutomation.Services;

public class PdfScannerService : IPdfScannerService
{
    private readonly ILogger<PdfScannerService> _logger;

    public PdfScannerService(ILogger<PdfScannerService> logger)
    {
        _logger = logger;
    }

    public IEnumerable<FileInfo> ScanForPdfFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            _logger.LogWarning("PDF folder path does not exist: {FolderPath}", folderPath);
            return Enumerable.Empty<FileInfo>();
        }

        var directoryInfo = new DirectoryInfo(folderPath);
        var files = directoryInfo.GetFiles("*.pdf");

        _logger.LogInformation("Found {Count} PDF files in {FolderPath}", files.Length, folderPath);

        return files;
    }
}
