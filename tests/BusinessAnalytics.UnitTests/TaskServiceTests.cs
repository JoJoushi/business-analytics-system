using BusinessAnalytics.Application.Common.Exceptions;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Services;
using BusinessAnalytics.Application.Validators;
using BusinessAnalytics.Domain.Entities;
using FluentValidation;
using Moq;

namespace BusinessAnalytics.UnitTests;

public class TaskServiceTests
{
    private readonly FakeUnitOfWork _uow = new();
    private readonly TaskService _sut;

    public TaskServiceTests()
    {
        _sut = new TaskService(_uow,
            new CreateTaskRequestValidator(), new UpdateTaskRequestValidator(),
            new UpdateTaskStatusRequestValidator(), new PagedQueryValidator(), TimeProvider.System);
    }

    private void GivenProject(long id, string status) =>
        _uow.ProjectsMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project { Id = id, Code = "P-" + id, Status = status });

    private void GivenUser(long id, bool active) =>
        _uow.UsersMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = id, Username = "u" + id, IsActive = active });

    [Fact]
    public async Task Create_commits_and_writes_audit_log_in_same_transaction()
    {
        GivenProject(1, ProjectStatus.Active);
        GivenUser(7, active: true);
        _uow.TasksMock.Setup(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>())).ReturnsAsync(42);
        _uow.TasksMock.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskItem { Id = 42, ProjectId = 1, Title = "Write docs" });

        var result = await _sut.CreateAsync(new CreateTaskRequest(1, "Write docs", null, 7));

        Assert.Equal(42, result.Id);
        Assert.Equal((1, 1, 0), (_uow.Begins, _uow.Commits, _uow.Rollbacks));
        _uow.LogsMock.Verify(r => r.AddAsync(It.Is<SystemLog>(l => l.Action == "create" && l.EntityId == 42), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Cancelled)]
    public async Task Create_on_closed_project_rolls_back(string status)
    {
        GivenProject(1, status);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(new CreateTaskRequest(1, "x", null, null)));

        Assert.Equal((1, 0, 1), (_uow.Begins, _uow.Commits, _uow.Rollbacks));
        _uow.TasksMock.Verify(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_with_inactive_assignee_rolls_back()
    {
        GivenProject(1, ProjectStatus.Active);
        GivenUser(9, active: false);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(new CreateTaskRequest(1, "x", null, 9)));
        Assert.Equal(1, _uow.Rollbacks);
        Assert.Equal(0, _uow.Commits);
    }

    [Fact]
    public async Task Invalid_request_fails_validation_before_opening_a_transaction()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync(new CreateTaskRequest(0, "", null, null, Priority: 9)));

        Assert.Equal(0, _uow.Begins);
    }

    [Fact]
    public async Task UpdateStatus_to_done_forces_full_progress_and_sets_completed_at()
    {
        var task = new TaskItem { Id = 5, ProjectId = 1, Title = "t", Status = TaskItemStatus.InProgress, ProgressPercent = 40 };
        _uow.TasksMock.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(task);

        await _sut.UpdateStatusAsync(5, new UpdateTaskStatusRequest(TaskItemStatus.Done, null));

        _uow.TasksMock.Verify(r => r.UpdateAsync(It.Is<TaskItem>(t =>
            t.Status == TaskItemStatus.Done && t.ProgressPercent == 100 && t.CompletedAt != null), It.IsAny<CancellationToken>()));
        Assert.Equal(1, _uow.Commits);
    }

    [Fact]
    public async Task Delete_missing_task_throws_not_found_and_rolls_back()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteAsync(999));
        Assert.Equal(1, _uow.Rollbacks);
    }
}
