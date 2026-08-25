# ZXing-C++ QR Code for Unity

Reusable Unity Package Manager package for decoding QR Codes from Gray8/luminance buffers with the ZXing-C++ C API.

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
