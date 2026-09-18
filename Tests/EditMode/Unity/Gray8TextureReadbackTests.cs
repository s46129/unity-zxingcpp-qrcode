using System;
using System.Collections;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using ZXingCpp.QRCode.Tests;

namespace ZXingCpp.QRCode.Unity.Tests
{
    /// <summary>Needs a graphics device: run these in an Editor, not under -nographics.</summary>
    public sealed class Gray8TextureReadbackTests
    {
        private const int LumaTolerance = 2;

        [SetUp]
        public void RequireAsyncReadback()
        {
            if (!Gray8TextureReadback.IsSupported)
                Assert.Ignore("This graphics device cannot render to R8 or read it back asynchronously.");
        }

        [UnityTest]
        public IEnumerator TryRequest_BottomUpTexture_ProducesTopLeftOriginLuma()
        {
            const int width = 4;
            const int height = 3;
            Texture2D texture = CreateTexture(width, height, out Color32[] bottomUp);
            using (var readback = new Gray8TextureReadback())
            {
                Assert.That(readback.TryRequest(texture), Is.True);
                yield return WaitForFrame(readback);

                byte[] buffer = new byte[readback.FrameByteLength];
                Gray8Image frame = readback.CopyFrame(buffer);

                Assert.That((frame.Width, frame.Height), Is.EqualTo((width, height)));
                AssertMatches(frame, ExpectedTopDownLuma(bottomUp, width, height), width, height);
            }
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [UnityTest]
        public IEnumerator TryRequest_FlipDisabled_KeepsTheStoredRowOrder()
        {
            const int width = 4;
            const int height = 3;
            Texture2D texture = CreateTexture(width, height, out Color32[] bottomUp);
            using (var readback = new Gray8TextureReadback())
            {
                Assert.That(readback.TryRequest(texture, flipVertically: false), Is.True);
                yield return WaitForFrame(readback);

                Gray8Image frame = readback.CopyFrame(new byte[readback.FrameByteLength]);

                byte[] expected = new byte[width * height];
                for (int index = 0; index < expected.Length; index++)
                    expected[index] = Luma(bottomUp[index]);
                AssertMatches(frame, expected, width, height);
            }
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [UnityTest]
        public IEnumerator TryRequest_DownscaleFactor2_BoxAveragesWithPartialEdges()
        {
            const int width = 5;
            const int height = 3;
            const int factor = 2;
            Texture2D texture = CreateTexture(width, height, out Color32[] bottomUp);
            using (var readback = new Gray8TextureReadback(factor))
            {
                Assert.That(readback.TryRequest(texture), Is.True);
                yield return WaitForFrame(readback);

                Gray8Image frame = readback.CopyFrame(new byte[readback.FrameByteLength]);

                const int outputWidth = 3;
                const int outputHeight = 2;
                Assert.That((frame.Width, frame.Height), Is.EqualTo((outputWidth, outputHeight)));
                byte[] source = ExpectedTopDownLuma(bottomUp, width, height);
                byte[] expected = new byte[outputWidth * outputHeight];
                for (int y = 0; y < outputHeight; y++)
                    for (int x = 0; x < outputWidth; x++)
                    {
                        int sum = 0;
                        int count = 0;
                        for (int sy = y * factor; sy < Math.Min(height, y * factor + factor); sy++)
                            for (int sx = x * factor; sx < Math.Min(width, x * factor + factor); sx++)
                            {
                                sum += source[sy * width + sx];
                                count++;
                            }
                        expected[y * outputWidth + x] = (byte)(sum / count);
                    }
                AssertMatches(frame, expected, outputWidth, outputHeight);
            }
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [UnityTest]
        public IEnumerator TryRequest_WhilePending_ReturnsFalse()
        {
            Texture2D texture = CreateTexture(4, 4, out _);
            using (var readback = new Gray8TextureReadback())
            {
                Assert.That(readback.TryRequest(texture), Is.True);
                Assert.That(readback.IsPending, Is.True);
                Assert.That(readback.HasFrame, Is.False);
                Assert.That(readback.TryRequest(texture), Is.False);
                Assert.That(() => readback.CopyFrame(new byte[16]), Throws.InvalidOperationException);

                yield return WaitForFrame(readback);

                Assert.That(readback.IsPending, Is.False);
                Assert.That(readback.TryRequest(texture), Is.True);
                yield return WaitForFrame(readback);
            }
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void Constructor_FactorOutOfRange_Throws()
        {
            Assert.That(() => new Gray8TextureReadback(0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Gray8TextureReadback(9), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [UnityTest]
        public IEnumerator TryRequest_TopLeftSymbolTexture_DecodesThroughTheNativePlugin()
        {
            if (!CanLoadNativePlugin())
                Assert.Ignore("The package ships Editor-loadable ZXing-C++ binaries for Windows x86_64 and macOS only.");

            const int size = TopLeftSymbol.FrameSize;
            byte[] topDown = TopLeftSymbol.CreateFrame();
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Texture rows start at the displayed bottom, so the top-down frame is stored reversed.
                    byte value = topDown[y * size + x];
                    pixels[(size - 1 - y) * size + x] = new Color32(value, value, value, 255);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            using (var readback = new Gray8TextureReadback())
            {
                Assert.That(readback.TryRequest(texture), Is.True);
                yield return WaitForFrame(readback);
                Gray8Image frame = readback.CopyFrame(new byte[readback.FrameByteLength]);

                var decoder = new ZXingCppQRCodeDecoder();
                bool found = decoder.TryDecode(frame, new QRCodeDecodeOptions(), out QRCodeResult result);

                Assert.That(found, Is.True, "The decoder found no QR code in the GPU frame.");
                Assert.That(result.Text, Is.EqualTo(TopLeftSymbol.Payload));
                Assert.That(result.TopLeft, Is.EqualTo(new QRCodePoint(TopLeftSymbol.SymbolLeft, TopLeftSymbol.SymbolTop)));
                Assert.That(result.BottomRight, Is.EqualTo(new QRCodePoint(TopLeftSymbol.SymbolRight, TopLeftSymbol.SymbolBottom)));
                Assert.That(result.Orientation, Is.EqualTo(0));
                Assert.That(result.IsMirrored, Is.False);
            }
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static IEnumerator WaitForFrame(Gray8TextureReadback readback)
        {
            bool failed = false;
            Action<Gray8TextureReadback> onFailed = _ => failed = true;
            readback.ReadbackFailed += onFailed;
            try
            {
                AsyncGPUReadback.WaitAllRequests();
                double deadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (!readback.HasFrame && !failed && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
            }
            finally
            {
                readback.ReadbackFailed -= onFailed;
            }

            Assert.That(failed, Is.False, "The GPU reported a readback error.");
            Assert.That(readback.HasFrame, Is.True, "The readback did not complete within 5 seconds.");
        }

        // Every pixel gets a distinct colour, so a swapped row or column changes the luma.
        private static Texture2D CreateTexture(int width, int height, out Color32[] bottomUp)
        {
            bottomUp = new Color32[width * height];
            for (int index = 0; index < bottomUp.Length; index++)
                bottomUp[index] = new Color32((byte)(index * 37 % 256), (byte)(255 - index * 53 % 256), (byte)(index * 11 % 256), 255);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixels32(bottomUp);
            texture.Apply(false, false);
            return texture;
        }

        private static byte[] ExpectedTopDownLuma(Color32[] bottomUp, int width, int height)
        {
            byte[] topDown = new byte[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    topDown[y * width + x] = Luma(bottomUp[(height - 1 - y) * width + x]);
            return topDown;
        }

        private static byte Luma(Color32 c) => (byte)((c.r * 77 + c.g * 151 + c.b * 28) >> 8);

        private static void AssertMatches(Gray8Image frame, byte[] expected, int width, int height)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    Assert.That(
                        (int)frame.Buffer[frame.Offset + y * frame.RowStride + x],
                        Is.EqualTo((int)expected[y * width + x]).Within(LumaTolerance),
                        $"Luma differs at ({x}, {y}).");
        }

        private static bool CanLoadNativePlugin()
        {
            bool windowsX64 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                              RuntimeInformation.ProcessArchitecture == Architecture.X64;
            bool macOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                         (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
                          RuntimeInformation.ProcessArchitecture == Architecture.Arm64);
            return windowsX64 || macOS;
        }
    }
}
