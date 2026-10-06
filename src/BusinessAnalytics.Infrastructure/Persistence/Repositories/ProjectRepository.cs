using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Interfaces.Persistence;
using BusinessAnalytics.Domain.Entities;

namespace BusinessAnalytics.Infrastructure.Persistence.Repositories;

public class ProjectRepository(DbSession session) : RepositoryBase(session), IProjectRepository
{
    private const string Columns =
        "id, code, name, description, department, owner_id, status, start_date, end_date, budget, created_at, updated_at";

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "code",
        ["name"] = "name",
        ["department"] = "department",
        ["status"] = "status",
        ["startDate"] = "start_date",
        ["endDate"] = "end_date",
        ["budget"] = "budget",
        ["createdAt"] = "created_at"
    };

    public Task<Project?> GetByIdAsync(long id, CancellationToken ct = default) =>
        QuerySingleOrDefaultAsync<Project>($"SELECT {Columns} FROM projects WHERE id = @id", new { id }, ct);

    public Task<bool> ExistsAsync(long id, CancellationToken ct = default) =>
        ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM projects WHERE id = @id)", new { id }, ct);

    public Task<bool> IsCodeTakenAsync(string code, long? excludeId = null, CancellationToken ct = default) =>
        ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM projects WHERE code = @code AND (@excludeId::bigint IS NULL OR id <> @excludeId))",
            new { code = code.Trim(), excludeId }, ct);

    public async Task<PagedResult<Project>> GetPagedAsync(ProjectQuery query, CancellationToken ct = default)
    {
        const string where = """
            WHERE (@search::text IS NULL OR name ILIKE @search OR code ILIKE @search)
              AND (@department::text IS NULL OR department = @department)
              AND (@status::text IS NULL OR status = @status)
              AND (@ownerId::bigint IS NULL OR owner_id = @ownerId)
            """;
        var param = new
        {
            search = LikePattern(query.Search),
            department = query.Department,
            status = query.Status,
            ownerId = query.OwnerId,
            limit = query.Limit,
            offset = query.Offset
        };
        var order = OrderBy(SortColumns, query.SortBy, query.Descending, "id");

        var (total, items) = await QueryPageAsync<Project>(
            $"SELECT COUNT(*) FROM projects {where}",
            $"SELECT {Columns} FROM projects {where} ORDER BY {order} LIMIT @limit OFFSET @offset",
            param, ct);
        return new PagedResult<Project>(items, query.Page, query.Limit, total);
    }

    public Task<long> AddAsync(Project entity, CancellationToken ct = default) =>
        ExecuteScalarAsync<long>("""
            INSERT INTO projects (code, name, description, department, owner_id, status, start_date, end_date, budget)
            VALUES (@Code, @Name, @Description, @Department, @OwnerId, @Status, @StartDate, @EndDate, @Budget)
            RETURNING id
            """, entity, ct);

    public async Task<bool> UpdateAsync(Project entity, CancellationToken ct = default) =>
        await ExecuteAsync("""
            UPDATE projects
            SET code = @Code, name = @Name, description = @Description, department = @Department,
                owner_id = @OwnerId, status = @Status, start_date = @StartDate, end_date = @EndDate, budget = @Budget
            WHERE id = @Id
            """, entity, ct) > 0;

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default) =>
        await ExecuteAsync("DELETE FROM projects WHERE id = @id", new { id }, ct) > 0;
}
