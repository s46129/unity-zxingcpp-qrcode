using System;
using System.Buffers;

namespace S46129.QRCode.Internal
{
    internal readonly struct PreparedGray8Image : IDisposable
    {
        private readonly byte[] _rentedBuffer;

        internal PreparedGray8Image(
            Gray8Image image,
            PixelRegion nativeCrop,
            bool applyNativeCrop,
            int originX,
            int originY,
            int coordinateScale,
            byte[] rentedBuffer)
        {
            Image = image;
            NativeCrop = nativeCrop;
            ApplyNativeCrop = applyNativeCrop;
            OriginX = originX;
            OriginY = originY;
            CoordinateScale = coordinateScale;
            _rentedBuffer = rentedBuffer;
        }

        internal Gray8Image Image { get; }

        internal PixelRegion NativeCrop { get; }

        internal bool ApplyNativeCrop { get; }

        internal int OriginX { get; }

        internal int OriginY { get; }

        internal int CoordinateScale { get; }

        public void Dispose()
        {
            if (_rentedBuffer != null)
                ArrayPool<byte>.Shared.Return(_rentedBuffer);
        }
    }
}
