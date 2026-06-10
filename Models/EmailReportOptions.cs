namespace EmailAutomation.Models;

public class EmailReportOptions
{
    public const string SectionName = "EmailReport";

    public string PdfFolderPath { get; set; } = string.Empty;
    public string ArchiveFolderPath { get; set; } = string.Empty;
    public bool MoveAfterSend { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public bool RunOnce { get; set; }
    public EmailSettings Email { get; set; } = new();
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool UseSsl { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string[] ToAddresses { get; set; } = Array.Empty<string>();
    public string[] CcAddresses { get; set; } = Array.Empty<string>();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
