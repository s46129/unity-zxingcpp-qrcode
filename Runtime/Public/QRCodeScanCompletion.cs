using System;

namespace ZXingCpp.QRCode
{
    /// <summary>Describes one accepted scanner frame after its borrowed buffer is no longer in use.</summary>
    public sealed class QRCodeScanCompletion
    {
        internal QRCodeScanCompletion(Gray8Image frame, QRCodeResult result, Exception error, bool discarded)
        {
            Frame = frame;
            Result = result;
            Error = error;
            Discarded = discarded;
        }

        public Gray8Image Frame { get; }

        public QRCodeResult Result { get; }

        public Exception Error { get; }

        public bool Discarded { get; }

        public bool Succeeded => !Discarded && Error == null && Result != null;
    }
}
