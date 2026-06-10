using EmailAutomation.Models;
using EmailAutomation.Services;
using EmailAutomation.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Configure Options
builder.Services.Configure<EmailReportOptions>(
    builder.Configuration.GetSection(EmailReportOptions.SectionName));

// Override RunOnce if --run-once flag is present
if (args.Contains("--run-once"))
{
    builder.Services.PostConfigure<EmailReportOptions>(options =>
    {
        options.RunOnce = true;
    });
}

// Register Services
builder.Services.AddSingleton<IPdfScannerService, PdfScannerService>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddHostedService<EmailReportWorker>();

var host = builder.Build();

try
{
    Log.Information("Starting EmailAutomation host...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
