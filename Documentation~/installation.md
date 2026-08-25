# Installation and native builds

## Add the source package

In Unity Package Manager, choose **Add package from git URL** and enter `https://github.com/s46129/unity-zxingcpp-qrcode.git`. You can also copy/link the source under the project's `Packages/com.s46129.qrcode` directory. The minimum supported Unity version is 2021.3.

## Build the native plugins

The package includes verified Windows x86_64 and Android arm64-v8a binaries. The scripts under `Native~` reproduce them by fetching ZXing-C++ v3.1.0 at commit `885baaf0840335153c1a487fa65f9c1388702c81` and building a reader-only shared library.

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

## Plugin importer settings

After the build, select **Tools > ZXing-C++ QR Code > Apply Native Plugin Import Settings**. The helper applies these settings:

| Binary | Any Platform | Editor | Build target | CPU |
|---|---:|---:|---|---|
| `Windows/x86_64/ZXing.dll` | Off | On, Windows only | Standalone Windows 64-bit | x86_64 |
| `Android/arm64-v8a/libZXing.so` | Off | Off | Android | ARM64 |

For Android Player Settings, include ARM64 and use IL2CPP or Mono as required by the project. The native library name remains `ZXing`; C# uses `[DllImport("ZXing")]` on both platforms.

## Texture and camera input

- Prefer camera Y planes, `TextureFormat.R8`, or `TextureFormat.Alpha8` buffers.
- `Gray8Image` accepts padded row stride and byte offset, which avoids repacking many camera planes.
- `Texture2D.GetRawTextureData<byte>()` returns native-backed memory. The sample copies it once into `byte[]` because background work cannot safely borrow the `NativeArray` after the call site.
- Avoid `GetPixels32`; it creates an RGBA representation and adds a conversion before decoding.

## Troubleshooting

- `DllNotFoundException` wrapped by `QRCodeNativeException`: verify that the binary exists and importer settings match the active platform/CPU.
- `EntryPointNotFoundException`: rebuild from the pinned source with `ZXING_C_API=ON`; a different system ZXing library may not export the required API.
- Android works in build but not Editor: expected. Android `.so` is excluded from Editor; the Windows DLL is the Editor plugin on Windows.
- No result after aggressive managed downscale: reduce `DownscaleFactor` so each QR module remains several pixels wide.
