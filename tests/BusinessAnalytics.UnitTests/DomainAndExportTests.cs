using System.Text;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Export;
using BusinessAnalytics.Application.Validators;
using BusinessAnalytics.Domain.Entities;

namespace BusinessAnalytics.UnitTests;

public class DomainAndExportTests
{
    [Fact]
    public void Normalize_reopening_a_done_task_clears_completed_at()
    {
        var task = new TaskItem { Status = TaskItemStatus.Done };
        task.Normalize(DateTime.UtcNow);
        Assert.NotNull(task.CompletedAt);

        task.Status = TaskItemStatus.InProgress;
        task.Normalize(DateTime.UtcNow);

        Assert.Null(task.CompletedAt);
        Assert.Equal(99, task.ProgressPercent);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("-12.5", "-12.5")]
    public void Csv_escape(string input, string expected) => Assert.Equal(expected, CsvWriter.Escape(input));

    [Fact]
    public void Csv_writes_snake_case_header_and_utf8_bom()
    {
        var bytes = CsvWriter.Write([new MonthlyTrendReport { Month = new DateOnly(2026, 1, 1), CreatedTasks = 3, CompletedTasks = 1, CumulativeCompleted = 1 }]);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("month,created_tasks,completed_tasks,cumulative_completed", lines[0]);
        Assert.Equal("2026-01-01,3,1,1", lines[1]);
    }

    [Fact]
    public void Project_end_date_before_start_date_is_invalid()
    {
        var result = new CreateProjectRequestValidator().Validate(new CreateProjectRequest(
            "P-1", "n", null, "IT", null, StartDate: new DateOnly(2026, 5, 1), EndDate: new DateOnly(2026, 4, 1)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateProjectRequest.EndDate));
    }
}
