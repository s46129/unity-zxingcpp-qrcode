# Performance

Measured numbers for the frame path, so the claims in the README can be checked against a real device. All figures come from Instruments (`xctrace`) on the device; nothing here is estimated.

## Setup

| | |
|---|---|
| Device | iPhone 17 (`iPhone18,3`), iOS 26.6.1 |
| Build | Unity 2022.3.62f2, IL2CPP, Release, Xcode 26.6 |
| Scene | `WebcamDecode.unity` from the Basic Gray8 Decode sample, package commit `5dde890` |
| Camera | 1280×720 requested, 30 fps, back camera |
| Runner | **Use Gpu Readback** on (label shows `gpu`), **Downscale Factor** 1, **Scan Interval** 0.2 s, **Suppress Repeats** on |
| Profiler | Instruments Time Profiler via `xctrace record`, 30 s, 1 ms sampling, app launched by `xctrace` |
| Window | the first 3 s (launch, dyld, permission check) are excluded; 27 s analysed |

A QR code was in view for part of the recording, so both successful decodes and empty scans are included.

## Main thread

| Call path | CPU over 27 s | Per second |
|---|---:|---:|
| Whole main thread | 1050 ms | 3.9% of one core |
| `QRCodeSampleRunner.Update` (everything the sample does per frame) | 78 ms | 2.9 ms |
| `Gray8TextureReadback.TryRequest` (blit + `AsyncGPUReadback` request) | 17 ms | 0.6 ms |
| Readback callback → `CopyFrame` + `QRCodeScanner.TrySubmitFrame` | 9 ms | 0.3 ms |
| Engine-side `AsyncGPUReadback` update | 9 ms | 0.3 ms |
| `QRCodeScanner.CanAcceptFrame` | 3 ms | 0.1 ms |
| `AlignWebcamPreview` (rotation, mirror, letterbox) | 5 ms | 0.2 ms |
| `GetPixels32`, `Gray8RowOrder.FlipVertically`, ZXing | 0 ms | the GPU path never runs them on the main thread |
| UI canvas rebuild / TextMesh Pro (stats label changes every 0.5 s) | 144 ms | 5.3 ms |
| `PlayerRender` | 173 ms | 6.4 ms |

The whole GPU frame path (request, engine readback, copy, submit) costs about 35 ms of main-thread CPU per 27 s, roughly 1.3 ms per second, or 0.13 ms per accepted frame at the 0.2 s scan interval. The largest single item on the main thread is the sample's own stats label, not the decoder.

Instruments' `potential-hangs` and `hang-risks` tables are empty for the run: no main-thread stall above 250 ms.

## Worker threads

| Thread | CPU over 27 s | Notes |
|---|---:|---|
| ZXing decode (`Task.Run` worker) | 56 ms sampled | about 1.2 ms per sampled decode; heaviest symbol `HybridBinarizer::getBlackMatrix`. Decodes that finish inside one 1 ms sample are not counted, so the per-decode figure is an upper bound |
| `UnityGfxDeviceWorker` | 733 ms | Metal render-target setup and buffer allocation; Unity's normal rendering cost |
| Unnamed threads | 649 ms | Metal command-buffer submission and `CAMetalDrawable present`; also rendering |
| `IL2CPP Threadpool worker` | 65 ms | scanner task scheduling |

## Not measured yet

- The CPU path (**Use Gpu Readback** off) on the same device. The toggle is a serialized scene field, so a comparison needs a second build. On the Editor the CPU path costs one `GetPixels32` of the full RGBA frame plus a managed luma loop per accepted frame; on a 1280×720 frame that is several milliseconds of main-thread time each scan.
- Android devices.
- Other iPhone generations; an iPhone 17 is at the fast end.

## Reproduce

Build the sample to a device with Xcode, then, with the phone unlocked and connected:

```sh
xcrun xctrace list devices                       # find the UDID
xcrun xctrace record --device <udid> --template 'Time Profiler' --time-limit 30s \
    --output run.trace --launch -- <bundle id>
xcrun xctrace export --input run.trace \
    --xpath '/trace-toc/run[@number="1"]/data/table[@schema="time-profile"]' --output run.xml
python3 Documentation~/Profiling/xctrace_time_profile.py run.xml 3
```

The script prints CPU time per thread, the inclusive cost of each step of the frame path on the main thread, and the ZXing decode cost on the worker threads. Open `run.trace` in Instruments for the call tree; the Release build keeps enough symbols for IL2CPP method names to appear.
