using System.Collections.Generic;
using NUnit.Framework;
using ZXingCpp.QRCode.Internal;

namespace ZXingCpp.QRCode.Tests
{
    public sealed class Gray8PreprocessorTests
    {
        [Test]
        public void Prepare_RoiAndDownscale_BoxAveragesGray8Pixels()
        {
            byte[] source =
            {
                 0,  0,  0,  0,
                 0, 10, 20,  0,
                 0, 30, 40,  0,
                 0,  0,  0,  0
            };
            var image = new Gray8Image(source, 4, 4);
            var options = new QRCodeDecodeOptions
            {
                Region = new QRCodeRegion(0.25f, 0.25f, 0.5f, 0.5f),
                DownscaleFactor = 2
            };

            using (PreparedGray8Image prepared = Gray8Preprocessor.Prepare(image, options))
            {
                Assert.That(prepared.Image.Width, Is.EqualTo(1));
                Assert.That(prepared.Image.Height, Is.EqualTo(1));
                Assert.That(prepared.Image.Buffer[0], Is.EqualTo(25));
                Assert.That(prepared.OriginX, Is.EqualTo(1));
                Assert.That(prepared.OriginY, Is.EqualTo(1));
                Assert.That(prepared.CoordinateScale, Is.EqualTo(2));
            }
        }

        [Test]
        public void Prepare_RoiWithoutDownscale_UsesNativeCropAndOriginalBuffer()
        {
            var image = new Gray8Image(new byte[100], 10, 10);
            var options = new QRCodeDecodeOptions
            {
                Region = new QRCodeRegion(0.2f, 0.3f, 0.5f, 0.4f)
            };

            using (PreparedGray8Image prepared = Gray8Preprocessor.Prepare(image, options))
            {
                Assert.That(prepared.Image.Buffer, Is.SameAs(image.Buffer));
                Assert.That(prepared.ApplyNativeCrop, Is.True);
                Assert.That(prepared.NativeCrop.Left, Is.EqualTo(2));
                Assert.That(prepared.NativeCrop.Top, Is.EqualTo(3));
                Assert.That(prepared.NativeCrop.Width, Is.EqualTo(5));
                Assert.That(prepared.NativeCrop.Height, Is.EqualTo(4));
            }
        }

        [Test]
        public void Prepare_RoiOutsideImage_ThrowsBeforeDownscale()
        {
            var image = new Gray8Image(new byte[100], 10, 10);
            var options = new QRCodeDecodeOptions
            {
                Region = new QRCodeRegion(0f, 1f, 1f, 0.000001f),
                DownscaleFactor = 2
            };

            Assert.That(() => Gray8Preprocessor.Prepare(image, options), Throws.ArgumentException);
        }

        private static IEnumerable<TestCaseData> BoundaryRoiCases()
        {
            var regions = new[]
            {
                new { Name = "FullImage", Region = QRCodeRegion.Full },
                new { Name = "RightEdge", Region = new QRCodeRegion(0.5f, 0f, 0.5f, 1f) },
                new { Name = "BottomEdge", Region = new QRCodeRegion(0f, 0.5f, 1f, 0.5f) },
                new { Name = "BottomRightCorner", Region = new QRCodeRegion(0.5f, 0.5f, 0.5f, 0.5f) },
                new { Name = "SubPixel", Region = new QRCodeRegion(0.5f, 0.5f, 0.0000001f, 0.0000001f) },
                new { Name = "OvershootingWithinTolerance", Region = new QRCodeRegion(0f, 0f, 1.000005f, 1.000005f) }
            };

            foreach (var candidate in regions)
                for (int factor = 1; factor <= 8; factor++)
                    yield return new TestCaseData(candidate.Region, factor)
                        .SetName($"Prepare_{candidate.Name}RoiAtDownscale{factor}_ReadsOnlyInsideImage");
        }

        [TestCaseSource(nameof(BoundaryRoiCases))]
        public void Prepare_BoundaryRoi_ReadsOnlyInsideImage(QRCodeRegion region, int factor)
        {
            const int padded = 12;
            byte[] source = new byte[padded * padded];
            for (int index = 0; index < source.Length; index++)
                source[index] = 255;
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                    source[y * padded + x] = 0;

            var image = new Gray8Image(source, 10, 10, padded);
            var options = new QRCodeDecodeOptions
            {
                Region = region,
                DownscaleFactor = factor
            };

            using (PreparedGray8Image prepared = Gray8Preprocessor.Prepare(image, options))
            {
                Gray8Image sampled = prepared.Image;
                PixelRegion window = prepared.NativeCrop;

                for (int y = 0; y < window.Height; y++)
                    for (int x = 0; x < window.Width; x++)
                        Assert.That(
                            sampled.Buffer[sampled.Offset + (window.Top + y) * sampled.RowStride + window.Left + x],
                            Is.EqualTo(0),
                            $"Preprocessing sampled a pixel outside the image at ({x}, {y}).");
            }
        }
    }
}
