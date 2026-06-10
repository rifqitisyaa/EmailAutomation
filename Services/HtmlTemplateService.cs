using System.Text;
using EmailAutomation.Models;

namespace EmailAutomation.Services;

public class HtmlTemplateService : IHtmlTemplateService
{
    public string BuildHtml(ReportData data)
    {
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html>");
        sb.Append("<html>");
        sb.Append("<head>");
        sb.Append("<style>");
        sb.Append("body { font-family: 'Calibri', 'Arial', sans-serif; }");
        sb.Append(".header { background-color: #1F3864; color: white; padding: 15px; text-align: center; font-size: 14pt; font-weight: bold; }");
        sb.Append(".info-section { background-color: #E2EFDA; color: #375623; padding: 10px; margin: 10px 0; font-style: italic; border-left: 5px solid #375623; }");
        sb.Append("table { width: 100%; border-collapse: collapse; margin-top: 20px; font-size: 9pt; }");
        sb.Append("th { background-color: #1F3864; color: white; border: 1px solid #cccccc; padding: 10px; text-align: center; }");
        sb.Append("td { border: 1px solid #cccccc; padding: 6px; }");
        sb.Append("tr:nth-child(even) { background-color: #F2F2F2; }");
        sb.Append("tr:nth-child(odd) { background-color: white; }");
        sb.Append(".summary-row { background-color: #D9E1F2; font-weight: bold; }");
        sb.Append(".footer { margin-top: 30px; font-size: 9pt; color: #777777; border-top: 1px solid #eeeeee; padding-top: 10px; }");
        sb.Append(".number-cell { text-align: right; }");
        sb.Append("</style>");
        sb.Append("</head>");
        sb.Append("<body>");

        sb.Append($"<div class='header'>{data.ReportTitle}</div>");

        sb.Append("<div class='info-section'>");
        sb.Append($"Perbandingan Tahun : {data.StartYear} s/d {data.EndYear}<br/>");
        sb.Append($"Digenerate pada : {data.GeneratedAt:dd-MM-yyyy HH:mm:ss}");
        sb.Append("</div>");

        sb.Append("<table>");
        sb.Append("<thead>");
        sb.Append("<tr>");
        // Build Header secara dinamis
        foreach (var col in data.Columns)
        {
            sb.Append($"<th>{col}</th>");
        }
        sb.Append("</tr>");
        sb.Append("</thead>");
        sb.Append("<tbody>");

        // Build Rows secara dinamis
        foreach (var row in data.Rows)
        {
            sb.Append("<tr>");
            foreach (var col in data.Columns)
            {
                var val = row[col];
                string displayValue;
                string cssClass = "";

                if (val == null)
                {
                    displayValue = "-";
                }
                else if (val is decimal d)
                {
                    displayValue = d.ToString("N2");
                    cssClass = "class='number-cell'";
                }
                else if (val is double db)
                {
                    displayValue = db.ToString("N2");
                    cssClass = "class='number-cell'";
                }
                else if (val is int i)
                {
                    displayValue = i.ToString("N0");
                    cssClass = "class='number-cell'";
                }
                else
                {
                    displayValue = val.ToString() ?? "-";
                }

                sb.Append($"<td {cssClass}>{displayValue}</td>");
            }
            sb.Append("</tr>");
        }

        sb.Append("</tbody>");
        sb.Append("</table>");

        sb.Append("<div class='footer'>");
        sb.Append("Dokumen ini digenerate otomatis oleh sistem. Harap tidak membalas email ini.");
        sb.Append("</div>");

        sb.Append("</body>");
        sb.Append("</html>");

        return sb.ToString();
    }
}
