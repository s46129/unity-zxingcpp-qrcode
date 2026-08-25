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
    }
}
