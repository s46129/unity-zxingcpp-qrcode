# Gray8TextureReadback — 規格文件

**狀態**：實作中
**最後更新**：2026-09-18
**對應 issue**：#13（`s46129/unity-zxingcpp-qrcode`）

---

## 0. 規格目標 (Purpose)

`QRCodeScanner` 早已把 ZXing-C++ 解碼放在背景執行緒，但「把 Unity texture 變成 Gray8 frame」這段仍在 main thread：`WebCamTexture.GetPixels32` 是同步 GPU→CPU readback、整張 RGBA32；接著 managed 迴圈算 luma；再整張 `Gray8RowOrder.FlipVertically`。每次被接受的 scan，main thread 都吃一次 ms 等級的 spike。

解完之後：任何 `Texture`（`WebCamTexture`、`RenderTexture`、`Texture2D`）都能在 GPU 上一次完成灰階、翻轉、縮圖，輸出 1 byte/pixel 的 R8 影像，再由 `AsyncGPUReadback` 非同步搬回 CPU。main thread 只剩發一次 Blit、發一次 readback、回呼時一次 memcpy。解碼流程、`QRCodeScanner` 的不變量、既有 CPU 路徑都不變。

**使用場景**：
- 場景 A：`WebCamTexture` 掃碼，手機上 main thread 不再因取幀掉幀。
- 場景 B：來源是 `RenderTexture`（例如畫面截取、外部影像 SDK 的輸出），不必先 `ReadPixels` 成 `Texture2D`。
- 場景 C：不支援 `AsyncGPUReadback` 的裝置自動退回既有 CPU 路徑，呼叫端行為不變。

---

## 1. 共通語言 (Ubiquitous Language)

| 名詞 | 定義（是什麼，不是做什麼） | 避免用詞 |
|---|---|---|
| Gray8TextureReadback | 把一張 `Texture` 經 GPU 轉成 top-left-origin Gray8 frame 並非同步讀回 CPU 的物件；一次只持有一個 pending request 與一張完成的 frame | GPU decoder、GPU scanner、frame source |
| readback | `AsyncGPUReadback` 把 GPU 端 R8 RenderTexture 內容搬進 CPU 端 `NativeArray<byte>` 的那一次非同步傳輸 | download、ReadPixels |
| 取樣提示 | `QRCodeScanner.CanAcceptFrame`：回答「此刻 submit 會不會被接受」，只是提示，`TrySubmitFrame` 才是裁決 | pre-check、IsBusy 預查 |

---

## 2. 輸入資料規格 (Given)

### 2.1 來源 texture

- `source` : `UnityEngine.Texture`
  - 約束：`width`、`height` 皆 > 0；shader 可取樣（`WebCamTexture`、`Texture2D`、`RenderTexture` 皆可，不要求 readable）。
  - 約束：raster 依 Unity 慣例 bottom-up（第一列是畫面最下列）。已是 top-down 的來源由呼叫端以 `flipVertically: false` 宣告。

### 2.2 縮圖倍率

- `downscaleFactor` : `int`
  - 約束：1 到 8，與 `QRCodeDecodeOptions.DownscaleFactor` 同範圍；輸出尺寸 `ceil(width / factor) × ceil(height / factor)`，box average，與 `Gray8Preprocessor` 同語意。

### 2.3 外部依賴

#### API：`UnityEngine.Rendering.AsyncGPUReadback.RequestIntoNativeArray`

- 輸入：持久 `NativeArray<byte>`、R8 `RenderTexture`、mip 0、`TextureFormat.R8`、完成回呼。
- 副作用：request 在途期間該 `NativeArray` 被 Unity 鎖住，不可讀寫或 dispose。
- 失敗情境：`request.hasError`（裝置遺失、格式不支援）；平台不支援時 `SystemInfo.supportsAsyncGPUReadback` 為 false。

---

## 3. 核心不變條件 (Invariants)

### 3.1 硬規則

- **硬規則 1**：readback 輸出的 frame 與既有 CPU 路徑（`GetPixels32` → 整數 Rec. 601 luma → `FlipVertically` → managed box average）逐像素相差不超過 2；角點座標語意因此完全一致。
- **硬規則 2**：同一個 `Gray8TextureReadback` 同時最多一個 pending request；pending 期間 `TryRequest` 回傳 false、`CopyFrame` 丟 `InvalidOperationException`。
- **硬規則 3**：`Dispose` 必須先等在途 request 結束，才釋放 `NativeArray`、`RenderTexture` 與 `Material`；Dispose 後不再觸發任何事件。
- **硬規則 4**：核心 assembly `ZXingCpp.QRCode` 維持 `noEngineReferences`；GPU 路徑放在獨立的 `ZXingCpp.QRCode.Unity` assembly。
- **硬規則 5**：`QRCodeScanner` 的既有不變量不變；`CanAcceptFrame` 只讀狀態，不推進 `nextScanTime`，不改 `_busy`。

### 3.2 軟規則

- **軟規則 1**：被 scanner 丟掉的 frame 不該花 GPU：呼叫端先問 `CanAcceptFrame` 再 `TryRequest`。提示與提交之間的競爭只會浪費一次 readback，不影響正確性。
- **軟規則 2**：Linear color space 專案裡，sRGB 來源取樣後在 shader 內轉回 gamma，讓 luma 與 `GetPixels32` 讀到的 bytes 一致；非 sRGB 來源會多一次單調的 gamma 編碼，不影響解碼。

---

## 4. 行為規格 (When / Then)

### 4.1 請求一張 frame

When：呼叫端持有可取樣的 `Texture`，呼叫 `TryRequest(source, flipVertically)`

Then：
1. 沒有 pending request 且尺寸合法 → 以 material Blit 到 R8 RenderTexture，發出 readback，回傳 true，`IsPending` 為 true、`HasFrame` 為 false。
2. 已有 pending request → 回傳 false，不動 GPU。
3. `IsSupported` 為 false → 丟 `NotSupportedException`；呼叫端應先查 `IsSupported` 並走 CPU 路徑。

### 4.2 readback 完成

When：Unity 在 main thread 觸發完成回呼

Then：
1. `hasError` 為 false → 記下 width/height/rowStride，`HasFrame` 為 true，觸發 `FrameReady`。
2. `hasError` 為 true → `HasFrame` 維持 false，觸發 `ReadbackFailed`。
3. 兩者皆先清 `IsPending`；物件已 Dispose 時不觸發事件。

### 4.3 交給 scanner

When：`FrameReady` 內呼叫 `scanner.TrySubmitFrame(provider)`，provider 內呼叫 `CopyFrame(byte[])`

Then：
1. scanner 接受 → provider 被叫，`CopyFrame` 把 `NativeArray` 複製進呼叫端 buffer，回傳 top-left-origin `Gray8Image`（含 rowStride）。
2. scanner 拒絕 → provider 不被叫，零複製。
3. 呼叫端 buffer 在 `ScanCompleted` 前屬借用，與既有契約一致。

### 4.4 不支援的裝置

When：`Gray8TextureReadback.IsSupported` 為 false

Then：Sample 走既有 `GetPixels32` 路徑，畫面與事件行為與現在相同。

---

## 5. 結果規格 (Then)

### 5.1 輸出內容

- `Gray8Image` — `Buffer` 為呼叫端提供的 `byte[]`，`Width`/`Height` 為縮圖後尺寸，`RowStride` 由 readback 的 `layerDataSize / height` 決定，`Offset` 為 0。
- `FrameByteLength` : `int` — 呼叫端要準備的最小 buffer 長度。

### 5.2 保證

- frame 的第一列是畫面最上列（`flipVertically: true` 時）。
- 縮圖倍率 > 1 時，角點座標落在縮圖座標系；乘回倍率即原始 texture 座標（整數倍近似，與 managed downscale 相同）。

### 5.3 例外／無解情況處理

- shader 資源缺失（`Resources.Load` 回 null）→ `InvalidOperationException`，訊息指出 shader 路徑。
- Dispose 後任何呼叫 → `ObjectDisposedException`。

---

## 6. 可測試規格 (Testable Specifications)

### Spec-GR-01：Texture2D 經 readback 後為 top-left-origin luma

Given：一張 4×3 RGBA32 `Texture2D`，各像素顏色互異
Then：readback 結果的第 y 列第 x 行 = CPU luma（77/151/28，>>8）於畫面第 y 列（即 raw 第 `height-1-y` 列），誤差 ≤ 2。

### Spec-GR-02：縮圖為 box average，邊界不足一格時取實際覆蓋

Given：5×3 texture、`downscaleFactor = 2`
Then：輸出 3×2，每格等於對應 2×2（右緣／下緣為 1×2、2×1、1×1）source luma 的整數平均，誤差 ≤ 2。

### Spec-GR-03：pending 期間第二次 TryRequest 回傳 false

Given：已發出一次 request 尚未完成
Then：`TryRequest` 回傳 false、`IsPending` 為 true。

### Spec-GR-04：GPU 取幀的 QR 影像可被 native decoder 解出

Given：把 `ZXingCppQRCodeDecoderTests` 的 TOPLEFT 符號畫成 RGBA32 texture（畫面左上角）
Then：readback → `ZXingCppQRCodeDecoder.TryDecode` 得到 `TOPLEFT`，`TopLeft` = (16, 16)，`Orientation` = 0、`IsMirrored` = false。

### Spec-GR-05：`CanAcceptFrame` 與 `TrySubmitFrame` 同一套條件

Given：scanner 未 Start／busy／interval 未到／就緒四種狀態
Then：前三者回 false，就緒回 true，且呼叫後 `TrySubmitFrame` 的裁決不變。

---

## 7. 手動驗證步驟 (Manual Verification)

### 共同前置

1. 匯入 **Basic Gray8 Decode** sample，開 `WebcamDecode.unity`。
2. 準備一張可掃的 QR Code。

### 驗證 V-01：GPU 路徑掃得到碼且 main thread 沒有取幀 spike

**前置**：runner 的 **Use Gpu Readback** 勾選（預設）。

**操作**：
1. 進 Play Mode，開 Profiler（CPU 模組），把 QR 對準相機。
2. 觀察 `QRCodeSampleRunner.Update` 與 stats label。

**預期**：
- stats label 顯示 `gpu`，`scans/s` 約等於 1 / Scan Interval。
- Console 出現 `QR decoded: <payload>`，角點與 CPU 路徑一致（切換 **Use Gpu Readback** 比對）。
- Profiler 裡 `Update` 沒有 `GetPixels32` 與整張迴圈；readback 回呼只有一段 `NativeArray.Copy`。

**異常**：
- `ReadbackFailed` 警告連續出現。
- `Detected` 的角點落在畫面下半（表示翻轉方向錯）。

### 驗證 V-02：關掉 GPU 路徑行為不變

**操作**：取消 **Use Gpu Readback**，重進 Play Mode。

**預期**：stats label 顯示 `cpu`，掃碼結果與 V-01 相同。

### 驗證 V-03：Downscale Factor 2 在 GPU 路徑

**操作**：**Downscale Factor** 設 2，重進 Play Mode。

**預期**：仍掃得到；result label 的角點乘回 2 後與 factor 1 的角點相差不超過 2 px。

---

## 8. 不在範圍內 (Out of Scope)

- ❌ 零複製（readback 直接寫進 pinned managed `byte[]`）—— 需要 `allowUnsafeCode` 與 `AtomicSafetyHandle` 手動管理；一次 memcpy 對 1280×720 是 0.1 ms 等級，先量再決定。
- ❌ 多 worker 平行解碼 —— ZXing-C++ 單張解碼是單執行緒；相機掃描是 latency-bound、連續幀高度重複，「同時最多一個 decode」是刻意設計。
- ❌ 不支援 `AsyncGPUReadback` 的平台上用同步 `ReadPixels` 退化 —— 退回既有 CPU 路徑就夠，不另開第三條路。
- ❌ 處理 `videoRotationAngle` —— 與現有 CPU 路徑一致，角點維持在 raw frame 座標系。
- ❌ AR Foundation `XRCpuImage` —— Y plane 本來就是 CPU 端 Gray8，走 `SubmitRawGray8` 已是最省。
