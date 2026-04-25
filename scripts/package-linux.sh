#!/usr/bin/env bash
# Package a Linux build of LiveSplit as a self-contained tarball + AppImage.
#
# Prerequisites:
#   - .NET 8 SDK
#   - Rust toolchain (for liblivesplit_core.so — see scripts/build-native-linux.sh)
#   - For the AppImage step: linuxdeploy (https://github.com/linuxdeploy/linuxdeploy)
#
# Usage:
#   scripts/package-linux.sh                  # build linux-x64 tarball
#   scripts/package-linux.sh --appimage       # also build the AppImage
#   scripts/package-linux.sh --rid linux-arm64
#
# Output:
#   dist/livesplit-linux-x64.tar.gz
#   dist/LiveSplit-x86_64.AppImage              (if --appimage)

set -euo pipefail

RID="linux-x64"
MAKE_APPIMAGE="false"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --appimage) MAKE_APPIMAGE="true"; shift ;;
        --rid) RID="$2"; shift 2 ;;
        *) echo "error: unknown option '$1'" >&2; exit 1 ;;
    esac
done

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST_DIR="$REPO_ROOT/dist"
PUBLISH_DIR="$REPO_ROOT/dist/publish-$RID"

mkdir -p "$DIST_DIR"
rm -rf "$PUBLISH_DIR"

# Build the Rust native lib for the target RID first (no-op if already built).
bash "$REPO_ROOT/scripts/build-native-linux.sh" "$RID"

# Self-contained publish so users don't need .NET installed.
dotnet publish "$REPO_ROOT/src/LiveSplit/LiveSplit.csproj" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:DebugType=None \
    -o "$PUBLISH_DIR"

# Tarball: livesplit-linux-x64.tar.gz containing the self-contained publish output.
TARBALL="$DIST_DIR/livesplit-${RID}.tar.gz"
tar -C "$DIST_DIR" -czf "$TARBALL" "publish-$RID"
echo "wrote $TARBALL"

# Optional AppImage. Requires linuxdeploy in PATH; ships an icon + .desktop file.
if [[ "$MAKE_APPIMAGE" == "true" ]]; then
    if ! command -v linuxdeploy >/dev/null 2>&1; then
        echo "error: linuxdeploy not found in PATH; install from https://github.com/linuxdeploy/linuxdeploy" >&2
        exit 1
    fi

    APPDIR="$DIST_DIR/AppDir"
    rm -rf "$APPDIR"
    mkdir -p "$APPDIR/usr/bin"
    cp -r "$PUBLISH_DIR"/* "$APPDIR/usr/bin/"

    # Minimal .desktop entry that AppImage's runtime expects.
    cat > "$APPDIR/livesplit.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=LiveSplit
Exec=LiveSplit
Icon=livesplit
Categories=Utility;
Terminal=false
EOF

    cp "$REPO_ROOT/res/Icon.png" "$APPDIR/livesplit.png" 2>/dev/null \
        || echo "(no Icon.png in res/; AppImage will ship without an icon)" >&2

    pushd "$DIST_DIR" >/dev/null
    linuxdeploy --appdir "$APPDIR" --output appimage
    popd >/dev/null
    echo "wrote $DIST_DIR/LiveSplit-$RID.AppImage"
fi
