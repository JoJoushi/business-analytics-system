namespace BusinessAnalytics.Application.DTOs;

public record ReportFilter
{
    public string? Department { get; init; }
    public string? ProjectStatus { get; init; }
    /// <summary>Inclusive lower bound on task creation date.</summary>
    public DateOnly? From { get; init; }
    /// <summary>Inclusive upper bound on task creation date.</summary>
    public DateOnly? To { get; init; }
}

// Report rows use settable properties (not positional records) so Dapper can map snake_case columns onto them.

public record DepartmentProgressReport
{
    public string Department { get; init; } = string.Empty;
    public int TotalProjects { get; init; }
    public int ActiveProjects { get; init; }
    public int TotalTasks { get; init; }
    public int CompletedTasks { get; init; }
    public int OverdueTasks { get; init; }
    /// <summary>Percentage of tasks with status 'done'.</summary>
    public decimal CompletionRate { get; init; }
    /// <summary>Average of progress_percent over all tasks.</summary>
    public decimal AvgProgress { get; init; }
    public decimal TotalBudget { get; init; }
}

public record ProjectSummaryReport
{
    public long ProjectId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? OwnerName { get; init; }
    public int TotalTasks { get; init; }
    public int TodoTasks { get; init; }
    public int InProgressTasks { get; init; }
    public int ReviewTasks { get; init; }
    public int DoneTasks { get; init; }
    public int OverdueTasks { get; init; }
    public decimal AvgProgress { get; init; }
    public decimal CompletionRate { get; init; }
    public int? DaysRemaining { get; init; }
    /// <summary>RANK() of the project's avg progress within its department.</summary>
    public int RankInDepartment { get; init; }
}

public record UserWorkloadReport
{
    public long UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public int AssignedTasks { get; init; }
    public int OpenTasks { get; init; }
    public int CompletedTasks { get; init; }
    public int OverdueTasks { get; init; }
    /// <summary>Open tasks with priority 1 or 2.</summary>
    public int HighPriorityOpen { get; init; }
    public decimal AvgProgress { get; init; }
}

public record MonthlyTrendReport
{
    public DateOnly Month { get; init; }
    public int CreatedTasks { get; init; }
    public int CompletedTasks { get; init; }
    public int CumulativeCompleted { get; init; }
}

public static class ReportNames
{
    public const string DepartmentProgress = "department-progress";
    public const string ProjectSummary = "project-summary";
    public const string UserWorkload = "user-workload";
    public const string MonthlyTrend = "monthly-trend";

    public static readonly string[] All = [DepartmentProgress, ProjectSummary, UserWorkload, MonthlyTrend];
}

public record ExportFile(byte[] Content, string ContentType, string FileName);
