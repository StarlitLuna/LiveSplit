#!/usr/bin/env bash
# Package a Linux build of LiveSplit. Three output modes:
#   tarball  (default) — self-contained publish, gzipped
#   appimage           — also produces dist/LiveSplit-<rid>.AppImage
#   flatpak            — produces dist/livesplit.flatpak (host-side native build skipped;
#                        flatpak-builder runs the whole pipeline inside the sandbox)
#
# Prerequisites:
#   tarball / appimage:
#     - .NET 8 SDK
#     - Rust toolchain (for the .so files — see scripts/build-native-linux.sh)
#     - linuxdeploy on PATH (appimage only)
#   flatpak:
#     - flatpak + flatpak-builder
#     - org.freedesktop.{Platform,Sdk}//23.08
#     - org.freedesktop.Sdk.Extension.{dotnet8,rust-stable}//23.08
#     (no .NET or Rust required on the host)
#
# Usage:
#   scripts/package-linux.sh                        # dist/livesplit-linux-x64.tar.gz
#   scripts/package-linux.sh --appimage             # also dist/LiveSplit-x86_64.AppImage
#   scripts/package-linux.sh --flatpak              # dist/livesplit.flatpak (linux-x64 only)
#   scripts/package-linux.sh --rid linux-arm64      # cross-RID tarball

set -euo pipefail

RID="linux-x64"
MAKE_APPIMAGE="false"
MAKE_FLATPAK="false"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --appimage) MAKE_APPIMAGE="true"; shift ;;
        --flatpak)  MAKE_FLATPAK="true"; shift ;;
        --rid)      RID="$2"; shift 2 ;;
        *) echo "error: unknown option '$1'" >&2; exit 1 ;;
    esac
done

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST_DIR="$REPO_ROOT/dist"
mkdir -p "$DIST_DIR"

# Flatpak short-circuits the host-side pipeline entirely: the manifest invokes
# build-native-linux.sh + dotnet publish inside the sandbox, where the dotnet8
# and rust-stable SDK extensions live.
if [[ "$MAKE_FLATPAK" == "true" ]]; then
    if ! command -v flatpak-builder >/dev/null 2>&1; then
        echo "error: flatpak-builder not found in PATH; install flatpak + flatpak-builder" >&2
        exit 1
    fi

    pushd "$REPO_ROOT" >/dev/null
    flatpak-builder --repo="$DIST_DIR/flatpak-repo" --force-clean \
        "$DIST_DIR/flatpak-build" org.livesplit.LiveSplit.yml
    flatpak build-bundle "$DIST_DIR/flatpak-repo" \
        "$DIST_DIR/livesplit.flatpak" org.livesplit.LiveSplit
    popd >/dev/null

    echo "wrote $DIST_DIR/livesplit.flatpak"
    echo "install with: flatpak install --user $DIST_DIR/livesplit.flatpak"
    exit 0
fi

PUBLISH_DIR="$DIST_DIR/publish-$RID"
rm -rf "$PUBLISH_DIR"

# Build the Rust native libs for the target RID first (no-op if already built).
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
    rm -f "$APPDIR/usr/bin/libcoreclrtraceptprovider.so"

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
