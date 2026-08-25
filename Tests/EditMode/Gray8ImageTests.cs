using System;
using NUnit.Framework;

namespace S46129.QRCode.Tests
{
    public sealed class Gray8ImageTests
    {
        [Test]
        public void Constructor_StridedBuffer_StoresLayout()
        {
            var image = new Gray8Image(new byte[32], 4, 3, 8, 2);

            Assert.That(image.Width, Is.EqualTo(4));
            Assert.That(image.Height, Is.EqualTo(3));
            Assert.That(image.RowStride, Is.EqualTo(8));
            Assert.That(image.Offset, Is.EqualTo(2));
        }

        [TestCase(0, 2, 2, 2)]
        [TestCase(2, 0, 2, 2)]
        [TestCase(3, 2, 2, 2)]
        public void Constructor_InvalidLayout_Throws(int bufferLength, int width, int height, int stride)
        {
            Assert.Catch<ArgumentException>(() => new Gray8Image(new byte[bufferLength], width, height, stride));
        }
    }
}
