using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Domain.Entities;

namespace BusinessAnalytics.Application.Interfaces.Persistence;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<long> AddAsync(T entity, CancellationToken ct = default);
    Task<bool> UpdateAsync(T entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<bool> ExistsAsync(long id, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<PagedResult<User>> GetPagedAsync(UserQuery query, CancellationToken ct = default);
    Task<bool> IsUsernameOrEmailTakenAsync(string username, string email, long? excludeId = null, CancellationToken ct = default);
}

public interface IProjectRepository : IRepository<Project>
{
    Task<PagedResult<Project>> GetPagedAsync(ProjectQuery query, CancellationToken ct = default);
    Task<bool> IsCodeTakenAsync(string code, long? excludeId = null, CancellationToken ct = default);
}

public interface ITaskRepository : IRepository<TaskItem>
{
    Task<PagedResult<TaskItem>> GetPagedAsync(TaskQuery query, CancellationToken ct = default);
}

public interface ISystemLogRepository
{
    Task<long> AddAsync(SystemLog log, CancellationToken ct = default);
    Task<PagedResult<SystemLog>> GetPagedAsync(SystemLogQuery query, CancellationToken ct = default);
}

public interface IReportRepository
{
    Task<IReadOnlyList<DepartmentProgressReport>> GetDepartmentProgressAsync(ReportFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectSummaryReport>> GetProjectSummaryAsync(ReportFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<UserWorkloadReport>> GetUserWorkloadAsync(ReportFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyTrendReport>> GetMonthlyTrendAsync(ReportFilter filter, CancellationToken ct = default);
}
