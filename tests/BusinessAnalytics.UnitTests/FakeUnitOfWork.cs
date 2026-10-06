using System.Data;
using BusinessAnalytics.Application.Interfaces.Persistence;
using Moq;

namespace BusinessAnalytics.UnitTests;

/// <summary>In-memory IUnitOfWork that records transaction calls; repositories are Moq mocks.</summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public Mock<IUserRepository> UsersMock { get; } = new();
    public Mock<IProjectRepository> ProjectsMock { get; } = new();
    public Mock<ITaskRepository> TasksMock { get; } = new();
    public Mock<ISystemLogRepository> LogsMock { get; } = new();
    public Mock<IReportRepository> ReportsMock { get; } = new();

    public int Begins { get; private set; }
    public int Commits { get; private set; }
    public int Rollbacks { get; private set; }

    public IUserRepository Users => UsersMock.Object;
    public IProjectRepository Projects => ProjectsMock.Object;
    public ITaskRepository Tasks => TasksMock.Object;
    public ISystemLogRepository SystemLogs => LogsMock.Object;
    public IReportRepository Reports => ReportsMock.Object;

    public bool HasActiveTransaction { get; private set; }

    public Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        Begins++;
        HasActiveTransaction = true;
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken ct = default)
    {
        Commits++;
        HasActiveTransaction = false;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        Rollbacks++;
        HasActiveTransaction = false;
        return Task.CompletedTask;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        if (HasActiveTransaction) return await action(ct);
        await BeginTransactionAsync(ct: ct);
        try
        {
            var result = await action(ct);
            await CommitAsync(ct);
            return result;
        }
        catch
        {
            await RollbackAsync(ct);
            throw;
        }
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default) =>
        ExecuteInTransactionAsync<object?>(async t => { await action(t); return null; }, ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
