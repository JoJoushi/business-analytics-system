using BusinessAnalytics.Application.Common.Models;
using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Interfaces.Persistence;
using BusinessAnalytics.Domain.Entities;

namespace BusinessAnalytics.Infrastructure.Persistence.Repositories;

public class TaskRepository(DbSession session) : RepositoryBase(session), ITaskRepository
{
    private const string Columns =
        "id, project_id, assignee_id, title, description, status, priority, progress_percent, due_date, completed_at, created_at, updated_at";

    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "title",
        ["status"] = "status",
        ["priority"] = "priority",
        ["progress"] = "progress_percent",
        ["dueDate"] = "due_date",
        ["createdAt"] = "created_at"
    };

    public Task<TaskItem?> GetByIdAsync(long id, CancellationToken ct = default) =>
        QuerySingleOrDefaultAsync<TaskItem>($"SELECT {Columns} FROM tasks WHERE id = @id", new { id }, ct);

    public Task<bool> ExistsAsync(long id, CancellationToken ct = default) =>
        ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM tasks WHERE id = @id)", new { id }, ct);

    public async Task<PagedResult<TaskItem>> GetPagedAsync(TaskQuery query, CancellationToken ct = default)
    {
        const string where = """
            WHERE (@search::text IS NULL OR title ILIKE @search)
              AND (@projectId::bigint IS NULL OR project_id = @projectId)
              AND (@assigneeId::bigint IS NULL OR assignee_id = @assigneeId)
              AND (@status::text IS NULL OR status = @status)
              AND (@overdue::boolean IS NULL
                   OR (@overdue AND status <> 'done' AND due_date < CURRENT_DATE)
                   OR (NOT @overdue AND NOT (status <> 'done' AND due_date < CURRENT_DATE)))
            """;
        var param = new
        {
            search = LikePattern(query.Search),
            projectId = query.ProjectId,
            assigneeId = query.AssigneeId,
            status = query.Status,
            overdue = query.Overdue,
            limit = query.Limit,
            offset = query.Offset
        };
        var order = OrderBy(SortColumns, query.SortBy, query.Descending, "id");

        var (total, items) = await QueryPageAsync<TaskItem>(
            $"SELECT COUNT(*) FROM tasks {where}",
            $"SELECT {Columns} FROM tasks {where} ORDER BY {order} LIMIT @limit OFFSET @offset",
            param, ct);
        return new PagedResult<TaskItem>(items, query.Page, query.Limit, total);
    }

    public Task<long> AddAsync(TaskItem entity, CancellationToken ct = default) =>
        ExecuteScalarAsync<long>("""
            INSERT INTO tasks (project_id, assignee_id, title, description, status, priority, progress_percent, due_date, completed_at)
            VALUES (@ProjectId, @AssigneeId, @Title, @Description, @Status, @Priority, @ProgressPercent, @DueDate, @CompletedAt)
            RETURNING id
            """, entity, ct);

    public async Task<bool> UpdateAsync(TaskItem entity, CancellationToken ct = default) =>
        await ExecuteAsync("""
            UPDATE tasks
            SET project_id = @ProjectId, assignee_id = @AssigneeId, title = @Title, description = @Description,
                status = @Status, priority = @Priority, progress_percent = @ProgressPercent,
                due_date = @DueDate, completed_at = @CompletedAt
            WHERE id = @Id
            """, entity, ct) > 0;

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default) =>
        await ExecuteAsync("DELETE FROM tasks WHERE id = @id", new { id }, ct) > 0;
}
