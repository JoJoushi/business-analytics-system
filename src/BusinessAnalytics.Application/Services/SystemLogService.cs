using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Interfaces.Persistence;
using BusinessAnalytics.Domain.Entities;
using FluentValidation;

namespace BusinessAnalytics.Application.Services;

public interface ISystemLogService
{
    Task<PagedResult<SystemLog>> GetPagedAsync(SystemLogQuery query, CancellationToken ct = default);
}

public class SystemLogService(IUnitOfWork uow, IValidator<PagedQuery> queryValidator) : ISystemLogService
{
    public async Task<PagedResult<SystemLog>> GetPagedAsync(SystemLogQuery query, CancellationToken ct = default)
    {
        await queryValidator.ValidateAndThrowAsync(query, ct);
        return await uow.SystemLogs.GetPagedAsync(query, ct);
    }
}
