using System.Text.Json;
using BusinessAnalytics.Application.Common.Exceptions;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Export;
using BusinessAnalytics.Application.Interfaces.Persistence;

namespace BusinessAnalytics.Application.Services;

public interface IReportService
{
    Task<IReadOnlyList<DepartmentProgressReport>> GetDepartmentProgressAsync(ReportFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectSummaryReport>> GetProjectSummaryAsync(ReportFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<UserWorkloadReport>> GetUserWorkloadAsync(ReportFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyTrendReport>> GetMonthlyTrendAsync(ReportFilter filter, CancellationToken ct = default);
    Task<ExportFile> ExportAsync(string reportName, string format, ReportFilter filter, CancellationToken ct = default);
}

public class ReportService(IUnitOfWork uow, TimeProvider clock) : IReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public Task<IReadOnlyList<DepartmentProgressReport>> GetDepartmentProgressAsync(ReportFilter filter, CancellationToken ct = default)
        => uow.Reports.GetDepartmentProgressAsync(Validate(filter), ct);

    public Task<IReadOnlyList<ProjectSummaryReport>> GetProjectSummaryAsync(ReportFilter filter, CancellationToken ct = default)
        => uow.Reports.GetProjectSummaryAsync(Validate(filter), ct);

    public Task<IReadOnlyList<UserWorkloadReport>> GetUserWorkloadAsync(ReportFilter filter, CancellationToken ct = default)
        => uow.Reports.GetUserWorkloadAsync(Validate(filter), ct);

    public Task<IReadOnlyList<MonthlyTrendReport>> GetMonthlyTrendAsync(ReportFilter filter, CancellationToken ct = default)
        => uow.Reports.GetMonthlyTrendAsync(Validate(filter), ct);

    public async Task<ExportFile> ExportAsync(string reportName, string format, ReportFilter filter, CancellationToken ct = default)
    {
        format = format.ToLowerInvariant();
        if (format is not ("csv" or "json"))
            throw new BusinessRuleException("Format must be 'csv' or 'json'.");

        var fileName = $"{reportName}_{clock.GetUtcNow():yyyyMMdd_HHmmss}.{format}";

        return reportName switch
        {
            ReportNames.DepartmentProgress => Build(await GetDepartmentProgressAsync(filter, ct), format, fileName),
            ReportNames.ProjectSummary => Build(await GetProjectSummaryAsync(filter, ct), format, fileName),
            ReportNames.UserWorkload => Build(await GetUserWorkloadAsync(filter, ct), format, fileName),
            ReportNames.MonthlyTrend => Build(await GetMonthlyTrendAsync(filter, ct), format, fileName),
            _ => throw new NotFoundException("Report", reportName)
        };
    }

    private static ExportFile Build<T>(IReadOnlyList<T> rows, string format, string fileName) => format == "csv"
        ? new ExportFile(CsvWriter.Write(rows), "text/csv; charset=utf-8", fileName)
        : new ExportFile(JsonSerializer.SerializeToUtf8Bytes(rows, JsonOptions), "application/json", fileName);

    private static ReportFilter Validate(ReportFilter filter)
    {
        if (filter.From is { } from && filter.To is { } to && from > to)
            throw new BusinessRuleException("'from' must be on or before 'to'.");
        return filter;
    }
}
