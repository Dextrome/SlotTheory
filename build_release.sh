#!/usr/bin/env bash
# build_release.sh - builds C# and exports a Windows release via Godot headless.
#
# Prerequisites (one-time setup):
#   1. Open Godot editor → Editor → Manage Export Templates → Download and Install
#      (must match the exact Godot version: 4.6.1 stable mono)
#   2. Run this script from the project root or anywhere.
#
# Output: export/SlotTheory.exe  +  export/SlotTheory_Data/ (bundled .NET runtime)

set -e

GODOT="E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe"
PROJECT="$(cd "$(dirname "$0")" && pwd)"
EXPORT_DIR="$PROJECT/export"

echo "=== [1/2] Building C# ==="
# Godot-generated .sln only has Debug config; Godot compiles Release internally during export
dotnet build "$PROJECT/SlotTheory.sln"

echo ""
echo "=== [2/2] Exporting Windows Desktop (headless) ==="
mkdir -p "$EXPORT_DIR"
"$GODOT" --headless --path "$PROJECT" --export-release "Windows Desktop" "$EXPORT_DIR/SlotTheory.exe"

echo ""
echo "=== Done ==="
ls -lh "$EXPORT_DIR"
