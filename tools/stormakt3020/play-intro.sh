#!/usr/bin/env bash
set -u

ROOT_DIR="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
INTRO_PATH="$ROOT_DIR/assets/stormakt3020/video/stormakt-3020-algkriget-intro-v1.mp4"
INTRO_INPUT="$ROOT_DIR/assets/stormakt3020/video/intro-input.conf"

if [ "${WAYLANDFORGE_STORMAKT_SKIP_INTRO:-0}" = "1" ] || [ ! -f "$INTRO_PATH" ]; then
    exit 0
fi

if command -v mpv >/dev/null 2>&1; then
    mpv --fs --no-terminal --really-quiet --keep-open=no --force-window=immediate \
        --no-osc --osd-level=0 --cursor-autohide=always --input-default-bindings=yes \
        --input-conf="$INTRO_INPUT" \
        "$INTRO_PATH" || true
    exit 0
fi

if command -v ffplay >/dev/null 2>&1; then
    ffplay -autoexit -fs -loglevel error -window_title "Stormakt 3020" "$INTRO_PATH" || true
    exit 0
fi

echo "Stormakt-introt hoppades över: installera mpv eller ffplay för videouppspelning." >&2
