#!/usr/bin/env bash
# Runs every web performance scenario with JMeter and writes one folder per scenario:
#   <out>/<scenario>/results.jtl   raw samples (with the 217 sub-requests of every page load)
#   <out>/<scenario>/dashboard/    JMeter HTML dashboard (open index.html)
#   <out>/<scenario>/server.csv    CPU % and memory of the server process every 2 s (local runs only)
# Usage: tools/perf/run-all.sh [out=/tmp/perf-results] [host=localhost] [port=8080] [protocol=http]
# Needs: JMETER_HOME (or jmeter on PATH). For a local run start the server first:
#   node tools/perf/serve-static.js 8080
# Against our own API instead (it answers deep links with 200; /shop needs no database):
#   SERVER_PID=<api pid> EXTRA="-Jhome=shop -Jdeeplink_code=200" tools/perf/run-all.sh /tmp/api-results localhost 5216
set -euo pipefail
OUT=${1:-/tmp/perf-results}; HOST=${2:-localhost}; PORT=${3:-8080}; PROTO=${4:-http}
PLAN="$(cd "$(dirname "$0")" && pwd)/super-app-web.jmx"
JM=${JMETER_HOME:+$JMETER_HOME/bin/}jmeter
SERVER_PID=${SERVER_PID:-$(pgrep -f serve-static.js | head -1 || true)}
export JVM_ARGS="${JVM_ARGS:--Xms1g -Xmx3g}"
mkdir -p "$OUT"

monitor() { # $1 = csv file; samples the server process until killed
  echo "epoch,cpu_pct,rss_mb" > "$1"
  while kill -0 "$SERVER_PID" 2>/dev/null; do
    read -r cpu rss < <(ps -o %cpu=,rss= -p "$SERVER_PID")
    echo "$(date +%s),$cpu,$((rss / 1024))" >> "$1"; sleep 2
  done
}

# name | new users | returning users | ramp-up s | duration s | think min ms | think range ms | loops
SCENARIOS=(
  "01-smoke|1|1|1|60|0|0|3"
  "02-baseline|8|2|20|120|2000|3000|-1"
  "03-load-100|80|20|60|300|2000|3000|-1"
  "04-stress-c05|5|0|5|60|0|0|-1"
  "04-stress-c10|10|0|5|60|0|0|-1"
  "04-stress-c25|25|0|10|60|0|0|-1"
  "04-stress-c50|50|0|10|60|0|0|-1"
  "04-stress-c100|100|0|10|60|0|0|-1"
  "05-spike-300|240|60|5|120|2000|3000|-1"
  "06-soak-50|40|10|30|600|2000|3000|-1"
)
for s in "${SCENARIOS[@]}"; do
  IFS='|' read -r name users ret ramp dur tmin trange loops <<< "$s"
  [[ -n "${ONLY:-}" && "$name" != ${ONLY} ]] && continue
  dir="$OUT/$name"; rm -rf "$dir"; mkdir -p "$dir"
  echo "=== $name: $users new + $ret returning users, ramp ${ramp}s, ${dur}s, think ${tmin}+${trange}ms"
  MON=""; if [[ -n "$SERVER_PID" ]]; then monitor "$dir/server.csv" & MON=$!; fi
  date +%s > "$dir/start"
  "$JM" -n -t "$PLAN" -Jhost="$HOST" -Jport="$PORT" -Jprotocol="$PROTO" \
    -Jusers="$users" -Jusers_returning="$ret" -Jrampup="$ramp" -Jduration="$dur" \
    -Jthink_min="$tmin" -Jthink_range="$trange" -Jloops="$loops" ${EXTRA:-} \
    -Jjmeter.save.saveservice.subresults=true \
    -Jjmeter.reportgenerator.sample_filter='^(?!.*-[0-9]+$).*' \
    -Jjmeter.reportgenerator.overall_granularity=5000 \
    -l "$dir/results.jtl" -j "$dir/jmeter.log" -e -o "$dir/dashboard" | grep -E 'summary =|Err:' | tail -1
  date +%s > "$dir/end"
  [[ -n "$MON" ]] && kill "$MON" 2>/dev/null || true
  sleep 5   # let the server settle between scenarios
done
echo "Done. Results in $OUT"
