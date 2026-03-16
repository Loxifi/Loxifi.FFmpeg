#!/bin/bash
set -e

# Build FFmpeg shared libraries for all platforms with identical codec support.
# Usage: ./scripts/build-ffmpeg.sh [lgpl|gpl] [linux-x64|win-x64|android-arm64|all]
# Default: both licenses, all platforms
#
# Prerequisites:
#   - Android NDK r27c: sdkmanager "ndk;27.2.12479018"
#   - mingw-w64 for Windows cross-compile: apt install gcc-mingw-w64-x86-64
#   - Host tools: gcc g++ make cmake nasm pkg-config autoconf automake libtool git meson ninja-build

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
LICENSE_MODE="${1:-both}"
PLATFORM_MODE="${2:-all}"

FFMPEG_VERSION="7.1"
FFMPEG_SOURCE_URL="https://ffmpeg.org/releases/ffmpeg-${FFMPEG_VERSION}.tar.xz"

NDK_VERSION="27.2.12479018"
NDK_DIR="${ANDROID_SDK_DIR:-/home/service-account/android-sdk}/ndk/$NDK_VERSION"
NDK_TOOLCHAIN="$NDK_DIR/toolchains/llvm/prebuilt/linux-x86_64"
NDK_SYSROOT="$NDK_TOOLCHAIN/sysroot"
NDK_API=21

WORK_DIR="/tmp/ffmpeg-build-$$"
NPROC=$(nproc)

# Current target being built — set by setup_target
TARGET=""        # linux-x64, win-x64, android-arm64
PREFIX=""         # install prefix for deps
HOST_TRIPLE=""    # autotools --host
CMAKE_TOOLCHAIN="" # cmake toolchain args
FFMPEG_TARGET_FLAGS="" # ffmpeg configure target args

cleanup() { rm -rf "$WORK_DIR"; }
trap cleanup EXIT
mkdir -p "$WORK_DIR"

# ── Target setup ──

setup_target() {
    TARGET="$1"
    PREFIX="$WORK_DIR/${TARGET}-deps"
    mkdir -p "$PREFIX/lib/pkgconfig" "$PREFIX/include"

    export PKG_CONFIG_PATH="$PREFIX/lib/pkgconfig"
    export PKG_CONFIG_LIBDIR="$PREFIX/lib/pkgconfig"

    case "$TARGET" in
        linux-x64)
            export CC=gcc CXX=g++ AR=ar NM=nm RANLIB=ranlib STRIP=strip
            export CFLAGS="-O2 -fPIC" CXXFLAGS="-O2 -fPIC" LDFLAGS=""
            HOST_TRIPLE=""
            CMAKE_TOOLCHAIN=""
            FFMPEG_TARGET_FLAGS=""
            FFMPEG_TOOLS=""
            ;;
        win-x64)
            export CC=x86_64-w64-mingw32-gcc CXX=x86_64-w64-mingw32-g++
            export AR=x86_64-w64-mingw32-ar NM=x86_64-w64-mingw32-nm
            export RANLIB=x86_64-w64-mingw32-ranlib STRIP=x86_64-w64-mingw32-strip
            export CFLAGS="-O2 -D_FORTIFY_SOURCE=0" CXXFLAGS="-O2 -D_FORTIFY_SOURCE=0" LDFLAGS=""
            HOST_TRIPLE="x86_64-w64-mingw32"
            CMAKE_TOOLCHAIN="-DCMAKE_SYSTEM_NAME=Windows -DCMAKE_C_COMPILER=$CC -DCMAKE_CXX_COMPILER=$CXX -DCMAKE_RC_COMPILER=x86_64-w64-mingw32-windres -DCMAKE_FIND_ROOT_PATH=$PREFIX -DCMAKE_FIND_ROOT_PATH_MODE_PROGRAM=NEVER -DCMAKE_FIND_ROOT_PATH_MODE_LIBRARY=ONLY -DCMAKE_FIND_ROOT_PATH_MODE_INCLUDE=ONLY"
            FFMPEG_TARGET_FLAGS="--enable-cross-compile --target-os=mingw64 --arch=x86_64 --cross-prefix=x86_64-w64-mingw32-"
            FFMPEG_TOOLS=""
            ;;
        android-arm64)
            local NDK_BIN="$WORK_DIR/ndk-bin"
            mkdir -p "$NDK_BIN"
            ln -sf "$NDK_TOOLCHAIN/bin/aarch64-linux-android${NDK_API}-clang"   "$NDK_BIN/aarch64-clang"
            ln -sf "$NDK_TOOLCHAIN/bin/aarch64-linux-android${NDK_API}-clang++" "$NDK_BIN/aarch64-clang++"
            ln -sf "$NDK_TOOLCHAIN/bin/clang"       "$NDK_BIN/clang"
            ln -sf "$NDK_TOOLCHAIN/bin/clang++"     "$NDK_BIN/clang++"
            for tool in llvm-nm llvm-ar llvm-ranlib llvm-strip llvm-strings llvm-objdump; do
                ln -sf "$NDK_TOOLCHAIN/bin/$tool" "$NDK_BIN/" 2>/dev/null || true
            done
            export PATH="$NDK_TOOLCHAIN/bin:$NDK_BIN:$PATH"
            export CC="$NDK_BIN/aarch64-clang" CXX="$NDK_BIN/aarch64-clang++"
            export AR="$NDK_BIN/llvm-ar" NM="$NDK_BIN/llvm-nm"
            export RANLIB="$NDK_BIN/llvm-ranlib" STRIP="$NDK_BIN/llvm-strip"
            export CFLAGS="-O2 -fPIC --sysroot=$NDK_SYSROOT"
            export CXXFLAGS="-O2 -fPIC --sysroot=$NDK_SYSROOT"
            export LDFLAGS="--sysroot=$NDK_SYSROOT"
            HOST_TRIPLE="aarch64-linux-android"
            CMAKE_TOOLCHAIN="-DCMAKE_TOOLCHAIN_FILE=$NDK_DIR/build/cmake/android.toolchain.cmake -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-$NDK_API -DANDROID_STL=c++_static"
            FFMPEG_TARGET_FLAGS="--enable-cross-compile --target-os=android --arch=aarch64 --cpu=armv8-a --host-cc=gcc --sysroot=$NDK_SYSROOT"
            # Store tool paths for FFmpeg configure (can't use env vars as they may conflict with dep builds)
            FFMPEG_CC="$NDK_BIN/aarch64-clang"
            FFMPEG_CXX="$NDK_BIN/aarch64-clang++"
            FFMPEG_TOOLS="--cc=$NDK_BIN/aarch64-clang --cxx=$NDK_BIN/aarch64-clang++ --nm=$NDK_BIN/llvm-nm --ar=$NDK_BIN/llvm-ar --ranlib=$NDK_BIN/llvm-ranlib --strip=$NDK_BIN/llvm-strip"
            ;;
    esac
}

# ── Helpers ──

download() {
    local url="$1" dest="$2"
    [ -f "$dest" ] || { echo "  Downloading $(basename "$dest")..."; curl -L -o "$dest" "$url"; }
}

git_clone() {
    local url="$1" dir="$2" tag="$3"
    [ -d "$dir" ] || { echo "  Cloning $(basename "$dir")..."; git clone --depth 1 ${tag:+--branch "$tag"} "$url" "$dir"; }
}

fix_config_sub() {
    for f in config.sub config.guess; do
        [ -f "$f" ] && { cp /usr/share/misc/$f . 2>/dev/null || curl -sL "https://git.savannah.gnu.org/cgit/config.git/plain/$f" > "$f"; }
    done
}

autotools_host() {
    [ -n "$HOST_TRIPLE" ] && echo "--host=$HOST_TRIPLE" || true
}

cmake_build() {
    local src="$1"; shift
    cmake "$src" \
        -DCMAKE_INSTALL_PREFIX="$PREFIX" \
        -DCMAKE_C_FLAGS="-O2 -fPIC" \
        -DCMAKE_CXX_FLAGS="-O2 -fPIC" \
        -DCMAKE_BUILD_TYPE=Release \
        $CMAKE_TOOLCHAIN \
        "$@"
    make -j$NPROC && make install
}

# ── Library builds (target-agnostic) ──

build_x264() {
    echo "Building x264..."
    git_clone "https://code.videolan.org/videolan/x264.git" "$WORK_DIR/x264-src" "stable"
    rm -rf "$WORK_DIR/x264-$TARGET" && cp -a "$WORK_DIR/x264-src" "$WORK_DIR/x264-$TARGET"
    cd "$WORK_DIR/x264-$TARGET"
    local host_flag="" cross_flag=""
    case "$TARGET" in
        linux-x64)   host_flag="--host=x86_64-linux-gnu" ;;
        win-x64)     host_flag="--host=x86_64-w64-mingw32" ; cross_flag="--cross-prefix=x86_64-w64-mingw32-" ;;
        android-arm64) host_flag="--host=aarch64-linux" ; cross_flag="--cross-prefix=" ;;
    esac
    ./configure --prefix="$PREFIX" --enable-static --disable-shared --disable-cli --enable-pic \
        $host_flag $cross_flag --extra-cflags="$CFLAGS"
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_x265() {
    echo "Building x265..."
    git_clone "https://bitbucket.org/multicoreware/x265_git.git" "$WORK_DIR/x265-src" "stable"
    rm -rf "$WORK_DIR/x265-build-$TARGET" && mkdir -p "$WORK_DIR/x265-build-$TARGET" && cd "$WORK_DIR/x265-build-$TARGET"
    local asm=OFF
    [ "$TARGET" = "linux-x64" ] && asm=ON
    cmake_build "$WORK_DIR/x265-src/source" -DENABLE_SHARED=OFF -DENABLE_CLI=OFF -DENABLE_ASSEMBLY=$asm
    # Always write pkg-config file — x265 cmake doesn't reliably create one for cross-builds
    local x265_private_libs="-lstdc++ -lm -lpthread"
    [ "$TARGET" = "win-x64" ] && x265_private_libs="-lstdc++ -lm"
    [ "$TARGET" = "android-arm64" ] && x265_private_libs="-lc++ -lm"
    cat > "$PREFIX/lib/pkgconfig/x265.pc" << PCEOF
prefix=$PREFIX
exec_prefix=\${prefix}
libdir=\${prefix}/lib
includedir=\${prefix}/include
Name: x265
Description: H.265/HEVC video encoder
Version: 3.6
Libs: -L\${libdir} -lx265
Libs.private: $x265_private_libs
Cflags: -I\${includedir}
PCEOF
    cd "$PROJECT_DIR"
}

build_libaom() {
    echo "Building libaom..."
    git_clone "https://aomedia.googlesource.com/aom" "$WORK_DIR/aom-src" "v3.11.0"
    rm -rf "$WORK_DIR/aom-build-$TARGET" && mkdir -p "$WORK_DIR/aom-build-$TARGET" && cd "$WORK_DIR/aom-build-$TARGET"
    local extra=""
    [ "$TARGET" = "android-arm64" ] && extra="-DENABLE_NEON=ON -DCONFIG_RUNTIME_CPU_DETECT=0"
    cmake_build "$WORK_DIR/aom-src" -DBUILD_SHARED_LIBS=OFF -DENABLE_TESTS=OFF -DENABLE_TOOLS=OFF -DENABLE_EXAMPLES=OFF -DENABLE_DOCS=OFF $extra
    cd "$PROJECT_DIR"
}

build_dav1d() {
    echo "Building dav1d..."
    git_clone "https://code.videolan.org/videolan/dav1d.git" "$WORK_DIR/dav1d-src" "1.5.1"

    local cross_arg=""
    if [ "$TARGET" != "linux-x64" ]; then
        local crossfile="$WORK_DIR/dav1d-cross-$TARGET.txt"
        case "$TARGET" in
            win-x64)
                cat > "$crossfile" << XEOF
[binaries]
c = 'x86_64-w64-mingw32-gcc'
cpp = 'x86_64-w64-mingw32-g++'
ar = 'x86_64-w64-mingw32-ar'
strip = 'x86_64-w64-mingw32-strip'
windres = 'x86_64-w64-mingw32-windres'
[host_machine]
system = 'windows'
cpu_family = 'x86_64'
cpu = 'x86_64'
endian = 'little'
XEOF
                ;;
            android-arm64)
                cat > "$crossfile" << XEOF
[binaries]
c = '$CC'
cpp = '$CXX'
ar = '$AR'
strip = '$STRIP'
[host_machine]
system = 'android'
cpu_family = 'aarch64'
cpu = 'aarch64'
endian = 'little'
[built-in options]
c_args = ['-O2', '-fPIC', '--sysroot=$NDK_SYSROOT']
c_link_args = ['--sysroot=$NDK_SYSROOT']
XEOF
                ;;
        esac
        cross_arg="--cross-file $crossfile"
    fi

    rm -rf "$WORK_DIR/dav1d-build-$TARGET"
    cd "$WORK_DIR/dav1d-src"
    meson setup "$WORK_DIR/dav1d-build-$TARGET" --prefix="$PREFIX" $cross_arg --default-library=static -Denable_tools=false -Denable_tests=false
    ninja -C "$WORK_DIR/dav1d-build-$TARGET" -j$NPROC
    ninja -C "$WORK_DIR/dav1d-build-$TARGET" install
    cd "$PROJECT_DIR"
}

build_svtav1() {
    echo "Building SVT-AV1..."
    git_clone "https://gitlab.com/AOMediaCodec/SVT-AV1.git" "$WORK_DIR/svtav1-src" "v2.3.0"
    rm -rf "$WORK_DIR/svtav1-build-$TARGET" && mkdir -p "$WORK_DIR/svtav1-build-$TARGET" && cd "$WORK_DIR/svtav1-build-$TARGET"
    mkdir -p "$WORK_DIR/svtav1-src/Bin/Release"
    cmake_build "$WORK_DIR/svtav1-src" -DBUILD_SHARED_LIBS=OFF -DBUILD_TESTING=OFF -DBUILD_APPS=OFF \
        -DBUILD_DEC=ON -DBUILD_ENC=ON -DSVT_AV1_LTO=OFF \
        -DUSE_CPUINFO=OFF -DUSE_EXTERNAL_CPUINFO=OFF -DCOMPILE_C_ONLY=ON
    cd "$PROJECT_DIR"
}

build_libvpx() {
    echo "Building libvpx..."
    git_clone "https://chromium.googlesource.com/webm/libvpx" "$WORK_DIR/libvpx-src" "v1.15.0"
    rm -rf "$WORK_DIR/libvpx-build-$TARGET" && cp -a "$WORK_DIR/libvpx-src" "$WORK_DIR/libvpx-build-$TARGET"
    cd "$WORK_DIR/libvpx-build-$TARGET"
    local vpx_target=""
    case "$TARGET" in
        linux-x64)     vpx_target="x86_64-linux-gcc" ;;
        win-x64)       vpx_target="x86_64-win64-gcc" ;;
        android-arm64) vpx_target="arm64-android-gcc" ;;
    esac
    CROSS="$CC" ./configure --prefix="$PREFIX" --target=$vpx_target \
        --enable-static --disable-shared --disable-examples --disable-tools --disable-docs --disable-unit-tests \
        --enable-pic --enable-vp8 --enable-vp9
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_lame() {
    echo "Building libmp3lame..."
    download "https://downloads.sourceforge.net/project/lame/lame/3.100/lame-3.100.tar.gz" "$WORK_DIR/lame.tar.gz"
    rm -rf "$WORK_DIR/lame-$TARGET" && mkdir -p "$WORK_DIR/lame-$TARGET" && tar xf "$WORK_DIR/lame.tar.gz" -C "$WORK_DIR/lame-$TARGET" --strip-components=1
    cd "$WORK_DIR/lame-$TARGET"
    fix_config_sub
    ./configure --prefix="$PREFIX" $(autotools_host) --enable-static --disable-shared --disable-frontend --with-pic
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_opus() {
    echo "Building libopus..."
    download "https://downloads.xiph.org/releases/opus/opus-1.5.2.tar.gz" "$WORK_DIR/opus.tar.gz"
    rm -rf "$WORK_DIR/opus-$TARGET" && mkdir -p "$WORK_DIR/opus-$TARGET" && tar xf "$WORK_DIR/opus.tar.gz" -C "$WORK_DIR/opus-$TARGET" --strip-components=1
    cd "$WORK_DIR/opus-$TARGET"
    fix_config_sub
    ./configure --prefix="$PREFIX" $(autotools_host) --enable-static --disable-shared --disable-doc --disable-extra-programs --with-pic
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_ogg() {
    echo "Building libogg..."
    download "https://downloads.xiph.org/releases/ogg/libogg-1.3.5.tar.xz" "$WORK_DIR/ogg.tar.xz"
    rm -rf "$WORK_DIR/ogg-$TARGET" && mkdir -p "$WORK_DIR/ogg-$TARGET" && tar xf "$WORK_DIR/ogg.tar.xz" -C "$WORK_DIR/ogg-$TARGET" --strip-components=1
    cd "$WORK_DIR/ogg-$TARGET"
    fix_config_sub
    ./configure --prefix="$PREFIX" $(autotools_host) --enable-static --disable-shared --with-pic
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_vorbis() {
    echo "Building libvorbis..."
    download "https://downloads.xiph.org/releases/vorbis/libvorbis-1.3.7.tar.xz" "$WORK_DIR/vorbis.tar.xz"
    rm -rf "$WORK_DIR/vorbis-$TARGET" && mkdir -p "$WORK_DIR/vorbis-$TARGET" && tar xf "$WORK_DIR/vorbis.tar.xz" -C "$WORK_DIR/vorbis-$TARGET" --strip-components=1
    cd "$WORK_DIR/vorbis-$TARGET"
    fix_config_sub
    ./configure --prefix="$PREFIX" $(autotools_host) --enable-static --disable-shared --with-pic --with-ogg="$PREFIX"
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_theora() {
    echo "Building libtheora..."
    download "https://downloads.xiph.org/releases/theora/libtheora-1.1.1.tar.bz2" "$WORK_DIR/theora.tar.bz2"
    rm -rf "$WORK_DIR/theora-$TARGET" && mkdir -p "$WORK_DIR/theora-$TARGET" && tar xf "$WORK_DIR/theora.tar.bz2" -C "$WORK_DIR/theora-$TARGET" --strip-components=1
    cd "$WORK_DIR/theora-$TARGET"
    fix_config_sub
    ./configure --prefix="$PREFIX" $(autotools_host) --enable-static --disable-shared --disable-examples --with-pic \
        --with-ogg="$PREFIX" --disable-oggtest --disable-vorbistest --disable-sdltest --disable-spec
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_webp() {
    echo "Building libwebp..."
    download "https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.5.0.tar.gz" "$WORK_DIR/webp.tar.gz"
    rm -rf "$WORK_DIR/webp-$TARGET" && mkdir -p "$WORK_DIR/webp-$TARGET" && tar xf "$WORK_DIR/webp.tar.gz" -C "$WORK_DIR/webp-$TARGET" --strip-components=1
    cd "$WORK_DIR/webp-$TARGET"
    fix_config_sub
    ./configure --prefix="$PREFIX" $(autotools_host) --enable-static --disable-shared --with-pic --enable-libwebpmux --enable-libwebpdemux
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_opencore_amr() {
    echo "Building opencore-amr..."
    download "https://downloads.sourceforge.net/project/opencore-amr/opencore-amr/opencore-amr-0.1.6.tar.gz" "$WORK_DIR/opencore-amr.tar.gz"
    rm -rf "$WORK_DIR/opencore-amr-$TARGET" && mkdir -p "$WORK_DIR/opencore-amr-$TARGET" && tar xf "$WORK_DIR/opencore-amr.tar.gz" -C "$WORK_DIR/opencore-amr-$TARGET" --strip-components=1
    cd "$WORK_DIR/opencore-amr-$TARGET"
    fix_config_sub
    ./configure --prefix="$PREFIX" $(autotools_host) --enable-static --disable-shared --with-pic
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

build_openh264() {
    echo "Building openh264..."
    git_clone "https://github.com/cisco/openh264.git" "$WORK_DIR/openh264-src" "v2.6.0"
    rm -rf "$WORK_DIR/openh264-$TARGET" && cp -a "$WORK_DIR/openh264-src" "$WORK_DIR/openh264-$TARGET"
    cd "$WORK_DIR/openh264-$TARGET"
    case "$TARGET" in
        linux-x64)     make -j$NPROC PREFIX="$PREFIX" BUILDTYPE=Release libraries install-static ;;
        win-x64)       make -j$NPROC OS=mingw_nt ARCH=x86_64 PREFIX="$PREFIX" BUILDTYPE=Release CC=$CC CXX=$CXX AR=$AR libraries install-static ;;
        android-arm64) make -j$NPROC OS=android ARCH=arm64 NDKROOT="$NDK_DIR" TARGET="android-$NDK_API" PREFIX="$PREFIX" BUILDTYPE=Release libraries install-static ;;
    esac
    cd "$PROJECT_DIR"
}

build_openjpeg() {
    echo "Building openjpeg..."
    git_clone "https://github.com/uclouvain/openjpeg.git" "$WORK_DIR/openjpeg-src" "v2.5.3"
    rm -rf "$WORK_DIR/openjpeg-build-$TARGET" && mkdir -p "$WORK_DIR/openjpeg-build-$TARGET" && cd "$WORK_DIR/openjpeg-build-$TARGET"
    cmake_build "$WORK_DIR/openjpeg-src" -DBUILD_SHARED_LIBS=OFF -DBUILD_CODEC=OFF -DBUILD_TESTING=OFF
    cd "$PROJECT_DIR"
}

build_xvid() {
    echo "Building libxvid..."
    download "https://downloads.xvid.com/downloads/xvidcore-1.3.7.tar.bz2" "$WORK_DIR/xvid.tar.bz2"
    rm -rf "$WORK_DIR/xvid-$TARGET" && mkdir -p "$WORK_DIR/xvid-$TARGET" && tar xf "$WORK_DIR/xvid.tar.bz2" -C "$WORK_DIR/xvid-$TARGET" --strip-components=1
    cd "$WORK_DIR/xvid-$TARGET/build/generic"
    fix_config_sub
    ./configure --prefix="$PREFIX" $(autotools_host) --disable-assembly
    # Remove -mno-cygwin (unsupported by modern mingw)
    sed -i 's/-mno-cygwin//g' platform.inc 2>/dev/null || true
    make -j$NPROC && make install
    rm -f "$PREFIX/lib/libxvidcore.so"* "$PREFIX/lib/libxvidcore.dll"* 2>/dev/null || true
    cd "$PROJECT_DIR"
}

build_zimg() {
    echo "Building zimg..."
    git_clone "https://github.com/sekrit-twc/zimg.git" "$WORK_DIR/zimg-src" "release-3.0.5"
    rm -rf "$WORK_DIR/zimg-$TARGET" && cp -a "$WORK_DIR/zimg-src" "$WORK_DIR/zimg-$TARGET"
    cd "$WORK_DIR/zimg-$TARGET"
    autoreconf -if
    fix_config_sub
    ./configure --prefix="$PREFIX" $(autotools_host) --enable-static --disable-shared --with-pic
    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

# ── Orchestration ──

build_all_deps() {
    local license="$1"
    build_ogg; build_vorbis; build_theora
    build_libaom; build_dav1d; build_svtav1; build_libvpx
    build_lame; build_opus; build_webp
    build_opencore_amr; build_openh264; build_openjpeg; build_zimg
    if [ "$license" = "gpl" ]; then
        build_x264; build_x265; build_xvid
    fi
}

build_ffmpeg() {
    local license="$1"
    local output_dir="$WORK_DIR/${TARGET}-${license}-out"
    local build_dir="$WORK_DIR/${TARGET}-${license}-ffmpeg"
    local gpl_flags=""
    local version3="--enable-version3"
    [ "$license" = "gpl" ] && gpl_flags="--enable-gpl --enable-libx264 --enable-libx265 --enable-libxvid"

    echo "Building FFmpeg $FFMPEG_VERSION ($license) for $TARGET..."

    # Unset dep-build env vars — FFmpeg configure uses its own --cc/--extra-cflags
    unset CC CXX AR NM RANLIB STRIP CFLAGS CXXFLAGS LDFLAGS

    rm -rf "$build_dir" && mkdir -p "$build_dir"
    tar xf "$WORK_DIR/ffmpeg-source.tar.xz" -C "$build_dir" --strip-components=1

    cd "$build_dir"
    export PKG_CONFIG_PATH="$PREFIX/lib/pkgconfig:$PREFIX/lib/x86_64-linux-gnu/pkgconfig"
    export PKG_CONFIG_LIBDIR="$PREFIX/lib/pkgconfig:$PREFIX/lib/x86_64-linux-gnu/pkgconfig"
    ./configure \
        --prefix="$output_dir" \
        --enable-shared --disable-static \
        --disable-programs --disable-doc \
        --disable-avdevice --disable-postproc --disable-avfilter \
        $version3 \
        $FFMPEG_TARGET_FLAGS \
        $FFMPEG_TOOLS \
        --extra-cflags="-O2 -fPIC -I$PREFIX/include" \
        --extra-ldflags="-L$PREFIX/lib -L$PREFIX/lib/x86_64-linux-gnu" \
        --extra-libs="-lm $([ "$TARGET" != "android-arm64" ] && echo "-lpthread")" \
        --pkg-config=pkg-config \
        --pkg-config-flags="--static" \
        --enable-libaom --enable-libdav1d --enable-libsvtav1 --enable-libvpx \
        --enable-libmp3lame --enable-libopus --enable-libvorbis --enable-libtheora \
        --enable-libwebp --enable-libopencore-amrnb --enable-libopencore-amrwb \
        --enable-libopenh264 --enable-libopenjpeg --enable-libzimg \
        $gpl_flags

    make -j$NPROC && make install
    cd "$PROJECT_DIR"
}

copy_output() {
    local license="$1"
    local suffix=""
    [ "$license" = "gpl" ] && suffix=".GPL"
    local src_dir="$WORK_DIR/${TARGET}-${license}-out/lib"
    local dst_dir="$PROJECT_DIR/src/Loxifi.FFmpeg.Runtime.${TARGET}${suffix}/runtimes/${TARGET}/native"
    mkdir -p "$dst_dir"
    rm -f "$dst_dir"/*.so* "$dst_dir"/*.dll 2>/dev/null

    case "$TARGET" in
        linux-x64)
            cp -L "$src_dir/libavformat.so.61" "$src_dir/libavcodec.so.61" "$src_dir/libavutil.so.59" \
                  "$src_dir/libswscale.so.8" "$src_dir/libswresample.so.5" "$dst_dir/"
            ;;
        win-x64)
            # FFmpeg cross-compiled for mingw produces .dll files in bin/
            local bin_dir="$WORK_DIR/${TARGET}-${license}-out/bin"
            cp "$bin_dir"/avformat-61.dll "$bin_dir"/avcodec-61.dll "$bin_dir"/avutil-59.dll \
               "$bin_dir"/swscale-8.dll "$bin_dir"/swresample-5.dll "$dst_dir/" 2>/dev/null || \
            cp "$bin_dir"/*.dll "$dst_dir/"
            ;;
        android-arm64)
            cp "$src_dir/libavformat.so" "$src_dir/libavcodec.so" "$src_dir/libavutil.so" \
               "$src_dir/libswscale.so" "$src_dir/libswresample.so" "$dst_dir/"
            # Bundle libc++_shared.so from NDK (required by C++ codec deps like x265, SVT-AV1)
            cp "$NDK_TOOLCHAIN/sysroot/usr/lib/aarch64-linux-android/libc++_shared.so" "$dst_dir/" 2>/dev/null || true
            ;;
    esac
    echo "Copied $TARGET ($license) to $dst_dir"
    local sample=$(ls "$dst_dir"/*avcodec* 2>/dev/null | head -1)
    [ -n "$sample" ] && strings "$sample" | grep "license" | head -1
}

build_target_license() {
    local target="$1" license="$2"
    echo ""
    echo "========================================"
    echo "  $target — FFmpeg $FFMPEG_VERSION ($license)"
    echo "========================================"

    setup_target "$target"

    download "$FFMPEG_SOURCE_URL" "$WORK_DIR/ffmpeg-source.tar.xz"

    # Check tools
    for tool in cmake meson ninja nasm autoconf automake libtool; do
        command -v $tool &>/dev/null || { echo "ERROR: $tool not installed"; exit 1; }
    done
    [ "$target" = "win-x64" ] && { command -v x86_64-w64-mingw32-gcc &>/dev/null || { echo "ERROR: mingw-w64 not installed (apt install gcc-mingw-w64-x86-64 g++-mingw-w64-x86-64)"; exit 1; }; }

    build_all_deps "$license"
    build_ffmpeg "$license"
    copy_output "$license"
}

# ── Main ──

run_license() {
    local license="$1"
    case "$PLATFORM_MODE" in
        linux-x64|win-x64|android-arm64) build_target_license "$PLATFORM_MODE" "$license" ;;
        all)
            for t in linux-x64 android-arm64 win-x64; do
                build_target_license "$t" "$license"
            done
            ;;
        *) echo "Unknown platform: $PLATFORM_MODE"; exit 1 ;;
    esac
}

case "$LICENSE_MODE" in
    lgpl) run_license "lgpl" ;;
    gpl)  run_license "gpl" ;;
    both) run_license "lgpl"; run_license "gpl" ;;
    *)    echo "Usage: $0 [lgpl|gpl|both] [linux-x64|win-x64|android-arm64|all]"; exit 1 ;;
esac

echo ""
echo "Done."
