# ZXing-C++ QR Code for Unity

Reusable Unity Package Manager package for decoding QR Codes from Gray8/luminance buffers with the ZXing-C++ C API.

## Relationship to ZXing-C++

This is an unofficial Unity integration for the [ZXing-C++ project](https://github.com/zxing-cpp/zxing-cpp). It is not affiliated with or endorsed by the ZXing-C++ project or its contributors.

The native decoding engine is built without source patches from [ZXing-C++ v3.1.0](https://github.com/zxing-cpp/zxing-cpp/releases/tag/v3.1.0), pinned to commit [`885baaf`](https://github.com/zxing-cpp/zxing-cpp/commit/885baaf0840335153c1a487fa65f9c1388702c81). This repository provides the Unity package structure, C# P/Invoke API, Gray8 preprocessing, asynchronous scanner, native build scripts, prebuilt plugins, samples, documentation, and tests.

Maintained as a personal open-source project by [s46129](https://github.com/s46129). The UPM package identity is `com.s46129.qrcode`; application code uses the descriptive `ZXingCpp.QRCode` namespace.

- Windows, macOS, Android and iOS native plugins behind one `ZXingCpp.QRCode` API
- No `Color32`/RGBA conversion in the decode path
- Zero-copy full-frame and ROI decode when managed downscale is disabled
- Background scanner with frequency limiting, busy-frame dropping, ROI, downscale, and stop-on-success
- Pure C# scheduling logic covered by EditMode tests

The package includes prebuilt binaries for every supported platform, all built from the pinned ZXing-C++ v3.1.0 source. Reproduce or update them with the scripts in `Native~`; see `Documentation~/installation.md`.

## Supported platforms

| Platform | Architecture | Bundled plugin | Editor | Player | Minimum OS |
|---|---|---|---|---|---|
| Windows | x86_64 | `Runtime/Plugins/Windows/x86_64/ZXing.dll` | Yes (Windows Editor) | Standalone Windows 64-bit | Whatever the Unity version requires |
| macOS | arm64 + x86_64 (universal) | `Runtime/Plugins/macOS/libZXing.dylib` | Yes (Apple silicon and Intel Editor) | Standalone macOS | macOS 11.0 |
| Android | arm64-v8a | `Runtime/Plugins/Android/arm64-v8a/libZXing.so` | No | Android (ARM64, IL2CPP or Mono) | Android 7.0 (API 24), 16 KB page sizes supported |
| iOS | arm64 (device) | `Runtime/Plugins/iOS/libZXing.a` | No | iOS (IL2CPP) | iOS 12.0 |

Not supported: Windows x86 (32-bit), Android armeabi-v7a/x86, iOS Simulator, tvOS, visionOS, Linux, WebGL, and UWP. The C# API compiles everywhere, but `ZXingCppQRCodeDecoder` throws `QRCodeNativeException` on a platform without a bundled plugin.

Requirements:

- Unity 2021.3 or newer. The Windows and macOS binaries are also what the Editor loads, so EditMode tests and Play Mode work on both desktop Editors.
- iOS: the plugin is a static library linked into the player, so the C# bindings switch to `DllImport("__Internal")` on iOS builds. Build the generated project with the Xcode version your Unity release supports; no extra frameworks or embedded binaries are required.
- Android: enable the ARM64 target architecture in Player Settings. The plugin statically links libc++, so no `libc++_shared.so` is deployed.

### Build environment for the native plugins

The bundled binaries are ready to use; this is only needed to rebuild or update them.

| Target | Host OS | Toolchain | Script |
|---|---|---|---|
| Windows x86_64 | Windows | CMake 3.21+, Visual Studio 2019 16.10+ or 2022 with C++ desktop workload | `Native~/build-windows.ps1` |
| macOS universal | macOS | CMake 3.21+, Xcode (or Command Line Tools), Ninja optional | `Native~/build-macos.sh` |
| Android arm64-v8a | Windows, macOS or Linux | CMake 3.21+, Ninja, Android NDK r23+ (Unity's bundled NDK works) | `Native~/build-android.ps1` / `build-android.sh` |
| iOS arm64 | macOS | CMake 3.21+, Xcode with the iOS SDK, Ninja optional | `Native~/build-ios.sh` |

Every build fetches the pinned ZXing-C++ source over Git on first configure, so network access is required once per build directory. See `Native~/README.md` for details.

## Install from Git

In Unity Package Manager, choose **Add package from git URL** and enter:

```text
https://github.com/s46129/unity-zxingcpp-qrcode.git
```

## Quick start

```csharp
using ZXingCpp.QRCode;

var decoder = new ZXingCppQRCodeDecoder();
var image = new Gray8Image(grayBytes, width, height);

if (decoder.TryDecode(image, out var result))
    UnityEngine.Debug.Log(result.Text);
```

For continuous camera frames, use `QRCodeScanner`; create it on Unity's main thread so callbacks are posted back to that thread.

`Gray8Image` rows run from the top of the image down, matching the top-left origin of `QRCodeRegion` and of the corners in `QRCodeResult`. Texture data arrives bottom-up, so pass it through `Gray8RowOrder.FlipVertically(image)` first.

## License and attribution

Original code developed for this Unity package is licensed under the [Apache License 2.0](LICENSE.md). The bundled native plugins include ZXing-C++ and other third-party components that remain under their respective licenses; see [Third-party notices](Third%20Party%20Notices.md) and the included [ZXing-C++ license](ZXing-C++%20LICENSE.md).
