using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Interfaces.Persistence;
using BusinessAnalytics.Domain.Entities;

namespace BusinessAnalytics.Infrastructure.Persistence.Repositories;

public class SystemLogRepository(DbSession session) : RepositoryBase(session), ISystemLogRepository
{
    private const string Columns = "id, level, action, entity, entity_id, user_id, message, metadata::text AS metadata, created_at";

    public Task<long> AddAsync(SystemLog log, CancellationToken ct = default) =>
        ExecuteScalarAsync<long>("""
            INSERT INTO system_logs (level, action, entity, entity_id, user_id, message, metadata)
            VALUES (@Level, @Action, @Entity, @EntityId, @UserId, @Message, @Metadata::jsonb)
            RETURNING id
            """, log, ct);

    public async Task<PagedResult<SystemLog>> GetPagedAsync(SystemLogQuery query, CancellationToken ct = default)
    {
        const string where = """
            WHERE (@search::text IS NULL OR message ILIKE @search)
              AND (@level::text IS NULL OR level = @level)
              AND (@entity::text IS NULL OR entity = @entity)
              AND (@entityId::bigint IS NULL OR entity_id = @entityId)
              AND (@from::timestamptz IS NULL OR created_at >= @from)
              AND (@to::timestamptz IS NULL OR created_at < @to)
            """;
        var param = new
        {
            search = LikePattern(query.Search),
            level = query.Level,
            entity = query.Entity,
            entityId = query.EntityId,
            from = query.From?.ToUniversalTime(),
            to = query.To?.ToUniversalTime(),
            limit = query.Limit,
            offset = query.Offset
        };
        // Logs are always newest first unless asc is requested explicitly.
        var dir = string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        var (total, items) = await QueryPageAsync<SystemLog>(
            $"SELECT COUNT(*) FROM system_logs {where}",
            $"SELECT {Columns} FROM system_logs {where} ORDER BY created_at {dir}, id {dir} LIMIT @limit OFFSET @offset",
            param, ct);
        return new PagedResult<SystemLog>(items, query.Page, query.Limit, total);
    }
}
