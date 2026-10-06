#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Log rotation (logrotate-style "copytruncate") so log files never fill the disk.
#
#   app.log  >  MAX_SIZE  →  app.log.1.gz, app.log.2.gz ... app.log.KEEP.gz
#   app.log is truncated in place, so a process holding it open keeps writing.
#
# Optional:
#   -c   collect: append new `docker logs` output of each container to logs/<container>.log first
#   -g N simulate: generate ~N MB of fake API log lines into logs/simulated-api.log first
#
# Usage:
#   scripts/log-rotate.sh                  # rotate *.log in ./logs larger than 10M, keep 5
#   scripts/log-rotate.sh -s 1M -k 3       # custom size / generations
#   scripts/log-rotate.sh -c               # collect container logs, then rotate
#   scripts/log-rotate.sh -g 25 -s 5M      # demo: create 25 MB log and rotate it
#
# Cron (every 15 minutes):
#   */15 * * * * /path/to/repo/scripts/log-rotate.sh -c >> /path/to/repo/logs/rotate.out 2>&1
# ---------------------------------------------------------------------------
source "$(dirname "$0")/lib/common.sh"
require_cmd gzip

MAX_SIZE="10M"
KEEP=5
COLLECT=false
SIMULATE_MB=0

while getopts ":s:k:d:cg:h" opt; do
  case "$opt" in
    s) MAX_SIZE="$OPTARG" ;;
    k) KEEP="$OPTARG" ;;
    d) LOG_DIR="$OPTARG" ;;
    c) COLLECT=true ;;
    g) SIMULATE_MB="$OPTARG" ;;
    h) sed -n '2,22p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) die "Unknown option -$OPTARG" ;;
  esac
done

MAX_BYTES=$(to_bytes "$MAX_SIZE")
[[ "$MAX_BYTES" =~ ^[0-9]+$ ]] || die "Invalid size: $MAX_SIZE"
[[ "$KEEP" =~ ^[0-9]+$ && "$KEEP" -ge 1 ]] || die "Keep must be >= 1"
mkdir -p "$LOG_DIR"

# Only one rotation at a time (cron overlap protection)
LOCK_DIR="${LOG_DIR}/.rotate.lock"
mkdir "$LOCK_DIR" 2>/dev/null || die "Another rotation is running (remove ${LOCK_DIR} if stale)"
trap 'rmdir "$LOCK_DIR" 2>/dev/null || true' EXIT

# ----- 1. collect container logs (incremental) -----
collect_container_logs() {
  require_cmd docker
  local c since_file since now
  for c in "$DB_CONTAINER" "$API_CONTAINER"; do
    docker inspect "$c" >/dev/null 2>&1 || { log_warn "skip ${c}: not found"; continue; }
    since_file="${LOG_DIR}/.since_${c}"
    since=$(cat "$since_file" 2>/dev/null || echo "0")
    now=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
    docker logs --timestamps --since "$since" --until "$now" "$c" >> "${LOG_DIR}/${c}.log" 2>&1
    echo "$now" > "$since_file"
    log_info "collected ${c} logs → ${LOG_DIR}/${c}.log"
  done
}

# ----- 2. simulate a chatty application -----
simulate_logs() {
  local target="${LOG_DIR}/simulated-api.log" mb="$1"
  log_info "Generating ~${mb} MB of fake log lines into ${target} ..."
  # awk is far faster than a bash loop for this
  awk -v target_bytes=$(( mb * 1024 * 1024 )) 'BEGIN {
      srand(42)
      split("GET POST PUT DELETE", m, " ")
      split("/api/users /api/projects /api/tasks /api/reports/department-progress /health", p, " ")
      split("200 200 200 201 204 400 404 409 500", s, " ")
      while (bytes < target_bytes) {
        line = sprintf("%s [INFO] %s %s -> %s in %dms traceId=%08x%08x",
                       strftime("%Y-%m-%dT%H:%M:%S"), m[int(rand()*4)+1], p[int(rand()*5)+1],
                       s[int(rand()*9)+1], int(rand()*300), int(rand()*2^31), int(rand()*2^31))
        print line
        bytes += length(line) + 1
      }
    }' >> "$target" 2>/dev/null || {
      # BSD awk (macOS) has no strftime → fall back to a fixed timestamp
      awk -v target_bytes=$(( mb * 1024 * 1024 )) -v now="$(date '+%Y-%m-%dT%H:%M:%S')" 'BEGIN {
        srand(42)
        while (bytes < target_bytes) {
          line = sprintf("%s [INFO] GET /api/tasks -> 200 in %dms traceId=%08x", now, int(rand()*300), int(rand()*2^31))
          print line; bytes += length(line) + 1
        }
      }' >> "$target"
    }
  log_info "$(basename "$target") is now $(human_size "$(file_size "$target")")"
}

# ----- 3. rotate -----
rotate_file() {
  local f="$1" size i
  size=$(file_size "$f")
  if (( size <= MAX_BYTES )); then
    log_info "$(basename "$f"): $(human_size "$size") ≤ ${MAX_SIZE}, skip"
    return
  fi

  # drop the oldest, shift the rest: .4.gz → .5.gz ...
  rm -f "${f}.${KEEP}.gz"
  for (( i = KEEP - 1; i >= 1; i-- )); do
    [[ -f "${f}.${i}.gz" ]] && mv "${f}.${i}.gz" "${f}.$((i + 1)).gz"
  done

  # copytruncate: the writer keeps its file descriptor
  cp -p "$f" "${f}.1"
  : > "$f"
  gzip -9 "${f}.1"

  log_ok "$(basename "$f"): rotated $(human_size "$size") → $(basename "$f").1.gz ($(human_size "$(file_size "${f}.1.gz")"))"
}

$COLLECT && collect_container_logs
(( SIMULATE_MB > 0 )) && simulate_logs "$SIMULATE_MB"

shopt -s nullglob
files=("$LOG_DIR"/*.log)
if (( ${#files[@]} == 0 )); then
  log_info "No *.log files in ${LOG_DIR}"
  exit 0
fi

log_info "Rotating logs in ${LOG_DIR} (max ${MAX_SIZE}, keep ${KEEP})"
for f in "${files[@]}"; do rotate_file "$f"; done

echo
log_info "Current log files:"
ls -lh "$LOG_DIR" | grep -E '\.log' || true
log_info "Total: $(du -sh "$LOG_DIR" | cut -f1)"
