using System;

namespace S46129.QRCode
{
    /// <summary>Controls background scan scheduling and decode behavior.</summary>
    public sealed class QRCodeScannerOptions
    {
        public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMilliseconds(200);

        public bool StopOnSuccess { get; set; } = true;

        public QRCodeDecodeOptions DecodeOptions { get; set; } = new QRCodeDecodeOptions();

        internal QRCodeScannerOptions Snapshot()
        {
            if (ScanInterval < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(ScanInterval), "Scan interval cannot be negative.");
            if (DecodeOptions == null)
                throw new ArgumentNullException(nameof(DecodeOptions));

            return new QRCodeScannerOptions
            {
                ScanInterval = ScanInterval,
                StopOnSuccess = StopOnSuccess,
                DecodeOptions = DecodeOptions.Snapshot()
            };
        }
    }
}
