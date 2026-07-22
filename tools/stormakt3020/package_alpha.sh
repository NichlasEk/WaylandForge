#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VERSION="${1:-0.1.0-alpha}"
PACKAGE_NAME="stormakt-3020-${VERSION}-linux-x64"
DIST_DIR="$ROOT_DIR/dist"
STAGE_DIR="$DIST_DIR/$PACKAGE_NAME"
ARCHIVE_PATH="$DIST_DIR/$PACKAGE_NAME.tar.gz"
HOST_PROJECT="$ROOT_DIR/src/SystemRegisIII.Host.WaylandForge/SystemRegisIII.Host.WaylandForge.csproj"
CORE_PROJECT="$ROOT_DIR/src/SystemRegisIII.ExternalCore.Stormakt3020/SystemRegisIII.ExternalCore.Stormakt3020.csproj"

publish_project() {
    local project="$1"
    local output="$2"
    local attempt
    for attempt in 1 2 3; do
        if dotnet publish "$project" -c Release -r linux-x64 --self-contained true \
            --no-restore -p:PublishSingleFile=false -o "$output" -v minimal; then
            return 0
        fi
        echo "publish attempt $attempt failed; retrying" >&2
    done
    return 1
}

rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR/waylandforge-host" "$STAGE_DIR/stormakt-core" \
    "$STAGE_DIR/config" "$STAGE_DIR/assets/stormakt3020/music" \
    "$STAGE_DIR/assets/stormakt3020/sfx" "$STAGE_DIR/assets/stormakt3020/radio/voices"

make -C "$ROOT_DIR/tools/waylandforge-audiod"
dotnet restore "$HOST_PROJECT" -r linux-x64 --disable-parallel -v minimal
publish_project "$HOST_PROJECT" "$STAGE_DIR/waylandforge-host"
publish_project "$CORE_PROJECT" "$STAGE_DIR/stormakt-core"

cp "$ROOT_DIR/src/SystemRegisIII.Host.WaylandForge/bin/Release/net10.0/libwaylandforge_native.so" \
    "$STAGE_DIR/waylandforge-host/"
cp "$ROOT_DIR/config/waylandforge.ui.toml" "$STAGE_DIR/config/waylandforge.ui.toml"
cp "$ROOT_DIR/tools/stormakt3020/alpha-config.toml" "$STAGE_DIR/config/waylandforge.ui.local.toml"
cp "$ROOT_DIR/tools/stormakt3020/alpha-launcher.sh" "$STAGE_DIR/start-stormakt-3020.sh"
cp "$ROOT_DIR/tools/waylandforge-audiod/waylandforge-audiod" "$STAGE_DIR/"
cp "$ROOT_DIR/assets/stormakt3020/stormakt3020.wfsa" "$STAGE_DIR/assets/stormakt3020/"
cp "$ROOT_DIR/assets/stormakt3020/stormakt-over-oresund-v1.wav" "$STAGE_DIR/assets/stormakt3020/"
music_files=(
    tre-kronors-jarnmarsch-loop-v1.wav
    skanska-skuggor-loop-v1.wav
    oresund-i-brand-v1.wav
    silverkroppen-faltmarsch-loop-v1.wav
    lemminkainen-gruva1-v1.wav
    kronans-sista-salva-loop-v2.wav
    rigsregnskabet-boss-loop-v1.wav
    lemminkainen-flykt-v1.wav
    lemminkainen-lattnad-v1.wav
    snapphanens-jakt-loop-v1.wav
    rode-hunde-drev-loop-v1.wav
    snapphanens-ed-seger-loop-v1.wav
    codex-argentum-clock-loop-v1.wav
    kopenhamns-ring-loop-v1.wav
    frederik-null-loop-v1.wav
    oresunds-oje-loop-v1.wav
    kungliga-armadan-loop-v1.wav
    christians-superfregatt-loop-v1.wav
    kopenhamn-landning-loop-v1.wav
    holmen-under-staden-loop-v1.wav
    argentum-legender-loop-v1.wav
    konung-christians-vrede-loop-v1.wav
    kopenhamn-silvergryning-loop-v1.wav
)
for music_file in "${music_files[@]}"; do
    cp "$ROOT_DIR/assets/stormakt3020/music/$music_file" "$STAGE_DIR/assets/stormakt3020/music/"
done
cp "$ROOT_DIR/assets/stormakt3020/sfx/"*.wav "$STAGE_DIR/assets/stormakt3020/sfx/"
cp "$ROOT_DIR/assets/stormakt3020/radio/voices/"*.wav "$STAGE_DIR/assets/stormakt3020/radio/voices/"
cp "$ROOT_DIR/docs/stormakt3020-alpha-release.md" "$STAGE_DIR/README.md"
chmod +x "$STAGE_DIR/start-stormakt-3020.sh" "$STAGE_DIR/waylandforge-audiod"

printf '%s\n' "$VERSION" > "$STAGE_DIR/VERSION"
(cd "$STAGE_DIR" && find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)
tar -C "$DIST_DIR" -czf "$ARCHIVE_PATH" "$PACKAGE_NAME"
sha256sum "$ARCHIVE_PATH" > "$ARCHIVE_PATH.sha256"

echo "$ARCHIVE_PATH"
