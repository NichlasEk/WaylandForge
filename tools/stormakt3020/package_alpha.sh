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

encode_opus() {
    local source_path="$1"
    local output_path="$2"
    local bitrate="$3"
    local channels="$4"
    ffmpeg -hide_banner -loglevel error -y -i "$source_path" -vn -ar 48000 -ac "$channels" \
        -c:a libopus -b:a "$bitrate" -vbr on -compression_level 10 "$output_path"
}

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
    "$STAGE_DIR/assets/stormakt3020/sfx" "$STAGE_DIR/assets/stormakt3020/radio/voices" \
    "$STAGE_DIR/assets/stormakt3020/video" \
    "$STAGE_DIR/lib" "$STAGE_DIR/licenses/opusfile" "$STAGE_DIR/licenses/opus" \
    "$STAGE_DIR/licenses/libogg"

make -C "$ROOT_DIR/tools/waylandforge-audiod"
dotnet restore "$HOST_PROJECT" -r linux-x64 --disable-parallel -v minimal
publish_project "$HOST_PROJECT" "$STAGE_DIR/waylandforge-host"
publish_project "$CORE_PROJECT" "$STAGE_DIR/stormakt-core"

cp "$ROOT_DIR/src/SystemRegisIII.Host.WaylandForge/bin/Release/net10.0/libwaylandforge_native.so" \
    "$STAGE_DIR/waylandforge-host/"
cp "$ROOT_DIR/config/waylandforge.ui.toml" "$STAGE_DIR/config/waylandforge.ui.toml"
cp "$ROOT_DIR/tools/stormakt3020/alpha-config.toml" "$STAGE_DIR/config/waylandforge.ui.local.toml"
cp "$ROOT_DIR/tools/stormakt3020/alpha-launcher.sh" "$STAGE_DIR/start-stormakt-3020.sh"
cp "$ROOT_DIR/tools/stormakt3020/play-intro.sh" "$STAGE_DIR/play-stormakt-intro.sh"
cp "$ROOT_DIR/tools/waylandforge-audiod/waylandforge-audiod" "$STAGE_DIR/"
cp "$ROOT_DIR/assets/stormakt3020/stormakt3020.wfsa" "$STAGE_DIR/assets/stormakt3020/"
cp "$ROOT_DIR/assets/stormakt3020/video/stormakt-3020-algkriget-intro-v1.mp4" \
    "$STAGE_DIR/assets/stormakt3020/video/"
cp "$ROOT_DIR/assets/stormakt3020/video/intro-input.conf" \
    "$STAGE_DIR/assets/stormakt3020/video/"
encode_opus "$ROOT_DIR/assets/stormakt3020/stormakt-over-oresund-v1.wav" \
    "$STAGE_DIR/assets/stormakt3020/stormakt-over-oresund-v1.opus" 128k 2
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
    encode_opus "$ROOT_DIR/assets/stormakt3020/music/$music_file" \
        "$STAGE_DIR/assets/stormakt3020/music/${music_file%.wav}.opus" 128k 2
done
for sfx_file in "$ROOT_DIR/assets/stormakt3020/sfx/"*.wav; do
    sfx_name="$(basename "$sfx_file" .wav)"
    encode_opus "$sfx_file" "$STAGE_DIR/assets/stormakt3020/sfx/$sfx_name.opus" 96k 2
done
for voice_file in "$ROOT_DIR/assets/stormakt3020/radio/voices/"*.wav; do
    voice_name="$(basename "$voice_file" .wav)"
    encode_opus "$voice_file" "$STAGE_DIR/assets/stormakt3020/radio/voices/$voice_name.opus" 48k 1
done
cp -L /usr/lib/libopusfile.so.0 "$STAGE_DIR/lib/"
cp -L /usr/lib/libopus.so.0 "$STAGE_DIR/lib/"
cp -L /usr/lib/libogg.so.0 "$STAGE_DIR/lib/"
cp /usr/share/licenses/opusfile/LICENSE "$STAGE_DIR/licenses/opusfile/"
cp /usr/share/licenses/opus/COPYING "$STAGE_DIR/licenses/opus/"
cp /usr/share/licenses/libogg/COPYING "$STAGE_DIR/licenses/libogg/"
cp "$ROOT_DIR/docs/stormakt3020-alpha-release.md" "$STAGE_DIR/README.md"
chmod +x "$STAGE_DIR/start-stormakt-3020.sh" "$STAGE_DIR/play-stormakt-intro.sh" \
    "$STAGE_DIR/waylandforge-audiod"

printf '%s\n' "$VERSION" > "$STAGE_DIR/VERSION"
(cd "$STAGE_DIR" && find . -type f ! -name SHA256SUMS -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS)
tar -C "$DIST_DIR" -czf "$ARCHIVE_PATH" "$PACKAGE_NAME"
sha256sum "$ARCHIVE_PATH" > "$ARCHIVE_PATH.sha256"

echo "$ARCHIVE_PATH"
