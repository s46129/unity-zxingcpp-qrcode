param(
    [string]$AndroidNdkRoot = $env:ANDROID_NDK_ROOT,
    [ValidateSet('Release', 'RelWithDebInfo')]
    [string]$Configuration = 'Release',
    [int]$AndroidApi = 24
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($AndroidNdkRoot)) {
    throw 'Pass -AndroidNdkRoot or set ANDROID_NDK_ROOT.'
}

$toolchain = Join-Path $AndroidNdkRoot 'build/cmake/android.toolchain.cmake'
if (-not (Test-Path -LiteralPath $toolchain)) {
    throw "Android NDK toolchain was not found at $toolchain"
}

$nativeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildRoot = Join-Path $nativeRoot 'build/android-arm64-v8a'

cmake -S $nativeRoot -B $buildRoot -G Ninja `
    -DCMAKE_TOOLCHAIN_FILE=$toolchain `
    -DANDROID_ABI=arm64-v8a `
    -DANDROID_PLATFORM="android-$AndroidApi" `
    -DCMAKE_BUILD_TYPE=$Configuration
if ($LASTEXITCODE -ne 0) { throw 'CMake configure failed.' }

cmake --build $buildRoot --parallel
if ($LASTEXITCODE -ne 0) { throw 'Android native build failed.' }

Write-Host 'Built Runtime/Plugins/Android/arm64-v8a/libZXing.so'
