# Native build

The native library is the upstream ZXing-C++ shared target with its C API enabled. CMake pins v3.1.0 at commit `885baaf0840335153c1a487fa65f9c1388702c81`, enables only readers and QR Code, and copies the output into the correct Unity plugin directory.

## Windows x86_64

Requirements: CMake 3.21+, Visual Studio 2019 16.10 or newer with Desktop development with C++. The script detects Visual Studio 2019/2022 automatically.

```powershell
./build-windows.ps1
```

## Android arm64-v8a

Requirements: CMake 3.21+, Ninja, Android NDK r23 or newer. Unity's installed NDK is suitable.

```powershell
./build-android.ps1 -AndroidNdkRoot 'C:/path/to/ndk'
```

On macOS/Linux, set `ANDROID_NDK_ROOT` and run `./build-android.sh`.

After either build, return to Unity and use **Tools > ZXing-C++ QR Code > Apply Native Plugin Import Settings**.
