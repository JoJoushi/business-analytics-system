using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BusinessAnalytics.Api.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController(IUserService service) : ControllerBase
{
    /// <summary>List users. Sort by: username, fullName, department, createdAt.</summary>
    [HttpGet]
    public Task<PagedResult<UserDto>> GetAll([FromQuery] UserQuery query, CancellationToken ct) => service.GetPagedAsync(query, ct);

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<UserDto> GetById(long id, CancellationToken ct) => service.GetByIdAsync(id, ct);

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:long}")]
    public Task<UserDto> Update(long id, UpdateUserRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
