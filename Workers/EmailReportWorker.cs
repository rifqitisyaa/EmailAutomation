using EmailAutomation.Models;
using EmailAutomation.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Cronos;

namespace EmailAutomation.Workers;

public class EmailReportWorker : BackgroundService
{
    private readonly ILogger<EmailReportWorker> _logger;
    private readonly IEmailService _emailService;
    private readonly IReportDataService _reportDataService;
    private readonly IHtmlTemplateService _htmlTemplateService;
    private readonly IPdfGeneratorService _pdfGeneratorService;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly EmailReportOptions _options;

    public EmailReportWorker(
        ILogger<EmailReportWorker> logger,
        IEmailService emailService,
        IReportDataService reportDataService,
        IHtmlTemplateService htmlTemplateService,
        IPdfGeneratorService pdfGeneratorService,
        IHostApplicationLifetime hostApplicationLifetime,
        IOptions<EmailReportOptions> options)
    {
        _logger = logger;
        _emailService = emailService;
        _reportDataService = reportDataService;
        _htmlTemplateService = htmlTemplateService;
        _pdfGeneratorService = pdfGeneratorService;
        _hostApplicationLifetime = hostApplicationLifetime;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.RunOnce)
        {
            _logger.LogInformation("RunOnce is enabled. Executing all jobs immediately.");
            await ProcessAllJobsAsync(stoppingToken);
            _logger.LogInformation("All jobs completed. Stopping application.");
            _hostApplicationLifetime.StopApplication();
            return;
        }

        _logger.LogInformation("Multi-Job Cron mode enabled. Monitoring {Count} jobs.", _options.Jobs.Count);

        // Dictionary untuk melacak waktu eksekusi berikutnya tiap job
        var nextOccurrences = new Dictionary<string, DateTimeOffset>();
        
        foreach (var job in _options.Jobs)
        {
            var cron = CronExpression.Parse(job.CronExpression);
            var next = cron.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
            if (next.HasValue)
            {
                nextOccurrences[job.JobName] = next.Value;
                _logger.LogInformation("Job [{JobName}] scheduled at: {Next}", job.JobName, next.Value);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;
            
            foreach (var job in _options.Jobs)
            {
                if (nextOccurrences.TryGetValue(job.JobName, out var next) && now >= next)
                {
                    _logger.LogInformation("Executing scheduled job: {JobName}", job.JobName);
                    await DoWorkForJobAsync(job, stoppingToken);

                    // Update waktu eksekusi berikutnya
                    var cron = CronExpression.Parse(job.CronExpression);
                    var newNext = cron.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
                    if (newNext.HasValue)
                    {
                        nextOccurrences[job.JobName] = newNext.Value;
                        _logger.LogInformation("Job [{JobName}] next schedule: {Next}", job.JobName, newNext.Value);
                    }
                }
            }

            await Task.Delay(10000, stoppingToken); // Cek tiap 10 detik
        }
    }

    private async Task ProcessAllJobsAsync(CancellationToken stoppingToken)
    {
        foreach (var job in _options.Jobs)
        {
            await DoWorkForJobAsync(job, stoppingToken);
        }
    }

    private async Task DoWorkForJobAsync(ReportJobConfig job, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("--- Starting Job: {JobName} ---", job.JobName);

            // 1. Get Data
            var reportDate = DateTime.Today;
            var reportData = await _reportDataService.GetReportDataAsync(job, reportDate);

            if (reportData.Rows.Count == 0)
            {
                _logger.LogWarning("Job [{JobName}]: No data found. Skipping.", job.JobName);
                return;
            }

            // 2. Build HTML
            var html = _htmlTemplateService.BuildHtml(reportData);

            // 3. Generate PDF
            var pdfBytes = await _pdfGeneratorService.GeneratePdfAsync(html);

            // 4. Send Email
            var attachmentFileName = $"{job.JobName}_{reportDate:yyyyMMdd}.pdf";
            await _emailService.SendJobEmailAsync(job, pdfBytes, attachmentFileName);

            _logger.LogInformation("--- Job {JobName} Completed Successfully ---", job.JobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job: {JobName}", job.JobName);
        }
    }
}
