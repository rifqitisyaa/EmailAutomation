using EmailAutomation.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailAutomation.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly GlobalEmailConfig _emailConfig;

    public EmailService(ILogger<EmailService> logger, IOptions<GlobalEmailConfig> emailConfig)
    {
        _logger = logger;
        _emailConfig = emailConfig.Value;
    }

    public async Task SendJobEmailAsync(ReportJobConfig jobConfig, byte[] pdfBytes, string attachmentFileName)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailConfig.FromName, _emailConfig.FromAddress));


        message.To.Add(MailboxAddress.Parse(jobConfig.ToAddresses));

        message.Cc.Add(MailboxAddress.Parse(jobConfig.CcAddresses));


        var dateStr = DateTime.Now.ToString("dd-MM-yyyy");
        message.Subject = jobConfig.Subject.Replace("{date}", dateStr);

        var bodyBuilder = new BodyBuilder
        {
            TextBody = jobConfig.Body.Replace("{date}", dateStr)
        };

        bodyBuilder.Attachments.Add(attachmentFileName, pdfBytes, ContentType.Parse("application/pdf"));

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            SecureSocketOptions socketOptions = _emailConfig.UseSsl
                ? (_emailConfig.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;

            await client.ConnectAsync(_emailConfig.SmtpHost, _emailConfig.SmtpPort, socketOptions);

            if (!string.IsNullOrEmpty(_emailConfig.Username))
            {
                await client.AuthenticateAsync(_emailConfig.Username, _emailConfig.Password);
            }

            await client.SendAsync(message);
            _logger.LogInformation("Email for job {JobName} sent successfully to {Count} recipients", jobConfig.JobName, jobConfig.ToAddresses.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email for job {JobName} via SMTP {Host}", jobConfig.JobName, _emailConfig.SmtpHost);
            throw;
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true);
        }
    }

    public async Task SendGroupedEmailAsync(
     ReportJobConfig representativeJob,
     List<(byte[] PdfBytes, string FileName)> attachments)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailConfig.FromName, _emailConfig.FromAddress));

        message.To.Add(MailboxAddress.Parse(representativeJob.ToAddresses));
        message.Cc.Add(MailboxAddress.Parse(representativeJob.CcAddresses));

        var dateStr = DateTime.Now.ToString("dd-MM-yyyy");
        message.Subject = representativeJob.Subject.Replace("{date}", dateStr);

        var bodyBuilder = new BodyBuilder
        {
            TextBody = representativeJob.Body.Replace("{date}", dateStr)
        };

        // CHANGED: loop semua attachment, bukan pakai variable lama
        foreach (var (pdfBytes, fileName) in attachments)
        {
            bodyBuilder.Attachments.Add(fileName, pdfBytes, ContentType.Parse("application/pdf"));
        }

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            SecureSocketOptions socketOptions = _emailConfig.UseSsl
                ? (_emailConfig.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;

            await client.ConnectAsync(_emailConfig.SmtpHost, _emailConfig.SmtpPort, socketOptions);

            if (!string.IsNullOrEmpty(_emailConfig.Username))
                await client.AuthenticateAsync(_emailConfig.Username, _emailConfig.Password);

            await client.SendAsync(message);
            _logger.LogInformation("Grouped email for job {JobName} sent with {Count} attachment(s)",
                representativeJob.JobName, attachments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send grouped email for job {JobName}", representativeJob.JobName);
            throw;
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true);
        }
    }

}
