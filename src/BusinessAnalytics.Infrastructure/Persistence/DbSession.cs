using System.Data;
using Npgsql;

namespace BusinessAnalytics.Infrastructure.Persistence;

/// <summary>
/// Scoped holder for the request's single connection and (optional) transaction.
/// Shared by the UnitOfWork and every repository so they all take part in the same transaction.
/// </summary>
public sealed class DbSession(NpgsqlDataSource dataSource) : IAsyncDisposable
{
    private NpgsqlConnection? _connection;

    public NpgsqlTransaction? Transaction { get; set; }

    public async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        _connection ??= dataSource.CreateConnection();
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);
        return _connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (Transaction is not null)
        {
            await Transaction.DisposeAsync();
            Transaction = null;
        }
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
