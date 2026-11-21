using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Results;

namespace QuizLive.Infrastructure.Export;

public sealed class ResultsExporter : IResultsExporter
{
    public byte[] ExportCsv(SessionResultsDto results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Rank,Nickname,TotalScore,CorrectCount,AvgResponseTimeMs");
        foreach (var s in results.Students)
        {
            sb.AppendLine(string.Join(",",
                s.Rank.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(s.Nickname),
                s.TotalScore.ToString(CultureInfo.InvariantCulture),
                s.CorrectCount.ToString(CultureInfo.InvariantCulture),
                s.AvgResponseTimeMs.ToString(CultureInfo.InvariantCulture)));
        }

        // UTF-8 BOM so Excel opens Arabic characters properly
        var utf8Bom = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var combined = new byte[utf8Bom.Length + body.Length];
        utf8Bom.CopyTo(combined, 0);
        body.CopyTo(combined, utf8Bom.Length);
        return combined;
    }

    public byte[] ExportXlsx(SessionResultsDto results)
    {
        using var wb = new XLWorkbook();

        var summary = wb.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = "Session ID";
        summary.Cell(1, 2).Value = results.SessionId.ToString();
        summary.Cell(2, 1).Value = "PIN";
        summary.Cell(2, 2).Value = results.Pin;
        summary.Cell(3, 1).Value = "Status";
        summary.Cell(3, 2).Value = results.Status.ToString();
        summary.Cell(4, 1).Value = "Total Players";
        summary.Cell(4, 2).Value = results.Students.Count;

        var lb = wb.Worksheets.Add("Leaderboard");
        lb.Cell(1, 1).Value = "Rank";
        lb.Cell(1, 2).Value = "Nickname";
        lb.Cell(1, 3).Value = "Score";
        lb.Cell(1, 4).Value = "Correct";
        lb.Cell(1, 5).Value = "Avg Response (ms)";
        for (var i = 0; i < results.Students.Count; i++)
        {
            var s = results.Students[i];
            var r = i + 2;
            lb.Cell(r, 1).Value = s.Rank;
            lb.Cell(r, 2).Value = SafeExcelText(s.Nickname);
            lb.Cell(r, 3).Value = s.TotalScore;
            lb.Cell(r, 4).Value = s.CorrectCount;
            lb.Cell(r, 5).Value = s.AvgResponseTimeMs;
        }

        var qs = wb.Worksheets.Add("Questions");
        qs.Cell(1, 1).Value = "#";
        qs.Cell(1, 2).Value = "Question";
        qs.Cell(1, 3).Value = "Correct %";
        qs.Cell(1, 4).Value = "Avg Response (ms)";
        for (var i = 0; i < results.Questions.Count; i++)
        {
            var q = results.Questions[i];
            var r = i + 2;
            qs.Cell(r, 1).Value = q.OrderIndex + 1;
            qs.Cell(r, 2).Value = SafeExcelText(q.Text);
            qs.Cell(r, 3).Value = q.CorrectPercent;
            qs.Cell(r, 4).Value = q.AvgResponseTimeMs;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string SafeExcelText(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
        {
            return "'" + value;
        }

        return value;
    }

    private static string EscapeCsv(string value)
    {
        var safe = SafeExcelText(value).Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{safe}\"";
    }
}
