using System;

namespace ZXingCpp.QRCode
{
    /// <summary>Controls QR detection and Gray8 preprocessing for one decode operation.</summary>
    public sealed class QRCodeDecodeOptions
    {
        public bool TryHarder { get; set; }

        public bool TryRotate { get; set; } = true;

        public bool TryInvert { get; set; }

        public bool TryNativeDownscale { get; set; } = true;

        public bool IsPure { get; set; }

        public QRCodeRegion Region { get; set; } = QRCodeRegion.Full;

        public int DownscaleFactor { get; set; } = 1;

        internal QRCodeDecodeOptions Snapshot()
        {
            if (DownscaleFactor < 1 || DownscaleFactor > 8)
                throw new ArgumentOutOfRangeException(nameof(DownscaleFactor), "Downscale factor must be between 1 and 8.");

            return new QRCodeDecodeOptions
            {
                TryHarder = TryHarder,
                TryRotate = TryRotate,
                TryInvert = TryInvert,
                TryNativeDownscale = TryNativeDownscale,
                IsPure = IsPure,
                Region = Region,
                DownscaleFactor = DownscaleFactor
            };
        }
    }
}
