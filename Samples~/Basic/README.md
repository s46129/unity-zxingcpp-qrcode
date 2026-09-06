# Basic Gray8 Decode sample

1. Import this sample from Package Manager.
2. Create a GameObject and add `QRCodeSampleRunner`.
3. Assign a readable `Texture2D` whose format is `R8` or `Alpha8`.
4. Enter Play Mode. The decode runs in the background and logs its result.

For a camera Y plane or another raw source, call:

```csharp
runner.SubmitRawGray8(yPlaneBytes, width, height, rowStride);
```

The Texture2D path copies the raw single-channel bytes into a managed array so the background worker has a safe lifetime. It never expands the texture to `Color32`/RGBA.

Texture rows start at the displayed bottom, while ROIs and result corners are measured from the top-left corner, so the copy is reversed in place with `Gray8RowOrder.FlipVertically`. Raw sources that already start at the top row, such as most camera Y planes, go through `SubmitRawGray8` unchanged.

The copy happens only after the scanner has accepted the frame, so submitting every frame no longer copies the image while the scanner is busy, stopped, or still inside its scan interval — a rejected submit costs one short-lived closure instead of the whole texture. Only mip level 0 is copied, into an `ArrayPool<byte>` buffer that goes back to the pool in `ScanCompleted` — never earlier, because the decode reads it until then.
