# Basic Gray8 Decode sample

1. Import this sample from Package Manager.
2. Open `BasicGray8Decode.unity` and enter Play Mode. The scene shows the source texture in a `RawImage` and, once the background decode finishes, prints the payload, the four corner points, orientation and mirroring in the label below it. The same result is also logged to the Console.
3. To see a QR that is not centred, set the runner's **Source Texture** to `Textures/QRCodeSample_TopLeft` and play again — the top-left corner should read close to `(16, 16)`.

Both textures are single-channel `R8`, so `RawImage` renders them through the red channel only; the red tint is expected and has nothing to do with the decode. The decoder never sees colour — it reads the same bytes that `Gray8Image` wraps.

To use the runner in your own scene instead: add `QRCodeSampleRunner` to a GameObject, assign a readable `Texture2D` whose format is `R8` or `Alpha8`, and optionally a `TMP_Text` for **Result Label**.

## Webcam

Open `WebcamDecode.unity` and enter Play Mode. The runner starts the default `WebCamTexture` (set **Webcam Device Name** to pick another camera; leave it empty for the default), shows it in the `RawImage`, and submits a frame whenever the camera delivers a new one. **Stop On Success** is off in this scene so it keeps scanning, and **Suppress Repeats** is on with **Misses Before Reset** at 3: a code that stays in view fires `Detected` once; take it out of view for three scan intervals (0.6 s at the default 0.2 s) and bring it back to fire again. The small label in the top-left corner shows the rendered FPS and how many scans complete per second.

Each accepted frame is converted from RGBA to Gray8 on the main thread (integer Rec. 601 luma) into an `ArrayPool<byte>` buffer, then flipped with `Gray8RowOrder.FlipVertically` because `WebCamTexture` rows start at the displayed bottom like every Unity texture. The conversion runs only after the scanner accepts the frame, so frames dropped by the scan interval cost nothing beyond the submit call.

The runner asks for camera permission before it creates the `WebCamTexture` (`Application.RequestUserAuthorization` on iOS, macOS and WebGL, `Permission.RequestUserPermission` on Android) and waits for the answer, because a texture created while the dialog is still open never delivers frames. On iOS the first launch therefore shows the system prompt and then starts the camera; a denied prompt leaves the preview black until the permission is granted in Settings.

The runner asks the camera for 1280×720 at 30 fps (**Webcam Width / Height / Fps**); the device picks the closest mode it has. 640×480 decodes fine but looks soft when the preview fills a phone screen.

Tapping the preview focuses the camera on that point once (`WebCamTexture.autoFocusPoint`) and returns to continuous auto focus after **Tap Focus Hold Seconds**; the stats label shows the tapped point while the hold lasts. Only Android and iOS cameras report `isAutoFocusPointSupported`; on other cameras the label reports the tap as unsupported and nothing else happens. Phones already run continuous auto focus, so a tap on something that is already sharp changes little — tap a subject at a clearly different distance to see it work.

The preview keeps the camera's aspect ratio: the `RawImage` size in the scene is treated as a bounding box and the image is letterboxed into it (with the box's sides swapped when the frame is turned a quarter). The tap is read from the Input System package when the project uses it (the sample assembly references `Unity.InputSystem` and defines `QRCODE_SAMPLE_INPUT_SYSTEM` only when the package is installed, so projects without it still compile) and from the legacy `Input` class otherwise.

Phone cameras deliver frames in sensor orientation. The preview turns its `RectTransform` by `-videoRotationAngle` and flips its `uvRect` when `videoVerticallyMirrored` is set, so it looks upright; the decoder still reads the raw frame, so `TopLeft`/`TopRight`/... and `Orientation` are measured in that unrotated frame (a portrait phone typically reports an orientation near ±90). Rotate the corners yourself if you need them in screen space.

For a camera Y plane or another raw source, call:

```csharp
runner.SubmitRawGray8(yPlaneBytes, width, height, rowStride);
```

The Texture2D path copies the raw single-channel bytes into a managed array so the background worker has a safe lifetime. It never expands the texture to `Color32`/RGBA.

Texture rows start at the displayed bottom, while ROIs and result corners are measured from the top-left corner, so the copy is reversed in place with `Gray8RowOrder.FlipVertically`. Raw sources that already start at the top row, such as most camera Y planes, go through `SubmitRawGray8` unchanged.

The copy happens only after the scanner has accepted the frame, so submitting every frame no longer copies the image while the scanner is busy, stopped, or still inside its scan interval — a rejected submit costs one short-lived closure instead of the whole texture. Only mip level 0 is copied, into an `ArrayPool<byte>` buffer that goes back to the pool in `ScanCompleted` — never earlier, because the decode reads it until then.
