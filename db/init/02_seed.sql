-- =====================================================================
-- Demo data so the reports have something to show right after `up`.
-- Deterministic (setseed) so every fresh volume gets the same numbers.
-- =====================================================================
\set ON_ERROR_STOP on

SELECT setseed(0.42);

INSERT INTO users (username, email, full_name, department, role) VALUES
    ('somchai',  'somchai@example.com',  'Somchai Jaidee',      'IT',         'manager'),
    ('suda',     'suda@example.com',     'Suda Rakdee',         'IT',         'member'),
    ('anan',     'anan@example.com',     'Anan Wongsa',         'IT',         'member'),
    ('malee',    'malee@example.com',    'Malee Srisuk',        'Finance',    'manager'),
    ('preecha',  'preecha@example.com',  'Preecha Thongdee',    'Finance',    'member'),
    ('kanya',    'kanya@example.com',    'Kanya Boonmee',       'Marketing',  'manager'),
    ('wichai',   'wichai@example.com',   'Wichai Chaiyo',       'Marketing',  'member'),
    ('nittaya',  'nittaya@example.com',  'Nittaya Kaewta',      'HR',         'manager'),
    ('thana',    'thana@example.com',    'Thana Saengthong',    'Operations', 'manager'),
    ('ploy',     'ploy@example.com',     'Ploy Charoen',        'Operations', 'member'),
    ('admin',    'admin@example.com',    'System Administrator','IT',         'admin');

INSERT INTO projects (code, name, description, department, owner_id, status, start_date, end_date, budget)
SELECT v.code, v.name, v.description, v.department, u.id, v.status,
       CURRENT_DATE + v.start_offset, CURRENT_DATE + v.end_offset, v.budget
FROM (VALUES
    ('IT-ERP',     'ERP Migration',              'Move legacy ERP to cloud',          'IT',         'somchai', 'active',    -300,  60, 2500000.00),
    ('IT-SEC',     'Security Hardening',         'ISO 27001 readiness',               'IT',         'somchai', 'active',    -120,  90,  800000.00),
    ('FIN-CLOSE',  'Fast Month-end Close',       'Automate reconciliation',           'Finance',    'malee',   'active',    -200,  30,  450000.00),
    ('FIN-AUDIT',  'Annual Audit 2026',          NULL,                                'Finance',    'malee',   'completed', -340, -20,  300000.00),
    ('MKT-BRAND',  'Brand Refresh',              'New CI and website',                'Marketing',  'kanya',   'active',    -150,  45,  950000.00),
    ('MKT-CRM',    'CRM Rollout',                'Customer 360',                      'Marketing',  'kanya',   'on_hold',   -90,  120, 1200000.00),
    ('HR-ONBOARD', 'Digital Onboarding',         'Paperless onboarding flow',         'HR',         'nittaya', 'planning',   10,  180,  250000.00),
    ('OPS-WMS',    'Warehouse Management System','Barcode + WMS integration',         'Operations', 'thana',   'active',    -250,  15, 1800000.00)
) AS v(code, name, description, department, owner, status, start_offset, end_offset, budget)
JOIN users u ON u.username = v.owner;

-- ~15 tasks per project, spread over the last 12 months, assigned within the department
WITH generated AS (
    SELECT p.id AS project_id,
           p.department,
           p.status AS project_status,
           g.n,
           NOW() - (random() * 330 || ' days')::interval AS created_at,
           random() AS r_status,
           (1 + floor(random() * 5))::smallint AS priority,
           (floor(random() * 120) - 40)::int AS due_offset
    FROM projects p
    CROSS JOIN generate_series(1, 15) AS g(n)
),
shaped AS (
    SELECT g.*,
           CASE
               WHEN g.project_status = 'completed' THEN 'done'
               WHEN g.project_status = 'planning'  THEN 'todo'
               WHEN g.r_status < 0.40 THEN 'done'
               WHEN g.r_status < 0.60 THEN 'review'
               WHEN g.r_status < 0.80 THEN 'in_progress'
               ELSE 'todo'
           END AS status
    FROM generated g
)
INSERT INTO tasks (project_id, assignee_id, title, status, priority, progress_percent, due_date, completed_at, created_at, updated_at)
SELECT s.project_id,
       (SELECT u.id FROM users u
         WHERE u.department = s.department AND u.role <> 'admin'
         ORDER BY md5(u.id::text || s.n::text) LIMIT 1),
       'Task ' || s.n || ' of project ' || s.project_id,
       s.status,
       s.priority,
       CASE s.status
           WHEN 'done'        THEN 100
           WHEN 'review'      THEN 80 + floor(random() * 19)
           WHEN 'in_progress' THEN 20 + floor(random() * 60)
           ELSE 0
       END,
       (s.created_at::date + 30 + s.due_offset),
       CASE WHEN s.status = 'done'
            THEN LEAST(NOW(), s.created_at + (random() * 60 || ' days')::interval)
       END,
       s.created_at,
       s.created_at
FROM shaped s;

INSERT INTO system_logs (level, action, entity, message, metadata)
VALUES ('info', 'seed', 'system', 'Database initialised with demo data',
        jsonb_build_object('users', (SELECT COUNT(*) FROM users),
                           'projects', (SELECT COUNT(*) FROM projects),
                           'tasks', (SELECT COUNT(*) FROM tasks)));
