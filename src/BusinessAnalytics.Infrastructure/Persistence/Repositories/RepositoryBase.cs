using Dapper;

namespace BusinessAnalytics.Infrastructure.Persistence.Repositories;

public abstract class RepositoryBase(DbSession session)
{
    protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param, CancellationToken ct)
    {
        var conn = await session.GetConnectionAsync(ct);
        return await conn.QueryAsync<T>(Command(sql, param, ct));
    }

    protected async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param, CancellationToken ct)
    {
        var conn = await session.GetConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<T>(Command(sql, param, ct));
    }

    protected async Task<T> ExecuteScalarAsync<T>(string sql, object? param, CancellationToken ct)
    {
        var conn = await session.GetConnectionAsync(ct);
        return (await conn.ExecuteScalarAsync<T>(Command(sql, param, ct)))!;
    }

    protected async Task<int> ExecuteAsync(string sql, object? param, CancellationToken ct)
    {
        var conn = await session.GetConnectionAsync(ct);
        return await conn.ExecuteAsync(Command(sql, param, ct));
    }

    /// <summary>Runs a COUNT query and a page query in one round trip.</summary>
    protected async Task<(long Total, List<T> Items)> QueryPageAsync<T>(string countSql, string pageSql, object param, CancellationToken ct)
    {
        var conn = await session.GetConnectionAsync(ct);
        using var grid = await conn.QueryMultipleAsync(Command($"{countSql};\n{pageSql}", param, ct));
        var total = await grid.ReadSingleAsync<long>();
        var items = (await grid.ReadAsync<T>()).ToList();
        return (total, items);
    }

    private CommandDefinition Command(string sql, object? param, CancellationToken ct) =>
        new(sql, param, session.Transaction, cancellationToken: ct);

    /// <summary>Escapes LIKE wildcards so user input is matched literally.</summary>
    protected static string? LikePattern(string? search) => string.IsNullOrWhiteSpace(search)
        ? null
        : "%" + search.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";

    /// <summary>Resolves a client sort key against a whitelist; never interpolate raw input into SQL.</summary>
    protected static string OrderBy(IReadOnlyDictionary<string, string> allowed, string? sortBy, bool desc, string fallback)
    {
        var column = sortBy is not null && allowed.TryGetValue(sortBy, out var c) ? c : fallback;
        return $"{column} {(desc ? "DESC" : "ASC")}, id {(desc ? "DESC" : "ASC")}";
    }
}
