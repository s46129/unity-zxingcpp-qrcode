using NUnit.Framework;
using ZXingCpp.QRCode.Internal;

namespace ZXingCpp.QRCode.Tests
{
    public sealed class PixelRegionTests
    {
        [Test]
        public void Resolve_RoiStartingAtBottomEdge_Throws()
        {
            var region = new QRCodeRegion(0f, 1f, 1f, 0.000001f);

            Assert.That(() => PixelRegion.Resolve(region, 10, 10), Throws.ArgumentException);
        }

        [Test]
        public void Resolve_RoiStartingAtRightEdge_Throws()
        {
            var region = new QRCodeRegion(1f, 0f, 0.000001f, 1f);

            Assert.That(() => PixelRegion.Resolve(region, 10, 10), Throws.ArgumentException);
        }

        [Test]
        public void Resolve_RoiSnappingToRightEdge_Throws()
        {
            var region = new QRCodeRegion(0.9999999f, 0f, 0.000001f, 1f);

            Assert.That(() => PixelRegion.Resolve(region, 10, 10), Throws.ArgumentException);
        }

        [Test]
        public void Resolve_RoiOvershootingWithinTolerance_ClampsToImageBounds()
        {
            var region = new QRCodeRegion(0f, 0f, 1.000005f, 1.000005f);

            PixelRegion resolved = PixelRegion.Resolve(region, 10, 10);

            Assert.That(resolved.Left, Is.EqualTo(0));
            Assert.That(resolved.Top, Is.EqualTo(0));
            Assert.That(resolved.Width, Is.EqualTo(10));
            Assert.That(resolved.Height, Is.EqualTo(10));
        }

        [Test]
        public void Resolve_SubPixelRoiInsideImage_YieldsOnePixel()
        {
            var region = new QRCodeRegion(0.5f, 0.5f, 0.0000001f, 0.0000001f);

            PixelRegion resolved = PixelRegion.Resolve(region, 10, 10);

            Assert.That(resolved.Left, Is.EqualTo(5));
            Assert.That(resolved.Top, Is.EqualTo(5));
            Assert.That(resolved.Width, Is.EqualTo(1));
            Assert.That(resolved.Height, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_RoiTouchingRightAndBottomEdge_StaysInsideImage()
        {
            var region = new QRCodeRegion(0.5f, 0.5f, 0.5f, 0.5f);

            PixelRegion resolved = PixelRegion.Resolve(region, 10, 10);

            Assert.That(resolved.Left, Is.EqualTo(5));
            Assert.That(resolved.Top, Is.EqualTo(5));
            Assert.That(resolved.Left + resolved.Width, Is.EqualTo(10));
            Assert.That(resolved.Top + resolved.Height, Is.EqualTo(10));
        }
    }
}
