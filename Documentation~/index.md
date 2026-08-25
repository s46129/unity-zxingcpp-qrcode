# ZXing-C++ QR Code for Unity

ZXing-C++ QR Code for Unity is a reusable Unity package that decodes QR Codes from Gray8/luminance buffers through the ZXing-C++ C API. It supports Windows x86_64 and Android arm64-v8a with one C# API and is maintained by [s46129](https://github.com/s46129).

## Runtime flow

1. Supply a `Gray8Image` backed by `byte[]`.
2. Call `ZXingCppQRCodeDecoder.TryDecode` directly, or submit frames to `QRCodeScanner`.
3. The decoder pins the Gray8 array only for the native call. A full-frame decode has no image copy.
4. A normalized ROI uses `ZXing_ImageView_crop` without copying when `DownscaleFactor` is `1`.
5. A managed downscale uses box averaging on luminance bytes, then maps result corners back into original image coordinates.

## API example

```csharp
using S46129.QRCode;

var decoder = new ZXingCppQRCodeDecoder();
var options = new QRCodeDecodeOptions
{
    Region = new QRCodeRegion(0.2f, 0.2f, 0.6f, 0.6f),
    DownscaleFactor = 2,
    TryRotate = true,
    TryInvert = false
};

var image = new Gray8Image(grayBytes, width, height, rowStride);
if (decoder.TryDecode(image, options, out QRCodeResult result))
    UnityEngine.Debug.Log(result.Text);
```

## Scanner example

```csharp
var scanner = new QRCodeScanner(decoder, new QRCodeScannerOptions
{
    ScanInterval = System.TimeSpan.FromMilliseconds(150),
    StopOnSuccess = true,
    DecodeOptions = options
});

scanner.Detected += result => UnityEngine.Debug.Log(result.Text);
scanner.ScanCompleted += completion => ReuseBuffer(completion.Frame.Buffer);
scanner.Start();

// Call from the camera update loop. False means the frame was rate-limited or a worker is busy.
scanner.TrySubmitFrame(new Gray8Image(yPlane, width, height, yRowStride));
```

Create the scanner on Unity's main thread. It captures the current `SynchronizationContext`, so result events are posted back there. An accepted frame buffer is borrowed and must not be changed or returned to a pool until `ScanCompleted` fires.

See [Installation and native builds](installation.md), [acceptance specification](Spec/QRCodePackageSpec1.md), and import the Basic Gray8 Decode sample from Package Manager.
