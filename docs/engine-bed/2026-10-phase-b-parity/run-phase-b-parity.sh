#!/bin/bash
# Phase B's parity bed (docs/PLAN-ENGINE-RESOLUTION.md): the pre-engine baseline's own command lines
# (docs/baselines/2026-10-pre-engine/run-baseline.sh), run by Domina.Sim inside the engine bed.
# Usage: run-phase-b-parity.sh <path-to-Godot_console.exe> <output-folder>      # PARALLEL=12 by default
#
#   core/          the resolver over its own kinematics, inside Godot's runtime: must give the baseline
#                  back line for line (all but the wall clock)
#   engine/        the resolver over the engine's ground (GodotArenaSpace): must stay inside its noise
#   engine-again/  the duel bed and one campaign over the engine's ground a second time: must be engine/
#                  line for line
#   */bed.txt      each run's closing lines from the bed: wall clock, motion tests a fight
#
# Then: python compare.py docs/baselines/2026-10-pre-engine <output-folder>
GODOT=$1
# Absolute: Godot's --path moves its working directory into the project, so a relative --report would
# land there.
OUT=$(mkdir -p "$2" && cd "$2" && pwd)
ROOT=$(cd "$(dirname "$0")/../../.." && pwd)
P=${PARALLEL:-12}
export GODOT OUT ROOT

# The editor binary loads the project's Debug assemblies; -p:Optimize=true makes them optimised ones.
dotnet build "$ROOT/src/Game/Domina.Game.csproj" -c Debug -p:Optimize=true > "$OUT/build.txt" 2>&1 \
  || { echo "build failed, see $OUT/build.txt"; exit 1; }

# One measurement: <run-folder> <report> <fighter> <the sim's arguments...>
job() {
  run=$1; name=$2; fighter=$3; shift 3
  [ -s "$OUT/$run/$name" ] && return 0
  "$GODOT" --headless --path "$ROOT/src/Game" res://Bed/engine_bed.tscn -- \
    --fighter "$fighter" --report "$OUT/$run/$name" --sim "$@" > "$OUT/$run/log/${name//\//-}" 2>&1
}
export -f job

PLAIN="--mode campaign --days 180 --campaigns 1600 --accept-ratio 2.0 --offers on --market on --school on --paths on --staff on --bounty on --province on --smith-upgrades on --standing on"
RICH="$PLAIN --charms on --thrown-fit everyone --retire maimed --class-fit torite --train-classes on"
SEEDS="1 100001 200001 300001 400001 500001"
SCENARIOS=$("$GODOT" --headless --path "$ROOT/src/Game" res://Bed/engine_bed.tscn -- --fighter core --sim --help 2>&1 \
  | sed -n '/Scenarios:/,/^$/p' | awk 'NR>1 && NF {print $1}')

for run in core engine engine-again; do mkdir -p "$OUT/$run/duel" "$OUT/$run/campaign" "$OUT/$run/log"; done

{
  for fighter in core engine; do
    for s in $SCENARIOS; do
      echo "$fighter duel/$s-never.txt $fighter --scenario $s --battles 20000 --seed 1 --policy never"
      echo "$fighter duel/$s-losing07.txt $fighter --scenario $s --battles 20000 --seed 1 --policy losing:0.7"
    done
    for tier in apprentice master legend; do
      for seed in $SEEDS; do
        echo "$fighter campaign/plain-$tier-s$seed.txt $fighter $PLAIN --difficulty $tier --seed $seed"
        echo "$fighter campaign/rich-$tier-s$seed.txt $fighter $RICH --difficulty $tier --seed $seed"
      done
    done
  done
  for s in $SCENARIOS; do
    echo "engine-again duel/$s-never.txt engine --scenario $s --battles 20000 --seed 1 --policy never"
    echo "engine-again duel/$s-losing07.txt engine --scenario $s --battles 20000 --seed 1 --policy losing:0.7"
  done
  echo "engine-again campaign/rich-master-s1.txt engine $RICH --difficulty master --seed 1"
} | xargs -P "$P" -L 1 bash -c 'job "$@"' _

# The bed's own closing lines, one file a run; the full logs stay in */log/ for a run that failed.
for run in core engine engine-again; do
  grep -H -E '^(fights|RESULT)' "$OUT/$run/log/"* | sed "s|^$OUT/$run/log/||" > "$OUT/$run/bed.txt"
done
