# QRCode Package

## 設計決策

- UPM 唯一發布識別保留 `com.s46129.qrcode`，Git repository 為 `s46129/unity-zxingcpp-qrcode`；公開程式識別固定為 `ZXingCpp.QRCode`，不得把維護者帳號帶入 namespace、assembly 或 native build target。
- 對外資料縫是 `IQRCodeDecoder`；native 實作與 EditMode fake decoder 都使用同一介面。
- `QRCodeScanner` 不是 `MonoBehaviour`。Unity 宿主只負責取得 frame 與呼叫 `TrySubmitFrame`。
- 輸入只接受 managed `byte[]` Gray8。完整 frame 與不縮圖 ROI 皆不複製；數值縮圖租用整張輸出大小的 `ArrayPool<byte>`，`Gray8RowOrder.FlipVertically` 租一條 row 當交換暫存。
- 影像座標契約是左上角原點（`QRCodeRegion` 的 ROI、`QRCodePoint` 的角點都以它為準）：送進來的 raster 第一列就是最上列。套件不猜來源方向，也不帶方向旗標——bottom-up 來源由呼叫端先過 `Gray8RowOrder.FlipVertically` 轉正。
- P/Invoke 直接對 upstream `ZXing` shared target 的 `ZXingC.h`，沒有自訂 C++ shim。

## 跨檔資料流

1. `QRCodeDecodeOptions.Snapshot` 固定背景工作所需設定。
2. `Gray8Preprocessor` 把 normalized ROI 解析成像素；factor 1 保留原始 stride/offset，factor > 1 做 box average。
3. `ZXingCppQRCodeDecoder` pin array，建立 `ZXing_ImageView`，需要時呼叫 `ZXing_ImageView_crop`。
4. `ZXing_ReadBarcodes` 只啟用 `QRCode` 且限制一個 symbol。
5. native 回傳的 text/bytes/symbology 都是 caller-owned，複製後一律 `ZXing_free`；barcode list 由 `ZXing_Barcodes_delete` 釋放。
6. ROI/downscale 後的四角座標由 decoder 映射回原始 frame 的 top-left 座標系。

## Scanner 不變量

- 同時最多一個 background decode；busy 時新 frame 直接拒絕。
- 只有接受 frame 時才推進 `nextScanTime`，避免 busy drop 延後下一次有效掃描。
- 拒絕一個 frame 必須是零影像成本。`TrySubmitFrame(Func<Gray8Image>)` 在 `_gate` 內判完 running／busy／interval 才呼叫 provider，所以呼叫端無法在被拒的路徑上付出複製成本；`TrySubmitFrame(Gray8Image)` 則是呼叫端已經持有影像時的入口。要新增取樣路徑走 provider overload，不要在外面預查 `IsBusy`——那既漏掉 interval 條件，又在預查與提交之間留下競爭。
- provider 丟例外時 `_busy` 一律清掉，`nextScanTime` 只在 generation 未變時回滾；沒清 `_busy` 會讓 scanner 永久卡住（沒有 decode 會來清它）。
- `Start`/`Stop`/`Dispose` 以 generation 讓舊工作結果變成 `Discarded`；native 呼叫本身不強制中止。
- 接受的 `Gray8Image.Buffer` 在 `ScanCompleted` 前屬借用狀態，不可改寫或回 pool。
- scanner 建構時捕捉 `SynchronizationContext`；Unity 使用端必須在 main thread 建構，無 context 時事件在 worker thread 執行。

## 原生耦合

- C API/library name: `ZXing`。
- Gray8 enum: `ZXing_ImageFormat_Lum = 0x01000000`。
- QR format ID: `0x2051`（ZXing-C++ 3.x 的 `ZXing_BarcodeFormat_QRCode`）。
- C `bool` P/Invoke 使用 `UnmanagedType.I1`。
- CMake pin: `v3.1.0` / `885baaf0840335153c1a487fa65f9c1388702c81`；讀取器 ON、writers 與非 QR formats OFF、`ZXING_C_API=ON`、shared library ON。
- Windows 使用 static MSVC runtime，避免 Player 另外安裝 Visual C++ Redistributable。
- Android 明確靜態連結 `c++_static`/`c++abi`，避開新版 NDK 搭 Unity 內附舊 CMake 時漏掉 libc++ 的 link line。
- 升級 upstream 時必須同步核對 `ZXingC.h`、P/Invoke ownership、format ID 與兩平台 binary exports。

## 已知限制

- 套件內含已驗證的 Windows x86_64 與 Android arm64-v8a binary；`Native~` 重建時首次 configure 需要 Git/network。
- API 不直接借用 `NativeArray<byte>`，因為 background lifetime 無法由 scanner 保證；Sample 明確複製 R8 raw data。
- Unity `Texture2D` 的 raw data 是 bottom-up：第一列是畫面最下列，與 `GetPixels32` 同序。Sample 因此在複製後就地跑 `Gray8RowOrder.FlipVertically`，不做這步的話角點會落在畫面下緣、`Orientation` 差 90 度、`IsMirrored` 變 true，上半部 ROI 也會選到畫面下半（issue #4 的留言有 Editor 實測數據）。就地翻轉是為了不再開第二塊整張影像大小的 buffer，代價是一次額外的整張走訪加一條 row 暫存。相機 Y plane 多半已經是 top-down，`SubmitRawGray8` 不翻——`FlipVertically` 是無條件反轉，對已經 top-down 的來源呼叫會把方向弄反。
- Sample 的 Texture 路徑複製 `GetRawTextureData<byte>()` 的前 `width * height` bytes——raw data 含所有 mip level，多複製的部分解碼用不到。buffer 來自 `ArrayPool<byte>.Shared`，記在一份 `HashSet<byte[]>` 帳上，`ScanCompleted` 只歸還帳上有的那條——`SubmitRawGray8` 的呼叫端 buffer 因此不會被誤還，`Remove` 也讓重複歸還不可能。**不能用單一欄位記**：`CompleteFrame` 先清 `_busy` 才觸發 `Detected`，所以事件處理器可以在 `ScanCompleted` 之前就再租一條，單欄位會被覆蓋而漏還前一條。`OnDestroy` 時在途的 buffer 直接丟掉不還，因為 decode 還在讀它。殘留配置只剩每次提交的 display class ＋ delegate（約 10^2 bytes），與整張影像不同量級。
- managed downscale 的角點回推是整數倍近似；payload 解碼不受影響。
- ROI 的兩層 1e-5 容差（`QRCodeRegion.BoundsTolerance`、`PixelRegion.PixelBoundaryTolerance`）都只吸收 float／rounding 誤差，判不出「整個 ROI 落在影像外」——那要影像尺寸才判得出來，所以唯一的拒絕點是 `PixelRegion.Resolve`：交集為空丟 `ArgumentException`，只有交集非空但不足一像素才補成一像素。native crop 與 managed downscale 吃同一個 `PixelRegion`，但只有 managed 那條真的索引 `Gray8Image.Buffer`，放寬這個檢查等於重新開啟越界讀取。
