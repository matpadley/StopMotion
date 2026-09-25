#!/bin/bash
# Builds StopMotion.app into StopMotionApp/build/.
# Usage: scripts/build-app.sh [--open]
set -euo pipefail

cd "$(dirname "$0")/.."

swift build -c release --product StopMotion
BIN_DIR="$(swift build -c release --show-bin-path)"

APP="build/StopMotion.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/StopMotion" "$APP/Contents/MacOS/StopMotion"
cp Support/Info.plist "$APP/Contents/Info.plist"

# Ad-hoc sign so Gatekeeper lets it run locally.
codesign --force --sign - "$APP" >/dev/null

echo "Built $(pwd)/$APP"
if [[ "${1:-}" == "--open" ]]; then
    open "$APP"
fi
