using System;

namespace ZXingCpp.QRCode
{
    /// <summary>Controls background scan scheduling and decode behavior.</summary>
    public sealed class QRCodeScannerOptions
    {
        public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMilliseconds(200);

        public bool StopOnSuccess { get; set; } = true;

        /// <summary>When true, <see cref="QRCodeScanner.Detected"/> fires only when the payload differs from the previous one; <see cref="MissesBeforeReset"/> consecutive empty scans clear it.</summary>
        public bool SuppressRepeats { get; set; }

        public int MissesBeforeReset { get; set; } = 3;

        public QRCodeDecodeOptions DecodeOptions { get; set; } = new QRCodeDecodeOptions();

        internal QRCodeScannerOptions Snapshot()
        {
            if (ScanInterval < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(ScanInterval), "Scan interval cannot be negative.");
            if (MissesBeforeReset < 1)
                throw new ArgumentOutOfRangeException(nameof(MissesBeforeReset), "At least one empty scan is needed before a repeated result can fire again.");
            if (DecodeOptions == null)
                throw new ArgumentNullException(nameof(DecodeOptions));

            return new QRCodeScannerOptions
            {
                ScanInterval = ScanInterval,
                StopOnSuccess = StopOnSuccess,
                SuppressRepeats = SuppressRepeats,
                MissesBeforeReset = MissesBeforeReset,
                DecodeOptions = DecodeOptions.Snapshot()
            };
        }
    }
}
