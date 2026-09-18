#!/usr/bin/env sh
# Builds the universal (arm64 + x86_64) macOS plugin into Runtime/Plugins/macOS/libZXing.dylib.
# Requires Xcode (or the Command Line Tools) and CMake 3.21+; Ninja is used when available.
# Override the tools with CMAKE=/path/to/cmake and CMAKE_GENERATOR=<generator>.
set -eu

native_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
build_root="$native_root/build/macos-universal"
cmake_bin="${CMAKE:-cmake}"
deployment_target="${MACOSX_DEPLOYMENT_TARGET:-11.0}"

if [ -z "${CMAKE_GENERATOR:-}" ]; then
    if command -v ninja >/dev/null 2>&1; then
        CMAKE_GENERATOR="Ninja"
    else
        CMAKE_GENERATOR="Unix Makefiles"
    fi
fi

"$cmake_bin" -S "$native_root" -B "$build_root" -G "$CMAKE_GENERATOR" \
    "-DCMAKE_OSX_ARCHITECTURES=arm64;x86_64" \
    "-DCMAKE_OSX_DEPLOYMENT_TARGET=$deployment_target" \
    -DCMAKE_BUILD_TYPE=Release

"$cmake_bin" --build "$build_root" --parallel
echo "Built Runtime/Plugins/macOS/libZXing.dylib"
