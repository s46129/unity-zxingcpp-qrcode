# Gray8TextureReadback — AI Context

> 讀者：AI agent。目的：不重讀全部原始碼即可安全修改此模組。上限 150 行。

## 範圍

- 涵蓋檔案/資料夾：`Runtime/Unity/`（`Gray8TextureReadback.cs`、`Resources/ZXingCpp.QRCode/Gray8Readback.shader`、`ZXingCpp.QRCode.Unity.asmdef`）、`Tests/EditMode/Unity/`、`QRCodeScanner.CanAcceptFrame`、Sample 的 webcam GPU 路徑
- 相關 Spec：`Documentation~/Spec/Gray8TextureReadbackSpec1.md`
- 相關人類文件：無

## 設計決策（為什麼是現在這樣）

- GPU 路徑放獨立 assembly `ZXingCpp.QRCode.Unity`：核心 `ZXingCpp.QRCode` 維持 `noEngineReferences`，EditMode 純邏輯測試不受影響。否決：塞進核心 assembly — 會讓所有測試失去離線快迴圈。
- 一次 fragment-shader `Graphics.Blit` 做 luma、flip、box average，輸出 R8 RenderTexture：readback 量從 4 bytes/pixel 降到 1，CPU 不再碰像素。否決：compute shader — GLES3.0 裝置支援不齊，且 blit 已足夠。
- 用 `AsyncGPUReadback.RequestIntoNativeArray` 寫進持久 `NativeArray<byte>`，再 `CopyFrame` memcpy 進呼叫端 `byte[]`：保住「scanner 只借 managed `byte[]`」的既有契約，且 readback 在途時 Unity 會鎖住那個 NativeArray、不會被 decode 讀到。否決：零複製把 readback 打進 pinned `byte[]` — 要 `allowUnsafeCode` 與手動 `AtomicSafetyHandle`，先量再說（Spec §8）。
- 縮圖在 shader 裡做、decoder 的 `DownscaleFactor` 設 1：省 readback 頻寬與 worker 時間。代價：角點落在縮圖座標，呼叫端自己乘回倍率（Sample 的 `Scale`）。
- `QRCodeScanner.CanAcceptFrame` 是**提示**不是閘門：readback 晚 1–3 幀才回來，provider overload 無法跨幀持有 slot；先問再發 request 只是為了不替必被丟掉的幀花 GPU。提示與提交之間的競爭最多浪費一次 readback。否決：非同步 claim API（claim → readback → complete/release）— 多一組 slot 洩漏的不變量，收益只是省幾次 readback。
- shader 走 `Resources.Load`：`Shader.Find` 在 Player build 會被 strip；`Resources` 資料夾在套件內同樣會打包。

## 資料流 / 呼叫順序

```
呼叫端 Update()
  → scanner.CanAcceptFrame()（false 就什麼都不做）
  → readback.TryRequest(texture, flipVertically)
      → EnsureResources()：Material（Resources.Load shader）、R8 RenderTexture(ceil(w/f), ceil(h/f))、NativeArray(w'*h')
      → material.SetVector(_Dimensions=(w,h,w',h'))、_Factor、_FlipVertically
      → Graphics.Blit(source, rt, material)
      → AsyncGPUReadback.RequestIntoNativeArray(ref native, rt, 0, R8, OnReadback)
Unity main thread（1–3 幀後）
  → OnReadback：hasError／stride 檢查 → HasFrame=true → FrameReady
呼叫端 FrameReady
  → scanner.TrySubmitFrame(provider)
      → provider：rent byte[FrameByteLength] → readback.CopyFrame(buffer)（NativeArray.Copy）→ Gray8Image(buffer, w', h', rowStride)
  → 既有 scanner 流程；buffer 在 ScanCompleted 歸還
```

## 不變量與隱藏耦合

- shader 的 luma 權重（77/151/28 ÷ 256）、flip 語意、`ceil` 縮圖與 `min(...)` 邊界必須與 CPU 路徑（Sample 的 `RentWebcamFrame`、`Gray8RowOrder.FlipVertically`、`Gray8Preprocessor.DownscaleBoxAverage`）一致；`Gray8TextureReadbackTests` 以 CPU 算法為 oracle、容差 2。改任何一邊都要跑它。
- 行序契約：shader 內 `uv.y == 0` 的輸出列是 readback 的第一列；`_FlipVertically` 開時它取 source 的 memory 最後一列（畫面最上列）。`AsyncGPUReadback` 回來的列序與 `GetRawTextureData` 相同（bottom-up），這是整條路徑「第一列＝畫面最上列」的前提；在 Metal（Editor 測試）驗證過，其他 API 靠 Unity 的 RT 投影翻轉保持一致。
- 同時最多一個 pending request：`_pixels` 在途時被 Unity 鎖住，第二次 `TryRequest` 回 false、`CopyFrame` 丟 `InvalidOperationException`。`EnsureResources` 只在非 pending 時被呼叫，所以可以安全重建 RT／NativeArray。
- `Dispose` 順序：`_disposed=true` → 有 pending 就 `AsyncGPUReadback.WaitAllRequests()`（會同步觸發 `OnReadback`，它看到 `_disposed` 就不發事件）→ dispose NativeArray → Release/Destroy RT → Destroy Material。跳過 WaitAllRequests 會在 dispose NativeArray 時被 Unity 丟例外。
- `CanAcceptFrame` 與 `TryClaimScanSlot` 的條件（`_running && !_busy && t >= _nextScanTime`）必須同一套，`QRCodeScannerTests` 的 `CanAcceptFrame_*` 鎖住這點；它不改任何狀態。
- Sample：GPU 路徑下 `QRCodeDecodeOptions.DownscaleFactor` 固定 1、`_cornerScale = downscaleFactor`；`OnDetected` 顯示前乘回。兩者要一起改。
- 執行緒：`FrameReady`／`ReadbackFailed`／`CopyFrame` 全在 main thread；`Gray8TextureReadback` 不是 thread-safe，也不需要是。

## 外部介接契約

- `Resources` 路徑 `ZXingCpp.QRCode/Gray8Readback` ↔ shader 名 `Hidden/ZXingCpp/QRCode/Gray8Readback`；改檔名或資料夾要同步改 `ShaderResourcePath`。
- shader uniform：`_Dimensions`（x,y source 尺寸；z,w output 尺寸）、`_Factor`、`_FlipVertically`（>0.5 為開）。`_MainTex_TexelSize` 由 Blit 自動設定。
- `IsSupported = SystemInfo.supportsAsyncGPUReadback && SupportsRenderTextureFormat(R8)`；false 時 `TryRequest` 丟 `NotSupportedException`，呼叫端要走 CPU 路徑。
- 色彩空間：Linear 專案裡 sRGB 來源取樣後在 shader 內 `LinearToGammaSpace`，對齊 `GetPixels32` 的 bytes；非 sRGB 來源會多一次單調 gamma 編碼，解碼不受影響、但與 CPU 路徑逐像素不相等。

## 已知的坑 / Workaround

- `RenderTexture` 來源若由 Camera 渲染，某些 API 上 `_MainTex_TexelSize.y < 0`，內容可能已是上下顛倒；本 shader 不做 `UNITY_UV_STARTS_AT_TOP` 補償，呼叫端以 `flipVertically:false` 校正。目前沒有這種來源的測試。
- `AsyncGPUReadback` 回呼要 Editor 有 graphics device：`Gray8TextureReadbackTests` 在 `-nographics` 下會 `Assert.Ignore`（`IsSupported` 為 false）。headless 驗證要跑不帶 `-nographics` 的 batchmode。
- `layerDataSize / height` 當 rowStride：目前所有 backend 回來都是緊密排列（stride == width），但保留 stride 是為了不假設；`rowStride < width` 視為 readback 失敗。
- WebCamTexture 在第一張真實幀前是 16×16，Sample 的 `width > 16` 判斷沿用既有 CPU 路徑。

## 待辦 / 技術債

- 零複製（pinned `byte[]` 直接當 readback 目標）與 `RenderTexture`-from-Camera 的方向測試：見 Spec §8，尚未開 issue。
