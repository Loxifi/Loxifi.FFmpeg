#!/bin/bash
set -e

# Build FFmpeg shared libraries for all platforms
# Usage: ./scripts/build-ffmpeg.sh [lgpl|gpl] (default: both)
#
# Prerequisites:
#   - Android NDK r27c (install via: sdkmanager "ndk;27.2.12479018")
#   - gcc (host compiler)
#   - make, pkg-config
#
# For GPL linux/win builds: downloads pre-built from BtbN/FFmpeg-Builds
# For Android: cross-compiles from source

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
BUILD_MODE="${1:-both}" # lgpl, gpl, or both

FFMPEG_VERSION="7.1"
FFMPEG_SOURCE_URL="https://ffmpeg.org/releases/ffmpeg-${FFMPEG_VERSION}.tar.xz"
BTBN_BASE="https://github.com/BtbN/FFmpeg-Builds/releases/download/latest"

NDK_VERSION="27.2.12479018"
NDK_DIR="${ANDROID_SDK_DIR:-/home/service-account/android-sdk}/ndk/$NDK_VERSION"
NDK_TOOLCHAIN="$NDK_DIR/toolchains/llvm/prebuilt/linux-x86_64"
NDK_SYSROOT="$NDK_TOOLCHAIN/sysroot"
NDK_API=21

WORK_DIR="/tmp/ffmpeg-build-$$"
NDK_BIN="/tmp/ndk-bin-$$"

cleanup() {
    rm -rf "$WORK_DIR" "$NDK_BIN"
}
trap cleanup EXIT

mkdir -p "$WORK_DIR"

# ── NDK symlinks ──

setup_ndk_symlinks() {
    mkdir -p "$NDK_BIN"
    ln -sf "$NDK_TOOLCHAIN/bin/aarch64-linux-android${NDK_API}-clang"   "$NDK_BIN/aarch64-clang"
    ln -sf "$NDK_TOOLCHAIN/bin/aarch64-linux-android${NDK_API}-clang++" "$NDK_BIN/aarch64-clang++"
    ln -sf "$NDK_TOOLCHAIN/bin/clang"       "$NDK_BIN/clang"
    ln -sf "$NDK_TOOLCHAIN/bin/clang++"     "$NDK_BIN/clang++"
    ln -sf "$NDK_TOOLCHAIN/bin/llvm-nm"     "$NDK_BIN/"
    ln -sf "$NDK_TOOLCHAIN/bin/llvm-ar"     "$NDK_BIN/"
    ln -sf "$NDK_TOOLCHAIN/bin/llvm-ranlib" "$NDK_BIN/"
    ln -sf "$NDK_TOOLCHAIN/bin/llvm-strip"  "$NDK_BIN/"
}

# ── Download FFmpeg source ──

download_source() {
    if [ ! -f "$WORK_DIR/ffmpeg-source.tar.xz" ]; then
        echo "Downloading FFmpeg $FFMPEG_VERSION source..."
        curl -L -o "$WORK_DIR/ffmpeg-source.tar.xz" "$FFMPEG_SOURCE_URL"
    fi
}

# ── Build Android arm64 from source ──

build_android() {
    local license="$1"  # lgpl or gpl
    local extra_flags=""
    local output_dir="$WORK_DIR/android-${license}-out"
    local build_dir="$WORK_DIR/android-${license}-build"

    if [ "$license" = "gpl" ]; then
        extra_flags="--enable-gpl"
    fi

    echo "Building FFmpeg $FFMPEG_VERSION ($license) for Android arm64..."

    rm -rf "$build_dir"
    mkdir -p "$build_dir"
    tar xf "$WORK_DIR/ffmpeg-source.tar.xz" -C "$build_dir" --strip-components=1

    cd "$build_dir"
    ./configure \
        --prefix="$output_dir" \
        --enable-shared \
        --disable-static \
        --disable-programs \
        --disable-doc \
        --disable-avdevice \
        --disable-postproc \
        --disable-avfilter \
        $extra_flags \
        --enable-cross-compile \
        --target-os=android \
        --arch=aarch64 \
        --cpu=armv8-a \
        --cc="$NDK_BIN/aarch64-clang" \
        --cxx="$NDK_BIN/aarch64-clang++" \
        --nm="$NDK_BIN/llvm-nm" \
        --ar="$NDK_BIN/llvm-ar" \
        --ranlib="$NDK_BIN/llvm-ranlib" \
        --strip="$NDK_BIN/llvm-strip" \
        --host-cc=gcc \
        --sysroot="$NDK_SYSROOT" \
        --extra-cflags="-O2 -fPIC" \
        --extra-ldflags="-lm"

    make -j$(nproc)
    make install
    cd "$PROJECT_DIR"
}

# ── Copy binaries to runtime packages ──

copy_linux_x64() {
    local license="$1"
    local suffix=""
    [ "$license" = "gpl" ] && suffix=".GPL"

    local src_dir="$WORK_DIR/ffmpeg-n${FFMPEG_VERSION}-latest-linux64-${license}-shared-${FFMPEG_VERSION}/lib"
    local dst_dir="$PROJECT_DIR/src/Loxifi.FFmpeg.Runtime.linux-x64${suffix}/runtimes/linux-x64/native"

    mkdir -p "$dst_dir"
    rm -f "$dst_dir"/*.so*
    cp -L "$src_dir/libavformat.so.61"   "$dst_dir/"
    cp -L "$src_dir/libavcodec.so.61"    "$dst_dir/"
    cp -L "$src_dir/libavutil.so.59"     "$dst_dir/"
    cp -L "$src_dir/libswscale.so.8"     "$dst_dir/"
    cp -L "$src_dir/libswresample.so.5"  "$dst_dir/"

    echo "Copied Linux x64 ($license) libs to $dst_dir"
}

copy_win_x64() {
    local license="$1"
    local suffix=""
    [ "$license" = "gpl" ] && suffix=".GPL"

    local src_dir="$WORK_DIR/ffmpeg-n${FFMPEG_VERSION}-latest-win64-${license}-shared-${FFMPEG_VERSION}/bin"
    local dst_dir="$PROJECT_DIR/src/Loxifi.FFmpeg.Runtime.win-x64${suffix}/runtimes/win-x64/native"

    mkdir -p "$dst_dir"
    rm -f "$dst_dir"/*.dll
    cp "$src_dir/avformat-61.dll"   "$dst_dir/"
    cp "$src_dir/avcodec-61.dll"    "$dst_dir/"
    cp "$src_dir/avutil-59.dll"     "$dst_dir/"
    cp "$src_dir/swscale-8.dll"     "$dst_dir/"
    cp "$src_dir/swresample-5.dll"  "$dst_dir/"

    echo "Copied Windows x64 ($license) libs to $dst_dir"
}

copy_android_arm64() {
    local license="$1"
    local suffix=""
    [ "$license" = "gpl" ] && suffix=".GPL"

    local src_dir="$WORK_DIR/android-${license}-out/lib"
    local dst_dir="$PROJECT_DIR/src/Loxifi.FFmpeg.Runtime.android-arm64${suffix}/runtimes/android-arm64/native"

    mkdir -p "$dst_dir"
    rm -f "$dst_dir"/*.so
    cp "$src_dir/libavformat.so"   "$dst_dir/"
    cp "$src_dir/libavcodec.so"    "$dst_dir/"
    cp "$src_dir/libavutil.so"     "$dst_dir/"
    cp "$src_dir/libswscale.so"    "$dst_dir/"
    cp "$src_dir/libswresample.so" "$dst_dir/"

    echo "Copied Android arm64 ($license) libs to $dst_dir"
}

# ── Main ──

build_license() {
    local license="$1"

    echo ""
    echo "========================================"
    echo "  Building FFmpeg $FFMPEG_VERSION ($license)"
    echo "========================================"

    # Linux x64 + Windows x64: download pre-built from BtbN
    echo "Downloading $license Linux x64..."
    curl -L -o "$WORK_DIR/linux64-${license}.tar.xz" \
        "$BTBN_BASE/ffmpeg-n${FFMPEG_VERSION}-latest-linux64-${license}-shared-${FFMPEG_VERSION}.tar.xz"
    tar xf "$WORK_DIR/linux64-${license}.tar.xz" -C "$WORK_DIR"

    echo "Downloading $license Windows x64..."
    curl -L -o "$WORK_DIR/win64-${license}.zip" \
        "$BTBN_BASE/ffmpeg-n${FFMPEG_VERSION}-latest-win64-${license}-shared-${FFMPEG_VERSION}.zip"
    unzip -qo "$WORK_DIR/win64-${license}.zip" -d "$WORK_DIR"

    # Android arm64: cross-compile from source
    download_source
    setup_ndk_symlinks
    build_android "$license"

    # Copy to runtime packages
    copy_linux_x64 "$license"
    copy_win_x64 "$license"
    copy_android_arm64 "$license"
}

case "$BUILD_MODE" in
    lgpl)
        build_license "lgpl"
        ;;
    gpl)
        build_license "gpl"
        ;;
    both)
        build_license "lgpl"
        build_license "gpl"
        ;;
    *)
        echo "Usage: $0 [lgpl|gpl|both]"
        exit 1
        ;;
esac

echo ""
echo "Done. Verify with: strings <lib> | grep license"
