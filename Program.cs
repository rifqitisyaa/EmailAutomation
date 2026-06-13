using EmailAutomation.Models;
using EmailAutomation.Services;
using EmailAutomation.Workers;
using Microsoft.EntityFrameworkCore; // Tambahkan ini untuk .UseSqlServer()
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EmailAutomation.Data; // Tambahkan ini untuk AppDbContext
using EmailAutomation.Services.Interceptors;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Setup Serilog dari appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Registrasi Konfigurasi Options
builder.Services.Configure<EmailReportOptions>(
    builder.Configuration.GetSection(EmailReportOptions.SectionName));
builder.Services.Configure<GlobalEmailConfig>(
    builder.Configuration.GetSection(GlobalEmailConfig.SectionName));

if (args.Contains("--run-once"))
{
    builder.Services.PostConfigure<EmailReportOptions>(options =>
    {
        options.RunOnce = true;
    });
}

// 1. DAFTARKAN EF CORE DBCONTEXT DI SINI
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.AddInterceptors(new DbLoggingInterceptor());
});

// 2. UBAH REGISTRASI SERVICE MENJADI SCOPED / TRANSIENT
// Supaya aman berinteraksi dengan AppDbContext yang sifatnya Scoped
builder.Services.AddScoped<IReportDataService, ReportDataService>();
builder.Services.AddScoped<IHtmlTemplateService, HtmlTemplateService>();
builder.Services.AddScoped<IPdfGeneratorService, PdfGeneratorService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Worker tetap Hosted Service (Singleton otomatis dari .NET)
builder.Services.AddHostedService<EmailReportWorker>();

var host = builder.Build();

try
{
    Log.Information("Starting EmailAutomation host with Multi-Job support...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
    
    // Cegah window langsung tertutup kalau bukan mode --run-once (atau biar user bisa baca log)
    if (args.Contains("--run-once") || !builder.Environment.IsProduction())
    {
        Console.WriteLine("\nExecution finished. Press any key to exit...");
        Console.ReadKey();
    }
}