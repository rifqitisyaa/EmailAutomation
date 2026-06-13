using EmailAutomation.Data;
using EmailAutomation.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailAutomation.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly GlobalEmailConfig _emailConfig;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPdfCompressionService _compressionService;
    private const long MaxAttachmentSizeInBytes = 12 * 1024 * 1024; // 12MB threshold for splitting
    private const long AbsoluteMaxRawSize = 18 * 1024 * 1024; // 18MB raw will be ~24MB in Base64 (SMTP limit)

    public EmailService(ILogger<EmailService> logger, IOptions<GlobalEmailConfig> emailConfig, IServiceScopeFactory scopeFactory, IPdfCompressionService compressionService)
    {
        _logger = logger;
        _emailConfig = emailConfig.Value;
        _scopeFactory = scopeFactory;
        _compressionService = compressionService;
    }

    public async Task SendJobEmailAsync(ReportJobConfig jobConfig, byte[] pdfBytes, string attachmentFileName)
    {
        await SendGroupedEmailAsync(jobConfig, new List<(byte[] PdfBytes, string FileName)> { (pdfBytes, attachmentFileName) });
    }

    public async Task SendGroupedEmailAsync(
        ReportJobConfig representativeJob,
        List<(byte[] PdfBytes, string FileName)> attachments)
    {
        // 1. Cek ukuran file, kalau ada yang kegedean, coba kompres dulu
        for (int i = 0; i < attachments.Count; i++)
        {
            if (attachments[i].PdfBytes.Length > AbsoluteMaxRawSize)
            {
                _logger.LogWarning("File {FileName} exceeds limit ({Size}MB). Attempting compression...", 
                    attachments[i].FileName, attachments[i].PdfBytes.Length / 1024 / 1024);
                
                var compressed = _compressionService.CompressPdf(attachments[i].PdfBytes);
                attachments[i] = (compressed, attachments[i].FileName);
            }
        }

        // 2. Check for any single file that is STILL too big even after compression
        var oversizedFiles = attachments.Where(a => a.PdfBytes.Length > AbsoluteMaxRawSize).ToList();
        if (oversizedFiles.Any())
        {
            foreach (var file in oversizedFiles)
            {
                var errorMsg = $"File {file.FileName} is STILL too large to send ({file.PdfBytes.Length / 1024 / 1024}MB) even after compression. SMTP limit is 25MB (including encoding overhead).";
                _logger.LogCritical(errorMsg);
                await LogErrorToDb(representativeJob, errorMsg);
            }
            
            // Remove oversized files so we can still try to send the others if any
            attachments = attachments.Where(a => a.PdfBytes.Length <= AbsoluteMaxRawSize).ToList();
            if (!attachments.Any()) return;
        }

        var totalRawSize = attachments.Sum(a => a.PdfBytes.Length);
        _logger.LogInformation("Processing grouped email for job {JobName}. Total raw size: {TotalSize} bytes", 
            representativeJob.JobName, totalRawSize);

        var chunks = SplitAttachmentsIntoChunks(attachments);
        var totalParts = chunks.Count;

        if (totalParts > 1)
        {
            _logger.LogWarning("Email for job {JobName} will be split into {Count} parts due to size limits.", 
                representativeJob.JobName, totalParts);
        }

        using var client = new SmtpClient();
        try
        {
            SecureSocketOptions socketOptions = _emailConfig.UseSsl
                ? (_emailConfig.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;

            await client.ConnectAsync(_emailConfig.SmtpHost, _emailConfig.SmtpPort, socketOptions);

            if (!string.IsNullOrEmpty(_emailConfig.Username))
                await client.AuthenticateAsync(_emailConfig.Username, _emailConfig.Password);

            for (int i = 0; i < chunks.Count; i++)
            {
                var currentPart = i + 1;
                var currentChunkSize = chunks[i].Sum(a => a.PdfBytes.Length);
                var message = CreateMimeMessage(representativeJob, chunks[i], currentPart, totalParts);
                
                _logger.LogInformation("Sending Part {Part}/{Total} for job {JobName}. Chunk size: {Size} bytes", 
                    currentPart, totalParts, representativeJob.JobName, currentChunkSize);

                await client.SendAsync(message);
                
                _logger.LogInformation("Successfully sent Part {Part}/{Total} for job {JobName}",
                    currentPart, totalParts, representativeJob.JobName);
            }
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

    private async Task LogErrorToDb(ReportJobConfig job, string message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.EmailAutomationLogs.Add(new EmailAutomationLog
            {
                Level = "ERROR",
                JobName = job.JobName,
                Message = message,
                Timestamp = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log attachment size error to database.");
        }
    }

    private List<List<(byte[] PdfBytes, string FileName)>> SplitAttachmentsIntoChunks(List<(byte[] PdfBytes, string FileName)> attachments)
    {
        var chunks = new List<List<(byte[] PdfBytes, string FileName)>>();
        var currentChunk = new List<(byte[] PdfBytes, string FileName)>();
        long currentChunkSize = 0;

        foreach (var attachment in attachments)
        {
            if (currentChunkSize + attachment.PdfBytes.Length > MaxAttachmentSizeInBytes && currentChunk.Any())
            {
                chunks.Add(currentChunk);
                currentChunk = new List<(byte[] PdfBytes, string FileName)>();
                currentChunkSize = 0;
            }

            currentChunk.Add(attachment);
            currentChunkSize += attachment.PdfBytes.Length;
        }

        if (currentChunk.Any())
        {
            chunks.Add(currentChunk);
        }

        return chunks;
    }

    private MimeMessage CreateMimeMessage(ReportJobConfig job, List<(byte[] PdfBytes, string FileName)> chunkAttachments, int part, int total)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailConfig.FromName, _emailConfig.FromAddress));

        message.To.Add(MailboxAddress.Parse(job.ToAddresses));
        if (!string.IsNullOrEmpty(job.CcAddresses))
            message.Cc.Add(MailboxAddress.Parse(job.CcAddresses));

        var dateStr = DateTime.Now.ToString("dd-MM-yyyy");
        var subject = job.Subject.Replace("{date}", dateStr);
        
        if (total > 1)
        {
            subject = $"[{part}/{total}] {subject}";
        }
        
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            TextBody = job.Body.Replace("{date}", dateStr)
        };

        if (total > 1)
        {
            bodyBuilder.TextBody += $"\n\n(Note: Lampiran dipecah menjadi {total} email karena ukuran file besar. Ini adalah bagian ke-{part}.)";
        }

        foreach (var (pdfBytes, fileName) in chunkAttachments)
        {
            bodyBuilder.Attachments.Add(fileName, pdfBytes, ContentType.Parse("application/pdf"));
        }

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }
}
