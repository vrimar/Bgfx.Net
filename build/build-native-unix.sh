#!/usr/bin/env bash
# Builds bgfx-shared-lib + shaderc/texturec/geometryc on Linux or macOS.
#
# Usage: build-native-unix.sh <rid>
# Where <rid> is one of: linux-x64, linux-arm64, osx-x64, osx-arm64
#
# Stages artifacts into artifacts/native/<rid>/ and artifacts/tools/<rid>/.
set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <rid>" >&2
    echo "  rid: linux-x64 | linux-arm64 | osx-x64 | osx-arm64" >&2
    exit 1
fi

RID="$1"
REPO="$(cd "$(dirname "$0")/.." && pwd)"
BGFX="$REPO/external/bgfx"

# Map RID to genie's --gcc flag, make config, build subdir, and platform name.
case "$RID" in
    linux-x64)
        GENIE="$REPO/external/bx/tools/bin/linux/genie"
        # The bundled x86_64 genie may have been linked against a newer glibc
        # than the host (e.g. Ubuntu 22.04 ships glibc 2.35; the upstream
        # binary at time of writing needs 2.38). If it can't even start, fall
        # back to building genie from source — same mechanism as linux-arm64.
        if [ ! -x "$GENIE" ] || ! "$GENIE" >/dev/null 2>&1; then
            GENIE="$REPO/external/genie/bin/linux/genie"
            BUILD_GENIE_FROM_SOURCE=1
        fi
        GCC_FLAG="--gcc=linux-gcc"
        PROJ_DIR=".build/projects/gmake-linux-gcc"
        CONFIG="release64"
        BIN_DIR=".build/linux64_gcc/bin"
        SUFFIX="Release"
        LIB_EXT="so"
        ;;
    linux-arm64)
        # bx ships only an x86_64 genie binary, so on arm64 hosts we build genie
        # from source (external/genie). The native arm64 binary is then used by
        # bgfx's gmake action; bgfx itself builds natively as arm64 via gcc.
        GENIE="$REPO/external/genie/bin/linux/genie"
        GCC_FLAG="--gcc=linux-gcc"
        PROJ_DIR=".build/projects/gmake-linux-gcc"
        CONFIG="release64"
        BIN_DIR=".build/linux64_gcc/bin"
        SUFFIX="Release"
        LIB_EXT="so"
        BUILD_GENIE_FROM_SOURCE=1
        ;;
    osx-arm64)
        GENIE="$REPO/external/bx/tools/bin/darwin/genie"
        GCC_FLAG="--gcc=osx-arm64"
        PROJ_DIR=".build/projects/gmake-osx-arm64"
        CONFIG="release"
        BIN_DIR=".build/osx-arm64/bin"
        SUFFIX="Release"
        LIB_EXT="dylib"
        ;;
    osx-x64)
        GENIE="$REPO/external/bx/tools/bin/darwin/genie"
        GCC_FLAG="--gcc=osx-x64"
        PROJ_DIR=".build/projects/gmake-osx-x64"
        CONFIG="release"
        BIN_DIR=".build/osx-x64/bin"
        SUFFIX="Release"
        LIB_EXT="dylib"
        # bx toolchain.lua defaults macosPlatform to 10.13.6 when _ACTION is
        # "gmake", which clang turns into -target x86_64-apple-macos10.13.6.
        # That overrides MACOSX_DEPLOYMENT_TARGET env vars and makes
        # std::filesystem::absolute "unavailable" (introduced 10.15) — bgfx's
        # vendored glslang uses it. Raise the floor to 11.0.
        GENIE_EXTRA="--with-macos=11.0"
        ;;
    *)
        echo "Unsupported RID: $RID" >&2
        exit 1
        ;;
esac

if [ "${BUILD_GENIE_FROM_SOURCE:-0}" = "1" ] && [ ! -x "$GENIE" ]; then
    GENIE_SRC="$REPO/external/genie"
    if [ ! -d "$GENIE_SRC" ]; then
        echo "external/genie submodule missing — run 'git submodule update --init'" >&2
        exit 1
    fi
    # GENie's checked-in gmake.linux makefile hardcodes -m64 (an x86-only
    # flag), which native arm64 gcc rejects. Strip it before building so the
    # same source tree works on any 64-bit Linux host arch.
    HOST_ARCH="$(uname -m)"
    if [ "$HOST_ARCH" = "aarch64" ] || [ "$HOST_ARCH" = "arm64" ]; then
        sed -i 's/ -m64//g' "$GENIE_SRC/build/gmake.linux/genie.make"
    fi
    echo "[build-native-unix] building genie from source ($GENIE_SRC)"
    make -C "$GENIE_SRC" -j"$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 2)"
fi

if [ ! -x "$GENIE" ]; then
    echo "genie not found or not executable at $GENIE" >&2
    exit 1
fi

NPROC="$(getconf _NPROCESSORS_ONLN 2>/dev/null || sysctl -n hw.physicalcpu 2>/dev/null || echo 4)"

echo "[build-native-unix] cd $BGFX && genie $GCC_FLAG ${GENIE_EXTRA:-} --with-shared-lib --with-tools gmake"
cd "$BGFX"
"$GENIE" "$GCC_FLAG" ${GENIE_EXTRA:-} --with-shared-lib --with-tools gmake

echo "[build-native-unix] make -C $PROJ_DIR config=$CONFIG bgfx-shared-lib shaderc texturec geometryc -j$NPROC"
make -C "$PROJ_DIR" "config=$CONFIG" bgfx-shared-lib shaderc texturec geometryc -j"$NPROC"

# Locate produced binaries — bgfx names them <target><Suffix>[.<ext>].
SHARED="$BGFX/$BIN_DIR/libbgfx-shared-lib${SUFFIX}.${LIB_EXT}"
SHADERC="$BGFX/$BIN_DIR/shaderc${SUFFIX}"
TEXTUREC="$BGFX/$BIN_DIR/texturec${SUFFIX}"
GEOMETRYC="$BGFX/$BIN_DIR/geometryc${SUFFIX}"

for f in "$SHARED" "$SHADERC" "$TEXTUREC" "$GEOMETRYC"; do
    if [ ! -f "$f" ]; then
        echo "Expected output missing: $f" >&2
        ls -la "$BGFX/$BIN_DIR" >&2 || true
        exit 1
    fi
done

NATIVE_OUT="$REPO/artifacts/native/$RID"
TOOLS_OUT="$REPO/artifacts/tools/$RID"
mkdir -p "$NATIVE_OUT" "$TOOLS_OUT"

cp -f "$SHARED" "$NATIVE_OUT/libbgfx.${LIB_EXT}"
cp -f "$SHADERC" "$TOOLS_OUT/shaderc"
cp -f "$TEXTUREC" "$TOOLS_OUT/texturec"
cp -f "$GEOMETRYC" "$TOOLS_OUT/geometryc"

chmod +x "$TOOLS_OUT/shaderc" "$TOOLS_OUT/texturec" "$TOOLS_OUT/geometryc"

# Stage RID-agnostic shader headers so consumer .sc files can #include them.
INCLUDE_OUT="$REPO/artifacts/tools/include"
mkdir -p "$INCLUDE_OUT"
cp -f "$BGFX/src/bgfx_shader.sh"             "$INCLUDE_OUT/"
cp -f "$BGFX/src/bgfx_compute.sh"            "$INCLUDE_OUT/"
cp -f "$BGFX/examples/common/common.sh"      "$INCLUDE_OUT/"
cp -f "$BGFX/examples/common/shaderlib.sh"   "$INCLUDE_OUT/"

# Strip and produce separate .dbg files for the shared lib.
if [ "$LIB_EXT" = "so" ]; then
    SO="$NATIVE_OUT/libbgfx.so"
    objcopy --only-keep-debug "$SO" "${SO}.dbg" || true
    strip --strip-unneeded "$SO" || true
    objcopy --add-gnu-debuglink="${SO}.dbg" "$SO" || true

    if command -v patchelf >/dev/null 2>&1; then
        patchelf --set-soname libbgfx.so "$SO" || true
    fi
elif [ "$LIB_EXT" = "dylib" ]; then
    DY="$NATIVE_OUT/libbgfx.dylib"
    if command -v dsymutil >/dev/null 2>&1; then
        dsymutil "$DY" -o "${DY}.dSYM" || true
    fi
    strip -S "$DY" || true
fi

# Symbol export check.
# Note: grep -q exits on first match and sends SIGPIPE to nm; under pipefail
# nm's 141 then dominates the pipeline and falsely fails this check. Letting
# grep drain the input (no -q, discard stdout) avoids the SIGPIPE race.
if [ "$LIB_EXT" = "so" ]; then
    if ! nm -D --defined-only "$NATIVE_OUT/libbgfx.so" 2>/dev/null | grep -E '\bbgfx_init$' >/dev/null; then
        echo "libbgfx.so does not export bgfx_init" >&2
        exit 1
    fi
elif [ "$LIB_EXT" = "dylib" ]; then
    if ! nm -gU "$NATIVE_OUT/libbgfx.dylib" 2>/dev/null | grep -E '_bgfx_init$' >/dev/null; then
        echo "libbgfx.dylib does not export bgfx_init" >&2
        exit 1
    fi
fi

echo "[build-native-unix] symbol check OK — $RID artifacts staged."
ls -la "$NATIVE_OUT" "$TOOLS_OUT"
