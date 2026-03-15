#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ADB="${ANDROID_SDK_DIR:-/home/service-account/android-sdk}/platform-tools/adb"
CONFIGURATION="${1:-Release}"

echo "=== Desktop Tests ==="
dotnet test "$SCRIPT_DIR/tests/Loxifi.FFmpeg.Tests/Loxifi.FFmpeg.Tests.csproj" -c "$CONFIGURATION"

echo ""
echo "=== Android Tests ==="

if ! "$ADB" get-state >/dev/null 2>&1; then
    echo "No Android device connected. Skipping Android tests."
    echo "Connect a device via 'adb pair' + 'adb connect' first."
    exit 0
fi

DEVICE=$("$ADB" devices -l | grep "device " | head -1 | awk '{print $1}')
echo "Device: $DEVICE"

# Build APK
dotnet publish "$SCRIPT_DIR/tests/Loxifi.FFmpeg.AndroidTests/Loxifi.FFmpeg.AndroidTests.csproj" \
    -c "$CONFIGURATION" \
    -p:AndroidSdkDirectory="${ANDROID_SDK_DIR:-/home/service-account/android-sdk}" \
    -p:JavaSdkDirectory="${JAVA_HOME:-/usr/lib/jvm/java-17-openjdk-amd64}" \
    --nologo

APK="$SCRIPT_DIR/tests/Loxifi.FFmpeg.AndroidTests/bin/$CONFIGURATION/net9.0-android/publish/com.loxifi.ffmpeg.tests-Signed.apk"

# Deploy
"$ADB" uninstall com.loxifi.ffmpeg.tests 2>/dev/null || true
"$ADB" install "$APK"

# Run
"$ADB" logcat -c
"$ADB" shell am start -n com.loxifi.ffmpeg.tests/crc64605637f7dac5db88.MainActivity

echo "Waiting for tests to complete..."
for i in $(seq 1 30); do
    sleep 1
    if "$ADB" logcat -d -s "FFmpegTests:*" 2>/dev/null | grep -q "TEST_RUN_COMPLETE"; then
        break
    fi
done

echo ""
"$ADB" logcat -d -s "FFmpegTests:*" 2>/dev/null

# Check result
if "$ADB" logcat -d -s "FFmpegTests:*" 2>/dev/null | grep -q "TEST_RUN_COMPLETE: SUCCESS"; then
    echo ""
    echo "Android tests PASSED"
else
    echo ""
    echo "Android tests FAILED"
    exit 1
fi
