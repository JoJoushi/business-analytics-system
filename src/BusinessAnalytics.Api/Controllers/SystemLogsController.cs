using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Services;
using BusinessAnalytics.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace BusinessAnalytics.Api.Controllers;

[ApiController]
[Route("api/system-logs")]
[Produces("application/json")]
public class SystemLogsController(ISystemLogService service) : ControllerBase
{
    /// <summary>Audit trail written by every create/update/delete (in the same transaction).</summary>
    [HttpGet]
    public Task<PagedResult<SystemLog>> GetAll([FromQuery] SystemLogQuery query, CancellationToken ct) => service.GetPagedAsync(query, ct);
}
