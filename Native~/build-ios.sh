#!/usr/bin/env sh
# Builds the iOS arm64 device static library into Runtime/Plugins/iOS/libZXing.a.
# Requires Xcode with the iOS SDK and CMake 3.21+; Ninja is used when available.
# Override the tools with CMAKE=/path/to/cmake and CMAKE_GENERATOR=<generator>.
set -eu

native_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
build_root="$native_root/build/ios-arm64"
cmake_bin="${CMAKE:-cmake}"
deployment_target="${IPHONEOS_DEPLOYMENT_TARGET:-12.0}"

if [ -z "${CMAKE_GENERATOR:-}" ]; then
    if command -v ninja >/dev/null 2>&1; then
        CMAKE_GENERATOR="Ninja"
    else
        CMAKE_GENERATOR="Unix Makefiles"
    fi
fi

"$cmake_bin" -S "$native_root" -B "$build_root" -G "$CMAKE_GENERATOR" \
    -DCMAKE_SYSTEM_NAME=iOS \
    -DCMAKE_OSX_SYSROOT=iphoneos \
    -DCMAKE_OSX_ARCHITECTURES=arm64 \
    "-DCMAKE_OSX_DEPLOYMENT_TARGET=$deployment_target" \
    -DCMAKE_BUILD_TYPE=Release

"$cmake_bin" --build "$build_root" --parallel
echo "Built Runtime/Plugins/iOS/libZXing.a"
