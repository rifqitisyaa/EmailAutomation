using EmailAutomation.Models;
using EmailAutomation.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Cronos;
using Microsoft.EntityFrameworkCore;
using EmailAutomation.Data;

namespace EmailAutomation.Workers;

public class EmailReportWorker : BackgroundService
{
    private readonly ILogger<EmailReportWorker> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailReportOptions _options;

    public EmailReportWorker(
        ILogger<EmailReportWorker> logger,
        IHostApplicationLifetime hostApplicationLifetime,
        IServiceScopeFactory scopeFactory,
        IOptions<EmailReportOptions> options)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    // NEW: record buat nyimpen hasil generate PDF per job
    private record JobPdfResult(ReportJobConfig Job, byte[] PdfBytes, string FileName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.RunOnce)
        {
            _logger.LogInformation("RunOnce is enabled. Executing all active jobs from database immediately.");
            await ProcessAllJobsAsync(stoppingToken);
            _logger.LogInformation("All jobs completed. Stopping application.");
            _hostApplicationLifetime.StopApplication();
            return;
        }

        _logger.LogInformation("Multi-Job Cron mode enabled. Monitoring jobs dynamically from Database.");

        var nextOccurrences = new Dictionary<string, DateTimeOffset>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var activeJobs = await dbContext.ReportJobConfigs.Where(j => j.IsActive).ToListAsync(stoppingToken);

                    foreach (var job in activeJobs)
                    {
                        if (!nextOccurrences.ContainsKey(job.JobName))
                        {
                            var cron = CronExpression.Parse(job.CronExpression);
                            var next = cron.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
                            if (next.HasValue)
                            {
                                nextOccurrences[job.JobName] = next.Value;
                                _logger.LogInformation("Job [{JobName}] dynamic schedule registered at: {Next}", job.JobName, next.Value);
                            }
                        }
                    }

                    // CHANGED: kumpulkan dulu semua job yang sudah waktunya
                    var jobsToRun = activeJobs
                        .Where(job => nextOccurrences.TryGetValue(job.JobName, out var nextExec) && now >= nextExec)
                        .ToList();

                    // CHANGED: kalau ada yang perlu jalan, proses sekaligus (untuk grouping email)
                    if (jobsToRun.Any())
                    {
                        _logger.LogInformation("Running {Count} scheduled job(s).", jobsToRun.Count);
                        await ProcessJobGroupAsync(scope, jobsToRun, stoppingToken);

                        // Update next schedule semua job yang baru jalan
                        foreach (var job in jobsToRun)
                        {
                            var cron = CronExpression.Parse(job.CronExpression);
                            var newNext = cron.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
                            if (newNext.HasValue)
                            {
                                nextOccurrences[job.JobName] = newNext.Value;
                                _logger.LogInformation("Job [{JobName}] next schedule: {Next}", job.JobName, newNext.Value);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during jobs monitoring cycle.");
            }

            await Task.Delay(10000, stoppingToken);
        }
    }

    // CHANGED: sekarang pakai ProcessJobGroupAsync
    private async Task ProcessAllJobsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activeJobs = await dbContext.ReportJobConfigs
            .Where(j => j.IsActive).ToListAsync(stoppingToken);

        await ProcessJobGroupAsync(scope, activeJobs, stoppingToken);
    }

    // NEW: inti logika grouping — generate semua PDF dulu, baru group by email, baru kirim
    private async Task ProcessJobGroupAsync(
        IServiceScope scope,
        List<ReportJobConfig> jobs,
        CancellationToken stoppingToken)
    {
        var results = new List<JobPdfResult>();

        // Pass 1: generate PDF untuk semua job
        foreach (var job in jobs)
        {
            var result = await GeneratePdfForJobAsync(scope, job, stoppingToken);
            if (result != null)
                results.Add(result);
        }

        if (!results.Any()) return;

        // Pass 2: group by recipients, kirim 1 email per group
        var grouped = results.GroupBy(r => NormalizeRecipients(r.Job));
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        foreach (var group in grouped)
        {
            var attachments = group
                .Select(r => (r.PdfBytes, r.FileName))
                .ToList();

            _logger.LogInformation(
                "Sending grouped email to [{Recipients}] with {Count} attachment(s): {Files}",
                group.Key,
                attachments.Count,
                string.Join(", ", group.Select(r => r.FileName)));

            await emailService.SendGroupedEmailAsync(group.First().Job, attachments);
        }
    }

    private static string NormalizeRecipients(ReportJobConfig job)
    {
        return string.Join(",",
            job.ToAddresses
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().ToLowerInvariant())
                .OrderBy(e => e));
    }

    // CHANGED: dulu DoWorkForJobAsync, sekarang dipecah jadi ini (hanya generate PDF, tidak kirim email)
    private async Task<JobPdfResult?> GeneratePdfForJobAsync(
        IServiceScope scope,
        ReportJobConfig job,
        CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("--- Generating PDF for Job: {JobName} ---", job.JobName);

            var reportDataService = scope.ServiceProvider.GetRequiredService<IReportDataService>();
            var htmlTemplateService = scope.ServiceProvider.GetRequiredService<IHtmlTemplateService>();
            var pdfGeneratorService = scope.ServiceProvider.GetRequiredService<IPdfGeneratorService>();

            var reportDate = DateTime.Today;
            var reportData = await reportDataService.GetReportDataAsync(job, reportDate);

            if (reportData == null || reportData.Rows.Count == 0)
            {
                _logger.LogWarning("Job [{JobName}]: No data found. Skipping.", job.JobName);
                return null;
            }

            var html = htmlTemplateService.BuildHtml(reportData);
            var pdfBytes = await pdfGeneratorService.GeneratePdfAsync(html);
            var fileName = $"{job.JobName}_{reportDate:yyyyMMdd}.pdf";

            return new JobPdfResult(job, pdfBytes, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF for job: {JobName}", job.JobName);
            return null;
        }
    }
}