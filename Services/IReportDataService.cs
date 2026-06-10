using EmailAutomation.Models;

namespace EmailAutomation.Services;

public interface IReportDataService
{
    Task<ReportData> GetReportDataAsync(ReportJobConfig config, DateTime reportDate);
}
