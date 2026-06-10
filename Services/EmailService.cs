using EmailAutomation.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace EmailAutomation.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task SendEmailWithAttachmentsAsync(EmailSettings settings, IEnumerable<FileInfo> attachments)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        
        foreach (var to in settings.ToAddresses)
        {
            message.To.Add(MailboxAddress.Parse(to));
        }

        foreach (var cc in settings.CcAddresses)
        {
            message.Cc.Add(MailboxAddress.Parse(cc));
        }

        var dateStr = DateTime.Now.ToString("dd-MM-yyyy");
        message.Subject = settings.Subject.Replace("{date}", dateStr);

        var bodyBuilder = new BodyBuilder
        {
            TextBody = settings.Body.Replace("{date}", dateStr)
        };

        foreach (var file in attachments)
        {
            await bodyBuilder.Attachments.AddAsync(file.FullName);
        }

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            SecureSocketOptions socketOptions = settings.UseSsl 
                ? (settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;

            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, socketOptions);

            if (!string.IsNullOrEmpty(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password);
            }

            await client.SendAsync(message);
            _logger.LogInformation("Email sent successfully to {Count} recipients", settings.ToAddresses.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SMTP {Host}:{Port}", settings.SmtpHost, settings.SmtpPort);
            throw;
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }
}
