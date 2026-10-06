using BusinessAnalytics.Application.Common.Exceptions;
using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Interfaces.Persistence;
using BusinessAnalytics.Domain.Entities;
using FluentValidation;

namespace BusinessAnalytics.Application.Services;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> GetPagedAsync(ProjectQuery query, CancellationToken ct = default);
    Task<ProjectDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<ProjectWithTasksDto> CreateWithTasksAsync(CreateProjectWithTasksRequest request, CancellationToken ct = default);
    Task<ProjectDto> UpdateAsync(long id, UpdateProjectRequest request, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public class ProjectService(
    IUnitOfWork uow,
    IValidator<CreateProjectRequest> createValidator,
    IValidator<CreateProjectWithTasksRequest> createWithTasksValidator,
    IValidator<UpdateProjectRequest> updateValidator,
    IValidator<PagedQuery> queryValidator,
    TimeProvider clock) : IProjectService
{
    public async Task<PagedResult<ProjectDto>> GetPagedAsync(ProjectQuery query, CancellationToken ct = default)
    {
        await queryValidator.ValidateAndThrowAsync(query, ct);
        var page = await uow.Projects.GetPagedAsync(query, ct);
        return page.Map(ProjectDto.From);
    }

    public async Task<ProjectDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var project = await uow.Projects.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Project), id);
        return ProjectDto.From(project);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        return await uow.ExecuteInTransactionAsync(token => InsertProjectAsync(request, token), ct);
    }

    /// <summary>
    /// Inserts the project, every task and the audit log in a single transaction.
    /// If any task fails (e.g. unknown assignee) nothing is persisted.
    /// </summary>
    public async Task<ProjectWithTasksDto> CreateWithTasksAsync(CreateProjectWithTasksRequest request, CancellationToken ct = default)
    {
        await createWithTasksValidator.ValidateAndThrowAsync(request, ct);

        return await uow.ExecuteInTransactionAsync(async token =>
        {
            var project = await InsertProjectAsync(request.Project, token);
            var now = clock.GetUtcNow().UtcDateTime;
            var tasks = new List<TaskDto>(request.Tasks.Count);

            foreach (var item in request.Tasks)
            {
                if (item.AssigneeId is { } assigneeId)
                    await TaskService.EnsureAssignableAsync(uow, assigneeId, token);

                var task = new TaskItem
                {
                    ProjectId = project.Id,
                    AssigneeId = item.AssigneeId,
                    Title = item.Title.Trim(),
                    Description = item.Description,
                    Priority = item.Priority,
                    DueDate = item.DueDate,
                    Status = TaskItemStatus.Todo
                };
                task.Normalize(now);
                task.Id = await uow.Tasks.AddAsync(task, token);
                tasks.Add(TaskDto.From((await uow.Tasks.GetByIdAsync(task.Id, token))!));
            }

            await uow.SystemLogs.AddAsync(SystemLog.Create("create_with_tasks", "project", project.Id,
                $"Project '{project.Code}' created with {tasks.Count} task(s)",
                AuditMetadata.Of(new { project.Code, TaskIds = tasks.Select(t => t.Id) })), token);

            return new ProjectWithTasksDto(project, tasks);
        }, ct);
    }

    public async Task<ProjectDto> UpdateAsync(long id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);

        return await uow.ExecuteInTransactionAsync(async token =>
        {
            var project = await uow.Projects.GetByIdAsync(id, token) ?? throw new NotFoundException(nameof(Project), id);

            if (await uow.Projects.IsCodeTakenAsync(request.Code, id, token))
                throw new ConflictException($"Project code '{request.Code}' is already in use.");
            if (request.OwnerId is { } ownerId && !await uow.Users.ExistsAsync(ownerId, token))
                throw new NotFoundException(nameof(User), ownerId);

            var previousStatus = project.Status;
            project.Code = request.Code.Trim();
            project.Name = request.Name.Trim();
            project.Description = request.Description;
            project.Department = request.Department.Trim();
            project.OwnerId = request.OwnerId;
            project.Status = request.Status;
            project.StartDate = request.StartDate;
            project.EndDate = request.EndDate;
            project.Budget = request.Budget;
            await uow.Projects.UpdateAsync(project, token);

            await uow.SystemLogs.AddAsync(SystemLog.Create("update", "project", id,
                previousStatus == project.Status
                    ? $"Project '{project.Code}' updated"
                    : $"Project '{project.Code}' status {previousStatus} -> {project.Status}",
                AuditMetadata.Of(request)), token);

            return ProjectDto.From((await uow.Projects.GetByIdAsync(id, token))!);
        }, ct);
    }

    public Task DeleteAsync(long id, CancellationToken ct = default) =>
        uow.ExecuteInTransactionAsync(async token =>
        {
            var project = await uow.Projects.GetByIdAsync(id, token) ?? throw new NotFoundException(nameof(Project), id);
            // tasks are removed by ON DELETE CASCADE
            await uow.Projects.DeleteAsync(id, token);
            await uow.SystemLogs.AddAsync(SystemLog.Create("delete", "project", id,
                $"Project '{project.Code}' deleted (tasks cascaded)", level: LogLevels.Warning), token);
        }, ct);

    private async Task<ProjectDto> InsertProjectAsync(CreateProjectRequest request, CancellationToken ct)
    {
        if (await uow.Projects.IsCodeTakenAsync(request.Code, null, ct))
            throw new ConflictException($"Project code '{request.Code}' is already in use.");
        if (request.OwnerId is { } ownerId && !await uow.Users.ExistsAsync(ownerId, ct))
            throw new NotFoundException(nameof(User), ownerId);

        var project = new Project
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description,
            Department = request.Department.Trim(),
            OwnerId = request.OwnerId,
            Status = request.Status,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Budget = request.Budget
        };
        project.Id = await uow.Projects.AddAsync(project, ct);

        await uow.SystemLogs.AddAsync(SystemLog.Create("create", "project", project.Id,
            $"Project '{project.Code}' created", AuditMetadata.Of(new { project.Code, project.Department })), ct);

        return ProjectDto.From((await uow.Projects.GetByIdAsync(project.Id, ct))!);
    }
}
