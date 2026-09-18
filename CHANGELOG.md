# Changelog

## Unreleased

- Added macOS support: a universal (arm64 + x86_64) `Runtime/Plugins/macOS/libZXing.dylib` that both the Apple silicon and the Intel Editor load, with a minimum deployment target of macOS 11.0 (#12).
- Added iOS support: a device-only arm64 static library `Runtime/Plugins/iOS/libZXing.a` linked into the player through `DllImport("__Internal")`, with a minimum deployment target of iOS 12.0 (#12).
- Added `Native~/build-macos.sh` and `Native~/build-ios.sh`; `Native~/CMakeLists.txt` now selects the plugin directory and library kind per Apple target and refuses partial macOS slices and iOS simulator builds (#12).
- Added a supported-platform table and the per-target build environment to the README (#12).
- The native decoder end-to-end EditMode tests now run on the macOS Editor instead of being ignored (#12).
- The plugin importer helper now configures the macOS and iOS plugins and disables every other bundled plugin on those targets (#12).
- Fixed the webcam sample on phones: it now requests camera permission before creating the `WebCamTexture` (the first iOS launch used to stay black) and turns the preview by `videoRotationAngle`/`videoVerticallyMirrored` so it renders upright (#12).

## 1.1.0 - 2026-09-08

- Added `QRCodeScannerOptions.SuppressRepeats` and `MissesBeforeReset`: a code that stays in view raises `Detected` once and fires again only after the configured number of empty scans (#10).
- Added `QRCodeScanner.TrySubmitFrame(Func<Gray8Image>)`: the frame is built only after the scanner accepts it, so a rejected submit costs nothing (#3).
- Added `Gray8RowOrder.FlipVertically` as the explicit exit from Unity's bottom-up texture rows into the top-left image contract (#4).
- Added `BasicGray8Decode.unity` and `WebcamDecode.unity` sample scenes with bundled R8 textures, on-screen result, FPS and scans per second (#8, #9).
- Added end-to-end EditMode tests that decode through the bundled Windows native binary (#6).
- Added a CMake check that fails the Android build when `libZXing.so` is not 16 KB page aligned (#1).
- Fixed the Android plugin: rebuilt with 16 KB ELF page alignment for Android 15+ devices (#1).
- Fixed `PixelRegion.Resolve` accepting ROIs outside the image, which could read past the buffer under managed downscale (#2).
- Fixed the texture sample copying every frame; it now copies only frames the scanner accepts, mip level 0 only, through `ArrayPool` (#3).
- Fixed the texture sample handing bottom-up rows to the decoder, which mirrored corner points and orientation (#4).
- Fixed the scanner staying busy forever when `Task.Run` or the callback context failed; an accepted frame now completes exactly once (#5).

## 1.0.0 - 2026-08-25

- Initial reusable UPM package.
- Added the publisher-neutral `ZXingCpp.QRCode` public namespace.
- Added ZXing-C++ C API bridge for Gray8 buffers.
- Added background scanner, ROI/downscale preprocessing, sample, build scripts, importer helper, and EditMode tests.
- Added explicit upstream attribution, unofficial-integration status, and complete notices for bundled third-party components.
