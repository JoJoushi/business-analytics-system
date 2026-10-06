namespace BusinessAnalytics.Domain.Entities;

public class Project : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Department { get; set; } = string.Empty;
    public long? OwnerId { get; set; }
    public string Status { get; set; } = ProjectStatus.Planning;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal Budget { get; set; }
}

public static class ProjectStatus
{
    public const string Planning = "planning";
    public const string Active = "active";
    public const string OnHold = "on_hold";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = [Planning, Active, OnHold, Completed, Cancelled];
}
