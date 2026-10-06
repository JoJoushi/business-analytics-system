using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BusinessAnalytics.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Produces("application/json")]
public class TasksController(ITaskService service) : ControllerBase
{
    /// <summary>List tasks. Sort by: title, status, priority, progress, dueDate, createdAt.</summary>
    [HttpGet]
    public Task<PagedResult<TaskDto>> GetAll([FromQuery] TaskQuery query, CancellationToken ct) => service.GetPagedAsync(query, ct);

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<TaskDto> GetById(long id, CancellationToken ct) => service.GetByIdAsync(id, ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var task = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:long}")]
    public Task<TaskDto> Update(long id, UpdateTaskRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    /// <summary>Change only status / progress. Setting status=done forces progress to 100%.</summary>
    [HttpPatch("{id:long}/status")]
    public Task<TaskDto> UpdateStatus(long id, UpdateTaskStatusRequest request, CancellationToken ct) => service.UpdateStatusAsync(id, request, ct);

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
