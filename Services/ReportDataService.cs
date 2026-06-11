using EmailAutomation.Data;
using EmailAutomation.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text.Json;
using System.Text;

namespace EmailAutomation.Services;

public class ReportDataService : IReportDataService
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;

    public ReportDataService(IConfiguration configuration, AppDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public async Task<ReportData> GetReportDataAsync(ReportJobConfig config, DateTime reportDate)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        var reportData = new ReportData
        {
            ReportDate = reportDate,
            GeneratedAt = DateTime.Now,
            ReportTitle = config.ReportTitle
        };

        using var conn = new SqlConnection(connectionString);
        using var cmd = new SqlCommand(config.SpName, conn);
        cmd.CommandType = CommandType.StoredProcedure;

        var paramLog = new StringBuilder();
        if (!string.IsNullOrEmpty(config.ParametersJson))
        {
            var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(config.ParametersJson);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    var resolvedValue = ResolveParameterValue(param.Value, reportDate);
                    cmd.Parameters.AddWithValue(param.Key, resolvedValue);
                    paramLog.Append($"{param.Key}={resolvedValue}; ");

                    if (param.Key.Contains("Start", StringComparison.OrdinalIgnoreCase)) reportData.StartYear = resolvedValue.ToString();
                    if (param.Key.Contains("To", StringComparison.OrdinalIgnoreCase) || param.Key.Contains("End", StringComparison.OrdinalIgnoreCase)) reportData.EndYear = resolvedValue.ToString();
                }
            }
        }

        try 
        {
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                reportData.Columns.Add(reader.GetName(i));
            }

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var val = reader.GetValue(i);
                    row[reader.GetName(i)] = val == DBNull.Value ? null : val;
                }
                reportData.Rows.Add(row);
            }

            // Log Success ke Database via EF Core
            _context.EmailAutomationLogs.Add(new EmailAutomationLog
            {
                Level = "INFO",
                JobName = config.JobName,
                Message = $"Successfully generated report data for {config.JobName}",
                CommandText = config.SpName,
                Parameters = paramLog.ToString()
            });
            await _context.SaveChangesAsync();

            return reportData;
        }
        catch (Exception ex)
        {
            // Log Error ke Database
            _context.EmailAutomationLogs.Add(new EmailAutomationLog
            {
                Level = "ERROR",
                JobName = config.JobName,
                Message = $"Failed to generate report data for {config.JobName}",
                Exception = ex.ToString(),
                CommandText = config.SpName,
                Parameters = paramLog.ToString()
            });
            await _context.SaveChangesAsync();
            throw;
        }
    }

    private object ResolveParameterValue(string value, DateTime reportDate)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var lowerValue = value.ToLower();
        return lowerValue switch
        {
            "{today}" => DateTime.Now.ToString("yyyyMMdd"),
            "{yesterday}" => DateTime.Now.AddDays(-1).ToString("yyyyMMdd"),
            "{report_date}" => reportDate.ToString("yyyyMMdd"),
            
            "{month_start}" => new DateTime(reportDate.Year, reportDate.Month, 1).ToString("yyyyMMdd"),
            "{month_end}" => new DateTime(reportDate.Year, reportDate.Month, 1).AddMonths(1).AddDays(-1).ToString("yyyyMMdd"),
            
            "{last_month_start}" => new DateTime(reportDate.Year, reportDate.Month, 1).AddMonths(-1).ToString("yyyyMMdd"),
            "{last_month_end}" => new DateTime(reportDate.Year, reportDate.Month, 1).AddDays(-1).ToString("yyyyMMdd"),
            
            "{year_start}" => new DateTime(reportDate.Year, 1, 1).ToString("yyyyMMdd"),
            "{year_end}" => new DateTime(reportDate.Year, 12, 31).ToString("yyyyMMdd"),
            "{last_year_start}" => new DateTime(reportDate.Year - 1, 1, 1).ToString("yyyyMMdd"),
            "{last_year_end}" => new DateTime(reportDate.Year - 1, 12, 31).ToString("yyyyMMdd"),

            "{week_start}" => reportDate.AddDays(-(int)(reportDate.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)reportDate.DayOfWeek - 1)).ToString("yyyyMMdd"),
            "{week_end}" => reportDate.AddDays(-(int)(reportDate.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)reportDate.DayOfWeek - 1)).AddDays(6).ToString("yyyyMMdd"),

            _ => value
        };
    }
}
