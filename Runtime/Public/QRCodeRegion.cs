using System;

namespace ZXingCpp.QRCode
{
    /// <summary>A normalized top-left-origin region of interest, where every component is in the range 0..1.</summary>
    public readonly struct QRCodeRegion : IEquatable<QRCodeRegion>
    {
        private const float BoundsTolerance = 0.00001f;
        public static readonly QRCodeRegion Full = new QRCodeRegion(0f, 0f, 1f, 1f);

        public QRCodeRegion(float x, float y, float width, float height)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(width) || float.IsNaN(height))
                throw new ArgumentException("ROI values cannot be NaN.");
            if (x < 0f || y < 0f || width <= 0f || height <= 0f ||
                x + width > 1f + BoundsTolerance || y + height > 1f + BoundsTolerance)
                throw new ArgumentOutOfRangeException(nameof(width), "ROI must be positive and contained by normalized image bounds.");

            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float X { get; }

        public float Y { get; }

        public float Width { get; }

        public float Height { get; }

        public bool IsFull => Equals(Full);

        public bool Equals(QRCodeRegion other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

        public override bool Equals(object obj) => obj is QRCodeRegion other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Width.GetHashCode();
                return (hashCode * 397) ^ Height.GetHashCode();
            }
        }
    }
}
