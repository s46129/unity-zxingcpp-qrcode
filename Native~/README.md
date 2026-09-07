# Native build

The native library is the upstream ZXing-C++ shared target with its C API enabled. CMake pins v3.1.0 at commit `885baaf0840335153c1a487fa65f9c1388702c81`, enables only readers and QR Code, and copies the output into the correct Unity plugin directory.

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

After either build, return to Unity and use **Tools > ZXing-C++ QR Code > Apply Native Plugin Import Settings**.

When replacing a bundled binary, update the build provenance and dependency licenses in `Documentation~/AI/LicenseCompliance.md` and `Third Party Notices.md`. A change to the ZXing-C++ pin, Android NDK, Visual C++ toolset, runtime linkage, or CMake feature flags requires a new third-party license audit.
