#!/usr/bin/env bash
# Cross-builds the bgfx static library for WebAssembly using Emscripten.
#
# Usage: build-native-wasm.sh
#
# Stages bgfx.a, bx.a and bimg.a into
# artifacts/native/browser-wasm/<emscripten-version>/.
#
# Naming a renderer in BGFX_CONFIG switches every other one off, so a WebGL2
# archive is BGFX_CONFIG=RENDERER_OPENGLES=30 plus -sMAX_WEBGL_VERSION=2 at link.
#
# Static, not shared: a browser-wasm consumer links the archive into
# dotnet.native.wasm at publish time, so there is nothing to load at runtime.
# Keyed by Emscripten version because wasm objects only link against a runtime
# pack built with the same emsdk.
#
# By default the toolchain is taken from the .NET wasm workload, which is the
# emsdk that must be matched. Override with DOTNET_ROOT, or set EMSDK_PATH and
# the DOTNET_EMSCRIPTEN_* vars to use an emsdk installed elsewhere.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
BGFX="$REPO/external/bgfx"
DOTNET_ROOT="${DOTNET_ROOT:-/usr/share/dotnet}"

if [ -z "${EMSDK_PATH:-}" ]; then
    SDK_PACK="$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NET.Runtime.Emscripten.*.Sdk.*/*/tools 2>/dev/null | sort -V | tail -1)"
    if [ -z "$SDK_PACK" ]; then
        echo "No Emscripten SDK pack under $DOTNET_ROOT/packs." >&2
        echo "  Install it with: dotnet workload install wasm-tools" >&2
        exit 1
    fi
    export EMSDK_PATH="$SDK_PACK"
    export DOTNET_EMSCRIPTEN_LLVM_ROOT="$SDK_PACK/bin"
    export DOTNET_EMSCRIPTEN_BINARYEN_ROOT="$SDK_PACK"
    export DOTNET_EMSCRIPTEN_NODE_JS="$(find "$DOTNET_ROOT"/packs/Microsoft.NET.Runtime.Emscripten.*.Node.*/ -name node -type f 2>/dev/null | sort -V | tail -1)"
    PACK_CACHE="$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NET.Runtime.Emscripten.*.Cache.*/*/tools/emscripten/cache 2>/dev/null | sort -V | tail -1)"
    # A copy: the pack's own is read-only and frozen, and --use-port builds into it.
    export EM_CACHE="$REPO/.build/emcache"
    if [ ! -d "$EM_CACHE" ]; then
        mkdir -p "$EM_CACHE"
        cp -r "$PACK_CACHE"/. "$EM_CACHE"/
    fi
    export FROZEN_CACHE=0
    export EM_FROZEN_CACHE=0
fi

if [ ! -x "${DOTNET_EMSCRIPTEN_NODE_JS:-}" ]; then
    echo "node not found — DOTNET_EMSCRIPTEN_NODE_JS=${DOTNET_EMSCRIPTEN_NODE_JS:-<unset>}" >&2
    exit 1
fi

# shellcheck source=/dev/null
source "$EMSDK_PATH/emsdk_env.sh" >/dev/null

export EMSCRIPTEN="$EMSDK_PATH/emscripten"
for tool in emcc em++ emar; do
    if [ ! -x "$EMSCRIPTEN/$tool" ]; then
        echo "$tool not found under $EMSCRIPTEN" >&2
        exit 1
    fi
done

EMVER="$(echo "$EMSDK_PATH" | grep -oE 'Emscripten\.[0-9]+\.[0-9]+\.[0-9]+' | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)"
if [ -z "$EMVER" ]; then
    EMVER="$("$EMSCRIPTEN/emcc" -v 2>&1 | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)"
fi
if [ -z "$EMVER" ]; then
    echo "Could not determine the Emscripten version from $EMSDK_PATH" >&2
    exit 1
fi

GENIE="$REPO/external/bx/tools/bin/linux/genie"
if [ ! -x "$GENIE" ]; then
    echo "genie not found or not executable at $GENIE" >&2
    exit 1
fi

NPROC="$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 4)"
PROJ_DIR=".build/projects/gmake-wasm"
BIN_DIR=".build/wasm/bin"

echo "[build-native-wasm] emscripten $EMVER at $EMSCRIPTEN"
cd "$BGFX"
BGFX_CONFIG="${BGFX_CONFIG:-RENDERER_WEBGPU=1}" "$GENIE" --gcc=wasm gmake

# A stale sanity stamp is cleared only after the port is unpacked into the cache,
# deleting it mid-build; settle the stamp before anything asks for the port.
mkdir -p "$REPO/.build"
printf 'int main(){return 0;}\n' > "$REPO/.build/sanity.c"
"$EMSCRIPTEN/emcc" "$REPO/.build/sanity.c" -o "$REPO/.build/sanity.o" -c

# bx reads __EMSCRIPTEN_MAJOR__ to derive BX_PLATFORM_EMSCRIPTEN, but emscripten
# stopped predefining it and the header it moved to spells it lowercase. Left
# undefined the platform reads as 0, so bx compiles its POSIX paths and fails on
# pthreads and nanosleep. Pull the header in and alias the three names.
export CPPFLAGS="${CPPFLAGS:-} -include emscripten/version.h"
export CPPFLAGS="$CPPFLAGS -D__EMSCRIPTEN_MAJOR__=__EMSCRIPTEN_major__"
export CPPFLAGS="$CPPFLAGS -D__EMSCRIPTEN_MINOR__=__EMSCRIPTEN_minor__"
export CPPFLAGS="$CPPFLAGS -D__EMSCRIPTEN_TINY__=__EMSCRIPTEN_tiny__"

export CPPFLAGS="$CPPFLAGS --use-port=emdawnwebgpu"

echo "[build-native-wasm] make -C $PROJ_DIR config=release bx bimg bgfx -j$NPROC"
make -C "$PROJ_DIR" config=release bx bimg bgfx -j"$NPROC"

NATIVE_OUT="$REPO/artifacts/native/browser-wasm/$EMVER"
mkdir -p "$NATIVE_OUT"

# bgfx links against bx and bimg, so all three archives have to ship or the
# consumer's link is undefined. Named without a lib prefix because the wasm
# pinvoke table matches [DllImport("bgfx")] against the archive's file stem.
for lib in bgfx bx bimg; do
    SRC="$BGFX/$BIN_DIR/${lib}Release.a"
    if [ ! -f "$SRC" ]; then
        echo "Expected $SRC" >&2
        ls -la "$BGFX/$BIN_DIR" >&2 || true
        exit 1
    fi
    cp -f "$SRC" "$NATIVE_OUT/${lib}.a"
done
OUT="$NATIVE_OUT/bgfx.a"

# Catches a host toolchain silently building a native archive instead. od, not
# grep: the wasm magic leads with a NUL byte.
if [ "$(ar p "$OUT" "$(ar t "$OUT" | head -1)" | od -An -tx1 -N4 | tr -d ' ')" != "0061736d" ]; then
    echo "$OUT does not contain wasm objects" >&2
    exit 1
fi

# grep without -q drains nm's output so an early SIGPIPE under pipefail can't
# masquerade as a build failure (see build-native-unix.sh).
if ! "$DOTNET_EMSCRIPTEN_LLVM_ROOT/llvm-nm" --defined-only "$OUT" 2>/dev/null | grep -E '\bbgfx_init$' >/dev/null; then
    echo "bgfx.a does not define bgfx_init" >&2
    exit 1
fi

echo "[build-native-wasm] symbol check OK — browser-wasm/$EMVER staged."
ls -la "$NATIVE_OUT"
