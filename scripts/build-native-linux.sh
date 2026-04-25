#!/usr/bin/env bash
# Build the Linux-native livesplit_core shared library and copy it into the
# runtimes/{rid}/native layout the .NET side expects. Run on a Linux host
# with the Rust toolchain installed (rustup + cargo + a linux-gnu target).
#
# Usage:
#   scripts/build-native-linux.sh            # builds linux-x64
#   scripts/build-native-linux.sh linux-x86  # builds 32-bit
#
# Prerequisites:
#   rustup target add x86_64-unknown-linux-gnu         # linux-x64 (default)
#   rustup target add i686-unknown-linux-gnu           # linux-x86
#
# Output:
#   src/LiveSplit.Core/runtimes/{rid}/native/liblivesplit_core.so
#
# asr_capi.so is not built here: upstream livesplit-core doesn't ship a C API for the
# auto-splitting crate. That wrapper lives in a separate repo; package the resulting .so
# into components/LiveSplit.AutoSplittingRuntime/src/LiveSplit.AutoSplittingRuntime/runtimes/
# manually once available.

set -euo pipefail

RID="${1:-linux-x64}"

case "$RID" in
    linux-x64)
        CARGO_TARGET="x86_64-unknown-linux-gnu"
        ;;
    linux-x86)
        CARGO_TARGET="i686-unknown-linux-gnu"
        ;;
    linux-arm64)
        CARGO_TARGET="aarch64-unknown-linux-gnu"
        ;;
    *)
        echo "error: unsupported RID '$RID'. Supported: linux-x64, linux-x86, linux-arm64" >&2
        exit 1
        ;;
esac

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CAPI_DIR="$REPO_ROOT/lib/livesplit-core/capi"
OUTPUT_DIR="$REPO_ROOT/src/LiveSplit.Core/runtimes/$RID/native"

mkdir -p "$OUTPUT_DIR"

pushd "$CAPI_DIR" >/dev/null
cargo build --release --target "$CARGO_TARGET"
popd >/dev/null

SOURCE_SO="$REPO_ROOT/lib/livesplit-core/target/$CARGO_TARGET/release/liblivesplit_core.so"
if [[ ! -f "$SOURCE_SO" ]]; then
    echo "error: cargo produced no .so at $SOURCE_SO" >&2
    exit 1
fi

cp "$SOURCE_SO" "$OUTPUT_DIR/liblivesplit_core.so"
echo "copied $SOURCE_SO -> $OUTPUT_DIR/liblivesplit_core.so"
