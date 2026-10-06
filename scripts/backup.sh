#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# PostgreSQL backup: pg_dump → gzip → /backups volume, with retention + checksum.
#
# Works from the host (runs pg_dump inside the db container via docker exec)
# or from inside a container that has pg_dump and PG* env vars set.
#
# Usage:
#   scripts/backup.sh                    # backup with default retention (7 days)
#   scripts/backup.sh -r 14              # keep 14 days
#   scripts/backup.sh -l                 # list existing backups
#
# Cron (daily 02:00):
#   0 2 * * * /path/to/repo/scripts/backup.sh >> /path/to/repo/logs/backup.log 2>&1
# ---------------------------------------------------------------------------
source "$(dirname "$0")/lib/common.sh"

RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-7}"
HOST_BACKUP_DIR="${PROJECT_ROOT}/backups"   # bind-mounted to /backups in the db container
CONTAINER_BACKUP_DIR="/backups"
LIST_ONLY=false

usage() { sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'; exit "${1:-0}"; }

while getopts ":r:lh" opt; do
  case "$opt" in
    r) RETENTION_DAYS="$OPTARG" ;;
    l) LIST_ONLY=true ;;
    h) usage 0 ;;
    *) usage 1 ;;
  esac
done
[[ "$RETENTION_DAYS" =~ ^[0-9]+$ ]] || die "Retention must be a number of days"

list_backups() {
  log_info "Backups in ${HOST_BACKUP_DIR}:"
  ls -lh "${HOST_BACKUP_DIR}"/*.sql.gz 2>/dev/null || echo "  (none)"
}

if $LIST_ONLY; then list_backups; exit 0; fi

STAMP="$(date '+%Y%m%d_%H%M%S')"
FILE="${POSTGRES_DB}_${STAMP}.sql.gz"
START=$(date +%s)

# Clean up a partial file if anything fails half-way
cleanup_partial() { rm -f "${HOST_BACKUP_DIR}/${FILE}.partial" 2>/dev/null || true; }
trap 'log_error "Backup failed (line $LINENO)"; cleanup_partial' ERR

if in_container && command -v pg_dump >/dev/null 2>&1; then
  # ----- inside a container: talk to postgres directly -----
  log_info "Running inside container → direct pg_dump to ${CONTAINER_BACKUP_DIR}"
  mkdir -p "$CONTAINER_BACKUP_DIR"
  pg_dump --no-owner --clean --if-exists -d "${POSTGRES_DB}" -U "${POSTGRES_USER}" \
    | gzip -9 > "${CONTAINER_BACKUP_DIR}/${FILE}.partial"
  mv "${CONTAINER_BACKUP_DIR}/${FILE}.partial" "${CONTAINER_BACKUP_DIR}/${FILE}"
  OUT="${CONTAINER_BACKUP_DIR}/${FILE}"
else
  # ----- on the host: exec into the db container -----
  require_cmd docker
  container_running "$DB_CONTAINER" || die "Container ${DB_CONTAINER} is not running (docker compose up -d)"
  mkdir -p "$HOST_BACKUP_DIR"

  log_info "Dumping database '${POSTGRES_DB}' from ${DB_CONTAINER} ..."
  docker exec "$DB_CONTAINER" sh -c "set -o pipefail 2>/dev/null; \
      pg_dump --no-owner --clean --if-exists -U '${POSTGRES_USER}' -d '${POSTGRES_DB}' \
      | gzip -9 > '${CONTAINER_BACKUP_DIR}/${FILE}.partial' \
      && mv '${CONTAINER_BACKUP_DIR}/${FILE}.partial' '${CONTAINER_BACKUP_DIR}/${FILE}'"
  OUT="${HOST_BACKUP_DIR}/${FILE}"
fi

# ----- verify -----
[[ -s "$OUT" ]] || die "Backup file is empty: $OUT"
gzip -t "$OUT" || die "Backup archive is corrupt: $OUT"

if command -v sha256sum >/dev/null 2>&1; then
  (cd "$(dirname "$OUT")" && sha256sum "$(basename "$OUT")" > "$(basename "$OUT").sha256")
elif command -v shasum >/dev/null 2>&1; then
  (cd "$(dirname "$OUT")" && shasum -a 256 "$(basename "$OUT")" > "$(basename "$OUT").sha256")
fi

SIZE=$(file_size "$OUT")
log_ok "Backup created: ${OUT} ($(human_size "$SIZE"), $(( $(date +%s) - START ))s)"

# ----- retention -----
BACKUP_DIR="$(dirname "$OUT")"
DELETED=$(find "$BACKUP_DIR" -maxdepth 1 -type f \( -name "${POSTGRES_DB}_*.sql.gz" -o -name "${POSTGRES_DB}_*.sql.gz.sha256" \) \
          -mtime +"$RETENTION_DAYS" -print -delete | wc -l | tr -d ' ')
log_info "Retention ${RETENTION_DAYS} day(s): removed ${DELETED} old file(s)"

list_backups
