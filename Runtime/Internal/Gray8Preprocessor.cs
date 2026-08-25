using System;
using System.Buffers;

namespace ZXingCpp.QRCode.Internal
{
    internal static class Gray8Preprocessor
    {
        internal static PreparedGray8Image Prepare(Gray8Image source, QRCodeDecodeOptions options)
        {
            PixelRegion region = PixelRegion.Resolve(options.Region, source.Width, source.Height);
            int factor = options.DownscaleFactor;

            if (factor == 1)
            {
                return new PreparedGray8Image(
                    source,
                    region,
                    !region.Covers(source.Width, source.Height),
                    region.Left,
                    region.Top,
                    1,
                    null);
            }

            int outputWidth = (region.Width + factor - 1) / factor;
            int outputHeight = (region.Height + factor - 1) / factor;
            byte[] output = ArrayPool<byte>.Shared.Rent(checked(outputWidth * outputHeight));

            try
            {
                DownscaleBoxAverage(source, region, factor, output, outputWidth, outputHeight);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(output);
                throw;
            }

            return new PreparedGray8Image(
                new Gray8Image(output, outputWidth, outputHeight),
                new PixelRegion(0, 0, outputWidth, outputHeight),
                false,
                region.Left,
                region.Top,
                factor,
                output);
        }

        private static void DownscaleBoxAverage(
            Gray8Image source,
            PixelRegion region,
            int factor,
            byte[] destination,
            int destinationWidth,
            int destinationHeight)
        {
            for (int outputY = 0; outputY < destinationHeight; outputY++)
            {
                int sourceYStart = region.Top + outputY * factor;
                int sourceYEnd = Math.Min(region.Top + region.Height, sourceYStart + factor);

                for (int outputX = 0; outputX < destinationWidth; outputX++)
                {
                    int sourceXStart = region.Left + outputX * factor;
                    int sourceXEnd = Math.Min(region.Left + region.Width, sourceXStart + factor);
                    int sum = 0;
                    int count = 0;

                    for (int sourceY = sourceYStart; sourceY < sourceYEnd; sourceY++)
                    {
                        int rowOffset = source.Offset + sourceY * source.RowStride;
                        for (int sourceX = sourceXStart; sourceX < sourceXEnd; sourceX++)
                        {
                            sum += source.Buffer[rowOffset + sourceX];
                            count++;
                        }
                    }

                    destination[outputY * destinationWidth + outputX] = (byte)(sum / count);
                }
            }
        }
    }
}
