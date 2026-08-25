using System;
using System.Runtime.InteropServices;
using System.Text;
using S46129.QRCode.Internal;

namespace S46129.QRCode
{
    /// <summary>A stateless Gray8 QR decoder backed by the ZXing-C++ C API.</summary>
    public sealed class ZXingCppQRCodeDecoder : IQRCodeDecoder
    {
        private static readonly int[] QRCodeFormats = { ZXingNativeMethods.QRCodeFormat };

        public string NativeVersion
        {
            get
            {
                try
                {
                    return CopyStaticUtf8(ZXingNativeMethods.ZXing_Version());
                }
                catch (Exception exception) when (IsNativeLoadException(exception))
                {
                    throw CreateLoadException(exception);
                }
            }
        }

        public bool TryDecode(Gray8Image image, out QRCodeResult result) =>
            TryDecode(image, new QRCodeDecodeOptions(), out result);

        public bool TryDecode(Gray8Image image, QRCodeDecodeOptions options, out QRCodeResult result)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            QRCodeDecodeOptions snapshot = options.Snapshot();

            try
            {
                return DecodeCore(image, snapshot, out result);
            }
            catch (Exception exception) when (IsNativeLoadException(exception))
            {
                throw CreateLoadException(exception);
            }
        }

        private static bool DecodeCore(Gray8Image source, QRCodeDecodeOptions options, out QRCodeResult result)
        {
            result = null;
            using (PreparedGray8Image prepared = Gray8Preprocessor.Prepare(source, options))
            {
                GCHandle pinnedBuffer = default;
                IntPtr imageView = IntPtr.Zero;
                IntPtr readerOptions = IntPtr.Zero;
                IntPtr barcodes = IntPtr.Zero;

                try
                {
                    pinnedBuffer = GCHandle.Alloc(prepared.Image.Buffer, GCHandleType.Pinned);
                    IntPtr data = IntPtr.Add(pinnedBuffer.AddrOfPinnedObject(), prepared.Image.Offset);
                    imageView = ZXingNativeMethods.ZXing_ImageView_new(
                        data,
                        prepared.Image.Width,
                        prepared.Image.Height,
                        ZXingNativeMethods.ImageFormat.Luminance,
                        prepared.Image.RowStride,
                        1);
                    EnsurePointer(imageView, "ZXing-C++ could not create an image view.");

                    if (prepared.ApplyNativeCrop)
                    {
                        PixelRegion crop = prepared.NativeCrop;
                        ZXingNativeMethods.ZXing_ImageView_crop(imageView, crop.Left, crop.Top, crop.Width, crop.Height);
                    }

                    readerOptions = ZXingNativeMethods.ZXing_ReaderOptions_new();
                    EnsurePointer(readerOptions, "ZXing-C++ could not create reader options.");
                    ConfigureReader(readerOptions, options);

                    barcodes = ZXingNativeMethods.ZXing_ReadBarcodes(imageView, readerOptions);
                    EnsurePointer(barcodes, "ZXing-C++ failed while reading the Gray8 image.");

                    if (ZXingNativeMethods.ZXing_Barcodes_size(barcodes) == 0)
                        return false;

                    IntPtr barcode = ZXingNativeMethods.ZXing_Barcodes_at(barcodes, 0);
                    if (barcode == IntPtr.Zero || !ZXingNativeMethods.ZXing_Barcode_isValid(barcode))
                        return false;

                    result = CreateResult(barcode, prepared);
                    return true;
                }
                finally
                {
                    if (barcodes != IntPtr.Zero)
                        ZXingNativeMethods.ZXing_Barcodes_delete(barcodes);
                    if (readerOptions != IntPtr.Zero)
                        ZXingNativeMethods.ZXing_ReaderOptions_delete(readerOptions);
                    if (imageView != IntPtr.Zero)
                        ZXingNativeMethods.ZXing_ImageView_delete(imageView);
                    if (pinnedBuffer.IsAllocated)
                        pinnedBuffer.Free();
                }
            }
        }

        private static void ConfigureReader(IntPtr readerOptions, QRCodeDecodeOptions options)
        {
            ZXingNativeMethods.ZXing_ReaderOptions_setFormats(readerOptions, QRCodeFormats, QRCodeFormats.Length);
            ZXingNativeMethods.ZXing_ReaderOptions_setMaxNumberOfSymbols(readerOptions, 1);
            ZXingNativeMethods.ZXing_ReaderOptions_setTryHarder(readerOptions, options.TryHarder);
            ZXingNativeMethods.ZXing_ReaderOptions_setTryRotate(readerOptions, options.TryRotate);
            ZXingNativeMethods.ZXing_ReaderOptions_setTryInvert(readerOptions, options.TryInvert);
            ZXingNativeMethods.ZXing_ReaderOptions_setTryDownscale(readerOptions, options.TryNativeDownscale);
            ZXingNativeMethods.ZXing_ReaderOptions_setIsPure(readerOptions, options.IsPure);
        }

        private static QRCodeResult CreateResult(IntPtr barcode, PreparedGray8Image prepared)
        {
            ZXingNativeMethods.Position position = ZXingNativeMethods.ZXing_Barcode_position(barcode);
            return new QRCodeResult(
                CopyOwnedUtf8(ZXingNativeMethods.ZXing_Barcode_text(barcode)),
                CopyOwnedBytes(ZXingNativeMethods.ZXing_Barcode_bytes(barcode, out int rawLength), rawLength),
                MapPoint(position.TopLeft, prepared),
                MapPoint(position.TopRight, prepared),
                MapPoint(position.BottomRight, prepared),
                MapPoint(position.BottomLeft, prepared),
                ZXingNativeMethods.ZXing_Barcode_orientation(barcode),
                ZXingNativeMethods.ZXing_Barcode_isInverted(barcode),
                ZXingNativeMethods.ZXing_Barcode_isMirrored(barcode),
                CopyOwnedUtf8(ZXingNativeMethods.ZXing_Barcode_symbologyIdentifier(barcode)));
        }

        private static QRCodePoint MapPoint(ZXingNativeMethods.Point point, PreparedGray8Image prepared) =>
            new QRCodePoint(
                prepared.OriginX + point.X * prepared.CoordinateScale,
                prepared.OriginY + point.Y * prepared.CoordinateScale);

        private static byte[] CopyOwnedBytes(IntPtr pointer, int length)
        {
            try
            {
                if (length == 0)
                    return Array.Empty<byte>();
                EnsurePointer(pointer, "ZXing-C++ returned a null payload pointer.");
                byte[] bytes = new byte[length];
                Marshal.Copy(pointer, bytes, 0, length);
                return bytes;
            }
            finally
            {
                if (pointer != IntPtr.Zero)
                    ZXingNativeMethods.ZXing_free(pointer);
            }
        }

        private static string CopyOwnedUtf8(IntPtr pointer)
        {
            try
            {
                return CopyStaticUtf8(pointer);
            }
            finally
            {
                if (pointer != IntPtr.Zero)
                    ZXingNativeMethods.ZXing_free(pointer);
            }
        }

        private static string CopyStaticUtf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
                return string.Empty;

            int length = 0;
            while (Marshal.ReadByte(pointer, length) != 0)
                length++;

            if (length == 0)
                return string.Empty;

            byte[] bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static void EnsurePointer(IntPtr pointer, string fallbackMessage)
        {
            if (pointer != IntPtr.Zero)
                return;

            IntPtr errorPointer = ZXingNativeMethods.ZXing_LastErrorMsg();
            string nativeMessage = CopyOwnedUtf8(errorPointer);
            throw new QRCodeNativeException(string.IsNullOrEmpty(nativeMessage) ? fallbackMessage : nativeMessage);
        }

        private static bool IsNativeLoadException(Exception exception) =>
            exception is DllNotFoundException ||
            exception is EntryPointNotFoundException ||
            exception is BadImageFormatException;

        private static QRCodeNativeException CreateLoadException(Exception exception) =>
            new QRCodeNativeException(
                "Could not load the ZXing-C++ native plugin. Build and place ZXing.dll for Windows x86_64 or libZXing.so for Android arm64-v8a, then apply the plugin importer settings documented by this package.",
                exception);
    }
}
