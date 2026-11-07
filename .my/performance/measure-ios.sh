#!/usr/bin/env bash
# Runs the iOS short set once and summarises it.
# Usage: Brinell/.my/performance/measure-ios.sh <label> [class ...]
set -uo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../.." && pwd)"
label="$1"; shift
# ONE_PROCESS=1 runs the classes in one test process, so they share one app session.
classes="${*:-MaterialsOverviewJourneyTests GroupedListGalleryTests ContactListSmokeTests GarbageOverviewJourneyTests}"
log=/tmp/appium.log
start=$(wc -c < "$log")
SUMMARY="$here/summary-ios.tsv" "$repo/.my/uitesting/run-classes.sh" $( [[ "${ONE_PROCESS:-}" == 1 ]] && echo "${classes// /+}" || echo $classes )
tail -c +"$((start + 1))" "$log" > "$here/logs/$label.appium.log"
trx=()
[[ "${ONE_PROCESS:-}" == 1 ]] && runs="${classes// /+}" || runs="$classes"
for c in $runs; do trx+=("$repo/.my/uitesting/results/$c.trx"); cp "$repo/.my/uitesting/results/$c.log" "$here/logs/$label.$c.log"; done
echo "wall $SECONDS s"
python3 "$here/analyze.py" "$here/logs/$label.appium.log" "$label" "${trx[@]}"
