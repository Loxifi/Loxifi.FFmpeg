#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ADB="${ANDROID_SDK_DIR:-/home/service-account/android-sdk}/platform-tools/adb"
CONFIGURATION="${1:-Release}"
FAILED=0
LOCAL_FEED="$SCRIPT_DIR/local-feed"
PKG_TEST_DIR="$SCRIPT_DIR/tests/Loxifi.FFmpeg.PackageTests"
TEST_VERSION="0.0.0-test.$(date +%s)"

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

run_package_tests() {
    echo ""
    echo "=== Package Integration Tests ==="
    echo "Packing to local feed ($TEST_VERSION)..."

    rm -rf "$LOCAL_FEED"
    mkdir -p "$LOCAL_FEED"

    # Pack all packages to local feed
    for proj in \
        src/Loxifi.FFmpeg/Loxifi.FFmpeg.csproj \
        src/Loxifi.FFmpeg.Runtime.linux-x64/Loxifi.FFmpeg.Runtime.linux-x64.csproj \
        src/Loxifi.FFmpeg.Runtime.linux-x64.GPL/Loxifi.FFmpeg.Runtime.linux-x64.GPL.csproj \
        src/Loxifi.FFmpeg.Runtime.win-x64/Loxifi.FFmpeg.Runtime.win-x64.csproj \
        src/Loxifi.FFmpeg.Runtime.win-x64.GPL/Loxifi.FFmpeg.Runtime.win-x64.GPL.csproj; do
        dotnet pack "$SCRIPT_DIR/$proj" -c Release -o "$LOCAL_FEED" \
            -p:PackageVersion="$TEST_VERSION" --nologo -v q
    done

    echo "Packed $(ls "$LOCAL_FEED"/*.nupkg | wc -l) packages"

    # Clear NuGet cache for our packages to force fresh restore
    dotnet nuget locals http-cache --clear > /dev/null 2>&1 || true
    rm -rf "$PKG_TEST_DIR/bin" "$PKG_TEST_DIR/obj"

    # Test LGPL on Linux
    echo ""
    echo "--- Package Test: Linux LGPL ---"
    dotnet run --project "$PKG_TEST_DIR" -c Release \
        -p:FFmpegPackageVersion="$TEST_VERSION" -p:GPLSuffix="" \
        --nologo 2>&1 || { FAILED=1; echo "PACKAGE TEST Linux LGPL FAILED"; }

    # Test GPL on Linux
    rm -rf "$PKG_TEST_DIR/bin" "$PKG_TEST_DIR/obj"
    echo ""
    echo "--- Package Test: Linux GPL ---"
    dotnet run --project "$PKG_TEST_DIR" -c Release \
        -p:FFmpegPackageVersion="$TEST_VERSION" -p:GPLSuffix=".GPL" \
        --nologo 2>&1 || { FAILED=1; echo "PACKAGE TEST Linux GPL FAILED"; }

    # Clean up
    rm -rf "$LOCAL_FEED" "$PKG_TEST_DIR/bin" "$PKG_TEST_DIR/obj"
}

# Run all test configurations
run_desktop_tests "LGPL"
run_desktop_tests "GPL"
run_package_tests
run_android_tests "LGPL"
run_android_tests "GPL"

echo ""
if [ $FAILED -eq 0 ]; then
    echo "========================================="
    echo "  ALL TESTS PASSED"
    echo "========================================="
else
    echo "========================================="
    echo "  SOME TESTS FAILED"
    echo "========================================="
    exit 1
fi
