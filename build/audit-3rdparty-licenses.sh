#!/usr/bin/env bash
# Usage: audit-3rdparty-licenses.sh [--check | --write]
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
NOTICES="$REPO/THIRD-PARTY-NOTICES.md"
MARKER='<!-- 3rdparty-inventory -->'

# All matching families, not just the first: glslang and libavif ship one file covering several.
classify() {
    local text found=()
    text="$(tr '[:upper:]' '[:lower:]' | tr -d '\r' | tr '\n' ' ')"

    case "$text" in *"apache license"*|*"spdx-license-identifier: apache-2.0"*) found+=("Apache-2.0") ;; esac
    case "$text" in *"gnu general public"*) found+=("GPL") ;; esac
    case "$text" in *"mozilla public license"*) found+=("MPL-2.0") ;; esac
    case "$text" in *"boost software license"*) found+=("BSL-1.0") ;; esac
    case "$text" in *"do what the fuck you want"*|*"wtfpl"*) found+=("WTFPL") ;; esac
    case "$text" in *"permission to use, copy, modify, and/or distribute"*) found+=("ISC") ;; esac
    case "$text" in
        *"permission is hereby granted, free of charge"*|*"spdx-license-identifier: mit"*) found+=("MIT") ;;
    esac
    case "$text" in *"neither the name"*) found+=("BSD-3-Clause") ;; esac
    case "$text" in
        *"redistribution and use in source and binary forms"*)
            case "$text" in *"neither the name"*) ;; *) found+=("BSD-2-Clause") ;; esac
            ;;
    esac
    case "$text" in *"altered source versions must be plainly marked"*) found+=("Zlib") ;; esac
    case "$text" in *"public domain"*|*"unlicense"*) found+=("Public domain / Unlicense") ;; esac

    if [ "${#found[@]}" -eq 0 ]; then
        echo "unclassified"
    else
        printf '%s' "${found[0]}"
        for ((i = 1; i < ${#found[@]}; i++)); do printf ', %s' "${found[i]}"; done
        echo
    fi
}

license_file() {
    find "$1" -maxdepth 1 -type f \
        \( -iname '*licen[cs]e*' -o -iname '*copying*' -o -iname 'notice*' \) \
        | sort | head -1
}

source_headers() {
    find "$1" -maxdepth 2 -type f \( -name '*.h' -o -name '*.hpp' -o -name '*.c' -o -name '*.cpp' \) \
        | sort | head -4 | xargs -r head -40
}

inventory() {
    echo "$MARKER"
    echo
    echo "| Component | Bundled in | License | Identified from |"
    echo "| --- | --- | --- | --- |"
    for repo in bgfx bx bimg; do
        root="$REPO/external/$repo/3rdparty"
        [ -d "$root" ] || continue
        while IFS= read -r dir; do
            file="$(license_file "$dir")"
            if [ -n "$file" ]; then
                license="$(classify < "$file")"
                source="${file#"$REPO/"}"
            else
                license="$(source_headers "$dir" | classify)"
                source="source headers"
            fi
            printf '| %s | %s | %s | %s |\n' \
                "$(basename "$dir")" "$repo" "$license" "$source"
        done < <(find "$root" -mindepth 1 -maxdepth 1 -type d | sort)
    done
}

require_marker() {
    if ! grep -qF "$MARKER" "$NOTICES"; then
        echo "Inventory marker not found in $NOTICES." >&2
        exit 1
    fi
}

case "${1:-}" in
    --check)
        require_marker
        current="$(sed -n "/$MARKER/,\$p" "$NOTICES")"
        if [ "$current" != "$(inventory)" ]; then
            echo "THIRD-PARTY-NOTICES.md is stale. Re-run build/audit-3rdparty-licenses.sh --write." >&2
            diff <(echo "$current") <(inventory) >&2 || true
            exit 1
        fi
        echo "[audit-3rdparty-licenses] THIRD-PARTY-NOTICES.md is up-to-date."
        ;;
    --write)
        require_marker
        tmp="$(mktemp)"
        sed "/$MARKER/,\$d" "$NOTICES" > "$tmp"
        inventory >> "$tmp"
        mv "$tmp" "$NOTICES"
        echo "[audit-3rdparty-licenses] Updated $NOTICES."
        ;;
    "")
        inventory
        ;;
    *)
        echo "Usage: $0 [--check | --write]" >&2
        exit 1
        ;;
esac
