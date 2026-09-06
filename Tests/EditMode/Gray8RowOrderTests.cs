using System;
using NUnit.Framework;
using ZXingCpp.QRCode.Internal;

namespace ZXingCpp.QRCode.Tests
{
    public sealed class Gray8RowOrderTests
    {
        [Test]
        public void FlipVertically_BottomUpRaster_ProducesTopLeftOriginRows()
        {
            // Displayed rows are "1 2" over "3 4"; a texture stores the bottom row first.
            byte[] raster = { 3, 4, 1, 2 };

            Gray8RowOrder.FlipVertically(new Gray8Image(raster, 2, 2));

            Assert.That(raster, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void FlipVertically_OddHeight_KeepsTheMiddleRowInPlace()
        {
            byte[] raster = { 1, 2, 3, 4, 5, 6 };

            Gray8RowOrder.FlipVertically(new Gray8Image(raster, 2, 3));

            Assert.That(raster, Is.EqualTo(new byte[] { 5, 6, 3, 4, 1, 2 }));
        }

        [Test]
        public void FlipVertically_SingleRow_LeavesTheRasterUnchanged()
        {
            byte[] raster = { 7, 8, 9 };

            Gray8RowOrder.FlipVertically(new Gray8Image(raster, 3, 1));

            Assert.That(raster, Is.EqualTo(new byte[] { 7, 8, 9 }));
        }

        [Test]
        public void FlipVertically_MarkerAtDisplayedTopLeft_LandsAtTopLeftImageCoordinates()
        {
            const int width = 6;
            const int height = 4;
            byte[] raster = new byte[width * height];
            // The first stored row is the displayed bottom row, so displayed (1, 1) is stored in row height - 2.
            raster[(height - 2) * width + 1] = 255;

            Gray8RowOrder.FlipVertically(new Gray8Image(raster, width, height));

            Assert.That(Array.IndexOf(raster, (byte)255), Is.EqualTo(1 * width + 1));
        }

        [Test]
        public void FlipVertically_StrideAndOffset_LeavesBytesOutsideTheImageUntouched()
        {
            const int width = 3;
            const int height = 3;
            const int rowStride = 4;
            const int offset = 2;
            byte[] raster = new byte[16];
            for (int index = 0; index < raster.Length; index++)
                raster[index] = 200;
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                    raster[offset + row * rowStride + column] = (byte)(row * width + column + 1);

            Gray8RowOrder.FlipVertically(new Gray8Image(raster, width, height, rowStride, offset));

            Assert.That(raster, Is.EqualTo(new byte[]
            {
                200, 200,
                7, 8, 9, 200,
                4, 5, 6, 200,
                1, 2, 3, 200,
                200, 200
            }));
        }

        [Test]
        public void FlipVertically_AppliedTwice_RestoresTheOriginalRaster()
        {
            byte[] raster = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            byte[] original = (byte[])raster.Clone();
            var image = new Gray8Image(raster, 4, 3);

            Gray8RowOrder.FlipVertically(image);
            Gray8RowOrder.FlipVertically(image);

            Assert.That(raster, Is.EqualTo(original));
        }

        [TestCase(0f, (byte)255)]
        [TestCase(0.5f, (byte)0)]
        public void FlipVertically_ThenHalfRoi_SelectsTheDisplayedHalf(float regionY, byte expected)
        {
            const int size = 8;
            byte[] raster = new byte[size * size];
            // Stored bottom-up: the first half of the rows is the displayed bottom half.
            for (int row = 0; row < size; row++)
                for (int column = 0; column < size; column++)
                    raster[row * size + column] = row < size / 2 ? (byte)0 : (byte)255;

            var image = new Gray8Image(raster, size, size);
            Gray8RowOrder.FlipVertically(image);

            var options = new QRCodeDecodeOptions { Region = new QRCodeRegion(0f, regionY, 1f, 0.5f) };
            using (PreparedGray8Image prepared = Gray8Preprocessor.Prepare(image, options))
            {
                PixelRegion window = prepared.NativeCrop;
                Gray8Image sampled = prepared.Image;

                Assert.That(window.Height, Is.EqualTo(size / 2));
                for (int y = 0; y < window.Height; y++)
                    for (int x = 0; x < window.Width; x++)
                        Assert.That(
                            sampled.Buffer[sampled.Offset + (window.Top + y) * sampled.RowStride + window.Left + x],
                            Is.EqualTo(expected),
                            $"The ROI selected the wrong half at ({x}, {y}).");
            }
        }

        [Test]
        public void FlipVertically_ImageWithoutBuffer_ThrowsArgumentException()
        {
            Assert.That(() => Gray8RowOrder.FlipVertically(default(Gray8Image)), Throws.ArgumentException);
        }
    }
}
