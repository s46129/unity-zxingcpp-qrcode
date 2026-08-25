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
