#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Restore a backup produced by backup.sh into the running db container.
#
# Usage:
#   scripts/restore.sh                       # restore the newest backup
#   scripts/restore.sh backups/<file>.sql.gz # restore a specific file
#   scripts/restore.sh -y ...                # skip confirmation
# ---------------------------------------------------------------------------
source "$(dirname "$0")/lib/common.sh"
require_cmd docker gzip

ASSUME_YES=false
if [[ "${1:-}" == "-y" ]]; then ASSUME_YES=true; shift; fi

FILE="${1:-$(ls -1t "${PROJECT_ROOT}"/backups/*.sql.gz 2>/dev/null | head -n1 || true)}"
[[ -n "$FILE" && -f "$FILE" ]] || die "No backup file found. Run scripts/backup.sh first."
gzip -t "$FILE" || die "Backup archive is corrupt: $FILE"

if [[ -f "${FILE}.sha256" ]]; then
  (cd "$(dirname "$FILE")" && { sha256sum -c "$(basename "$FILE").sha256" 2>/dev/null || shasum -a 256 -c "$(basename "$FILE").sha256"; } >/dev/null) \
    && log_ok "Checksum verified" || die "Checksum mismatch for $FILE"
fi

container_running "$DB_CONTAINER" || die "Container ${DB_CONTAINER} is not running"

if ! $ASSUME_YES; then
  read -r -p "This will OVERWRITE database '${POSTGRES_DB}' with $(basename "$FILE"). Continue? [y/N] " ans
  [[ "$ans" =~ ^[Yy]$ ]] || { log_warn "Aborted"; exit 1; }
fi

log_info "Restoring $(basename "$FILE") → ${POSTGRES_DB} ..."
gunzip -c "$FILE" | docker exec -i "$DB_CONTAINER" psql -q -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" >/dev/null
log_ok "Restore complete"
