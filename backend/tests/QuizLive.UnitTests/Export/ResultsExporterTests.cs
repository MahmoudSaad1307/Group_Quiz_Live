using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using QuizLive.Application.Results;
using QuizLive.Domain.Enums;
using QuizLive.Infrastructure.Export;

namespace QuizLive.UnitTests.Export;

public sealed class ResultsExporterTests
{
    private static SessionResultsDto SampleResults() =>
        new(
            Guid.NewGuid(),
            "123456",
            QuizLanguage.Ar,
            SessionStatus.Finished,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow,
            [
                new(Guid.NewGuid(), "أحمد", 1, 1800, 2, 1200.5),
                new(Guid.NewGuid(), "=HYPERLINK(\"http://evil\")", 2, 900, 1, 3500.0)
            ],
            [
                new(Guid.NewGuid(), 0, "ما هي عاصمة مصر؟", 100.0, 1200.5,
                    [new(Guid.NewGuid(), "القاهرة", true, 2), new(Guid.NewGuid(), "الإسكندرية", false, 0)])
            ]);

    [Fact]
    public void ExportCsv_IncludesUtf8Bom_ArabicText_AndEscapesFormulas()
    {
        var exporter = new ResultsExporter();
        var bytes = exporter.ExportCsv(SampleResults());

        bytes.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        text.Should().Contain("أحمد");
        text.Should().Contain("\"'=HYPERLINK");
    }

    [Fact]
    public void ExportXlsx_ProducesThreeValidSheets()
    {
        var exporter = new ResultsExporter();
        var bytes = exporter.ExportXlsx(SampleResults());

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        wb.Worksheets.Select(w => w.Name).Should().ContainInOrder("Summary", "Leaderboard", "Questions");
        wb.Worksheet("Leaderboard").Cell(2, 2).GetString().Should().Be("أحمد");
        var escapedCell = wb.Worksheet("Leaderboard").Cell(3, 2);
        escapedCell.HasFormula.Should().BeFalse();
        escapedCell.GetString().Should().Be("=HYPERLINK(\"http://evil\")");
    }
}
