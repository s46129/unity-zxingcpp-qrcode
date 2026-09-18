# Native build

The native library is the upstream ZXing-C++ `ZXing` target with its C API enabled: a shared library on Windows, macOS and Android, and a static library on iOS (Unity links iOS plugins into the player). CMake pins v3.1.0 at commit `885baaf0840335153c1a487fa65f9c1388702c81`, enables only readers and QR Code, and copies the output into the correct Unity plugin directory.

| Target | Host | Requirements | Output |
|---|---|---|---|
| Windows x86_64 | Windows | CMake 3.21+, Visual Studio 2019 16.10+ / 2022 with Desktop development with C++ | `Runtime/Plugins/Windows/x86_64/ZXing.dll` |
| macOS universal (arm64 + x86_64) | macOS | CMake 3.21+, Xcode or Command Line Tools; Ninja optional | `Runtime/Plugins/macOS/libZXing.dylib` |
| Android arm64-v8a | any | CMake 3.21+, Ninja, Android NDK r23+ | `Runtime/Plugins/Android/arm64-v8a/libZXing.so` |
| iOS arm64 (device) | macOS | CMake 3.21+, Xcode with the iOS SDK; Ninja optional | `Runtime/Plugins/iOS/libZXing.a` |

All scripts run `cmake` from `PATH`; the shell scripts also honour `CMAKE=/path/to/cmake` and `CMAKE_GENERATOR`. Unity's Android SDK & NDK Tools module bundles CMake and Ninja under `PlaybackEngines/AndroidPlayer/SDK/cmake/<version>/bin`.

## Windows x86_64

Requirements: CMake 3.21+, Visual Studio 2019 16.10 or newer with Desktop development with C++. The script detects Visual Studio 2019/2022 automatically.

```powershell
./build-windows.ps1
```

## Android arm64-v8a

Requirements: CMake 3.21+, Ninja, Android NDK r23 or newer. Unity's installed NDK is suitable.

```powershell
./build-android.ps1 -AndroidNdkRoot 'C:/path/to/ndk'
```

On macOS/Linux, set `ANDROID_NDK_ROOT` and run `./build-android.sh`.

### 16 KB page sizes

The Android build links with `-Wl,-z,max-page-size=16384` and `-Wl,-z,common-page-size=16384` so the plugin loads on 16 KB page-size devices, and it fails the build if any `PT_LOAD` segment of the stripped output is aligned below 16 KB. Check a binary outside a build with:

```powershell
cmake -DELF_FILE=Runtime/Plugins/Android/arm64-v8a/libZXing.so -P 'Native~/cmake/CheckElfAlignment.cmake'
```

That check is static. Confirming the plugin actually loads needs a 16 KB device or emulator image, where `adb shell getconf PAGE_SIZE` reports `16384`: run a player that decodes a QR code, and watch `adb logcat` for `dlopen failed: ... not page aligned`. The bundled plugin is only one of the shared libraries in an APK — verify the packaging alignment of the whole APK (`zipalign -c -P 16 -v 4 <apk>`) and the other native dependencies separately.

## macOS universal

```sh
./build-macos.sh
```

The script passes `-DCMAKE_OSX_ARCHITECTURES=arm64;x86_64` and a deployment target of macOS 11.0 (`MACOSX_DEPLOYMENT_TARGET` overrides it). CMake refuses a single-architecture build because one dylib serves both the Apple silicon and the Intel Editor. The copy step renames upstream's versioned `libZXing.<soname>.dylib` to `libZXing.dylib`, sets its install name to `@rpath/libZXing.dylib`, strips local symbols and re-signs ad hoc, since Apple silicon refuses unsigned code and `strip` invalidates the linker's signature. The result links only the system `libc++` and `libSystem`; verify with `otool -L`.

## iOS arm64

```sh
./build-ios.sh
```

The script configures with `-DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_SYSROOT=iphoneos -DCMAKE_OSX_ARCHITECTURES=arm64` and a deployment target of iOS 12.0 (`IPHONEOS_DEPLOYMENT_TARGET` overrides it). `BUILD_SHARED_LIBS` is switched off for iOS so the output is `libZXing.a`; the C# bindings use `DllImport("__Internal")` on iOS to match. CMake refuses a simulator sysroot because the package ships the device slice only.

After any build, return to Unity and use **Tools > ZXing-C++ QR Code > Apply Native Plugin Import Settings**.

When replacing a bundled binary, update the build provenance and dependency licenses in `Documentation~/AI/LicenseCompliance.md` and `Third Party Notices.md`. A change to the ZXing-C++ pin, Android NDK, Visual C++ toolset, Xcode/Apple SDK, runtime linkage, or CMake feature flags requires a new third-party license audit.
