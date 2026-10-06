#!/usr/bin/env bash
# Shared helpers for the ops scripts. Source, don't execute.

set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Load .env if present (same values docker compose uses)
if [[ -f "${PROJECT_ROOT}/.env" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "${PROJECT_ROOT}/.env"
  set +a
fi

POSTGRES_DB="${POSTGRES_DB:-business_analytics}"
POSTGRES_USER="${POSTGRES_USER:-analytics}"
API_PORT="${API_PORT:-8080}"
DB_CONTAINER="${DB_CONTAINER:-ba-postgres}"
API_CONTAINER="${API_CONTAINER:-ba-api}"
LOG_DIR="${LOG_DIR:-${PROJECT_ROOT}/logs}"

if [[ -t 1 ]]; then
  C_RED=$'\e[31m'; C_GREEN=$'\e[32m'; C_YELLOW=$'\e[33m'; C_BLUE=$'\e[34m'; C_RESET=$'\e[0m'
else
  C_RED=""; C_GREEN=""; C_YELLOW=""; C_BLUE=""; C_RESET=""
fi

ts()        { date '+%Y-%m-%d %H:%M:%S'; }
log_info()  { echo "${C_BLUE}[$(ts)] [INFO]${C_RESET}  $*"; }
log_ok()    { echo "${C_GREEN}[$(ts)] [OK]${C_RESET}    $*"; }
log_warn()  { echo "${C_YELLOW}[$(ts)] [WARN]${C_RESET}  $*" >&2; }
log_error() { echo "${C_RED}[$(ts)] [ERROR]${C_RESET} $*" >&2; }
die()       { log_error "$*"; exit 1; }

require_cmd() {
  local cmd
  for cmd in "$@"; do
    command -v "$cmd" >/dev/null 2>&1 || die "Required command not found: $cmd"
  done
}

# True when this script is itself running inside a container
in_container() { [[ -f /.dockerenv ]] || grep -qsE '(docker|containerd|kubepods)' /proc/1/cgroup; }

container_running() {
  [[ "$(docker inspect -f '{{.State.Running}}' "$1" 2>/dev/null || echo false)" == "true" ]]
}

# Portable file size in bytes (GNU + BSD/macOS)
file_size() { wc -c < "$1" | tr -d ' '; }

human_size() {
  awk -v b="$1" 'BEGIN { split("B KB MB GB TB", u); i = 1; while (b >= 1024 && i < 5) { b /= 1024; i++ } printf "%.1f%s", b, u[i] }'
}

# Parse sizes like 10M, 512K, 1G, 2048 → bytes
to_bytes() {
  local v
  v="$(echo "$1" | tr '[:lower:]' '[:upper:]')"
  case "$v" in
    *G) echo $(( ${v%G} * 1024 * 1024 * 1024 )) ;;
    *M) echo $(( ${v%M} * 1024 * 1024 )) ;;
    *K) echo $(( ${v%K} * 1024 )) ;;
    *)  echo "$v" ;;
  esac
}
