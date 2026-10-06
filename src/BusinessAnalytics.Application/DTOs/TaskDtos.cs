using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Domain.Entities;

namespace BusinessAnalytics.Application.DTOs;

public record TaskDto(long Id, long ProjectId, long? AssigneeId, string Title, string? Description, string Status, short Priority,
    short ProgressPercent, DateOnly? DueDate, DateTime? CompletedAt, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static TaskDto From(TaskItem t) => new(t.Id, t.ProjectId, t.AssigneeId, t.Title, t.Description, t.Status, t.Priority,
        t.ProgressPercent, t.DueDate, t.CompletedAt, t.CreatedAt, t.UpdatedAt);
}

public record CreateTaskRequest(long ProjectId, string Title, string? Description, long? AssigneeId,
    string Status = TaskItemStatus.Todo, short Priority = 3, short ProgressPercent = 0, DateOnly? DueDate = null);

public record UpdateTaskRequest(long ProjectId, string Title, string? Description, long? AssigneeId,
    string Status, short Priority, short ProgressPercent, DateOnly? DueDate);

public record UpdateTaskStatusRequest(string Status, short? ProgressPercent);

public record TaskQuery : PagedQuery
{
    public long? ProjectId { get; init; }
    public long? AssigneeId { get; init; }
    public string? Status { get; init; }
    public bool? Overdue { get; init; }
}
