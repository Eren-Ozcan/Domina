#!/bin/bash
# Baseline before the engine move (docs/PLAN-ENGINE-RESOLUTION.md, step 0).
# Usage: run-baseline.sh <path-to-Domina.Sim.exe> <output-folder>
# Duel bed: every scenario, 20,000 fights (seed 1..20000), under two policies.
# Campaign bed: the plain documented bed and the rich bed, three difficulty tiers, six seeds.
# EXTRA is added to every run: since Open Decision #25 (2026-10-02) the default ground is the slide, and
# this folder reads back with EXTRA='--ground kinematic'.
SIM=$1; OUT=$2; P=${PARALLEL:-12}; X=${EXTRA:-}  # no trailing blank: xargs -L would join the lines
mkdir -p "$OUT/duel" "$OUT/campaign"
PLAIN="--mode campaign --days 180 --campaigns 1600 --accept-ratio 2.0 --offers on --market on --school on --paths on --staff on --bounty on --province on --smith-upgrades on --standing on"
RICH="$PLAIN --charms on --thrown-fit everyone --retire maimed --class-fit torite --train-classes on"
SEEDS="1 100001 200001 300001 400001 500001"
SCENARIOS=$("$SIM" --help 2>&1 | sed -n '/Scenarios:/,/^$/p' | awk 'NR>1 && NF {print $1}')
{
  for s in $SCENARIOS; do
    echo "duel/$s-never.txt --scenario $s --battles 20000 --seed 1 --policy never${X:+ $X}"
    echo "duel/$s-losing07.txt --scenario $s --battles 20000 --seed 1 --policy losing:0.7${X:+ $X}"
  done
  for tier in apprentice master legend; do
    for seed in $SEEDS; do
      echo "campaign/plain-$tier-s$seed.txt $PLAIN --difficulty $tier --seed $seed${X:+ $X}"
      echo "campaign/rich-$tier-s$seed.txt $RICH --difficulty $tier --seed $seed${X:+ $X}"
    done
  done
} | xargs -P "$P" -L 1 sh -c 'out=$0; [ -s "'"$OUT"'/$out" ] && exit 0; "'"$SIM"'" "$@" > "'"$OUT"'/$out" 2>&1'
