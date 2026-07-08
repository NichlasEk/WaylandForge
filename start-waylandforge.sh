#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AUDIO_DIR="$ROOT_DIR/tools/waylandforge-audiod"
AUDIO_BIN="$AUDIO_DIR/waylandforge-audiod"
AUDIO_SOCKET="/tmp/waylandforge-audio.sock"
AUDIO_PID=""

audio_ping() {
python - "$AUDIO_SOCKET" <<'PY'
import socket
import sys

path = sys.argv[1]
try:
    with socket.socket(socket.AF_UNIX) as s:
        s.settimeout(0.2)
        s.connect(path)
        s.sendall(b"PING\n")
        sys.exit(0 if s.recv(32).startswith(b"PONG") else 1)
except OSError:
    sys.exit(1)
PY
}

cleanup() {
    if [ -n "$AUDIO_PID" ]; then
        kill "$AUDIO_PID" 2>/dev/null || true
        wait "$AUDIO_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT INT TERM

make -C "$AUDIO_DIR"

if audio_ping; then
    echo "waylandforge-audiod already running on $AUDIO_SOCKET"
else
    "$AUDIO_BIN" &
    AUDIO_PID="$!"
    for _ in $(seq 1 50); do
        if audio_ping; then
            break
        fi
        sleep 0.05
    done
    if ! audio_ping; then
        echo "failed to start waylandforge-audiod on $AUDIO_SOCKET" >&2
        exit 1
    fi
fi

dotnet run --project "$ROOT_DIR/src/SystemRegisIII.Host.WaylandForge"
