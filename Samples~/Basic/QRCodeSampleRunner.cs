using System;
using Unity.Collections;
using UnityEngine;

namespace ZXingCpp.QRCode.Samples
{
    public sealed class QRCodeSampleRunner : MonoBehaviour
    {
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField, Min(0f)] private float scanIntervalSeconds = 0.2f;
        [SerializeField] private bool stopOnSuccess = true;
        [SerializeField, Range(1, 4)] private int downscaleFactor = 1;

        private QRCodeScanner _scanner;

        private void Start()
        {
            var decoder = new ZXingCppQRCodeDecoder();
            _scanner = new QRCodeScanner(decoder, new QRCodeScannerOptions
            {
                ScanInterval = TimeSpan.FromSeconds(scanIntervalSeconds),
                StopOnSuccess = stopOnSuccess,
                DecodeOptions = new QRCodeDecodeOptions
                {
                    DownscaleFactor = downscaleFactor,
                    TryRotate = true
                }
            });
            _scanner.Detected += OnDetected;
            _scanner.DecodeFailed += OnDecodeFailed;
            _scanner.Start();

            if (sourceTexture != null)
                SubmitTexture(sourceTexture);
        }

        public bool SubmitTexture(Texture2D texture)
        {
            if (texture == null)
                throw new ArgumentNullException(nameof(texture));
            if (texture.format != TextureFormat.R8 && texture.format != TextureFormat.Alpha8)
                throw new ArgumentException("The sample accepts only readable R8 or Alpha8 textures.", nameof(texture));

            NativeArray<byte> rawData = texture.GetRawTextureData<byte>();
            return SubmitRawGray8(rawData.ToArray(), texture.width, texture.height);
        }

        public bool SubmitRawGray8(byte[] gray8, int width, int height, int rowStride = 0)
        {
            if (_scanner == null)
                throw new InvalidOperationException("The sample runner has not started.");

            return _scanner.TrySubmitFrame(new Gray8Image(gray8, width, height, rowStride));
        }

        private static void OnDetected(QRCodeResult result)
        {
            Debug.Log($"QR decoded: {result.Text}");
        }

        private static void OnDecodeFailed(Exception exception)
        {
            Debug.LogException(exception);
        }

        private void OnDestroy()
        {
            if (_scanner == null)
                return;

            _scanner.Detected -= OnDetected;
            _scanner.DecodeFailed -= OnDecodeFailed;
            _scanner.Dispose();
        }
    }
}
