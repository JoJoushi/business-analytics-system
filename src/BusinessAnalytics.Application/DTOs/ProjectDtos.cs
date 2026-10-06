using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Domain.Entities;

namespace BusinessAnalytics.Application.DTOs;

public record ProjectDto(long Id, string Code, string Name, string? Description, string Department, long? OwnerId, string Status,
    DateOnly? StartDate, DateOnly? EndDate, decimal Budget, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static ProjectDto From(Project p) => new(p.Id, p.Code, p.Name, p.Description, p.Department, p.OwnerId, p.Status,
        p.StartDate, p.EndDate, p.Budget, p.CreatedAt, p.UpdatedAt);
}

public record CreateProjectRequest(string Code, string Name, string? Description, string Department, long? OwnerId,
    string Status = ProjectStatus.Planning, DateOnly? StartDate = null, DateOnly? EndDate = null, decimal Budget = 0);

public record UpdateProjectRequest(string Code, string Name, string? Description, string Department, long? OwnerId,
    string Status, DateOnly? StartDate, DateOnly? EndDate, decimal Budget);

/// <summary>Creates a project and its initial tasks atomically in one Unit of Work.</summary>
public record CreateProjectWithTasksRequest(CreateProjectRequest Project, IReadOnlyList<CreateProjectTaskItem> Tasks);

public record CreateProjectTaskItem(string Title, string? Description, long? AssigneeId, short Priority = 3, DateOnly? DueDate = null);

public record ProjectWithTasksDto(ProjectDto Project, IReadOnlyList<TaskDto> Tasks);

public record ProjectQuery : PagedQuery
{
    public string? Department { get; init; }
    public string? Status { get; init; }
    public long? OwnerId { get; init; }
}
