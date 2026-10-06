using System.Data;

namespace BusinessAnalytics.Application.Interfaces.Persistence;

/// <summary>
/// Groups all repositories over a single database connection so that several
/// writes can be committed or rolled back together.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IUserRepository Users { get; }
    IProjectRepository Projects { get; }
    ITaskRepository Tasks { get; }
    ISystemLogRepository SystemLogs { get; }
    IReportRepository Reports { get; }

    bool HasActiveTransaction { get; }

    Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="action"/> inside a transaction: commits on success, rolls back on any exception.
    /// If a transaction is already active the action joins it instead of starting a new one.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
