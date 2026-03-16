#!/usr/bin/env bash
# build_android_debug.sh - builds APK for testing
#
# Prerequisites:
#   1. Complete ANDROID_SETUP.md instructions
#   2. Android SDK, JDK installed and configured in Godot
#   3. Android build template installed
#
# Output: export/SlotTheory.apk (debug version for testing)

set -e

GODOT="E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe"
PROJECT="$(cd "$(dirname "$0")" && pwd)"
EXPORT_DIR="$PROJECT/export"

echo "=== [1/2] Building C# ==="
dotnet build "$PROJECT/SlotTheory.sln"

echo ""
echo "=== [2/2] Exporting Android APK (debug) ==="
mkdir -p "$EXPORT_DIR"
"$GODOT" --headless --path "$PROJECT" --export-debug "Android" "$EXPORT_DIR/SlotTheory.apk"

echo ""
echo "=== Done ==="
ls -lh "$EXPORT_DIR/SlotTheory.apk" 2>/dev/null || echo "APK not found - check errors above"