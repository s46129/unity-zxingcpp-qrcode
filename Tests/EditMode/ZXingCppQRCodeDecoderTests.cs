using System.Runtime.InteropServices;
using NUnit.Framework;

namespace ZXingCpp.QRCode.Tests
{
    public sealed class ZXingCppQRCodeDecoderTests
    {
        private const string Payload = TopLeftSymbol.Payload;
        private const int FrameSize = TopLeftSymbol.FrameSize;
        private const int Downscale = 2;
        private const int SymbolLeft = TopLeftSymbol.SymbolLeft;
        private const int SymbolTop = TopLeftSymbol.SymbolTop;
        private const int SymbolRight = TopLeftSymbol.SymbolRight;
        private const int SymbolBottom = TopLeftSymbol.SymbolBottom;

        [SetUp]
        public void RequireAnEditorThatCanLoadTheNativePlugin()
        {
            bool windowsX64 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                              RuntimeInformation.ProcessArchitecture == Architecture.X64;
            bool macOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                         (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
                          RuntimeInformation.ProcessArchitecture == Architecture.Arm64);
            if (!windowsX64 && !macOS)
                Assert.Ignore("The package ships Editor-loadable ZXing-C++ binaries for Windows x86_64 and macOS (arm64/x86_64) only, so no other Editor can load it.");
        }

        [Test]
        public void TryDecode_SymbolInTheTopLeftCorner_ReadsThePayload()
        {
            QRCodeResult result = Decode(CreateFrame(), new QRCodeDecodeOptions());

            Assert.That(result.Text, Is.EqualTo(Payload));
        }

        [Test]
        public void TryDecode_SymbolInTheTopLeftCorner_ReportsCornersInFrameCoordinates()
        {
            QRCodeResult result = Decode(CreateFrame(), new QRCodeDecodeOptions());

            Assert.That(result.TopLeft, Is.EqualTo(new QRCodePoint(SymbolLeft, SymbolTop)));
            Assert.That(result.TopRight, Is.EqualTo(new QRCodePoint(SymbolRight, SymbolTop)));
            Assert.That(result.BottomRight, Is.EqualTo(new QRCodePoint(SymbolRight, SymbolBottom)));
            Assert.That(result.BottomLeft, Is.EqualTo(new QRCodePoint(SymbolLeft, SymbolBottom)));
        }

        [Test]
        public void TryDecode_UprightSymbol_ReportsNoRotationMirroringOrInversion()
        {
            QRCodeResult result = Decode(CreateFrame(), new QRCodeDecodeOptions());

            Assert.That(result.Orientation, Is.EqualTo(0));
            Assert.That(result.IsMirrored, Is.False);
            Assert.That(result.IsInverted, Is.False);
        }

        [Test]
        public void TryDecode_UpperHalfRoi_FindsTheSymbolAndMapsCornersBackToTheFrame()
        {
            var options = new QRCodeDecodeOptions { Region = new QRCodeRegion(0f, 0f, 1f, 0.5f) };

            QRCodeResult result = Decode(CreateFrame(), options);

            Assert.That(result.Text, Is.EqualTo(Payload));
            Assert.That(result.TopLeft, Is.EqualTo(new QRCodePoint(SymbolLeft, SymbolTop)));
            Assert.That(result.BottomRight, Is.EqualTo(new QRCodePoint(SymbolRight, SymbolBottom)));
        }

        [Test]
        public void TryDecode_LowerHalfRoi_FindsNothing()
        {
            var options = new QRCodeDecodeOptions { Region = new QRCodeRegion(0f, 0.5f, 1f, 0.5f) };
            var decoder = new ZXingCppQRCodeDecoder();

            bool found = decoder.TryDecode(CreateImage(CreateFrame()), options, out QRCodeResult result);

            Assert.That(found, Is.False);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void TryDecode_DownscaledFrame_MapsCornersBackToTheFullResolutionFrame()
        {
            var options = new QRCodeDecodeOptions { DownscaleFactor = Downscale };

            QRCodeResult result = Decode(CreateFrame(), options);

            Assert.That(result.Text, Is.EqualTo(Payload));
            Assert.That(result.TopLeft.X, Is.EqualTo(SymbolLeft).Within(Downscale));
            Assert.That(result.TopLeft.Y, Is.EqualTo(SymbolTop).Within(Downscale));
            Assert.That(result.BottomRight.X, Is.EqualTo(SymbolRight).Within(Downscale));
            Assert.That(result.BottomRight.Y, Is.EqualTo(SymbolBottom).Within(Downscale));
        }

        [Test]
        public void TryDecode_BottomUpRaster_ReportsTheSymbolMirroredInTheLowerHalf()
        {
            QRCodeResult result = Decode(CreateBottomUpFrame(), new QRCodeDecodeOptions());

            Assert.That(result.Text, Is.EqualTo(Payload));
            Assert.That(result.IsMirrored, Is.True);
            Assert.That(result.Orientation, Is.Not.EqualTo(0));
            Assert.That(result.TopLeft.Y, Is.GreaterThan(FrameSize / 2));
        }

        [Test]
        public void TryDecode_BottomUpRasterWithUpperHalfRoi_FindsNothing()
        {
            var options = new QRCodeDecodeOptions { Region = new QRCodeRegion(0f, 0f, 1f, 0.5f) };
            var decoder = new ZXingCppQRCodeDecoder();

            Assert.That(decoder.TryDecode(CreateImage(CreateBottomUpFrame()), options, out _), Is.False);
        }

        private static QRCodeResult Decode(byte[] frame, QRCodeDecodeOptions options)
        {
            var decoder = new ZXingCppQRCodeDecoder();
            Assert.That(
                decoder.TryDecode(CreateImage(frame), options, out QRCodeResult result),
                Is.True,
                "The decoder found no QR code in the frame.");
            return result;
        }

        private static Gray8Image CreateImage(byte[] frame) => new Gray8Image(frame, FrameSize, FrameSize);

        private static byte[] CreateBottomUpFrame()
        {
            byte[] frame = CreateFrame();
            Gray8RowOrder.FlipVertically(CreateImage(frame));
            return frame;
        }

        private static byte[] CreateFrame() => TopLeftSymbol.CreateFrame();
    }
}
