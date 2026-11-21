#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
    echo "Usage: $0 <Bgfx.Net version> <package source>" >&2
    exit 1
fi

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="$HERE/Bgfx.Net.IosSmoke.csproj"
BUNDLE=net.bgfx.iossmoke
COMMON=(-c Release -p:_IsPublishing=true "-p:BgfxNetVersion=$1" "-p:RestoreAdditionalProjectSources=$2")

dotnet build "$PROJ" -r ios-arm64 -p:EnableCodeSigning=false "${COMMON[@]}"
dotnet build "$PROJ" -r iossimulator-arm64 "${COMMON[@]}"
APP="$HERE/bin/Release/net11.0-ios/iossimulator-arm64/Bgfx.Net.IosSmoke.app"

UDID=$(xcrun simctl list -j | python3 -I -c '
import json, sys
data = json.load(sys.stdin)
runtimes = [r for r in data["runtimes"] if r.get("isAvailable") and r["name"].startswith("iOS ")]
runtimes.sort(key=lambda r: [int(p) for p in r["version"].split(".")])
for runtime in reversed(runtimes):
    phones = [d for d in data["devices"].get(runtime["identifier"], []) if d.get("isAvailable") and d["name"].startswith("iPhone")]
    if phones:
        print(phones[0]["udid"])
        break
')
if [ -z "$UDID" ]; then
    echo "No available iPhone simulator" >&2
    exit 1
fi

xcrun simctl boot "$UDID" 2>/dev/null || true
xcrun simctl bootstatus "$UDID" -b
xcrun simctl install "$UDID" "$APP"
RESULT="$(xcrun simctl get_app_container "$UDID" "$BUNDLE" data)/Documents/smoke.txt"
rm -f "$RESULT"

xcrun simctl launch --console --terminate-running-process "$UDID" "$BUNDLE" &
LAUNCH=$!
for _ in $(seq 1 120); do
    if grep -qx "done" "$RESULT" 2>/dev/null; then
        break
    fi
    sleep 1
done
kill "$LAUNCH" 2>/dev/null || true
xcrun simctl terminate "$UDID" "$BUNDLE" 2>/dev/null || true

if [ ! -f "$RESULT" ]; then
    echo "The app wrote no result: it crashed or hung before starting bgfx" >&2
    exit 1
fi
cat "$RESULT"
grep -qx "noop=ok renderer=Noop" "$RESULT"
grep -qx "metal=ok renderer=Metal" "$RESULT"
