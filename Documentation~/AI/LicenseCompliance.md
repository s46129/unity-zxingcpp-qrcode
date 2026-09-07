# License Compliance

## 授權邊界

- 本 package 自行創作的 C#、Editor、Samples、Tests、文件與 build scripts 採 `Apache-2.0`；根目錄 `LICENSE.md` 必須保留完整標準正文。
- 原生 plugins 內的第三方程式碼不因 package 授權而改變，對外歸屬集中維護在 `Third Party Notices.md`。
- `ZXing-C++ LICENSE.md` 保留 pinned upstream 的完整 Apache-2.0 正文；README 必須明示本 package 是 unofficial integration，不能暗示上游維護者背書。

## Current binary provenance

| Binary | 來源／runtime | 建置識別 |
|---|---|---|
| `Runtime/Plugins/Windows/x86_64/ZXing.dll` | ZXing-C++、libzueci、Hoehrmann UTF-8 DFA、static MSVC runtime | ZXing-C++ `v3.1.0` / `885baaf0840335153c1a487fa65f9c1388702c81` |
| `Runtime/Plugins/Android/arm64-v8a/libZXing.so` | ZXing-C++、libzueci、Hoehrmann UTF-8 DFA、`libc++_static`、`libc++abi` | 同上；NDK r27c `27.2.12479018`、Clang `18.0.3`、LLVM `d8003a456d14a3deb8054cdaa529ffbf02d9b262`；連結時加 `-Wl,-z,max-page-size=16384`／`-Wl,-z,common-page-size=16384`，PT_LOAD 對齊 16 KB |

## Release invariants

- 升級 ZXing-C++ 時，同步核對 upstream `LICENSE`、是否新增 `NOTICE`、`core/src/libzueci/` 的 SPDX／嵌入 notices，以及 CMake 實際納入的其他元件。
- 重建 Android binary 時，記錄實際 NDK／Clang／LLVM revision；如果不再使用 r27c，必須同步更新 `Third Party Notices.md` 的 LLVM provenance 與條款。
- Android binary 必須通過 16 KB `PT_LOAD` 對齊檢查（build flow 在 strip 之後自動跑 `Native~/cmake/CheckElfAlignment.cmake`）；退回 4 KB 的 binary 不可發版。
- 重建 Windows binary 時，確認 builder 具備適用的 Visual Studio／Build Tools 授權，並核對 static runtime 的 redistribution terms。
- 變更 `ZXING_*` flags、toolchain、static/shared runtime 或新增 native dependency 後，依 binary imports／link map 重新審核 notices。
- 發版前確認 README、三份 license/notices 文件、`package.json` 的 `license` 欄位和 bundled binaries 一致。

此文件是工程合規紀錄，不是法律意見；以公司名義商用、提供保固／賠償或遇到專利風險時應交由合格律師確認。
