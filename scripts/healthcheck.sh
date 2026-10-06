#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Container health check for the stack.
#   - container state, Docker HEALTHCHECK status, restart count, uptime
#   - PostgreSQL: pg_isready + a real query
#   - API: /health/live and /health (includes DB readiness)
#
# Usage:
#   scripts/healthcheck.sh             # one-shot, exit 0 = healthy, 1 = unhealthy
#   scripts/healthcheck.sh -w 30       # re-check every 30 seconds (Ctrl+C to stop)
#   scripts/healthcheck.sh -q          # quiet: only print failures (cron-friendly)
# ---------------------------------------------------------------------------
source "$(dirname "$0")/lib/common.sh"
require_cmd docker curl

WATCH=0
QUIET=false
API_URL="${API_URL:-http://localhost:${API_PORT}}"

while getopts ":w:qh" opt; do
  case "$opt" in
    w) WATCH="$OPTARG" ;;
    q) QUIET=true ;;
    h) sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) die "Unknown option -$OPTARG" ;;
  esac
done

FAILURES=0
ok()   { $QUIET || log_ok "$*"; }
fail() { log_error "$*"; FAILURES=$((FAILURES + 1)); }

check_container() {
  local name="$1"
  if ! docker inspect "$name" >/dev/null 2>&1; then
    fail "${name}: container does not exist"
    return
  fi

  local state health restarts started
  state=$(docker inspect -f '{{.State.Status}}' "$name")
  health=$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}n/a{{end}}' "$name")
  restarts=$(docker inspect -f '{{.RestartCount}}' "$name")
  started=$(docker inspect -f '{{.State.StartedAt}}' "$name" | cut -d. -f1)

  if [[ "$state" != "running" ]]; then
    fail "${name}: state=${state}"
  elif [[ "$health" == "unhealthy" ]]; then
    local last
    last=$(docker inspect -f '{{range .State.Health.Log}}{{.Output}}{{end}}' "$name" | tail -n 3)
    fail "${name}: Docker health=unhealthy — ${last}"
  else
    ok "${name}: state=${state} health=${health} restarts=${restarts} started=${started}"
  fi

  if (( restarts > 3 )); then
    log_warn "${name}: restarted ${restarts} times — check 'docker logs ${name}'"
  fi
}

check_postgres() {
  container_running "$DB_CONTAINER" || return
  if docker exec "$DB_CONTAINER" pg_isready -q -U "$POSTGRES_USER" -d "$POSTGRES_DB"; then
    local stats
    stats=$(docker exec "$DB_CONTAINER" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc \
      "SELECT pg_size_pretty(pg_database_size(current_database())) || ', ' ||
              (SELECT count(*) FROM pg_stat_activity WHERE datname = current_database()) || ' connection(s)'" 2>/dev/null) \
      || { fail "postgres: accepting connections but query failed"; return; }
    ok "postgres: ready (${stats})"
  else
    fail "postgres: pg_isready failed"
  fi
}

check_http() {
  local label="$1" url="$2" code body
  body=$(curl -sS -m 5 -w '\n%{http_code}' "$url" 2>&1) || { fail "${label}: ${url} unreachable"; return; }
  code=$(tail -n1 <<<"$body")
  body=$(sed '$d' <<<"$body")
  if [[ "$code" == "200" ]]; then
    ok "${label}: ${url} → ${code} ${body:0:120}"
  else
    fail "${label}: ${url} → HTTP ${code} ${body:0:200}"
  fi
}

run_checks() {
  FAILURES=0
  $QUIET || log_info "Health check @ $(hostname)"
  check_container "$DB_CONTAINER"
  check_container "$API_CONTAINER"
  check_postgres
  check_http "api-live"  "${API_URL}/health/live"
  check_http "api-ready" "${API_URL}/health"

  if (( FAILURES == 0 )); then
    $QUIET || log_ok "All checks passed"
  else
    log_error "${FAILURES} check(s) failed"
  fi
}

if (( WATCH > 0 )); then
  trap 'echo; log_info "Stopped"; exit 0' INT TERM
  while true; do run_checks || true; echo; sleep "$WATCH"; done
else
  run_checks
  (( FAILURES == 0 ))
fi
