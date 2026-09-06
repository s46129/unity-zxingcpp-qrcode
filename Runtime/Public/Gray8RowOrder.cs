using System;
using System.Buffers;

namespace ZXingCpp.QRCode
{
    /// <summary>Converts a bottom-up raster, such as Unity texture data, to the top-left-origin image contract.</summary>
    public static class Gray8RowOrder
    {
        /// <summary>Reverses the row order of <paramref name="image"/> in place; bytes outside the image, including row padding, stay untouched.</summary>
        public static void FlipVertically(Gray8Image image)
        {
            if (image.Buffer == null)
                throw new ArgumentException("The image has no buffer.", nameof(image));
            if (image.Height < 2)
                return;

            byte[] rowCopy = ArrayPool<byte>.Shared.Rent(image.Width);
            try
            {
                for (int top = 0, bottom = image.Height - 1; top < bottom; top++, bottom--)
                {
                    int topRow = image.Offset + top * image.RowStride;
                    int bottomRow = image.Offset + bottom * image.RowStride;
                    Array.Copy(image.Buffer, topRow, rowCopy, 0, image.Width);
                    Array.Copy(image.Buffer, bottomRow, image.Buffer, topRow, image.Width);
                    Array.Copy(rowCopy, 0, image.Buffer, bottomRow, image.Width);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rowCopy);
            }
        }
    }
}
