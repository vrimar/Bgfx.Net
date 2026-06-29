#!/usr/bin/env bash
# Cross-builds the bgfx shared library for Android using the NDK clang toolchain.
#
# Usage: build-native-android.sh <rid>
# Where <rid> is one of: android-arm64, android-x64
#
# Stages the shared lib into artifacts/native/<rid>/. No host tools are built —
# shaderc/texturec/geometryc are build-host binaries and have no Android target.
#
# Requires ANDROID_NDK_ROOT pointing at an installed NDK. bx's toolchain.lua
# reads only this var and derives the clang/llvm path itself; do not set
# ANDROID_NDK_CLANG or per-arch variables.
set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <rid>" >&2
    echo "  rid: android-arm64 | android-x64" >&2
    exit 1
fi

RID="$1"
REPO="$(cd "$(dirname "$0")/.." && pwd)"
BGFX="$REPO/external/bgfx"

if [ -z "${ANDROID_NDK_ROOT:-}" ]; then
    echo "ANDROID_NDK_ROOT is not set — point it at an installed Android NDK." >&2
    echo "  e.g. export ANDROID_NDK_ROOT=\$HOME/Android/Sdk/ndk/<version>" >&2
    exit 1
fi

NDK_BIN="$ANDROID_NDK_ROOT/toolchains/llvm/prebuilt/linux-x86_64/bin"
if [ ! -x "$NDK_BIN/llvm-strip" ]; then
    echo "NDK llvm tools not found under $NDK_BIN" >&2
    echo "  ANDROID_NDK_ROOT=$ANDROID_NDK_ROOT may be wrong or not a linux-x86_64 NDK." >&2
    exit 1
fi

# Map RID to genie's --gcc action and the resulting build/output subdirs. The
# arch is baked into the action, so the make config carries no 32/64 suffix
# (named-arch actions like osx-arm64 use plain "release").
case "$RID" in
    android-arm64)
        GCC_FLAG="--gcc=android-arm64"
        PROJ_DIR=".build/projects/gmake-android-arm64"
        BIN_DIR=".build/android-arm64/bin"
        ;;
    android-x64)
        GCC_FLAG="--gcc=android-x86_64"
        PROJ_DIR=".build/projects/gmake-android-x86_64"
        BIN_DIR=".build/android-x86_64/bin"
        ;;
    *)
        echo "Unsupported RID: $RID" >&2
        exit 1
        ;;
esac

GENIE="$REPO/external/bx/tools/bin/linux/genie"
if [ ! -x "$GENIE" ]; then
    echo "genie not found or not executable at $GENIE" >&2
    exit 1
fi

NPROC="$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 4)"

echo "[build-native-android] cd $BGFX && genie $GCC_FLAG --with-shared-lib gmake"
cd "$BGFX"
"$GENIE" "$GCC_FLAG" --with-shared-lib gmake

# bx's android config links the C++ runtime dynamically (-lc++_shared), leaving
# libbgfx.so with a runtime dependency on libc++_shared.so that consumers would
# have to ship. Static-link libc++ into the shared lib instead so it is
# self-contained, and hide the libc++ symbols so they can't clash with a
# different libc++ already loaded in the consumer's process (e.g. the .NET
# Android runtime). Patch the genie-generated makefile in place.
SHARED_MAKE="$PROJ_DIR/bgfx-shared-lib.make"
if ! grep -q -- '-lc++_shared' "$SHARED_MAKE"; then
    echo "Expected -lc++_shared in $SHARED_MAKE — bx android config changed; revisit static-libc++ patch." >&2
    exit 1
fi
sed -i 's/-lc++_shared/-static-libstdc++ -Wl,--exclude-libs,libc++_static.a -Wl,--exclude-libs,libc++abi.a/g' "$SHARED_MAKE"

# Named-arch Android actions use config=release; genie's gmake Makefiles reject
# an unknown config, so probe with a dry run and fall back to release64.
CONFIG="release"
if ! make -C "$PROJ_DIR" -n "config=release" bgfx-shared-lib >/dev/null 2>&1; then
    CONFIG="release64"
fi

echo "[build-native-android] make -C $PROJ_DIR config=$CONFIG bgfx-shared-lib -j$NPROC"
make -C "$PROJ_DIR" "config=$CONFIG" bgfx-shared-lib -j"$NPROC"

SHARED="$BGFX/$BIN_DIR/libbgfx-shared-libRelease.so"
if [ ! -f "$SHARED" ]; then
    echo "Expected output missing: $SHARED" >&2
    ls -la "$BGFX/$BIN_DIR" >&2 || true
    exit 1
fi

NATIVE_OUT="$REPO/artifacts/native/$RID"
mkdir -p "$NATIVE_OUT"
SO="$NATIVE_OUT/libbgfx.so"
cp -f "$SHARED" "$SO"

# Split debug info and strip, using NDK llvm tools (host binutils mishandle a
# cross-arch ELF).
"$NDK_BIN/llvm-objcopy" --only-keep-debug "$SO" "${SO}.dbg" || true
"$NDK_BIN/llvm-strip" --strip-unneeded "$SO" || true
"$NDK_BIN/llvm-objcopy" --add-gnu-debuglink="${SO}.dbg" "$SO" || true

if command -v patchelf >/dev/null 2>&1; then
    patchelf --set-soname libbgfx.so "$SO" || true
fi

# Symbol export check. grep without -q drains nm's output so an early SIGPIPE
# under pipefail can't masquerade as a build failure (see build-native-unix.sh).
if ! "$NDK_BIN/llvm-nm" -D --defined-only "$SO" 2>/dev/null | grep -E '\bbgfx_init$' >/dev/null; then
    echo "libbgfx.so does not export bgfx_init" >&2
    exit 1
fi

echo "[build-native-android] symbol check OK — $RID artifacts staged."
ls -la "$NATIVE_OUT"
