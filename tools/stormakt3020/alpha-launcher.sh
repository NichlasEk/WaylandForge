#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AUDIO_SOCKET="/tmp/waylandforge-audio.sock"
AUDIO_PID=""

cleanup() {
    if [ -n "$AUDIO_PID" ]; then
        kill "$AUDIO_PID" 2>/dev/null || true
        wait "$AUDIO_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT INT TERM

cd "$ROOT_DIR"
if [ -x ./play-stormakt-intro.sh ]; then
    ./play-stormakt-intro.sh "$ROOT_DIR"
fi

if [ -x ./waylandforge-audiod ]; then
    ./waylandforge-audiod &
    AUDIO_PID="$!"
    for _ in $(seq 1 50); do
        [ -S "$AUDIO_SOCKET" ] && break
        sleep 0.05
    done
fi

export WAYLANDFORGE_START_STORMAKT=1
export WAYLANDFORGE_STORMAKT_ALPHA=1
export LD_LIBRARY_PATH="$ROOT_DIR/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
./waylandforge-host/SystemRegisIII.Host.WaylandForge
