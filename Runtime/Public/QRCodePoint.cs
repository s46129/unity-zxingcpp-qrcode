using System;

namespace S46129.QRCode
{
    /// <summary>An integer image-space point measured from the original Gray8 image's top-left corner.</summary>
    public readonly struct QRCodePoint : IEquatable<QRCodePoint>
    {
        public QRCodePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public bool Equals(QRCodePoint other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is QRCodePoint other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => $"({X}, {Y})";
    }
}
