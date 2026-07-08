#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AUDIO_DIR="$ROOT_DIR/tools/waylandforge-audiod"
AUDIO_BIN="$AUDIO_DIR/waylandforge-audiod"
AUDIO_SOCKET="/tmp/waylandforge-audio.sock"
HOST_PROJECT="$ROOT_DIR/src/SystemRegisIII.Host.WaylandForge/SystemRegisIII.Host.WaylandForge.csproj"
HOST_DLL="$ROOT_DIR/src/SystemRegisIII.Host.WaylandForge/bin/Debug/net8.0/SystemRegisIII.Host.WaylandForge.dll"
LOCAL_OPENTYRIAN_BIN="$ROOT_DIR/local/opentyrian-wfcore/opentyrian"
AUDIO_PID=""
HOST_PID=""

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

audio_quit() {
python - "$AUDIO_SOCKET" <<'PY'
import socket
import sys

path = sys.argv[1]
try:
    with socket.socket(socket.AF_UNIX) as s:
        s.settimeout(0.5)
        s.connect(path)
        s.sendall(b"QUIT\n")
        s.recv(32)
except OSError:
    pass
PY
}

stop_existing_audio() {
    if ! audio_ping; then
        return
    fi

    echo "stopping existing waylandforge-audiod on $AUDIO_SOCKET"
    audio_quit
    for _ in $(seq 1 50); do
        if ! audio_ping; then
            return
        fi
        sleep 0.05
    done

    if command -v pgrep >/dev/null 2>&1; then
        while read -r pid; do
            if [ -n "$pid" ]; then
                kill "$pid" 2>/dev/null || true
            fi
        done < <(pgrep -f "$AUDIO_BIN" || true)
        for _ in $(seq 1 50); do
            if ! audio_ping; then
                return
            fi
            sleep 0.05
        done
    fi

    echo "existing waylandforge-audiod did not stop cleanly" >&2
    exit 1
}

stop_local_external_cores() {
    if [ ! -x "$LOCAL_OPENTYRIAN_BIN" ] || ! command -v pgrep >/dev/null 2>&1; then
        return
    fi

    while read -r pid; do
        if [ -n "$pid" ]; then
            echo "stopping stale external core pid $pid"
            kill "$pid" 2>/dev/null || true
        fi
    done < <(pgrep -f "$LOCAL_OPENTYRIAN_BIN" || true)
}

cleanup() {
    if [ -n "$HOST_PID" ]; then
        kill "$HOST_PID" 2>/dev/null || true
        wait "$HOST_PID" 2>/dev/null || true
    fi
    stop_local_external_cores
    if [ -n "$AUDIO_PID" ]; then
        kill "$AUDIO_PID" 2>/dev/null || true
        wait "$AUDIO_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT INT TERM

make -C "$AUDIO_DIR"
dotnet build "$HOST_PROJECT" --nologo
stop_local_external_cores

stop_existing_audio

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

dotnet "$HOST_DLL" &
HOST_PID="$!"
wait "$HOST_PID"
HOST_PID=""
