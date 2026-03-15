#!/bin/bash
set -e

# Build FFmpeg shared libraries for all platforms with identical codec support.
# Usage: ./scripts/build-ffmpeg.sh [lgpl|gpl] (default: both)
#
# All platforms (Linux x64, Windows x64, Android arm64) are cross-compiled
# from source with the same configuration to ensure parity.
#
# Prerequisites:
#   - Android NDK r27c: sdkmanager "ndk;27.2.12479018"
#   - Host tools: gcc g++ make cmake nasm pkg-config autoconf automake libtool git

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
BUILD_MODE="${1:-both}"

FFMPEG_VERSION="7.1"
FFMPEG_SOURCE_URL="https://ffmpeg.org/releases/ffmpeg-${FFMPEG_VERSION}.tar.xz"

NDK_VERSION="27.2.12479018"
NDK_DIR="${ANDROID_SDK_DIR:-/home/service-account/android-sdk}/ndk/$NDK_VERSION"
NDK_TOOLCHAIN="$NDK_DIR/toolchains/llvm/prebuilt/linux-x86_64"
NDK_SYSROOT="$NDK_TOOLCHAIN/sysroot"
NDK_API=21

WORK_DIR="/tmp/ffmpeg-build-$$"
NDK_BIN="$WORK_DIR/ndk-bin"
ANDROID_PREFIX="$WORK_DIR/android-deps"
NPROC=$(nproc)

# Cross-platform codec libraries to build (identical on all platforms)
# GPL-only libs: x264, x265, xvid
# LGPL/BSD libs: all others
COMMON_LIBS="libaom libdav1d libsvtav1 libvpx libmp3lame libopus libvorbis libtheora libwebp libopencore-amrnb libopencore-amrwb libopenh264 libopenjpeg libzimg"
GPL_LIBS="libx264 libx265 libxvid"

cleanup() {
    rm -rf "$WORK_DIR"
}
trap cleanup EXIT

mkdir -p "$WORK_DIR" "$ANDROID_PREFIX/lib/pkgconfig" "$ANDROID_PREFIX/include"

# ── Toolchain setup ──

setup_ndk() {
    mkdir -p "$NDK_BIN"
    ln -sf "$NDK_TOOLCHAIN/bin/aarch64-linux-android${NDK_API}-clang"   "$NDK_BIN/aarch64-clang"
    ln -sf "$NDK_TOOLCHAIN/bin/aarch64-linux-android${NDK_API}-clang++" "$NDK_BIN/aarch64-clang++"
    ln -sf "$NDK_TOOLCHAIN/bin/clang"       "$NDK_BIN/clang"
    ln -sf "$NDK_TOOLCHAIN/bin/clang++"     "$NDK_BIN/clang++"
    ln -sf "$NDK_TOOLCHAIN/bin/llvm-nm"     "$NDK_BIN/"
    ln -sf "$NDK_TOOLCHAIN/bin/llvm-ar"     "$NDK_BIN/"
    ln -sf "$NDK_TOOLCHAIN/bin/llvm-ranlib" "$NDK_BIN/"
    ln -sf "$NDK_TOOLCHAIN/bin/llvm-strip"  "$NDK_BIN/"
    ln -sf "$NDK_TOOLCHAIN/bin/llvm-strings" "$NDK_BIN/" 2>/dev/null || ln -sf "$(which strings)" "$NDK_BIN/llvm-strings"
    ln -sf "$NDK_TOOLCHAIN/bin/llvm-objdump" "$NDK_BIN/" 2>/dev/null || true

    export PATH="$NDK_TOOLCHAIN/bin:$NDK_BIN:$PATH"
    export CC="$NDK_BIN/aarch64-clang"
    export CXX="$NDK_BIN/aarch64-clang++"
    export AR="$NDK_BIN/llvm-ar"
    export NM="$NDK_BIN/llvm-nm"
    export RANLIB="$NDK_BIN/llvm-ranlib"
    export STRIP="$NDK_BIN/llvm-strip"
    export CFLAGS="-O2 -fPIC --sysroot=$NDK_SYSROOT"
    export CXXFLAGS="-O2 -fPIC --sysroot=$NDK_SYSROOT"
    export LDFLAGS="--sysroot=$NDK_SYSROOT"
    export PKG_CONFIG_PATH="$ANDROID_PREFIX/lib/pkgconfig"
    export PKG_CONFIG_LIBDIR="$ANDROID_PREFIX/lib/pkgconfig"
}

download() {
    local url="$1" dest="$2"
    if [ ! -f "$dest" ]; then
        echo "  Downloading $(basename "$dest")..."
        curl -L -o "$dest" "$url"
    fi
}

# Update config.sub/config.guess for projects with old autotools
fix_config_sub() {
    for f in config.sub config.guess; do
        if [ -f "$f" ]; then
            cp /usr/share/misc/$f . 2>/dev/null || \
            curl -sL "https://git.savannah.gnu.org/cgit/config.git/plain/$f" > "$f"
        fi
    done
}

git_clone() {
    local url="$1" dir="$2" tag="$3"
    if [ ! -d "$dir" ]; then
        echo "  Cloning $(basename "$dir")..."
        git clone --depth 1 ${tag:+--branch "$tag"} "$url" "$dir"
    fi
}

# ── Individual library builds ──

build_x264() {
    echo "Building x264..."
    git_clone "https://code.videolan.org/videolan/x264.git" "$WORK_DIR/x264" "stable"
    cd "$WORK_DIR/x264"
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --enable-static --disable-shared --disable-cli \
        --enable-pic \
        --host=aarch64-linux \
        --cross-prefix="" \
        --sysroot="$NDK_SYSROOT" \
        --extra-cflags="$CFLAGS"
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_x265() {
    echo "Building x265..."
    git_clone "https://bitbucket.org/multicoreware/x265_git.git" "$WORK_DIR/x265" "stable"
    mkdir -p "$WORK_DIR/x265-build" && cd "$WORK_DIR/x265-build"
    cmake "$WORK_DIR/x265/source" \
        -DCMAKE_TOOLCHAIN_FILE="$NDK_DIR/build/cmake/android.toolchain.cmake" \
        -DANDROID_ABI=arm64-v8a \
        -DANDROID_PLATFORM=android-$NDK_API \
        -DANDROID_STL=c++_static \
        -DCMAKE_INSTALL_PREFIX="$ANDROID_PREFIX" \
        -DCMAKE_C_FLAGS="-O2 -fPIC" \
        -DCMAKE_CXX_FLAGS="-O2 -fPIC" \
        -DENABLE_SHARED=OFF \
        -DENABLE_CLI=OFF \
        -DENABLE_ASSEMBLY=OFF
    make -j$NPROC && make install

    # Always write pkg-config file — x265 cmake doesn't reliably create one for cross-builds
    cat > "$ANDROID_PREFIX/lib/pkgconfig/x265.pc" << PCEOF
prefix=$ANDROID_PREFIX
exec_prefix=\${prefix}
libdir=\${prefix}/lib
includedir=\${prefix}/include

Name: x265
Description: H.265/HEVC video encoder
Version: 3.6
Libs: -L\${libdir} -lx265
Libs.private: -lstdc++ -lm
Cflags: -I\${includedir}
PCEOF
    cd "$PROJECT_DIR"
}

build_libaom() {
    echo "Building libaom..."
    git_clone "https://aomedia.googlesource.com/aom" "$WORK_DIR/aom" "v3.11.0"
    mkdir -p "$WORK_DIR/aom-build" && cd "$WORK_DIR/aom-build"
    cmake "$WORK_DIR/aom" \
        -DCMAKE_TOOLCHAIN_FILE="$NDK_DIR/build/cmake/android.toolchain.cmake" \
        -DANDROID_ABI=arm64-v8a \
        -DANDROID_PLATFORM=android-$NDK_API \
        -DANDROID_STL=c++_static \
        -DCMAKE_INSTALL_PREFIX="$ANDROID_PREFIX" \
        -DCMAKE_C_FLAGS="-O2 -fPIC" \
        -DBUILD_SHARED_LIBS=OFF \
        -DENABLE_TESTS=OFF \
        -DENABLE_TOOLS=OFF \
        -DENABLE_EXAMPLES=OFF \
        -DENABLE_DOCS=OFF \
        -DENABLE_NEON=ON \
        -DCONFIG_RUNTIME_CPU_DETECT=0
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_dav1d() {
    echo "Building dav1d..."
    git_clone "https://code.videolan.org/videolan/dav1d.git" "$WORK_DIR/dav1d" "1.5.1"
    cd "$WORK_DIR/dav1d"

    # Write meson cross file
    cat > "$WORK_DIR/android-arm64-cross.txt" << CROSSEOF
[binaries]
c = '$NDK_BIN/aarch64-clang'
cpp = '$NDK_BIN/aarch64-clang++'
ar = '$NDK_BIN/llvm-ar'
strip = '$NDK_BIN/llvm-strip'
[host_machine]
system = 'android'
cpu_family = 'aarch64'
cpu = 'aarch64'
endian = 'little'
[built-in options]
c_args = ['-O2', '-fPIC', '--sysroot=$NDK_SYSROOT']
c_link_args = ['--sysroot=$NDK_SYSROOT']
CROSSEOF

    meson setup "$WORK_DIR/dav1d-build" \
        --prefix="$ANDROID_PREFIX" \
        --cross-file "$WORK_DIR/android-arm64-cross.txt" \
        --default-library=static \
        -Denable_tools=false \
        -Denable_tests=false
    ninja -C "$WORK_DIR/dav1d-build" -j$NPROC
    ninja -C "$WORK_DIR/dav1d-build" install
    cd "$PROJECT_DIR"
}

build_svtav1() {
    echo "Building SVT-AV1..."
    git_clone "https://gitlab.com/AOMediaCodec/SVT-AV1.git" "$WORK_DIR/svtav1" "v2.3.0"
    mkdir -p "$WORK_DIR/svtav1-build" && cd "$WORK_DIR/svtav1-build"
    # SVT-AV1 sets ARCHIVE_OUTPUT_DIRECTORY to $SOURCE/Bin/$BUILD_TYPE
    mkdir -p "$WORK_DIR/svtav1/Bin/Release" "$WORK_DIR/svtav1/Bin/Debug" "$WORK_DIR/svtav1/Bin/RelWithDebInfo" "$WORK_DIR/svtav1/Bin/MinSizeRel"
    cmake "$WORK_DIR/svtav1" \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_TOOLCHAIN_FILE="$NDK_DIR/build/cmake/android.toolchain.cmake" \
        -DANDROID_ABI=arm64-v8a \
        -DANDROID_PLATFORM=android-$NDK_API \
        -DANDROID_STL=c++_static \
        -DCMAKE_INSTALL_PREFIX="$ANDROID_PREFIX" \
        -DCMAKE_C_FLAGS="-O2 -fPIC" \
        -DCMAKE_AR="$NDK_TOOLCHAIN/bin/llvm-ar" \
        -DCMAKE_RANLIB="$NDK_TOOLCHAIN/bin/llvm-ranlib" \
        -DBUILD_SHARED_LIBS=OFF \
        -DBUILD_TESTING=OFF \
        -DBUILD_APPS=OFF \
        -DBUILD_DEC=ON \
        -DBUILD_ENC=ON \
        -DSVT_AV1_LTO=OFF \
        -DUSE_CPUINFO=OFF
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_libvpx() {
    echo "Building libvpx..."
    git_clone "https://chromium.googlesource.com/webm/libvpx" "$WORK_DIR/libvpx" "v1.15.0"
    cd "$WORK_DIR/libvpx"
    CROSS="$NDK_BIN/aarch64-clang" \
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --target=arm64-android-gcc \
        --enable-static --disable-shared \
        --disable-examples --disable-tools --disable-docs --disable-unit-tests \
        --enable-pic \
        --enable-vp8 --enable-vp9 \
        --enable-runtime-cpu-detect
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_lame() {
    echo "Building libmp3lame..."
    download "https://downloads.sourceforge.net/project/lame/lame/3.100/lame-3.100.tar.gz" "$WORK_DIR/lame.tar.gz"
    mkdir -p "$WORK_DIR/lame" && tar xf "$WORK_DIR/lame.tar.gz" -C "$WORK_DIR/lame" --strip-components=1
    cd "$WORK_DIR/lame"
    fix_config_sub
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --host=aarch64-linux-android \
        --enable-static --disable-shared \
        --disable-frontend \
        --with-pic
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_opus() {
    echo "Building libopus..."
    download "https://downloads.xiph.org/releases/opus/opus-1.5.2.tar.gz" "$WORK_DIR/opus.tar.gz"
    mkdir -p "$WORK_DIR/opus" && tar xf "$WORK_DIR/opus.tar.gz" -C "$WORK_DIR/opus" --strip-components=1
    cd "$WORK_DIR/opus"
    fix_config_sub
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --host=aarch64-linux-android \
        --enable-static --disable-shared --disable-doc --disable-extra-programs \
        --with-pic
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_ogg() {
    echo "Building libogg..."
    download "https://downloads.xiph.org/releases/ogg/libogg-1.3.5.tar.xz" "$WORK_DIR/ogg.tar.xz"
    mkdir -p "$WORK_DIR/ogg" && tar xf "$WORK_DIR/ogg.tar.xz" -C "$WORK_DIR/ogg" --strip-components=1
    cd "$WORK_DIR/ogg"
    fix_config_sub
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --host=aarch64-linux-android \
        --enable-static --disable-shared \
        --with-pic
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_vorbis() {
    echo "Building libvorbis..."
    download "https://downloads.xiph.org/releases/vorbis/libvorbis-1.3.7.tar.xz" "$WORK_DIR/vorbis.tar.xz"
    mkdir -p "$WORK_DIR/vorbis" && tar xf "$WORK_DIR/vorbis.tar.xz" -C "$WORK_DIR/vorbis" --strip-components=1
    cd "$WORK_DIR/vorbis"
    fix_config_sub
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --host=aarch64-linux-android \
        --enable-static --disable-shared \
        --with-pic \
        --with-ogg="$ANDROID_PREFIX"
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_theora() {
    echo "Building libtheora..."
    download "https://downloads.xiph.org/releases/theora/libtheora-1.1.1.tar.bz2" "$WORK_DIR/theora.tar.bz2"
    mkdir -p "$WORK_DIR/theora" && tar xf "$WORK_DIR/theora.tar.bz2" -C "$WORK_DIR/theora" --strip-components=1
    cd "$WORK_DIR/theora"
    # Update config.sub/config.guess for aarch64-linux-android support
    cp /usr/share/misc/config.sub . 2>/dev/null || curl -sL "https://git.savannah.gnu.org/cgit/config.git/plain/config.sub" > config.sub
    cp /usr/share/misc/config.guess . 2>/dev/null || curl -sL "https://git.savannah.gnu.org/cgit/config.git/plain/config.guess" > config.guess
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --host=aarch64-linux-android \
        --enable-static --disable-shared --disable-examples \
        --with-pic \
        --with-ogg="$ANDROID_PREFIX" \
        --disable-oggtest --disable-vorbistest --disable-sdltest \
        --disable-spec
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_webp() {
    echo "Building libwebp..."
    download "https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.5.0.tar.gz" "$WORK_DIR/webp.tar.gz"
    mkdir -p "$WORK_DIR/webp" && tar xf "$WORK_DIR/webp.tar.gz" -C "$WORK_DIR/webp" --strip-components=1
    cd "$WORK_DIR/webp"
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --host=aarch64-linux-android \
        --enable-static --disable-shared \
        --with-pic \
        --enable-libwebpmux --enable-libwebpdemux
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_opencore_amr() {
    echo "Building opencore-amr..."
    download "https://downloads.sourceforge.net/project/opencore-amr/opencore-amr/opencore-amr-0.1.6.tar.gz" "$WORK_DIR/opencore-amr.tar.gz"
    mkdir -p "$WORK_DIR/opencore-amr" && tar xf "$WORK_DIR/opencore-amr.tar.gz" -C "$WORK_DIR/opencore-amr" --strip-components=1
    cd "$WORK_DIR/opencore-amr"
    fix_config_sub
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --host=aarch64-linux-android \
        --enable-static --disable-shared \
        --with-pic
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_openh264() {
    echo "Building openh264..."
    git_clone "https://github.com/cisco/openh264.git" "$WORK_DIR/openh264" "v2.6.0"
    cd "$WORK_DIR/openh264"
    make -j$NPROC \
        OS=android ARCH=arm64 NDKROOT="$NDK_DIR" TARGET="android-$NDK_API" \
        PREFIX="$ANDROID_PREFIX" \
        BUILDTYPE=Release \
        libraries install-static
    cd "$PROJECT_DIR"
}

build_openjpeg() {
    echo "Building openjpeg..."
    git_clone "https://github.com/uclouvain/openjpeg.git" "$WORK_DIR/openjpeg" "v2.5.3"
    mkdir -p "$WORK_DIR/openjpeg-build" && cd "$WORK_DIR/openjpeg-build"
    cmake "$WORK_DIR/openjpeg" \
        -DCMAKE_TOOLCHAIN_FILE="$NDK_DIR/build/cmake/android.toolchain.cmake" \
        -DANDROID_ABI=arm64-v8a \
        -DANDROID_PLATFORM=android-$NDK_API \
        -DANDROID_STL=c++_static \
        -DCMAKE_INSTALL_PREFIX="$ANDROID_PREFIX" \
        -DCMAKE_C_FLAGS="-O2 -fPIC" \
        -DBUILD_SHARED_LIBS=OFF \
        -DBUILD_CODEC=OFF \
        -DBUILD_TESTING=OFF
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_xvid() {
    echo "Building libxvid..."
    download "https://downloads.xvid.com/downloads/xvidcore-1.3.7.tar.bz2" "$WORK_DIR/xvid.tar.bz2"
    mkdir -p "$WORK_DIR/xvid" && tar xf "$WORK_DIR/xvid.tar.bz2" -C "$WORK_DIR/xvid" --strip-components=1
    cd "$WORK_DIR/xvid/build/generic"
    fix_config_sub
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --host=aarch64-linux-android \
        --disable-assembly
    make -j$NPROC && make install
    # Remove shared lib, keep only static
    rm -f "$ANDROID_PREFIX/lib/libxvidcore.so"* 2>/dev/null || true
    cd "$PROJECT_DIR"
}

build_zimg() {
    echo "Building zimg..."
    git_clone "https://github.com/sekrit-twc/zimg.git" "$WORK_DIR/zimg" "release-3.0.5"
    cd "$WORK_DIR/zimg"
    autoreconf -if
    fix_config_sub
    ./configure \
        --prefix="$ANDROID_PREFIX" \
        --host=aarch64-linux-android \
        --enable-static --disable-shared \
        --with-pic
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

# ── Build all deps then FFmpeg ──

build_all_deps() {
    local license="$1"

    # Ogg must come before vorbis/theora
    build_ogg
    build_vorbis
    build_theora

    # Independent libs
    build_libaom
    build_dav1d
    build_svtav1
    build_libvpx
    build_lame
    build_opus
    build_webp
    build_opencore_amr
    build_openh264
    build_openjpeg
    build_zimg

    # GPL-only
    if [ "$license" = "gpl" ]; then
        build_x264
        build_x265
        build_xvid
    fi
}

build_ffmpeg_android() {
    local license="$1"
    local output_dir="$WORK_DIR/android-${license}-out"
    local build_dir="$WORK_DIR/android-${license}-ffmpeg"

    local gpl_flags=""
    if [ "$license" = "gpl" ]; then
        gpl_flags="--enable-gpl --enable-version3 --enable-libx264 --enable-libx265 --enable-libxvid"
    fi

    echo "Building FFmpeg $FFMPEG_VERSION ($license) for Android arm64..."

    rm -rf "$build_dir"
    mkdir -p "$build_dir"
    tar xf "$WORK_DIR/ffmpeg-source.tar.xz" -C "$build_dir" --strip-components=1

    cd "$build_dir"
    PKG_CONFIG_PATH="$ANDROID_PREFIX/lib/pkgconfig:$ANDROID_PREFIX/lib/x86_64-linux-gnu/pkgconfig" \
    ./configure \
        --prefix="$output_dir" \
        --enable-shared \
        --disable-static \
        --disable-programs \
        --disable-doc \
        --disable-avdevice \
        --disable-postproc \
        --disable-avfilter \
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
        --extra-cflags="-O2 -fPIC -I$ANDROID_PREFIX/include" \
        --extra-ldflags="-L$ANDROID_PREFIX/lib -lm" \
        --pkg-config-flags="--static" \
        --enable-libaom \
        --enable-libdav1d \
        --enable-libsvtav1 \
        --enable-libvpx \
        --enable-libmp3lame \
        --enable-libopus \
        --enable-libvorbis \
        --enable-libtheora \
        --enable-libwebp \
        --enable-libopencore-amrnb \
        --enable-libopencore-amrwb \
        --enable-libopenh264 \
        --enable-libopenjpeg \
        --enable-libzimg \
        $gpl_flags

    make -j$NPROC
    make install
    cd "$PROJECT_DIR"
}

# ── Copy to runtime packages ──

copy_android() {
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

    echo "Copied Android arm64 ($license) to $dst_dir"
    strings "$dst_dir/libavcodec.so" | grep "license"
}

# ── Main ──

build_platform() {
    local license="$1"

    echo ""
    echo "========================================"
    echo "  Android arm64 — FFmpeg $FFMPEG_VERSION ($license)"
    echo "  Cross-compiling all codec dependencies"
    echo "========================================"

    # Download FFmpeg source
    if [ ! -f "$WORK_DIR/ffmpeg-source.tar.xz" ]; then
        echo "Downloading FFmpeg $FFMPEG_VERSION source..."
        curl -L -o "$WORK_DIR/ffmpeg-source.tar.xz" "$FFMPEG_SOURCE_URL"
    fi

    # Setup NDK
    setup_ndk

    # Install host tools check
    for tool in cmake meson ninja nasm autoconf automake libtool; do
        if ! command -v $tool &>/dev/null; then
            echo "ERROR: $tool is required but not installed"
            exit 1
        fi
    done

    # Build all dependencies as static libs
    build_all_deps "$license"

    # Build FFmpeg with all deps
    build_ffmpeg_android "$license"

    # Copy to runtime package
    copy_android "$license"
}

case "$BUILD_MODE" in
    lgpl)  build_platform "lgpl" ;;
    gpl)   build_platform "gpl" ;;
    both)
        build_platform "lgpl"
        # Reset deps for GPL (x264/x265/xvid added)
        rm -rf "$ANDROID_PREFIX"
        mkdir -p "$ANDROID_PREFIX/lib/pkgconfig" "$ANDROID_PREFIX/include"
        build_platform "gpl"
        ;;
    *)
        echo "Usage: $0 [lgpl|gpl|both]"
        exit 1
        ;;
esac

echo ""
echo "Done."
