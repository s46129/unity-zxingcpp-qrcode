using System.Runtime.InteropServices;
using NUnit.Framework;

namespace ZXingCpp.QRCode.Tests
{
    public sealed class ZXingCppQRCodeDecoderTests
    {
        private const string Payload = "TOPLEFT";
        private const int ModuleCount = 21;
        private const int ModuleScale = 4;
        private const int QuietZoneModules = 4;
        private const int FrameSize = 256;
        private const int Downscale = 2;
        private const byte White = 255;
        private const byte Black = 0;

        private const int SymbolLeft = QuietZoneModules * ModuleScale;
        private const int SymbolTop = QuietZoneModules * ModuleScale;
        private const int SymbolRight = SymbolLeft + ModuleCount * ModuleScale;
        private const int SymbolBottom = SymbolTop + ModuleCount * ModuleScale;

        // Inline so the suite needs no image asset; regenerate with a QR encoder to change the payload.
        private static readonly string[] SymbolModules =
        {
            "111111100111101111111",
            "100000100101001000001",
            "101110100100001011101",
            "101110101111001011101",
            "101110101011101011101",
            "100000100010101000001",
            "111111101010101111111",
            "000000000111100000000",
            "110001110100100011000",
            "010000011000101010011",
            "101010110101010010100",
            "101111001100000011011",
            "111011100010001000011",
            "000000001001111001110",
            "111111101100101100111",
            "100000101111110010010",
            "101110100110100100000",
            "101110100100100000100",
            "101110100000001110111",
            "100000101010000101101",
            "111111101111010011100"
        };

        [SetUp]
        public void RequireAnEditorThatCanLoadTheNativePlugin()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                RuntimeInformation.ProcessArchitecture != Architecture.X64)
                Assert.Ignore("The package only ships a ZXing-C++ binary for Windows x86_64 and Android arm64-v8a, so no other Editor can load it.");
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

        private static byte[] CreateFrame()
        {
            byte[] frame = new byte[FrameSize * FrameSize];
            for (int index = 0; index < frame.Length; index++)
                frame[index] = White;

            for (int row = 0; row < ModuleCount; row++)
            {
                string modules = SymbolModules[row];
                for (int column = 0; column < ModuleCount; column++)
                {
                    if (modules[column] != '1')
                        continue;

                    int top = SymbolTop + row * ModuleScale;
                    int left = SymbolLeft + column * ModuleScale;
                    for (int y = top; y < top + ModuleScale; y++)
                        for (int x = left; x < left + ModuleScale; x++)
                            frame[y * FrameSize + x] = Black;
                }
            }

            return frame;
        }
    }
}
