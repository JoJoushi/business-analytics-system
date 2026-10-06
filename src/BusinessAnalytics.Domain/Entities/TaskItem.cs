namespace BusinessAnalytics.Domain.Entities;

/// <summary>Named TaskItem to avoid clashing with System.Threading.Tasks.Task.</summary>
public class TaskItem : BaseEntity
{
    public long ProjectId { get; set; }
    public long? AssigneeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = TaskItemStatus.Todo;
    public short Priority { get; set; } = 3;
    public short ProgressPercent { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Keeps Status, ProgressPercent and CompletedAt consistent with each other.</summary>
    public void Normalize(DateTime utcNow)
    {
        if (Status == TaskItemStatus.Done)
        {
            ProgressPercent = 100;
            CompletedAt ??= utcNow;
        }
        else
        {
            CompletedAt = null;
            if (ProgressPercent >= 100) ProgressPercent = 99;
        }
    }
}

public static class TaskItemStatus
{
    public const string Todo = "todo";
    public const string InProgress = "in_progress";
    public const string Review = "review";
    public const string Done = "done";

    public static readonly string[] All = [Todo, InProgress, Review, Done];
}
