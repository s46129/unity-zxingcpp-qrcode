using System;

namespace S46129.QRCode.Internal
{
    internal readonly struct PixelRegion
    {
        private const double PixelBoundaryTolerance = 0.00001d;

        internal PixelRegion(int left, int top, int width, int height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        internal int Left { get; }

        internal int Top { get; }

        internal int Width { get; }

        internal int Height { get; }

        internal bool Covers(int width, int height) => Left == 0 && Top == 0 && Width == width && Height == height;

        internal static PixelRegion Resolve(QRCodeRegion region, int imageWidth, int imageHeight)
        {
            if (region.Width <= 0f || region.Height <= 0f || region.X < 0f || region.Y < 0f ||
                region.X + region.Width > 1.00001f || region.Y + region.Height > 1.00001f)
                throw new ArgumentException("The decode ROI is not a valid normalized region.", nameof(region));

            int left = Math.Max(0, FloorPixel((double)region.X * imageWidth));
            int top = Math.Max(0, FloorPixel((double)region.Y * imageHeight));
            int right = Math.Min(imageWidth, CeilingPixel(((double)region.X + region.Width) * imageWidth));
            int bottom = Math.Min(imageHeight, CeilingPixel(((double)region.Y + region.Height) * imageHeight));

            return new PixelRegion(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }

        private static int FloorPixel(double value) => (int)Math.Floor(SnapPixelBoundary(value));

        private static int CeilingPixel(double value) => (int)Math.Ceiling(SnapPixelBoundary(value));

        private static double SnapPixelBoundary(double value)
        {
            double rounded = Math.Round(value);
            return Math.Abs(value - rounded) <= PixelBoundaryTolerance ? rounded : value;
        }
    }
}
