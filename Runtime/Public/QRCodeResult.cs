using System;

namespace ZXingCpp.QRCode
{
    /// <summary>An immutable decoded QR payload and its position in the original input image.</summary>
    public sealed class QRCodeResult
    {
        public QRCodeResult(
            string text,
            byte[] rawBytes,
            QRCodePoint topLeft,
            QRCodePoint topRight,
            QRCodePoint bottomRight,
            QRCodePoint bottomLeft,
            int orientation,
            bool isInverted,
            bool isMirrored,
            string symbologyIdentifier)
        {
            Text = text ?? string.Empty;
            RawBytes = rawBytes ?? Array.Empty<byte>();
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
            Orientation = orientation;
            IsInverted = isInverted;
            IsMirrored = isMirrored;
            SymbologyIdentifier = symbologyIdentifier ?? string.Empty;
        }

        public string Text { get; }

        public byte[] RawBytes { get; }

        public QRCodePoint TopLeft { get; }

        public QRCodePoint TopRight { get; }

        public QRCodePoint BottomRight { get; }

        public QRCodePoint BottomLeft { get; }

        public int Orientation { get; }

        public bool IsInverted { get; }

        public bool IsMirrored { get; }

        public string SymbologyIdentifier { get; }
    }
}
