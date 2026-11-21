#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <ios-arm64 | iossimulator-arm64>" >&2
    exit 1
fi

RID="$1"
REPO="$(cd "$(dirname "$0")/.." && pwd)"
BGFX="$REPO/external/bgfx"
BX="$REPO/external/bx"

case "$RID" in
    ios-arm64)
        GCC=ios-arm64
        PLATFORM=2
        TARGET_CFLAGS=""
        ;;
    iossimulator-arm64)
        GCC=ios-simulator
        PLATFORM=7
        # bx's ios-simulator config sets no target triple, so clang would build for macOS.
        TARGET_CFLAGS="-target arm64-apple-ios16.0-simulator -I$BX/include/compat/ios"
        ;;
    *)
        echo "Unsupported RID: $RID" >&2
        exit 1
        ;;
esac

# bx compiles with the Xcode at this path, whatever xcode-select points to.
if [ ! -d /Applications/Xcode.app ]; then
    echo "/Applications/Xcode.app not found" >&2
    exit 1
fi

GENIE="$BX/tools/bin/darwin/genie"
if [ ! -x "$GENIE" ]; then
    echo "genie not found or not executable at $GENIE" >&2
    exit 1
fi

NPROC="$(sysctl -n hw.ncpu)"
PROJ_DIR=".build/projects/gmake-$GCC"
BIN_DIR=".build/$GCC/bin"

cd "$BGFX"
echo "[build-native-ios] genie --gcc=$GCC gmake"
"$GENIE" --gcc="$GCC" gmake

sed -i '' 's/-fembed-bitcode//g' "$PROJ_DIR"/*.make

echo "[build-native-ios] make -C $PROJ_DIR config=release bx bimg bgfx -j$NPROC"
make -C "$PROJ_DIR" config=release -j"$NPROC" CFLAGS="$TARGET_CFLAGS" bx bimg bgfx

NATIVE_OUT="$REPO/artifacts/native/$RID"
mkdir -p "$NATIVE_OUT"
OUT="$NATIVE_OUT/libbgfx.a"
libtool -static -o "$OUT" "$BIN_DIR/libbgfxRelease.a" "$BIN_DIR/libbxRelease.a" "$BIN_DIR/libbimgRelease.a"

PLATFORMS="$(otool -l "$OUT" | awk '$1 == "platform" { print $2 }' | sort -u | tr '\n' ' ')"
if [ "$PLATFORMS" != "$PLATFORM " ]; then
    echo "$OUT holds objects for Mach-O platforms '$PLATFORMS', expected only $PLATFORM" >&2
    exit 1
fi

# No -q: grep must drain nm, or SIGPIPE under pipefail fails the build.
if ! nm -m "$OUT" 2>/dev/null | grep -E ' _bgfx_init$' >/dev/null; then
    echo "libbgfx.a does not define bgfx_init" >&2
    exit 1
fi

echo "[build-native-ios] platform and symbol checks OK — $RID staged."
ls -la "$NATIVE_OUT"
