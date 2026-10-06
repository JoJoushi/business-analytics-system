using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Application.Interfaces.Persistence;

namespace BusinessAnalytics.Infrastructure.Persistence.Repositories;

/// <summary>Read-only analytics queries (CTEs, FILTER aggregates, window functions).</summary>
public class ReportRepository(DbSession session) : RepositoryBase(session), IReportRepository
{
    // Shared predicate: restricts tasks to the requested creation-date window.
    private const string TaskDateFilter = """
        (@from::date IS NULL OR t.created_at >= @from::date)
        AND (@to::date IS NULL OR t.created_at < @to::date + 1)
        """;

    private const string ProjectFilter = """
        (@department::text IS NULL OR p.department = @department)
        AND (@projectStatus::text IS NULL OR p.status = @projectStatus)
        """;

    public async Task<IReadOnlyList<DepartmentProgressReport>> GetDepartmentProgressAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var sql = $"""
            WITH task_stats AS (
                SELECT t.project_id,
                       COUNT(*)                                                         AS total_tasks,
                       COUNT(*) FILTER (WHERE t.status = 'done')                        AS completed_tasks,
                       COUNT(*) FILTER (WHERE t.status <> 'done' AND t.due_date < CURRENT_DATE) AS overdue_tasks,
                       SUM(t.progress_percent)                                          AS progress_sum
                FROM tasks t
                WHERE {TaskDateFilter}
                GROUP BY t.project_id
            )
            SELECT p.department,
                   COUNT(*)::int                                                   AS total_projects,
                   COUNT(*) FILTER (WHERE p.status = 'active')::int                AS active_projects,
                   COALESCE(SUM(ts.total_tasks), 0)::int                           AS total_tasks,
                   COALESCE(SUM(ts.completed_tasks), 0)::int                       AS completed_tasks,
                   COALESCE(SUM(ts.overdue_tasks), 0)::int                         AS overdue_tasks,
                   COALESCE(ROUND(100.0 * SUM(ts.completed_tasks) / NULLIF(SUM(ts.total_tasks), 0), 2), 0) AS completion_rate,
                   COALESCE(ROUND(SUM(ts.progress_sum)::numeric / NULLIF(SUM(ts.total_tasks), 0), 2), 0)   AS avg_progress,
                   SUM(p.budget)                                                   AS total_budget
            FROM projects p
            LEFT JOIN task_stats ts ON ts.project_id = p.id
            WHERE {ProjectFilter}
            GROUP BY p.department
            ORDER BY avg_progress DESC, p.department
            """;
        return (await QueryAsync<DepartmentProgressReport>(sql, Params(filter), ct)).ToList();
    }

    public async Task<IReadOnlyList<ProjectSummaryReport>> GetProjectSummaryAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var sql = $"""
            WITH stats AS (
                SELECT p.id                                                         AS project_id,
                       p.code, p.name, p.department, p.status, p.end_date,
                       u.full_name                                                  AS owner_name,
                       COUNT(t.id)                                                  AS total_tasks,
                       COUNT(t.id) FILTER (WHERE t.status = 'todo')                 AS todo_tasks,
                       COUNT(t.id) FILTER (WHERE t.status = 'in_progress')          AS in_progress_tasks,
                       COUNT(t.id) FILTER (WHERE t.status = 'review')               AS review_tasks,
                       COUNT(t.id) FILTER (WHERE t.status = 'done')                 AS done_tasks,
                       COUNT(t.id) FILTER (WHERE t.status <> 'done' AND t.due_date < CURRENT_DATE) AS overdue_tasks,
                       COALESCE(ROUND(AVG(t.progress_percent), 2), 0)               AS avg_progress
                FROM projects p
                LEFT JOIN users u ON u.id = p.owner_id
                LEFT JOIN tasks t ON t.project_id = p.id AND {TaskDateFilter}
                WHERE {ProjectFilter}
                GROUP BY p.id, u.full_name
            )
            SELECT project_id, code, name, department, status, owner_name,
                   total_tasks::int, todo_tasks::int, in_progress_tasks::int, review_tasks::int,
                   done_tasks::int, overdue_tasks::int, avg_progress,
                   COALESCE(ROUND(100.0 * done_tasks / NULLIF(total_tasks, 0), 2), 0) AS completion_rate,
                   (end_date - CURRENT_DATE)                                          AS days_remaining,
                   RANK() OVER (PARTITION BY department ORDER BY avg_progress DESC)::int AS rank_in_department
            FROM stats
            ORDER BY department, rank_in_department, code
            """;
        return (await QueryAsync<ProjectSummaryReport>(sql, Params(filter), ct)).ToList();
    }

    public async Task<IReadOnlyList<UserWorkloadReport>> GetUserWorkloadAsync(ReportFilter filter, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT u.id                                                                   AS user_id,
                   u.full_name,
                   u.department,
                   COUNT(t.id)::int                                                       AS assigned_tasks,
                   COUNT(t.id) FILTER (WHERE t.status <> 'done')::int                     AS open_tasks,
                   COUNT(t.id) FILTER (WHERE t.status = 'done')::int                      AS completed_tasks,
                   COUNT(t.id) FILTER (WHERE t.status <> 'done' AND t.due_date < CURRENT_DATE)::int AS overdue_tasks,
                   COUNT(t.id) FILTER (WHERE t.status <> 'done' AND t.priority <= 2)::int AS high_priority_open,
                   COALESCE(ROUND(AVG(t.progress_percent), 2), 0)                         AS avg_progress
            FROM users u
            LEFT JOIN tasks t    ON t.assignee_id = u.id AND {TaskDateFilter}
            LEFT JOIN projects p ON p.id = t.project_id
            WHERE u.is_active
              AND (@department::text IS NULL OR u.department = @department)
              AND (@projectStatus::text IS NULL OR t.id IS NULL OR p.status = @projectStatus)
            GROUP BY u.id
            ORDER BY open_tasks DESC, overdue_tasks DESC, u.full_name
            """;
        return (await QueryAsync<UserWorkloadReport>(sql, Params(filter), ct)).ToList();
    }

    public async Task<IReadOnlyList<MonthlyTrendReport>> GetMonthlyTrendAsync(ReportFilter filter, CancellationToken ct = default)
    {
        const string sql = """
            WITH bounds AS (
                SELECT date_trunc('month', COALESCE(@from::date, CURRENT_DATE - INTERVAL '11 months'))::date AS first_month,
                       date_trunc('month', COALESCE(@to::date, CURRENT_DATE))::date                         AS last_month
            ),
            months AS (
                SELECT generate_series(first_month, last_month, INTERVAL '1 month')::date AS month FROM bounds
            ),
            scoped AS (
                SELECT t.created_at, t.completed_at
                FROM tasks t
                JOIN projects p ON p.id = t.project_id
                WHERE (@department::text IS NULL OR p.department = @department)
                  AND (@projectStatus::text IS NULL OR p.status = @projectStatus)
            ),
            created AS (
                SELECT date_trunc('month', created_at)::date AS month, COUNT(*) AS n FROM scoped GROUP BY 1
            ),
            completed AS (
                SELECT date_trunc('month', completed_at)::date AS month, COUNT(*) AS n
                FROM scoped WHERE completed_at IS NOT NULL GROUP BY 1
            )
            SELECT m.month,
                   COALESCE(c.n, 0)::int                                          AS created_tasks,
                   COALESCE(d.n, 0)::int                                          AS completed_tasks,
                   SUM(COALESCE(d.n, 0)) OVER (ORDER BY m.month)::int             AS cumulative_completed
            FROM months m
            LEFT JOIN created   c ON c.month = m.month
            LEFT JOIN completed d ON d.month = m.month
            ORDER BY m.month
            """;
        return (await QueryAsync<MonthlyTrendReport>(sql, Params(filter), ct)).ToList();
    }

    private static object Params(ReportFilter f) => new
    {
        department = f.Department,
        projectStatus = f.ProjectStatus,
        from = f.From,
        to = f.To
    };
}
