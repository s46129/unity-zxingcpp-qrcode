# Installation and native builds

## Add the source package

In Unity Package Manager, choose **Add package from git URL** and enter `https://github.com/s46129/unity-zxingcpp-qrcode.git`. You can also copy/link the source under the project's `Packages/com.s46129.qrcode` directory. The minimum supported Unity version is 2021.3.

## Build the native plugins

The package includes verified Windows x86_64, macOS universal (arm64 + x86_64), Android arm64-v8a and iOS arm64 binaries. The scripts under `Native~` reproduce them by fetching ZXing-C++ v3.1.0 at commit `885baaf0840335153c1a487fa65f9c1388702c81` and building a reader-only library: a shared library everywhere except iOS, where Unity requires a static library.

### Windows x86_64

Install CMake and Visual Studio 2019 16.10 or newer C++ tools, then run (the script detects VS 2019/2022):

```powershell
./Native~/build-windows.ps1
```

Output: `Runtime/Plugins/Windows/x86_64/ZXing.dll`.

### Android arm64-v8a

Use Unity Hub's Android SDK & NDK Tools module, CMake, and Ninja. Pass Unity's NDK folder:

```powershell
./Native~/build-android.ps1 -AndroidNdkRoot 'C:/Program Files/Unity/Hub/Editor/<version>/Editor/Data/PlaybackEngines/AndroidPlayer/NDK'
```

Output: `Runtime/Plugins/Android/arm64-v8a/libZXing.so`.

### macOS universal

Install Xcode (or the Command Line Tools) and CMake 3.21+. Ninja is used when it is on `PATH`; otherwise the script falls back to Unix Makefiles. Unity's Android SDK & NDK Tools module ships both CMake and Ninja under `PlaybackEngines/AndroidPlayer/SDK/cmake/<version>/bin`, which is enough if no other CMake is installed:

```sh
./Native~/build-macos.sh
```

Output: `Runtime/Plugins/macOS/libZXing.dylib`, an ad-hoc signed universal dylib with a minimum deployment target of macOS 11.0 (override with `MACOSX_DEPLOYMENT_TARGET`). It links only the system `libc++` and `libSystem`.

### iOS arm64

Install Xcode with the iOS SDK and CMake 3.21+:

```sh
./Native~/build-ios.sh
```

Output: `Runtime/Plugins/iOS/libZXing.a`, a device-only static library with a minimum deployment target of iOS 12.0 (override with `IPHONEOS_DEPLOYMENT_TARGET`). The build refuses a simulator SDK because the shipped file would be overwritten by a slice the device build cannot use.

## Plugin importer settings

After the build, select **Tools > ZXing-C++ QR Code > Apply Native Plugin Import Settings**. The helper applies these settings:

| Binary | Any Platform | Editor | Build target | CPU |
|---|---:|---:|---|---|
| `Windows/x86_64/ZXing.dll` | Off | On, Windows only | Standalone Windows 64-bit | x86_64 |
| `macOS/libZXing.dylib` | Off | On, macOS only (Any CPU) | Standalone macOS | Any CPU |
| `Android/arm64-v8a/libZXing.so` | Off | Off | Android | ARM64 |
| `iOS/libZXing.a` | Off | Off | iOS | — |

For Android Player Settings, include ARM64 and use IL2CPP or Mono as required by the project. The native library name is `ZXing` on Windows, macOS and Android; on iOS the static library is linked into the player, so the bindings use `[DllImport("__Internal")]` there. Unity adds `libZXing.a` to the generated Xcode project automatically; no framework dependencies or embedded binaries are needed.

## Texture and camera input

- Prefer camera Y planes, `TextureFormat.R8`, or `TextureFormat.Alpha8` buffers.
- `Gray8Image` accepts padded row stride and byte offset, which avoids repacking many camera planes.
- `Texture2D.GetRawTextureData<byte>()` returns native-backed memory. The sample copies it once into `byte[]` because background work cannot safely borrow the `NativeArray` after the call site.
- `QRCodeRegion` and `QRCodePoint` are measured from the image's top-left corner, and the decoder reads the first row of a `Gray8Image` as the top row. Texture rows arrive in the opposite order, so call `Gray8RowOrder.FlipVertically(frame)` on texture data before submitting it; otherwise corners land near the bottom edge, ROIs select the mirrored half, and `Orientation`/`IsMirrored` describe the flipped copy instead of the source.
- Avoid `GetPixels32`; it creates an RGBA representation and adds a conversion before decoding.

## Troubleshooting

- `DllNotFoundException` wrapped by `QRCodeNativeException`: verify that the binary exists and importer settings match the active platform/CPU.
- `EntryPointNotFoundException`: rebuild from the pinned source with `ZXING_C_API=ON`; a different system ZXing library may not export the required API.
- Android or iOS works in build but not Editor: expected. Those plugins are excluded from the Editor; the Windows DLL and the macOS dylib are the Editor plugins on their respective hosts.
- macOS refuses to load `libZXing.dylib` after a local rebuild: Apple silicon requires a code signature. The build target re-signs ad hoc after stripping; if you copied the file by other means, run `codesign --force --sign - libZXing.dylib`.
- iOS link errors for `_ZXing_*` symbols: the `.a` was not added to the Xcode project, usually because the importer is not set to iOS. Re-run the importer helper.
- iOS Simulator link errors: expected. Only the device slice ships; build a simulator slice separately with `-DCMAKE_OSX_SYSROOT=iphonesimulator` into a build directory outside the package.
- No result after aggressive managed downscale: reduce `DownscaleFactor` so each QR module remains several pixels wide.
