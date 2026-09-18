namespace ZXingCpp.QRCode.Tests
{
    /// <summary>A version 1-L "TOPLEFT" symbol drawn top-down into the top-left corner of a 256x256 frame; shared by the native and the GPU readback tests.</summary>
    public static class TopLeftSymbol
    {
        public const string Payload = "TOPLEFT";
        public const int ModuleCount = 21;
        public const int ModuleScale = 4;
        public const int QuietZoneModules = 4;
        public const int FrameSize = 256;
        public const byte White = 255;
        public const byte Black = 0;

        public const int SymbolLeft = QuietZoneModules * ModuleScale;
        public const int SymbolTop = QuietZoneModules * ModuleScale;
        public const int SymbolRight = SymbolLeft + ModuleCount * ModuleScale;
        public const int SymbolBottom = SymbolTop + ModuleCount * ModuleScale;

        // Inline so the suite needs no image asset; regenerate with a QR encoder to change the payload.
        private static readonly string[] SymbolModules =
        {
            "111111100111101111111",
            "100000100101001000001",
            "101110100100001011101",
            "101110101111001011101",
            "101110101011101011101",
            "100000100010101000001",
            "111111101010101111111",
            "000000000111100000000",
            "110001110100100011000",
            "010000011000101010011",
            "101010110101010010100",
            "101111001100000011011",
            "111011100010001000011",
            "000000001001111001110",
            "111111101100101100111",
            "100000101111110010010",
            "101110100110100100000",
            "101110100100100000100",
            "101110100000001110111",
            "100000101010000101101",
            "111111101111010011100"
        };

        /// <summary>Returns the frame with its first row at the top, the package's image contract.</summary>
        public static byte[] CreateFrame()
        {
            byte[] frame = new byte[FrameSize * FrameSize];
            for (int index = 0; index < frame.Length; index++)
                frame[index] = White;

            for (int row = 0; row < ModuleCount; row++)
            {
                string modules = SymbolModules[row];
                for (int column = 0; column < ModuleCount; column++)
                {
                    if (modules[column] != '1')
                        continue;

                    int top = SymbolTop + row * ModuleScale;
                    int left = SymbolLeft + column * ModuleScale;
                    for (int y = top; y < top + ModuleScale; y++)
                        for (int x = left; x < left + ModuleScale; x++)
                            frame[y * FrameSize + x] = Black;
                }
            }

            return frame;
        }
    }
}
