using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BusinessAnalytics.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Produces("application/json")]
public class ProjectsController(IProjectService service, ITaskService taskService) : ControllerBase
{
    /// <summary>List projects. Sort by: code, name, department, status, startDate, endDate, budget, createdAt.</summary>
    [HttpGet]
    public Task<PagedResult<ProjectDto>> GetAll([FromQuery] ProjectQuery query, CancellationToken ct) => service.GetPagedAsync(query, ct);

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ProjectDto> GetById(long id, CancellationToken ct) => service.GetByIdAsync(id, ct);

    [HttpGet("{id:long}/tasks")]
    public Task<PagedResult<TaskDto>> GetTasks(long id, [FromQuery] TaskQuery query, CancellationToken ct) =>
        taskService.GetPagedAsync(query with { ProjectId = id }, ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var project = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    /// <summary>Create a project together with its tasks in one transaction (all-or-nothing).</summary>
    [HttpPost("with-tasks")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectWithTasksDto>> CreateWithTasks(CreateProjectWithTasksRequest request, CancellationToken ct)
    {
        var result = await service.CreateWithTasksAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Project.Id }, result);
    }

    [HttpPut("{id:long}")]
    public Task<ProjectDto> Update(long id, UpdateProjectRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    /// <summary>Delete a project. Its tasks are removed by ON DELETE CASCADE.</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
