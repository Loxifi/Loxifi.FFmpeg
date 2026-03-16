#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ADB="${ANDROID_SDK_DIR:-/home/service-account/android-sdk}/platform-tools/adb"
CONFIGURATION="${1:-Release}"
FAILED=0

run_desktop_tests() {
    local license="$1"
    local use_gpl="false"
    [ "$license" = "GPL" ] && use_gpl="true"

    echo "=== Desktop Tests ($license) ==="
    dotnet test "$SCRIPT_DIR/tests/Loxifi.FFmpeg.Tests/Loxifi.FFmpeg.Tests.csproj" \
        -c "$CONFIGURATION" -p:UseGPL=$use_gpl --nologo || { FAILED=1; echo "DESKTOP $license FAILED"; }
}

run_android_tests() {
    local license="$1"
    local use_gpl="false"
    [ "$license" = "GPL" ] && use_gpl="true"

    echo ""
    echo "=== Android Tests ($license) ==="

    if ! "$ADB" get-state >/dev/null 2>&1; then
        echo "No Android device connected. Skipping."
        return
    fi

    DEVICE=$("$ADB" devices -l | grep "device " | head -1 | awk '{print $1}')
    echo "Device: $DEVICE"

    dotnet publish "$SCRIPT_DIR/tests/Loxifi.FFmpeg.AndroidTests/Loxifi.FFmpeg.AndroidTests.csproj" \
        -c "$CONFIGURATION" \
        -p:AndroidSdkDirectory="${ANDROID_SDK_DIR:-/home/service-account/android-sdk}" \
        -p:JavaSdkDirectory="${JAVA_HOME:-/usr/lib/jvm/java-17-openjdk-amd64}" \
        -p:UseGPL=$use_gpl \
        --nologo

    APK="$SCRIPT_DIR/tests/Loxifi.FFmpeg.AndroidTests/bin/$CONFIGURATION/net9.0-android/publish/com.loxifi.ffmpeg.tests-Signed.apk"

    "$ADB" uninstall com.loxifi.ffmpeg.tests 2>/dev/null || true
    "$ADB" install "$APK"
    "$ADB" logcat -c
    "$ADB" shell am start -n com.loxifi.ffmpeg.tests/crc64605637f7dac5db88.MainActivity

    echo "Waiting for tests..."
    for i in $(seq 1 30); do
        sleep 1
        if "$ADB" logcat -d -s "FFmpegTests:*" 2>/dev/null | grep -q "TEST_RUN_COMPLETE"; then
            break
        fi
    done

    echo ""
    "$ADB" logcat -d -s "FFmpegTests:*" 2>/dev/null

    if "$ADB" logcat -d -s "FFmpegTests:*" 2>/dev/null | grep -q "TEST_RUN_COMPLETE: SUCCESS"; then
        echo "Android $license PASSED"
    else
        echo "Android $license FAILED"
        FAILED=1
    fi
}

# Run all 4 test configurations
run_desktop_tests "LGPL"
run_desktop_tests "GPL"
run_android_tests "LGPL"
run_android_tests "GPL"

echo ""
if [ $FAILED -eq 0 ]; then
    echo "========================================="
    echo "  ALL TESTS PASSED (LGPL + GPL, Desktop + Android)"
    echo "========================================="
else
    echo "========================================="
    echo "  SOME TESTS FAILED"
    echo "========================================="
    exit 1
fi
