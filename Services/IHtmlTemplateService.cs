using EmailAutomation.Models;

namespace EmailAutomation.Services;

public interface IHtmlTemplateService
{
    string BuildHtml(ReportData data, ReportJobConfig config);
}
