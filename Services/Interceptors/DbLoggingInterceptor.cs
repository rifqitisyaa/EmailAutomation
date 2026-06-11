using EmailAutomation.Models;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Text;

namespace EmailAutomation.Services.Interceptors;

public class DbLoggingInterceptor : DbCommandInterceptor
{
    // Kita nggak bisa inject AppDbContext kesini karena bakal circular dependency.
    // Jadi kita butuh cara lain untuk simpan lognya, atau kita pakai ILogger standar
    // dan biarkan Worker yang simpan ke DB. 
    // Tapi karena lo minta interceptor buat log ke tabel, kita buat logic simpan manual pakai koneksi yang ada.

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        LogToDb(command, eventData.Exception.ToString(), "ERROR");
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        LogToDb(command, eventData.Exception.ToString(), "ERROR");
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void LogToDb(DbCommand command, string? exception, string level)
    {
        try
        {
            // Karena kita sedang di interceptor, kita pakai koneksi yang sama untuk insert log
            // biar nggak ribet urusan connection string.
            using var logConn = (DbConnection)((ICloneable)command.Connection).Clone();
            logConn.Open();

            using var logCmd = logConn.CreateCommand();
            logCmd.CommandText = @"
                INSERT INTO EmailAutomationLog (Timestamp, Level, Message, Exception, CommandText, Parameters)
                VALUES (@Timestamp, @Level, @Message, @Exception, @CommandText, @Parameters)";

            var parameters = new StringBuilder();
            foreach (DbParameter param in command.Parameters)
            {
                parameters.Append($"{param.ParameterName}={param.Value}; ");
            }

            AddParameter(logCmd, "@Timestamp", DateTime.Now);
            AddParameter(logCmd, "@Level", level);
            AddParameter(logCmd, "@Message", $"Command failed: {command.CommandText}");
            AddParameter(logCmd, "@Exception", exception ?? (object)DBNull.Value);
            AddParameter(logCmd, "@CommandText", command.CommandText);
            AddParameter(logCmd, "@Parameters", parameters.ToString());

            logCmd.ExecuteNonQuery();
        }
        catch
        {
            // Fail safe, jangan sampai logger bikin aplikasi mati
        }
    }

    private void AddParameter(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}