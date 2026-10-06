-- =====================================================================
-- Business Analytics — schema
-- Runs automatically on first `docker compose up` (empty data volume)
-- via /docker-entrypoint-initdb.d.
-- =====================================================================
\set ON_ERROR_STOP on

CREATE EXTENSION IF NOT EXISTS pg_trgm;   -- fast ILIKE '%term%' search

-- ---------------------------------------------------------------------
-- Shared trigger: keep updated_at current on every UPDATE
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION set_updated_at() RETURNS trigger AS $$
BEGIN
    NEW.updated_at := NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- ---------------------------------------------------------------------
-- users
-- ---------------------------------------------------------------------
CREATE TABLE users (
    id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username    VARCHAR(50)  NOT NULL,
    email       VARCHAR(255) NOT NULL,
    full_name   VARCHAR(150) NOT NULL,
    department  VARCHAR(100) NOT NULL,
    role        VARCHAR(20)  NOT NULL DEFAULT 'member'
                CHECK (role IN ('admin', 'manager', 'member')),
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_users_email CHECK (email ~* '^[^@\s]+@[^@\s]+\.[^@\s]+$')
);

-- case-insensitive uniqueness
CREATE UNIQUE INDEX ux_users_username_lower ON users (lower(username));
CREATE UNIQUE INDEX ux_users_email_lower    ON users (lower(email));
CREATE INDEX ix_users_department            ON users (department) WHERE is_active;
CREATE INDEX ix_users_full_name_trgm        ON users USING gin (full_name gin_trgm_ops);

CREATE TRIGGER trg_users_updated_at BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ---------------------------------------------------------------------
-- projects
-- ---------------------------------------------------------------------
CREATE TABLE projects (
    id           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code         VARCHAR(30)   NOT NULL UNIQUE,
    name         VARCHAR(200)  NOT NULL,
    description  TEXT,
    department   VARCHAR(100)  NOT NULL,
    owner_id     BIGINT        REFERENCES users (id) ON DELETE SET NULL,
    status       VARCHAR(20)   NOT NULL DEFAULT 'planning'
                 CHECK (status IN ('planning', 'active', 'on_hold', 'completed', 'cancelled')),
    start_date   DATE,
    end_date     DATE,
    budget       NUMERIC(14,2) NOT NULL DEFAULT 0 CHECK (budget >= 0),
    created_at   TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_projects_dates CHECK (end_date IS NULL OR start_date IS NULL OR end_date >= start_date)
);

CREATE INDEX ix_projects_owner_id          ON projects (owner_id);
CREATE INDEX ix_projects_department_status ON projects (department, status);
CREATE INDEX ix_projects_name_trgm         ON projects USING gin (name gin_trgm_ops);

CREATE TRIGGER trg_projects_updated_at BEFORE UPDATE ON projects
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ---------------------------------------------------------------------
-- tasks
-- ---------------------------------------------------------------------
CREATE TABLE tasks (
    id                BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    project_id        BIGINT       NOT NULL REFERENCES projects (id) ON DELETE CASCADE,
    assignee_id       BIGINT       REFERENCES users (id) ON DELETE SET NULL,
    title             VARCHAR(200) NOT NULL,
    description       TEXT,
    status            VARCHAR(20)  NOT NULL DEFAULT 'todo'
                      CHECK (status IN ('todo', 'in_progress', 'review', 'done')),
    priority          SMALLINT     NOT NULL DEFAULT 3 CHECK (priority BETWEEN 1 AND 5),  -- 1 = highest
    progress_percent  SMALLINT     NOT NULL DEFAULT 0 CHECK (progress_percent BETWEEN 0 AND 100),
    due_date          DATE,
    completed_at      TIMESTAMPTZ,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    -- 'done' <=> completed_at set and 100%
    CONSTRAINT chk_tasks_done_consistency CHECK (
        (status = 'done' AND completed_at IS NOT NULL AND progress_percent = 100)
        OR (status <> 'done' AND completed_at IS NULL)
    )
);

CREATE INDEX ix_tasks_project_status  ON tasks (project_id, status);
CREATE INDEX ix_tasks_assignee_status ON tasks (assignee_id, status) WHERE assignee_id IS NOT NULL;
-- partial index used by every "overdue" filter
CREATE INDEX ix_tasks_open_due_date   ON tasks (due_date) WHERE status <> 'done';
CREATE INDEX ix_tasks_created_at      ON tasks (created_at);
CREATE INDEX ix_tasks_completed_at    ON tasks (completed_at) WHERE completed_at IS NOT NULL;
CREATE INDEX ix_tasks_title_trgm      ON tasks USING gin (title gin_trgm_ops);

CREATE TRIGGER trg_tasks_updated_at BEFORE UPDATE ON tasks
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

-- ---------------------------------------------------------------------
-- system_logs  (append-only audit trail)
-- ---------------------------------------------------------------------
CREATE TABLE system_logs (
    id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    level       VARCHAR(10)  NOT NULL DEFAULT 'info' CHECK (level IN ('info', 'warning', 'error')),
    action      VARCHAR(50)  NOT NULL,
    entity      VARCHAR(50)  NOT NULL,
    entity_id   BIGINT,
    user_id     BIGINT       REFERENCES users (id) ON DELETE SET NULL,
    message     TEXT         NOT NULL,
    metadata    JSONB,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- BRIN is tiny and ideal for an append-only, time-ordered table
CREATE INDEX ix_system_logs_created_at_brin ON system_logs USING brin (created_at);
CREATE INDEX ix_system_logs_entity          ON system_logs (entity, entity_id);
CREATE INDEX ix_system_logs_level           ON system_logs (level) WHERE level <> 'info';
CREATE INDEX ix_system_logs_metadata        ON system_logs USING gin (metadata jsonb_path_ops);

-- ---------------------------------------------------------------------
-- Reporting view: live per-project progress (handy for psql / BI tools)
-- ---------------------------------------------------------------------
CREATE VIEW v_project_progress AS
SELECT p.id,
       p.code,
       p.name,
       p.department,
       p.status,
       COUNT(t.id)                                        AS total_tasks,
       COUNT(t.id) FILTER (WHERE t.status = 'done')       AS done_tasks,
       COALESCE(ROUND(AVG(t.progress_percent), 2), 0)     AS avg_progress
FROM projects p
LEFT JOIN tasks t ON t.project_id = p.id
GROUP BY p.id;
