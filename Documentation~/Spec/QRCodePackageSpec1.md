# QRCode Package Spec 1

## 目標

提供一個可重用的 Unity Package Manager 套件，讓 Windows x86_64 與 Android arm64-v8a 使用同一套 C# API，從 Gray8/luminance frame 高效率解析 QR Code，且連續掃描不阻塞 Unity main thread。

## 必要能力

- UPM 結構包含 `package.json`、`Runtime`、`Editor`、`Samples~`、`Documentation~`。
- 公開契約包含 `IQRCodeDecoder`、`QRCodeResult`、`QRCodeScanner`。
- C# 直接 P/Invoke ZXing-C++ C API；兩平台 library name 均為 `ZXing`。
- 輸入支援 `byte[]`、width、height、row stride、offset，不要求 RGBA/`Color32`。
- scanner 支援 background decode、掃描間隔、busy frame drop、normalized ROI、managed downscale、成功後停止。
- 核心狀態與前處理不依賴 `MonoBehaviour`，可由 EditMode tests 驗證。
- 交付 Windows/Android plugin 目錄、可重現 CMake scripts、importer 設定與最小 Texture2D/raw Gray8 Sample。

## 驗收

- Unity 2021.3 以上可解析 package/asmdef/plugin importer。
- Windows x86_64 可從已知 Gray8 buffer 解出預期 payload。
- ROI、downscale 與 inverted luminance 各有成功的 native decode 案例。
- Android binary 匯出 C# wrapper 使用的 C API symbols，且無 `libc++_shared.so` 額外部署需求。
- scanner 的 busy drop、frequency limit、stop-on-success、stale result discard、failure completion flow 皆有 EditMode tests。
- package 內所有 Unity assets 有穩定且不重複的 `.meta` GUID。

## 手動驗證

### Texture2D

1. 在 Package Manager 匯入 **Basic Gray8 Decode** sample。
2. 建立 GameObject 並加入 `QRCodeSampleRunner`。
3. 指派 readable `R8` 或 `Alpha8` QR Texture2D。
4. 進入 Play Mode。
5. 預期 Console 顯示 `QR decoded: <payload>`，main thread 不因 native decode 卡住。

### Raw Gray8

1. 從 camera Y plane 取得 `byte[]`、width、height 與 row stride。
2. 呼叫 `SubmitRawGray8` 或直接建立 `Gray8Image`。
3. 預期 scanner 忙碌或未到掃描時間時回傳 `false`；接受時回傳 `true`，並在 `ScanCompleted` 後才允許重用 buffer。

## 不在範圍內

- iOS、macOS、Linux 與 Android 32-bit binary。
- QR 以外的 barcode format。
- 相機擷取、權限、UI overlay 與 QR 產生器。
- 直接 background 借用 `NativeArray<byte>`；需要呼叫端自行保證生命週期或複製成 managed buffer。
