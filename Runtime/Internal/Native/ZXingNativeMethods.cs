using System;
using System.Runtime.InteropServices;

namespace S46129.QRCode.Internal
{
    internal static class ZXingNativeMethods
    {
        internal const string LibraryName = "ZXing";
        internal const int QRCodeFormat = 0x2051;

        internal enum ImageFormat
        {
            Luminance = 0x01000000
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Position
        {
            internal Point TopLeft;
            internal Point TopRight;
            internal Point BottomRight;
            internal Point BottomLeft;
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ZXing_ImageView_new(
            IntPtr data,
            int width,
            int height,
            ImageFormat format,
            int rowStride,
            int pixelStride);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_ImageView_delete(IntPtr imageView);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_ImageView_crop(IntPtr imageView, int left, int top, int width, int height);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ZXing_ReaderOptions_new();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_ReaderOptions_delete(IntPtr options);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_ReaderOptions_setTryHarder(
            IntPtr options,
            [MarshalAs(UnmanagedType.I1)] bool value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_ReaderOptions_setTryRotate(
            IntPtr options,
            [MarshalAs(UnmanagedType.I1)] bool value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_ReaderOptions_setTryInvert(
            IntPtr options,
            [MarshalAs(UnmanagedType.I1)] bool value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_ReaderOptions_setTryDownscale(
            IntPtr options,
            [MarshalAs(UnmanagedType.I1)] bool value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_ReaderOptions_setIsPure(
            IntPtr options,
            [MarshalAs(UnmanagedType.I1)] bool value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_ReaderOptions_setFormats(IntPtr options, int[] formats, int count);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_ReaderOptions_setMaxNumberOfSymbols(IntPtr options, int value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ZXing_ReadBarcodes(IntPtr imageView, IntPtr options);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ZXing_Barcodes_size(IntPtr barcodes);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ZXing_Barcodes_at(IntPtr barcodes, int index);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_Barcodes_delete(IntPtr barcodes);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool ZXing_Barcode_isValid(IntPtr barcode);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ZXing_Barcode_text(IntPtr barcode);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ZXing_Barcode_bytes(IntPtr barcode, out int length);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ZXing_Barcode_symbologyIdentifier(IntPtr barcode);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern Position ZXing_Barcode_position(IntPtr barcode);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ZXing_Barcode_orientation(IntPtr barcode);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool ZXing_Barcode_isInverted(IntPtr barcode);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool ZXing_Barcode_isMirrored(IntPtr barcode);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ZXing_LastErrorMsg();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ZXing_Version();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ZXing_free(IntPtr pointer);
    }
}
