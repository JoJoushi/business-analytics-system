using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Interfaces.Persistence;
using BusinessAnalytics.Domain.Entities;

namespace BusinessAnalytics.Infrastructure.Persistence.Repositories;

public class UserRepository(DbSession session) : RepositoryBase(session), IUserRepository
{
    private const string Columns = "id, username, email, full_name, department, role, is_active, created_at, updated_at";

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["username"] = "username",
        ["fullName"] = "full_name",
        ["department"] = "department",
        ["createdAt"] = "created_at"
    };

    public Task<User?> GetByIdAsync(long id, CancellationToken ct = default) =>
        QuerySingleOrDefaultAsync<User>($"SELECT {Columns} FROM users WHERE id = @id", new { id }, ct);

    public Task<bool> ExistsAsync(long id, CancellationToken ct = default) =>
        ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM users WHERE id = @id)", new { id }, ct);

    public Task<bool> IsUsernameOrEmailTakenAsync(string username, string email, long? excludeId = null, CancellationToken ct = default) =>
        ExecuteScalarAsync<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM users
                WHERE (lower(username) = lower(@username) OR lower(email) = lower(@email))
                  AND (@excludeId::bigint IS NULL OR id <> @excludeId))
            """, new { username = username.Trim(), email = email.Trim(), excludeId }, ct);

    public async Task<PagedResult<User>> GetPagedAsync(UserQuery query, CancellationToken ct = default)
    {
        const string where = """
            WHERE (@search::text IS NULL OR username ILIKE @search OR full_name ILIKE @search OR email ILIKE @search)
              AND (@department::text IS NULL OR department = @department)
              AND (@isActive::boolean IS NULL OR is_active = @isActive)
            """;
        var param = new
        {
            search = LikePattern(query.Search),
            department = query.Department,
            isActive = query.IsActive,
            limit = query.Limit,
            offset = query.Offset
        };
        var order = OrderBy(SortColumns, query.SortBy, query.Descending, "id");

        var (total, items) = await QueryPageAsync<User>(
            $"SELECT COUNT(*) FROM users {where}",
            $"SELECT {Columns} FROM users {where} ORDER BY {order} LIMIT @limit OFFSET @offset",
            param, ct);
        return new PagedResult<User>(items, query.Page, query.Limit, total);
    }

    public Task<long> AddAsync(User entity, CancellationToken ct = default) =>
        ExecuteScalarAsync<long>("""
            INSERT INTO users (username, email, full_name, department, role, is_active)
            VALUES (@Username, @Email, @FullName, @Department, @Role, @IsActive)
            RETURNING id
            """, entity, ct);

    public async Task<bool> UpdateAsync(User entity, CancellationToken ct = default) =>
        await ExecuteAsync("""
            UPDATE users
            SET username = @Username, email = @Email, full_name = @FullName,
                department = @Department, role = @Role, is_active = @IsActive
            WHERE id = @Id
            """, entity, ct) > 0;

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default) =>
        await ExecuteAsync("DELETE FROM users WHERE id = @id", new { id }, ct) > 0;
}
