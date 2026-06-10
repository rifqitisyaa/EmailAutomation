using EmailAutomation.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

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
        
        var startYear = (reportDate.Year - 3).ToString(); // Contoh dinamis 2023
        var endYear = reportDate.Year.ToString();        // Contoh dinamis 2026

        

        var reportData = new ReportData
        {
            ReportDate = reportDate,
            GeneratedAt = DateTime.Now,
            ReportTitle = config.ReportTitle,
            StartYear = startYear,
            EndYear = endYear
        };

        using var conn = new SqlConnection(connectionString);
        using var cmd = new SqlCommand(config.SpName, conn);
        cmd.CommandType = CommandType.StoredProcedure;
        
        cmd.Parameters.AddWithValue("@StartDate", startYear);
        cmd.Parameters.AddWithValue("@EndDate", endYear);

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
}
