using System;

namespace S46129.QRCode
{
    /// <summary>Reports a missing, incompatible, or failed ZXing-C++ native plugin call.</summary>
    public sealed class QRCodeNativeException : Exception
    {
        public QRCodeNativeException(string message)
            : base(message)
        {
        }

        public QRCodeNativeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
