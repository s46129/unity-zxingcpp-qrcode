using System;

namespace ZXingCpp.QRCode
{
    /// <summary>A borrowed, row-strided 8-bit luminance image stored in a managed byte array.</summary>
    public readonly struct Gray8Image
    {
        public Gray8Image(byte[] buffer, int width, int height, int rowStride = 0, int offset = 0)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

            rowStride = rowStride == 0 ? width : rowStride;
            if (rowStride < width)
                throw new ArgumentOutOfRangeException(nameof(rowStride), "Row stride cannot be smaller than width.");
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            long lastByteExclusive = (long)offset + ((long)height - 1) * rowStride + width;
            if (lastByteExclusive > buffer.LongLength)
                throw new ArgumentException("The buffer is too small for the image dimensions, stride, and offset.", nameof(buffer));

            Buffer = buffer;
            Width = width;
            Height = height;
            RowStride = rowStride;
            Offset = offset;
        }

        public byte[] Buffer { get; }

        public int Width { get; }

        public int Height { get; }

        public int RowStride { get; }

        public int Offset { get; }
    }
}
