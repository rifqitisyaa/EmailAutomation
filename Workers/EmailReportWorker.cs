using Cronos;
using EmailAutomation.Models;
using EmailAutomation.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailAutomation.Workers;

public class EmailReportWorker : BackgroundService
{
    private readonly ILogger<EmailReportWorker> _logger;
    private readonly IEmailService _emailService;
    private readonly IPdfScannerService _pdfScannerService;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly EmailReportOptions _options;

    public EmailReportWorker(
        ILogger<EmailReportWorker> logger,
        IEmailService emailService,
        IPdfScannerService pdfScannerService,
        IHostApplicationLifetime hostApplicationLifetime,
        IOptions<EmailReportOptions> options)
    {
        _logger = logger;
        _emailService = emailService;
        _pdfScannerService = pdfScannerService;
        _hostApplicationLifetime = hostApplicationLifetime;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.RunOnce)
        {
            _logger.LogInformation("RunOnce is enabled. Executing job immediately.");
            await DoWorkAsync(stoppingToken);
            _logger.LogInformation("Job completed. Stopping application.");
            _hostApplicationLifetime.StopApplication();
            return;
        }

        var cron = CronExpression.Parse(_options.CronExpression);
        _logger.LogInformation("Cron mode enabled. Schedule: {CronExpression}", _options.CronExpression);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = cron.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
            if (next.HasValue)
            {
                var delay = next.Value - DateTimeOffset.Now;
                if (delay.TotalMilliseconds > 0)
                {
                    _logger.LogInformation("Next execution scheduled at: {NextOccurrence}", next.Value);
                    await Task.Delay(delay, stoppingToken);
                }

                await DoWorkAsync(stoppingToken);
            }
            else
            {
                _logger.LogWarning("No more occurrences for the cron expression. Stopping worker.");
                break;
            }
        }
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email report job started at: {Time}", DateTimeOffset.Now);

        try
        {
            var files = _pdfScannerService.ScanForPdfFiles(_options.PdfFolderPath).ToList();

            if (!files.Any())
            {
                _logger.LogWarning("No PDF files found in {Path}. Skipping email.", _options.PdfFolderPath);
                return;
            }

            await _emailService.SendEmailWithAttachmentsAsync(_options.Email, files);

            if (_options.MoveAfterSend)
            {
                ArchiveFiles(files);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during the email report job execution.");
        }

        _logger.LogInformation("Email report job finished at: {Time}", DateTimeOffset.Now);
    }

    private void ArchiveFiles(IEnumerable<FileInfo> files)
    {
        if (!Directory.Exists(_options.ArchiveFolderPath))
        {
            Directory.CreateDirectory(_options.ArchiveFolderPath);
            _logger.LogInformation("Created archive folder: {Path}", _options.ArchiveFolderPath);
        }

        foreach (var file in files)
        {
            try
            {
                var destination = Path.Combine(_options.ArchiveFolderPath, file.Name);
                
                // Handle filename collision
                if (File.Exists(destination))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    destination = Path.Combine(_options.ArchiveFolderPath, $"{Path.GetFileNameWithoutExtension(file.Name)}_{timestamp}{file.Extension}");
                }

                file.MoveTo(destination);
                _logger.LogInformation("Moved file {FileName} to archive", file.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move file {FileName} to archive", file.Name);
            }
        }
    }
}
