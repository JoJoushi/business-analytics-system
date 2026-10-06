using BusinessAnalytics.Application.Common.Exceptions;
using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Interfaces.Persistence;
using BusinessAnalytics.Domain.Entities;
using FluentValidation;

namespace BusinessAnalytics.Application.Services;

public interface ITaskService
{
    Task<PagedResult<TaskDto>> GetPagedAsync(TaskQuery query, CancellationToken ct = default);
    Task<TaskDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskDto> UpdateAsync(long id, UpdateTaskRequest request, CancellationToken ct = default);
    Task<TaskDto> UpdateStatusAsync(long id, UpdateTaskStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public class TaskService(
    IUnitOfWork uow,
    IValidator<CreateTaskRequest> createValidator,
    IValidator<UpdateTaskRequest> updateValidator,
    IValidator<UpdateTaskStatusRequest> statusValidator,
    IValidator<PagedQuery> queryValidator,
    TimeProvider clock) : ITaskService
{
    private static readonly string[] ClosedProjectStatuses = [ProjectStatus.Completed, ProjectStatus.Cancelled];

    public async Task<PagedResult<TaskDto>> GetPagedAsync(TaskQuery query, CancellationToken ct = default)
    {
        await queryValidator.ValidateAndThrowAsync(query, ct);
        var page = await uow.Tasks.GetPagedAsync(query, ct);
        return page.Map(TaskDto.From);
    }

    public async Task<TaskDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var task = await uow.Tasks.GetByIdAsync(id, ct) ?? throw new NotFoundException("Task", id);
        return TaskDto.From(task);
    }

    public async Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        return await uow.ExecuteInTransactionAsync(async token =>
        {
            await EnsureProjectOpenAsync(request.ProjectId, token);
            if (request.AssigneeId is { } assigneeId)
                await EnsureAssignableAsync(uow, assigneeId, token);

            var task = new TaskItem
            {
                ProjectId = request.ProjectId,
                AssigneeId = request.AssigneeId,
                Title = request.Title.Trim(),
                Description = request.Description,
                Status = request.Status,
                Priority = request.Priority,
                ProgressPercent = request.ProgressPercent,
                DueDate = request.DueDate
            };
            task.Normalize(clock.GetUtcNow().UtcDateTime);
            task.Id = await uow.Tasks.AddAsync(task, token);

            await uow.SystemLogs.AddAsync(SystemLog.Create("create", "task", task.Id,
                $"Task '{task.Title}' created in project {task.ProjectId}",
                AuditMetadata.Of(new { task.ProjectId, task.AssigneeId, task.Status })), token);

            return TaskDto.From((await uow.Tasks.GetByIdAsync(task.Id, token))!);
        }, ct);
    }

    public async Task<TaskDto> UpdateAsync(long id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);

        return await uow.ExecuteInTransactionAsync(async token =>
        {
            var task = await uow.Tasks.GetByIdAsync(id, token) ?? throw new NotFoundException("Task", id);

            if (task.ProjectId != request.ProjectId)
                await EnsureProjectOpenAsync(request.ProjectId, token);
            if (request.AssigneeId is { } assigneeId && assigneeId != task.AssigneeId)
                await EnsureAssignableAsync(uow, assigneeId, token);

            task.ProjectId = request.ProjectId;
            task.AssigneeId = request.AssigneeId;
            task.Title = request.Title.Trim();
            task.Description = request.Description;
            task.Status = request.Status;
            task.Priority = request.Priority;
            task.ProgressPercent = request.ProgressPercent;
            task.DueDate = request.DueDate;
            task.Normalize(clock.GetUtcNow().UtcDateTime);
            await uow.Tasks.UpdateAsync(task, token);

            await uow.SystemLogs.AddAsync(SystemLog.Create("update", "task", id,
                $"Task '{task.Title}' updated", AuditMetadata.Of(request)), token);

            return TaskDto.From((await uow.Tasks.GetByIdAsync(id, token))!);
        }, ct);
    }

    public async Task<TaskDto> UpdateStatusAsync(long id, UpdateTaskStatusRequest request, CancellationToken ct = default)
    {
        await statusValidator.ValidateAndThrowAsync(request, ct);

        return await uow.ExecuteInTransactionAsync(async token =>
        {
            var task = await uow.Tasks.GetByIdAsync(id, token) ?? throw new NotFoundException("Task", id);
            var previous = task.Status;

            task.Status = request.Status;
            if (request.ProgressPercent is { } progress) task.ProgressPercent = progress;
            task.Normalize(clock.GetUtcNow().UtcDateTime);
            await uow.Tasks.UpdateAsync(task, token);

            await uow.SystemLogs.AddAsync(SystemLog.Create("status_change", "task", id,
                $"Task '{task.Title}' {previous} -> {task.Status} ({task.ProgressPercent}%)",
                AuditMetadata.Of(new { From = previous, To = task.Status, task.ProgressPercent })), token);

            return TaskDto.From((await uow.Tasks.GetByIdAsync(id, token))!);
        }, ct);
    }

    public Task DeleteAsync(long id, CancellationToken ct = default) =>
        uow.ExecuteInTransactionAsync(async token =>
        {
            var task = await uow.Tasks.GetByIdAsync(id, token) ?? throw new NotFoundException("Task", id);
            await uow.Tasks.DeleteAsync(id, token);
            await uow.SystemLogs.AddAsync(SystemLog.Create("delete", "task", id,
                $"Task '{task.Title}' deleted", level: LogLevels.Warning), token);
        }, ct);

    internal static async Task EnsureAssignableAsync(IUnitOfWork uow, long userId, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(userId, ct) ?? throw new NotFoundException(nameof(User), userId);
        if (!user.IsActive)
            throw new BusinessRuleException($"User '{user.Username}' is inactive and cannot be assigned tasks.");
    }

    private async Task EnsureProjectOpenAsync(long projectId, CancellationToken ct)
    {
        var project = await uow.Projects.GetByIdAsync(projectId, ct) ?? throw new NotFoundException(nameof(Project), projectId);
        if (ClosedProjectStatuses.Contains(project.Status))
            throw new BusinessRuleException($"Project '{project.Code}' is {project.Status}; tasks cannot be added to it.");
    }
}
