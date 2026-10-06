#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Resource usage of the running containers (CPU / memory / net / block I/O)
# plus disk usage of the data volume, backups and logs.
# Warns when CPU or memory crosses a threshold.
#
# Usage:
#   scripts/monitor.sh                      # one snapshot
#   scripts/monitor.sh -w 5                 # refresh every 5 s
#   scripts/monitor.sh -c 70 -m 80          # thresholds: CPU 70 %, MEM 80 %
#   scripts/monitor.sh -o logs/monitor.csv  # also append a CSV row per container
# ---------------------------------------------------------------------------
source "$(dirname "$0")/lib/common.sh"
require_cmd docker awk

WATCH=0
CPU_LIMIT=80
MEM_LIMIT=85
CSV_OUT=""

while getopts ":w:c:m:o:h" opt; do
  case "$opt" in
    w) WATCH="$OPTARG" ;;
    c) CPU_LIMIT="$OPTARG" ;;
    m) MEM_LIMIT="$OPTARG" ;;
    o) CSV_OUT="$OPTARG" ;;
    h) sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) die "Unknown option -$OPTARG" ;;
  esac
done

CONTAINERS=("$DB_CONTAINER" "$API_CONTAINER")

snapshot() {
  local running=()
  for c in "${CONTAINERS[@]}"; do container_running "$c" && running+=("$c"); done
  if (( ${#running[@]} == 0 )); then
    log_warn "No stack containers are running"
    return
  fi

  echo "=== Container resources  $(ts) ==="
  # One sample of docker stats (no streaming)
  local stats
  stats=$(docker stats --no-stream --format '{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}|{{.MemPerc}}|{{.NetIO}}|{{.BlockIO}}|{{.PIDs}}' "${running[@]}")

  printf '%-14s %8s %22s %8s %22s %22s %5s\n' NAME CPU% MEM_USAGE/LIMIT MEM% NET_I/O BLOCK_I/O PIDS
  while IFS='|' read -r name cpu mem memp net blk pids; do
    printf '%-14s %8s %22s %8s %22s %22s %5s\n' "$name" "$cpu" "$mem" "$memp" "$net" "$blk" "$pids"

    local cpu_n=${cpu%\%} mem_n=${memp%\%}
    if awk -v a="$cpu_n" -v b="$CPU_LIMIT" 'BEGIN { exit !(a > b) }'; then
      log_warn "${name}: CPU ${cpu} exceeds ${CPU_LIMIT}%"
    fi
    if awk -v a="$mem_n" -v b="$MEM_LIMIT" 'BEGIN { exit !(a > b) }'; then
      log_warn "${name}: memory ${memp} exceeds ${MEM_LIMIT}%"
    fi

    if [[ -n "$CSV_OUT" ]]; then
      [[ -f "$CSV_OUT" ]] || echo "timestamp,container,cpu_pct,mem_usage,mem_pct,net_io,block_io,pids" > "$CSV_OUT"
      echo "$(ts),${name},${cpu_n},\"${mem}\",${mem_n},\"${net}\",\"${blk}\",${pids}" >> "$CSV_OUT"
    fi
  done <<<"$stats"

  echo
  echo "=== Disk usage ==="
  if container_running "$DB_CONTAINER"; then
    printf '%-28s %s\n' "postgres data (volume)" \
      "$(docker exec "$DB_CONTAINER" du -sh /var/lib/postgresql/data 2>/dev/null | cut -f1)"
    printf '%-28s %s\n' "database size" \
      "$(docker exec "$DB_CONTAINER" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc 'SELECT pg_size_pretty(pg_database_size(current_database()))' 2>/dev/null)"
  fi
  printf '%-28s %s\n' "backups/" "$(du -sh "${PROJECT_ROOT}/backups" 2>/dev/null | cut -f1)"
  printf '%-28s %s\n' "logs/" "$(du -sh "${LOG_DIR}" 2>/dev/null | cut -f1)"
  for c in "${running[@]}"; do
    local lp
    lp=$(docker inspect -f '{{.LogPath}}' "$c" 2>/dev/null || true)
    [[ -n "$lp" && -r "$lp" ]] && printf '%-28s %s\n' "docker log (${c})" "$(human_size "$(file_size "$lp")")"
  done
  printf '%-28s %s\n' "host filesystem (/)" "$(df -h / | awk 'NR==2 { print $3 " used / " $2 " (" $5 ")" }')"
}

if (( WATCH > 0 )); then
  trap 'echo; log_info "Stopped"; exit 0' INT TERM
  while true; do
    [[ -t 1 ]] && clear
    snapshot
    sleep "$WATCH"
  done
else
  snapshot
fi
