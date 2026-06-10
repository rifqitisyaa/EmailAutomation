namespace EmailAutomation.Services;

public interface IPdfScannerService
{
    IEnumerable<FileInfo> ScanForPdfFiles(string folderPath);
}
