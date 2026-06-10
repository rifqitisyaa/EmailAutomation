namespace EmailAutomation.Models;

public class EmailReportOptions
{
    public const string SectionName = "EmailReport";
    public string PdfOutputPath { get; set; } = string.Empty;
    public bool RunOnce { get; set; }
    public List<ReportJobConfig> Jobs { get; set; } = new();
}

public class ReportJobConfig
{
    public string JobName { get; set; } = string.Empty;
    public string SpName { get; set; } = string.Empty;
    public string ReportTitle { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public EmailJobSettings Email { get; set; } = new();

    public bool IsActive { get; set; }
}

public class EmailJobSettings
{
    public string[] ToAddresses { get; set; } = Array.Empty<string>();
    public string[] CcAddresses { get; set; } = Array.Empty<string>();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class GlobalEmailConfig
{
    public const string SectionName = "EmailConfig";
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool UseSsl { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}

public class ReportData
{
    public DateTime ReportDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string ReportTitle { get; set; } = string.Empty;
    public string StartYear { get; set; } = string.Empty;
    public string EndYear { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}
