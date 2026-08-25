namespace ZXingCpp.QRCode
{
    /// <summary>Decodes a QR Code from a validated Gray8 image.</summary>
    public interface IQRCodeDecoder
    {
        bool TryDecode(Gray8Image image, out QRCodeResult result);

        bool TryDecode(Gray8Image image, QRCodeDecodeOptions options, out QRCodeResult result);
    }
}
