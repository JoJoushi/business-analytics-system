using BusinessAnalytics.Application.Common.Exceptions;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Services;
using BusinessAnalytics.Application.Validators;
using BusinessAnalytics.Domain.Entities;
using Moq;

namespace BusinessAnalytics.UnitTests;

public class ProjectServiceTests
{
    private readonly FakeUnitOfWork _uow = new();
    private readonly ProjectService _sut;

    public ProjectServiceTests()
    {
        _sut = new ProjectService(_uow, new CreateProjectRequestValidator(), new CreateProjectWithTasksRequestValidator(),
            new UpdateProjectRequestValidator(), new PagedQueryValidator(), TimeProvider.System);

        _uow.ProjectsMock.Setup(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>())).ReturnsAsync(10);
        _uow.ProjectsMock.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project { Id = 10, Code = "NEW-1", Status = ProjectStatus.Planning });
    }

    private static CreateProjectRequest NewProject => new("NEW-1", "New project", null, "IT", null);

    [Fact]
    public async Task CreateWithTasks_is_all_or_nothing_when_a_task_fails()
    {
        _uow.UsersMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new User { Id = 1, IsActive = true });
        _uow.UsersMock.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _uow.TasksMock.Setup(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>())).ReturnsAsync(100);
        _uow.TasksMock.Setup(r => r.GetByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(new TaskItem { Id = 100 });

        var request = new CreateProjectWithTasksRequest(NewProject,
        [
            new CreateProjectTaskItem("ok task", null, 1),
            new CreateProjectTaskItem("bad assignee", null, 2)
        ]);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateWithTasksAsync(request));

        // project + first task were written inside the transaction, then everything was rolled back
        _uow.ProjectsMock.Verify(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal((1, 0, 1), (_uow.Begins, _uow.Commits, _uow.Rollbacks));
    }

    [Fact]
    public async Task CreateWithTasks_commits_once_for_project_and_all_tasks()
    {
        long nextId = 100;
        _uow.TasksMock.Setup(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => nextId++);
        _uow.TasksMock.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => new TaskItem { Id = id, ProjectId = 10 });

        var result = await _sut.CreateWithTasksAsync(new CreateProjectWithTasksRequest(NewProject,
            [new("a", null, null), new("b", null, null), new("c", null, null)]));

        Assert.Equal(3, result.Tasks.Count);
        Assert.Equal((1, 1, 0), (_uow.Begins, _uow.Commits, _uow.Rollbacks));
    }

    [Fact]
    public async Task Create_with_duplicate_code_throws_conflict()
    {
        _uow.ProjectsMock.Setup(r => r.IsCodeTakenAsync("NEW-1", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateAsync(NewProject));
        Assert.Equal(1, _uow.Rollbacks);
    }
}
