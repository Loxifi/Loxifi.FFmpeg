#!/bin/bash
# ============================================================================
# build.sh — Build and pack all Loxifi.FFmpeg NuGet packages
# ============================================================================
#
# Builds the core P/Invoke library, all platform-specific runtime packages,
# and both test projects (desktop + Android).
#
# Usage:
#   ./build.sh [Configuration]
#
# Arguments:
#   $1  Build configuration: "Release" or "Debug". Default: Release
#
# Prerequisites:
#   - .NET 9 SDK
#   - Android SDK with appropriate platform tools (for Android test build)
#   - Java SDK (for Android test build)
#
# Environment variables:
#   ANDROID_SDK_DIR  Path to Android SDK (used for Android test project)
#   ANDROID_HOME     Fallback for ANDROID_SDK_DIR
#   JAVA_HOME        Path to Java SDK (used for Android test project)
#
# Output:
#   NuGet packages (.nupkg) are placed in ./release/
# ============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RELEASE_DIR="$SCRIPT_DIR/release"
CONFIGURATION="${1:-Release}"

echo "Building Loxifi.FFmpeg ($CONFIGURATION)..."

# Clean and recreate the release output folder
rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

# Pack the core P/Invoke library
echo "=== Loxifi.FFmpeg ==="
dotnet pack "$SCRIPT_DIR/src/Loxifi.FFmpeg/Loxifi.FFmpeg.csproj" \
    -c "$CONFIGURATION" \
    -o "$RELEASE_DIR" \
    --nologo

# Pack platform-specific runtime packages (contain native FFmpeg shared libraries)
for RID in win-x64 linux-x64 android-arm64; do
    echo "=== Loxifi.FFmpeg.Runtime.$RID ==="
    dotnet pack "$SCRIPT_DIR/src/Loxifi.FFmpeg.Runtime.$RID/Loxifi.FFmpeg.Runtime.$RID.csproj" \
        -c "$CONFIGURATION" \
        -o "$RELEASE_DIR" \
        --nologo
done

# Build desktop test project
echo "=== Loxifi.FFmpeg.Tests ==="
dotnet build "$SCRIPT_DIR/tests/Loxifi.FFmpeg.Tests/Loxifi.FFmpeg.Tests.csproj" \
    -c "$CONFIGURATION" \
    --nologo

# Build Android test project (requires Android SDK and Java SDK paths)
echo "=== Loxifi.FFmpeg.AndroidTests ==="
ANDROID_SDK_ARGS=""
if [ -n "$ANDROID_SDK_DIR" ]; then
    ANDROID_SDK_ARGS="-p:AndroidSdkDirectory=$ANDROID_SDK_DIR"
elif [ -n "$ANDROID_HOME" ]; then
    ANDROID_SDK_ARGS="-p:AndroidSdkDirectory=$ANDROID_HOME"
fi

JAVA_SDK_ARGS=""
if [ -n "$JAVA_HOME" ]; then
    JAVA_SDK_ARGS="-p:JavaSdkDirectory=$JAVA_HOME"
fi

dotnet build "$SCRIPT_DIR/tests/Loxifi.FFmpeg.AndroidTests/Loxifi.FFmpeg.AndroidTests.csproj" \
    -c "$CONFIGURATION" \
    $ANDROID_SDK_ARGS \
    $JAVA_SDK_ARGS \
    --nologo

echo ""
echo "Build complete. Packages in: $RELEASE_DIR"
ls -1 "$RELEASE_DIR"
