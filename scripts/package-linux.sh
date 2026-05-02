#!/usr/bin/env bash
# Package a Flatpak build of LiveSplit. Flatpak is the supported cross-distro
# Linux artifact; Fedora RPM packaging lives in scripts/package-fedora-rpm.sh.
#
# Prerequisites:
#   - flatpak + flatpak-builder
#   - org.freedesktop.{Platform,Sdk}//24.08
#   - org.freedesktop.Sdk.Extension.{dotnet8,rust-stable}//24.08
#   (no .NET or Rust required on the host)
#
# Usage:
#   scripts/package-linux.sh                        # dist/LiveSplit.flatpak
#   scripts/package-linux.sh --flatpak              # same as above

set -euo pipefail

while [[ $# -gt 0 ]]; do
    case "$1" in
        --flatpak)  shift ;;
        *) echo "error: unknown option '$1'" >&2; exit 1 ;;
    esac
done

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST_DIR="$REPO_ROOT/dist"
mkdir -p "$DIST_DIR"

if ! command -v flatpak-builder >/dev/null 2>&1; then
    echo "error: flatpak-builder not found in PATH; install flatpak + flatpak-builder" >&2
    exit 1
fi

pushd "$REPO_ROOT" >/dev/null
flatpak-builder --repo="$DIST_DIR/flatpak-repo" --force-clean \
    "$DIST_DIR/flatpak-build" org.livesplit.LiveSplit.yml
flatpak build-bundle "$DIST_DIR/flatpak-repo" \
    "$DIST_DIR/LiveSplit.flatpak" org.livesplit.LiveSplit
popd >/dev/null

echo "wrote $DIST_DIR/LiveSplit.flatpak"
echo "install with: flatpak install --user $DIST_DIR/LiveSplit.flatpak"
