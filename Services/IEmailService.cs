using EmailAutomation.Models;

namespace EmailAutomation.Services;

public interface IEmailService
{
    Task SendEmailWithAttachmentsAsync(EmailSettings settings, IEnumerable<FileInfo> attachments);
}
