#!/usr/bin/env sh
set -eu

if [ -z "${ANDROID_NDK_ROOT:-}" ]; then
    echo "Set ANDROID_NDK_ROOT before running this script." >&2
    exit 1
fi

native_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
build_root="$native_root/build/android-arm64-v8a"
android_api="${ANDROID_API:-24}"

cmake -S "$native_root" -B "$build_root" -G Ninja \
    -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_ROOT/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI=arm64-v8a \
    -DANDROID_PLATFORM="android-$android_api" \
    -DCMAKE_BUILD_TYPE=Release

cmake --build "$build_root" --parallel
echo "Built Runtime/Plugins/Android/arm64-v8a/libZXing.so"
