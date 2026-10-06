using System.Data;
using BusinessAnalytics.Application.Interfaces.Persistence;
using Microsoft.Extensions.Logging;

namespace BusinessAnalytics.Infrastructure.Persistence;

public sealed class UnitOfWork(
    DbSession session,
    IUserRepository users,
    IProjectRepository projects,
    ITaskRepository tasks,
    ISystemLogRepository systemLogs,
    IReportRepository reports,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public IUserRepository Users { get; } = users;
    public IProjectRepository Projects { get; } = projects;
    public ITaskRepository Tasks { get; } = tasks;
    public ISystemLogRepository SystemLogs { get; } = systemLogs;
    public IReportRepository Reports { get; } = reports;

    public bool HasActiveTransaction => session.Transaction is not null;

    public async Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        if (HasActiveTransaction)
            throw new InvalidOperationException("A transaction is already in progress.");

        var connection = await session.GetConnectionAsync(ct);
        session.Transaction = await connection.BeginTransactionAsync(isolationLevel, ct);
        logger.LogDebug("Transaction started ({IsolationLevel})", isolationLevel);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        var tx = session.Transaction ?? throw new InvalidOperationException("No active transaction to commit.");
        try
        {
            await tx.CommitAsync(ct);
            logger.LogDebug("Transaction committed");
        }
        finally
        {
            await tx.DisposeAsync();
            session.Transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        var tx = session.Transaction;
        if (tx is null) return;
        try
        {
            await tx.RollbackAsync(ct);
            logger.LogWarning("Transaction rolled back");
        }
        finally
        {
            await tx.DisposeAsync();
            session.Transaction = null;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        // Nested call: join the outer transaction, the outermost caller decides commit/rollback.
        if (HasActiveTransaction)
            return await action(ct);

        await BeginTransactionAsync(ct: ct);
        try
        {
            var result = await action(ct);
            await CommitAsync(ct);
            return result;
        }
        catch
        {
            // Roll back even if the request was cancelled.
            await RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default) =>
        ExecuteInTransactionAsync<object?>(async token =>
        {
            await action(token);
            return null;
        }, ct);

    public ValueTask DisposeAsync() => session.DisposeAsync();
}
