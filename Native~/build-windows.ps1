param(
    [ValidateSet('Release', 'RelWithDebInfo')]
    [string]$Configuration = 'Release',
    [string]$Generator
)

$ErrorActionPreference = 'Stop'
$nativeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildRoot = Join-Path $nativeRoot 'build/windows-x86_64'

if ([string]::IsNullOrWhiteSpace($Generator)) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw 'Visual Studio Installer (vswhere.exe) was not found. Pass -Generator explicitly.'
    }

    $installationVersion = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationVersion
    if ([string]::IsNullOrWhiteSpace($installationVersion)) {
        throw 'Visual Studio C++ build tools were not found.'
    }

    $visualStudioMajor = ([Version]$installationVersion).Major
    if ($visualStudioMajor -ge 17) {
        $Generator = 'Visual Studio 17 2022'
    } elseif ($visualStudioMajor -eq 16) {
        $Generator = 'Visual Studio 16 2019'
    } else {
        throw "Visual Studio 2019 or newer is required; found major version $visualStudioMajor."
    }
}

cmake -S $nativeRoot -B $buildRoot -G $Generator -A x64
if ($LASTEXITCODE -ne 0) { throw 'CMake configure failed.' }

cmake --build $buildRoot --config $Configuration --parallel
if ($LASTEXITCODE -ne 0) { throw 'Windows native build failed.' }

Write-Host 'Built Runtime/Plugins/Windows/x86_64/ZXing.dll'
