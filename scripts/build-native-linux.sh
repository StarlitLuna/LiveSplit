#!/usr/bin/env bash
# Build the Linux-native shared libs LiveSplit P/Invokes into:
#   - liblivesplit_core.so  (timing + splits-file engine)
#   - libasr_capi.so        (auto-splitting runtime; 64-bit only)
#
# Both .so files get copied into the runtimes/{rid}/native/ layout the .NET
# NativeLibraryResolver expects. Run on a Linux host with rustup + cargo and
# the relevant linux-gnu target installed.
#
# Usage:
#   scripts/build-native-linux.sh                # builds linux-x64
#   scripts/build-native-linux.sh linux-arm64    # cross-build (toolchain required)
#   scripts/build-native-linux.sh linux-x86      # 32-bit; skips asr_capi
#
# Prerequisites:
#   rustup target add x86_64-unknown-linux-gnu        # linux-x64 (default)
#   rustup target add aarch64-unknown-linux-gnu       # linux-arm64
#   rustup target add i686-unknown-linux-gnu          # linux-x86
#
# Outputs:
#   src/LiveSplit.Core/runtimes/{rid}/native/liblivesplit_core.so
#   components/LiveSplit.AutoSplittingRuntime/src/LiveSplit.AutoSplittingRuntime/runtimes/{rid}/native/libasr_capi.so

set -euo pipefail

RID="${1:-linux-x64}"

case "$RID" in
    linux-x64)   CARGO_TARGET="x86_64-unknown-linux-gnu";  BUILD_ASR=true  ;;
    linux-arm64) CARGO_TARGET="aarch64-unknown-linux-gnu"; BUILD_ASR=true  ;;
    linux-x86)   CARGO_TARGET="i686-unknown-linux-gnu";    BUILD_ASR=false ;;
    *)
        echo "error: unsupported RID '$RID'. Supported: linux-x64, linux-x86, linux-arm64" >&2
        exit 1
        ;;
esac

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

build_crate() {
    local crate_dir="$1"
    local out_dir="$2"
    local so_name="$3"

    mkdir -p "$out_dir"

    pushd "$crate_dir" >/dev/null
    cargo build --release --target "$CARGO_TARGET"
    popd >/dev/null

    local source_so="$crate_dir/target/$CARGO_TARGET/release/$so_name"
    if [[ ! -f "$source_so" ]]; then
        echo "error: cargo produced no .so at $source_so" >&2
        exit 1
    fi

    cp "$source_so" "$out_dir/$so_name"
    echo "copied $source_so -> $out_dir/$so_name"
}

# livesplit_core lives in a cargo workspace, so its build output ends up in the
# workspace target dir (lib/livesplit-core/target), not under capi/target.
LSCORE_OUT="$REPO_ROOT/src/LiveSplit.Core/runtimes/$RID/native"
mkdir -p "$LSCORE_OUT"

pushd "$REPO_ROOT/lib/livesplit-core/capi" >/dev/null
cargo build --release --target "$CARGO_TARGET"
popd >/dev/null

LSCORE_SO="$REPO_ROOT/lib/livesplit-core/target/$CARGO_TARGET/release/liblivesplit_core.so"
if [[ ! -f "$LSCORE_SO" ]]; then
    echo "error: cargo produced no .so at $LSCORE_SO" >&2
    exit 1
fi
cp "$LSCORE_SO" "$LSCORE_OUT/liblivesplit_core.so"
echo "copied $LSCORE_SO -> $LSCORE_OUT/liblivesplit_core.so"

# asr_capi is standalone (its own target/), and only declares deps for 64-bit
# pointer widths, so building it for linux-x86 won't compile.
if [[ "$BUILD_ASR" != "true" ]]; then
    echo "skipping asr_capi: the auto-splitting crate is 64-bit only"
    exit 0
fi

build_crate \
    "$REPO_ROOT/components/LiveSplit.AutoSplittingRuntime/src/asr-capi" \
    "$REPO_ROOT/components/LiveSplit.AutoSplittingRuntime/src/LiveSplit.AutoSplittingRuntime/runtimes/$RID/native" \
    "libasr_capi.so"
