using EmailAutomation.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text.Json;

namespace EmailAutomation.Services;

public class ReportDataService : IReportDataService
{
    private readonly IConfiguration _configuration;

    public ReportDataService(IConfiguration configuration)
    {
        _configuration = configuration;
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

        if (!string.IsNullOrEmpty(config.ParametersJson))
        {
            var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(config.ParametersJson);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    var resolvedValue = ResolveParameterValue(param.Value, reportDate);
                    cmd.Parameters.AddWithValue(param.Key, resolvedValue);

                    if (param.Key.Contains("Start", StringComparison.OrdinalIgnoreCase)) reportData.StartYear = resolvedValue.ToString();
                    if (param.Key.Contains("To", StringComparison.OrdinalIgnoreCase) || param.Key.Contains("End", StringComparison.OrdinalIgnoreCase)) reportData.EndYear = resolvedValue.ToString();
                }
            }
        }

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

        return reportData;
    }

    private object ResolveParameterValue(string value, DateTime reportDate)
    {
        if (string.IsNullOrEmpty(value)) return value;

        return value.ToLower() switch
        {
            "{today}" => DateTime.Now.ToString("yyyyMMdd"),
            "{yesterday}" => DateTime.Now.AddDays(-1).ToString("yyyyMMdd"),
            "{report_date}" => reportDate.ToString("yyyyMMdd"),
            "{month_start}" => new DateTime(reportDate.Year, reportDate.Month, 1).ToString("yyyyMMdd"),
            "{month_end}" => new DateTime(reportDate.Year, reportDate.Month, 1).AddMonths(1).AddDays(-1).ToString("yyyyMMdd"),
            _ => value
        };
    }
}
