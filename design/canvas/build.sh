#!/usr/bin/env bash
# Reassembles the .dc.html artboards from the shared chrome (_head.part) and each
# screen's body. Edit a *.body file, run this, then re-seed the canvas.
set -euo pipefail
cd "$(dirname "$0")"
for n in Main Roster Arena Armoury Market School Crowd Report; do
  cat _head.part "$n.body" _tail.part > "$n.dc.html"
done
echo "rebuilt: $(ls *.dc.html | tr '\n' ' ')"
