#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RAPTOR_DIR="$ROOT_DIR/local/raptor"
PATCH_FILE="$ROOT_DIR/tools/raptor-wfcore/raptor-wfcore.patch"

if [ ! -d "$RAPTOR_DIR/.git" ]; then
    mkdir -p "$ROOT_DIR/local"
    git clone https://github.com/skynettx/raptor "$RAPTOR_DIR"
fi

if ! git -C "$RAPTOR_DIR" diff --quiet -- src/i_video.cpp; then
    echo "local/raptor already has changes; leaving them in place"
else
    git -C "$RAPTOR_DIR" apply "$PATCH_FILE"
fi

cmake -S "$RAPTOR_DIR" -B "$RAPTOR_DIR/build"
cmake --build "$RAPTOR_DIR/build" -j "$(nproc)"

cat <<EOF
Raptor WF core built:
  $RAPTOR_DIR/build/bin/raptor

Copy Raptor 1.2+ GLB assets into:
  $RAPTOR_DIR/

Shareware:
  FILE0000.GLB
  FILE0001.GLB

Full version:
  FILE0000.GLB
  FILE0001.GLB
  FILE0002.GLB
  FILE0003.GLB
  FILE0004.GLB
EOF
