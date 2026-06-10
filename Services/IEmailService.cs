using EmailAutomation.Models;

namespace EmailAutomation.Services;

public interface IEmailService
{
    Task SendJobEmailAsync(ReportJobConfig jobConfig, byte[] pdfBytes, string attachmentFileName);
    Task SendGroupedEmailAsync(
    ReportJobConfig representativeJob,
    List<(byte[] PdfBytes, string FileName)> attachments);
}
