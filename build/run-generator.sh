#!/usr/bin/env bash
# Builds the Bgfx.Net.Generator tool and runs it to produce bgfx.g.cs from bgfx.raw.cs.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"

generatorProj="$REPO/src/Bgfx.Net.Generator/Bgfx.Net.Generator.csproj"
raw="$REPO/src/Bgfx.Net/Generated/bgfx.raw.cs"
out="$REPO/src/Bgfx.Net/Generated/bgfx.g.cs"

if [ ! -f "$raw" ]; then
    echo "bgfx.raw.cs not found at $raw. Run build/sync-bindings.sh first." >&2
    exit 1
fi

echo "[run-generator] Building generator and emitting bgfx.g.cs"
dotnet run --project "$generatorProj" --configuration Release -- "$raw" "$out"
