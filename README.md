# ZXing-C++ QR Code for Unity

Reusable Unity Package Manager package for decoding QR Codes from Gray8/luminance buffers with the ZXing-C++ C API.

## Relationship to ZXing-C++

This is an unofficial Unity integration for the [ZXing-C++ project](https://github.com/zxing-cpp/zxing-cpp). It is not affiliated with or endorsed by the ZXing-C++ project or its contributors.

The native decoding engine is built without source patches from [ZXing-C++ v3.1.0](https://github.com/zxing-cpp/zxing-cpp/releases/tag/v3.1.0), pinned to commit [`885baaf`](https://github.com/zxing-cpp/zxing-cpp/commit/885baaf0840335153c1a487fa65f9c1388702c81). This repository provides the Unity package structure, C# P/Invoke API, Gray8 preprocessing, asynchronous scanner, native build scripts, prebuilt plugins, samples, documentation, and tests.

Maintained as a personal open-source project by [s46129](https://github.com/s46129). The UPM package identity is `com.s46129.qrcode`; application code uses the descriptive `ZXingCpp.QRCode` namespace.

- Windows x86_64 and Android arm64-v8a native plugin layout
- No `Color32`/RGBA conversion in the decode path
- Zero-copy full-frame and ROI decode when managed downscale is disabled
- Background scanner with frequency limiting, busy-frame dropping, ROI, downscale, and stop-on-success
- Pure C# scheduling logic covered by EditMode tests

The package includes verified Windows x86_64 and Android arm64-v8a binaries built from pinned ZXing-C++ v3.1.0 source. Reproduce or update them with the scripts in `Native~`; see `Documentation~/installation.md`.

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
