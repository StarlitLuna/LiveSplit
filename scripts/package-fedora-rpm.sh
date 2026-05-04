#!/usr/bin/env bash
# Build a Fedora RPM from a native linux-x64 self-contained publish.
#
# Prerequisites on Fedora:
#   sudo dnf install @development-tools dotnet-sdk-8.0 cargo rpm-build \
#       rpmdevtools desktop-file-utils pkgconf-pkg-config git tar gzip \
#       vlc-devel vlc-libs vlc-plugin-ffmpeg
#
# Usage:
#   scripts/package-fedora-rpm.sh
#   RPM_VERSION=0.0.0 RPM_RELEASE=1 scripts/package-fedora-rpm.sh

set -euo pipefail

RID="linux-x64"
RPM_VERSION="${RPM_VERSION:-0.0.0}"
RPM_RELEASE="${RPM_RELEASE:-1}"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --rid) RID="$2"; shift 2 ;;
        *) echo "error: unknown option '$1'" >&2; exit 1 ;;
    esac
done

if [[ "$RID" != "linux-x64" ]]; then
    echo "error: Fedora RPM packaging currently supports linux-x64 only" >&2
    exit 1
fi

if [[ ! "$RPM_VERSION" =~ ^[0-9][A-Za-z0-9_.+~]*$ ]]; then
    echo "error: RPM_VERSION must start with a digit and contain only RPM-safe version characters" >&2
    exit 1
fi

if [[ ! "$RPM_RELEASE" =~ ^[0-9][A-Za-z0-9_.+~]*$ ]]; then
    echo "error: RPM_RELEASE must start with a digit and contain only RPM-safe release characters" >&2
    exit 1
fi

if ! command -v rpmbuild >/dev/null 2>&1; then
    echo "error: rpmbuild not found in PATH; install rpm-build" >&2
    exit 1
fi

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST_DIR="$REPO_ROOT/dist"
RPM_STAGE="$DIST_DIR/rpm-stage"
RPM_TOPDIR="$DIST_DIR/rpmbuild"
RPM_OUT="$DIST_DIR/rpm"
PUBLISH_DIR="$RPM_STAGE/livesplit-$RID"
TARBALL="$DIST_DIR/livesplit-${RID}.tar.gz"

rm -rf "$RPM_STAGE" "$RPM_TOPDIR" "$RPM_OUT"
mkdir -p "$RPM_STAGE" "$RPM_TOPDIR/SOURCES" "$RPM_TOPDIR/SPECS" "$RPM_OUT"

bash "$REPO_ROOT/scripts/build-native-linux.sh" "$RID"

dotnet publish "$REPO_ROOT/src/LiveSplit/LiveSplit.csproj" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:DebugType=None \
    -o "$PUBLISH_DIR"

tar -C "$RPM_STAGE" -czf "$TARBALL" "livesplit-$RID"

cp "$TARBALL" "$RPM_TOPDIR/SOURCES/"
cp "$REPO_ROOT/org.livesplit.LiveSplit.desktop" "$RPM_TOPDIR/SOURCES/"
cp "$REPO_ROOT/res/Icon.svg" "$RPM_TOPDIR/SOURCES/"
cp "$REPO_ROOT/LICENSE" "$RPM_TOPDIR/SOURCES/"

rpmbuild -bb "$REPO_ROOT/packaging/rpm/livesplit.spec" \
    --define "_topdir $RPM_TOPDIR" \
    --define "livesplit_version $RPM_VERSION" \
    --define "livesplit_release $RPM_RELEASE" \
    --define "livesplit_rid $RID" \
    --define "livesplit_tarball $(basename "$TARBALL")"

find "$RPM_TOPDIR/RPMS" -type f -name '*.rpm' -exec cp {} "$RPM_OUT/" \;

echo "wrote RPM artifacts:"
find "$RPM_OUT" -type f -name '*.rpm' -print
