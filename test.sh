#!/bin/bash
# ============================================================================
# test.sh — Run all test suites for Loxifi.FFmpeg
# ============================================================================
#
# Runs desktop unit tests (LGPL + GPL), package integration tests, and
# Android on-device tests. Each suite is run for both LGPL and GPL variants.
#
# Usage:
#   ./test.sh [Configuration]
#
# Arguments:
#   $1  Build configuration: "Release" or "Debug". Default: Release
#
# Prerequisites:
#   - .NET 9 SDK
#   - Android device connected via ADB (for Android tests; skipped if absent)
#   - WSL with Windows dotnet on PATH (for Windows package tests; skipped if absent)
#
# Environment variables:
#   ANDROID_SDK_DIR  Path to Android SDK (default: /home/service-account/android-sdk)
#   JAVA_HOME        Path to Java SDK (default: /usr/lib/jvm/java-17-openjdk-amd64)
#
# Exit code:
#   0 if all tests pass, 1 if any test fails
# ============================================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ADB="${ANDROID_SDK_DIR:-/home/service-account/android-sdk}/platform-tools/adb"
CONFIGURATION="${1:-Release}"
FAILED=0
LOCAL_FEED="$SCRIPT_DIR/local-feed"
PKG_TEST_DIR="$SCRIPT_DIR/tests/Loxifi.FFmpeg.PackageTests"
TEST_VERSION="0.0.0-test.$(date +%s)"

# ── Desktop unit tests ────────────────────────────────────────────────────────
# Runs the dotnet test project for the given license variant.

run_desktop_tests() {
    local license="$1"
    local use_gpl="false"
    [ "$license" = "GPL" ] && use_gpl="true"

    echo "=== Desktop Tests ($license) ==="
    dotnet test "$SCRIPT_DIR/tests/Loxifi.FFmpeg.Tests/Loxifi.FFmpeg.Tests.csproj" \
        -c "$CONFIGURATION" -p:UseGPL=$use_gpl --nologo || { FAILED=1; echo "DESKTOP $license FAILED"; }
}

# ── Android on-device tests ──────────────────────────────────────────────────
# Publishes the Android test APK, installs it on a connected device via ADB,
# launches it, and monitors logcat for test results.

run_android_tests() {
    local license="$1"
    local use_gpl="false"
    [ "$license" = "GPL" ] && use_gpl="true"

    echo ""
    echo "=== Android Tests ($license) ==="

    # Skip if no device is connected
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

    # Install and launch the test APK
    "$ADB" uninstall com.loxifi.ffmpeg.tests 2>/dev/null || true
    "$ADB" install "$APK"
    "$ADB" logcat -c
    "$ADB" shell am start -n com.loxifi.ffmpeg.tests/crc64605637f7dac5db88.MainActivity

    # Poll logcat for up to 30 seconds waiting for test completion
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

# ── Package integration tests ────────────────────────────────────────────────
# Packs all NuGet packages to a local feed with a unique test version, then
# runs the PackageTests project against them. This verifies that consumers can
# install and use the packages end-to-end. Runs on both Linux and Windows (WSL).

run_package_tests() {
    echo ""
    echo "=== Package Integration Tests ==="
    echo "Packing to local feed ($TEST_VERSION)..."

    rm -rf "$LOCAL_FEED"
    mkdir -p "$LOCAL_FEED"

    # Pack all packages to a temporary local NuGet feed
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

    # Clear NuGet HTTP cache to force fresh restore from local feed
    dotnet nuget locals http-cache --clear > /dev/null 2>&1 || true
    rm -rf "$PKG_TEST_DIR/bin" "$PKG_TEST_DIR/obj"

    # Convert WSL path to Windows path for cross-OS package tests
    local WIN_PKG_TEST_DIR
    WIN_PKG_TEST_DIR=$(wslpath -w "$PKG_TEST_DIR" 2>/dev/null || echo "")

    # Test LGPL variant on Linux
    echo ""
    echo "--- Package Test: Linux LGPL ---"
    dotnet run --project "$PKG_TEST_DIR" -c Release \
        -p:FFmpegPackageVersion="$TEST_VERSION" -p:GPLSuffix="" \
        --nologo 2>&1 || { FAILED=1; echo "PACKAGE TEST Linux LGPL FAILED"; }

    # Test GPL variant on Linux
    rm -rf "$PKG_TEST_DIR/bin" "$PKG_TEST_DIR/obj"
    echo ""
    echo "--- Package Test: Linux GPL ---"
    dotnet run --project "$PKG_TEST_DIR" -c Release \
        -p:FFmpegPackageVersion="$TEST_VERSION" -p:GPLSuffix=".GPL" \
        --nologo 2>&1 || { FAILED=1; echo "PACKAGE TEST Linux GPL FAILED"; }

    # Test on Windows via cmd.exe (only possible when running under WSL)
    if [ -n "$WIN_PKG_TEST_DIR" ] && command -v cmd.exe &>/dev/null; then
        rm -rf "$PKG_TEST_DIR/bin" "$PKG_TEST_DIR/obj"
        echo ""
        echo "--- Package Test: Windows LGPL ---"
        cmd.exe /c "dotnet run --project $WIN_PKG_TEST_DIR -c Release -p:FFmpegPackageVersion=$TEST_VERSION --nologo" 2>&1 \
            || { FAILED=1; echo "PACKAGE TEST Windows LGPL FAILED"; }

        rm -rf "$PKG_TEST_DIR/bin" "$PKG_TEST_DIR/obj"
        echo ""
        echo "--- Package Test: Windows GPL ---"
        cmd.exe /c "dotnet run --project $WIN_PKG_TEST_DIR -c Release -p:FFmpegPackageVersion=$TEST_VERSION -p:GPLSuffix=.GPL --nologo" 2>&1 \
            || { FAILED=1; echo "PACKAGE TEST Windows GPL FAILED"; }
    else
        echo ""
        echo "--- Windows package tests: skipped (not running under WSL) ---"
    fi

    # Clean up temporary local feed and build artifacts
    rm -rf "$LOCAL_FEED" "$PKG_TEST_DIR/bin" "$PKG_TEST_DIR/obj"
}

# ── Run all test suites ──────────────────────────────────────────────────────

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
