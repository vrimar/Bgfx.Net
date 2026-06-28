#!/usr/bin/env bash
# Initialize submodules and ensure the repo is in a buildable state.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"

echo "[bootstrap] Updating submodules under $REPO/external"
git -C "$REPO" submodule update --init --recursive

echo "[bootstrap] Submodule status:"
git -C "$REPO" submodule status --recursive
