using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BusinessAnalytics.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public class ReportsController(IReportService service) : ControllerBase
{
    /// <summary>Task completion % and average progress grouped by department.</summary>
    [HttpGet(ReportNames.DepartmentProgress)]
    public Task<IReadOnlyList<DepartmentProgressReport>> DepartmentProgress([FromQuery] ReportFilter filter, CancellationToken ct) =>
        service.GetDepartmentProgressAsync(filter, ct);

    /// <summary>Per-project task breakdown by status, ranked within its department.</summary>
    [HttpGet(ReportNames.ProjectSummary)]
    public Task<IReadOnlyList<ProjectSummaryReport>> ProjectSummary([FromQuery] ReportFilter filter, CancellationToken ct) =>
        service.GetProjectSummaryAsync(filter, ct);

    /// <summary>Open / overdue / high-priority workload per active user.</summary>
    [HttpGet(ReportNames.UserWorkload)]
    public Task<IReadOnlyList<UserWorkloadReport>> UserWorkload([FromQuery] ReportFilter filter, CancellationToken ct) =>
        service.GetUserWorkloadAsync(filter, ct);

    /// <summary>Tasks created / completed per month with a running total (defaults to the last 12 months).</summary>
    [HttpGet(ReportNames.MonthlyTrend)]
    public Task<IReadOnlyList<MonthlyTrendReport>> MonthlyTrend([FromQuery] ReportFilter filter, CancellationToken ct) =>
        service.GetMonthlyTrendAsync(filter, ct);

    /// <summary>Download a report as a CSV or JSON file.</summary>
    /// <param name="reportName">department-progress | project-summary | user-workload | monthly-trend</param>
    /// <param name="format">csv | json</param>
    [HttpGet("{reportName}/export")]
    [Produces("text/csv", "application/json")]
    public async Task<IActionResult> Export(string reportName, [FromQuery] ReportFilter filter, [FromQuery] string format = "csv", CancellationToken ct = default)
    {
        var file = await service.ExportAsync(reportName, format, filter, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
