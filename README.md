# EmailAutomation

An automated email reporting tool built with .NET 10 that scans a folder for PDF files and sends them as attachments via SMTP on a schedule.

## Features
- **PDF Scanning**: Automatically finds `*.pdf` files in a configured directory.
- **SMTP Integration**: Uses MailKit for reliable email delivery.
- **Flexible Scheduling**: Supports standard Cron expressions via Cronos.
- **One-Shot Mode**: Can be run once for manual triggering or integration with other schedulers.
- **Archiving**: Automatically moves processed files to an archive folder after successful delivery.
- **Logging**: Detailed logging via Serilog.

## Configuration

Edit `appsettings.json` to configure the application:

```json
{
  "EmailReport": {
    "PdfFolderPath": "C:\\Reports\\PDF",
    "ArchiveFolderPath": "C:\\Reports\\Archived",
    "MoveAfterSend": true,
    "CronExpression": "0 8 * * 1-5",
    "RunOnce": false,
    "Email": {
      "SmtpHost": "smtp.example.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "Username": "sender@example.com",
      "Password": "your-password",
      "FromAddress": "sender@example.com",
      "FromName": "Report System",
      "ToAddresses": ["recipient1@example.com"],
      "CcAddresses": [],
      "Subject": "Daily Report - {date}",
      "Body": "Please find the attached reports for {date}."
    }
  }
}
```

### Placeholders
- `{date}` in `Subject` and `Body` will be replaced with the current date in `dd-MM-yyyy` format.

## How to Run

### Standard Mode (Cron)
Uses the `CronExpression` from configuration.
```bash
dotnet run
```

### One-Shot Mode
Executes immediately and then exits, regardless of the `CronExpression`.
```bash
dotnet run -- --run-once
```

## Deployment as Windows Service

1. **Publish the application**:
   ```bash
   dotnet publish -c Release -o C:\Services\EmailAutomation
   ```

2. **Register the service**:
   Open PowerShell as Administrator and run:
   ```powershell
   sc.exe create EmailAutomation binPath= "C:\Services\EmailAutomation\EmailAutomation.exe" start= auto
   sc.exe start EmailAutomation
   ```

## Example Cron Expressions

| Expression | Description |
|------------|-------------|
| `0 8 * * 1-5` | Every Monday to Friday at 8:00 AM |
| `0 0 * * *` | Every day at midnight |
| `0 9 1 * *` | First day of every month at 9:00 AM |
| `*/30 * * * *` | Every 30 minutes |

## Requirements
- .NET 10 SDK
- SMTP Server access (Gmail, Outlook, SendGrid, etc.)
