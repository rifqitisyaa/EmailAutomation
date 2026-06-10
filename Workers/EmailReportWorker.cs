using EmailAutomation.Models;
using EmailAutomation.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection; // WAJIB TAMBAHKAN INI
using Cronos;
using EmailAutomation.Data;
using Microsoft.EntityFrameworkCore;

namespace EmailAutomation.Workers;

public class EmailReportWorker : BackgroundService
{
    private readonly ILogger<EmailReportWorker> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IServiceScopeFactory _scopeFactory; // <--- 1. Inject ini untuk handle Scoped service
    private readonly EmailReportOptions _options;

    public EmailReportWorker(
        ILogger<EmailReportWorker> logger,
        IHostApplicationLifetime hostApplicationLifetime,
        IServiceScopeFactory scopeFactory, // <--- Ganti service lama dengan ini
        IOptions<EmailReportOptions> options)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // JIKA RUN ONCE DIAKTIFKAN
        if (_options.RunOnce)
        {
            _logger.LogInformation("RunOnce is enabled. Executing all active jobs from database immediately.");
            await ProcessAllJobsAsync(stoppingToken);
            _logger.LogInformation("All jobs completed. Stopping application.");
            _hostApplicationLifetime.StopApplication();
            return;
        }

        _logger.LogInformation("Multi-Job Cron mode enabled. Monitoring jobs dynamically from Database.");

        // Dictionary untuk melacak waktu eksekusi berikutnya tiap job
        var nextOccurrences = new Dictionary<string, DateTimeOffset>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;

            try
            {
                // 2. Bikin scope tiap looping buat baca list job paling up-to-date dari DB
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Ambil list job aktif langsung dari tabel database
                    var activeJobs = await dbContext.ReportJobConfigs.Where(j => j.IsActive).ToListAsync(stoppingToken);

                    foreach (var job in activeJobs)
                    {
                        // Jika job baru ditambah ke DB dan belum ada di tracking dictionary, hitung schedule-nya
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

                        // Cek apakah sudah masuk waktunya dieksekusi
                        if (nextOccurrences.TryGetValue(job.JobName, out var nextExecution) && now >= nextExecution)
                        {
                            _logger.LogInformation("Executing scheduled job from DB: {JobName}", job.JobName);

                            // Eksekusi job pakai scope yang ada
                            await DoWorkForJobAsync(scope, job, stoppingToken);

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
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during jobs monitoring cycle.");
            }

            await Task.Delay(10000, stoppingToken); // Cek tiap 10 detik
        }
    }

    private async Task ProcessAllJobsAsync(CancellationToken stoppingToken)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var activeJobs = await dbContext.ReportJobConfigs.Where(j => j.IsActive).ToListAsync(stoppingToken);

            foreach (var job in activeJobs)
            {
                await DoWorkForJobAsync(scope, job, stoppingToken);
            }
        }
    }

    // 3. Modifikasi method ini agar menerima 'IServiceScope' untuk me-resolve service scoped secara aman
    private async Task DoWorkForJobAsync(IServiceScope scope, ReportJobConfig job, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("--- Starting Job: {JobName} ---", job.JobName);

            // Resolve service di dalam lingkup scope per-job
            var reportDataService = scope.ServiceProvider.GetRequiredService<IReportDataService>();
            var htmlTemplateService = scope.ServiceProvider.GetRequiredService<IHtmlTemplateService>();
            var pdfGeneratorService = scope.ServiceProvider.GetRequiredService<IPdfGeneratorService>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            // 1. Get Data via Stored Procedure dinamis yang ada di service lu
            var reportDate = DateTime.Today;
            var reportData = await reportDataService.GetReportDataAsync(job, reportDate);

            if (reportData == null || reportData.Rows.Count == 0)
            {
                _logger.LogWarning("Job [{JobName}]: No data found. Skipping.", job.JobName);
                return;
            }

            // 2. Build HTML
            var html = htmlTemplateService.BuildHtml(reportData);

            // 3. Generate PDF
            var pdfBytes = await pdfGeneratorService.GeneratePdfAsync(html);

            // 4. Send Email
            var attachmentFileName = $"{job.JobName}_{reportDate:yyyyMMdd}.pdf";
            await emailService.SendJobEmailAsync(job, pdfBytes, attachmentFileName);

            _logger.LogInformation("--- Job {JobName} Completed Successfully ---", job.JobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job: {JobName}", job.JobName);
        }
    }
}