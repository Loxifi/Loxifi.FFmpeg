#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RELEASE_DIR="$SCRIPT_DIR/release"
CONFIGURATION="${1:-Release}"

echo "Building Loxifi.FFmpeg ($CONFIGURATION)..."

# Clean release folder
rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

# Build and pack core library
echo "=== Loxifi.FFmpeg ==="
dotnet pack "$SCRIPT_DIR/src/Loxifi.FFmpeg/Loxifi.FFmpeg.csproj" \
    -c "$CONFIGURATION" \
    -o "$RELEASE_DIR" \
    --nologo

# Build and pack runtime packages
for RID in win-x64 linux-x64 android-arm64; do
    echo "=== Loxifi.FFmpeg.Runtime.$RID ==="
    dotnet pack "$SCRIPT_DIR/src/Loxifi.FFmpeg.Runtime.$RID/Loxifi.FFmpeg.Runtime.$RID.csproj" \
        -c "$CONFIGURATION" \
        -o "$RELEASE_DIR" \
        --nologo
done

# Build desktop tests
echo "=== Loxifi.FFmpeg.Tests ==="
dotnet build "$SCRIPT_DIR/tests/Loxifi.FFmpeg.Tests/Loxifi.FFmpeg.Tests.csproj" \
    -c "$CONFIGURATION" \
    --nologo

# Build Android tests (requires AndroidSdkDirectory and JavaSdkDirectory)
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
