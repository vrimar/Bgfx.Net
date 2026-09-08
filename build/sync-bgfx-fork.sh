#!/usr/bin/env bash
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
BGFX="$REPO/external/bgfx"
BRANCH="bgfx.net"
UPSTREAM_URL="https://github.com/bkaradzic/bgfx.git"

if ! git -C "$BGFX" remote get-url upstream >/dev/null 2>&1; then
    git -C "$BGFX" remote add upstream "$UPSTREAM_URL"
fi

git -C "$BGFX" fetch -q upstream master
git -C "$BGFX" fetch -q origin "$BRANCH"

if [ -n "$(git -C "$BGFX" status --porcelain)" ]; then
    echo "external/bgfx has local changes; commit or discard them first." >&2
    exit 1
fi

git -C "$BGFX" checkout -q "$BRANCH"
git -C "$BGFX" reset -q --hard "origin/$BRANCH"
# Rebase, never merge: the branch must stay exactly upstream master plus the carried fixes.
git -C "$BGFX" rebase -q upstream/master

echo "[sync-bgfx-fork] carried on top of upstream master:"
git -C "$BGFX" log --oneline "upstream/master..$BRANCH"

git -C "$BGFX" push -q --force-with-lease origin "$BRANCH"
echo "[sync-bgfx-fork] $BRANCH is at $(git -C "$BGFX" rev-parse --short HEAD); now bump the submodule pointer."
