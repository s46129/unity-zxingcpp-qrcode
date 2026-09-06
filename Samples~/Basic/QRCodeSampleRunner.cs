using System;
using System.Buffers;
using System.Collections.Generic;
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

        private readonly HashSet<byte[]> _rentedFrameBuffers = new HashSet<byte[]>();

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
            _scanner.ScanCompleted += OnScanCompleted;
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
            if (texture.GetRawTextureData<byte>().Length < texture.width * texture.height)
                throw new ArgumentException("The texture raw data is smaller than its mip level 0.", nameof(texture));
            if (_scanner == null)
                throw new InvalidOperationException("The sample runner has not started.");

            return _scanner.TrySubmitFrame(() => RentTextureFrame(texture));
        }

        public bool SubmitRawGray8(byte[] gray8, int width, int height, int rowStride = 0)
        {
            if (_scanner == null)
                throw new InvalidOperationException("The sample runner has not started.");

            return _scanner.TrySubmitFrame(new Gray8Image(gray8, width, height, rowStride));
        }

        private Gray8Image RentTextureFrame(Texture2D texture)
        {
            int mipLevel0Length = texture.width * texture.height;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(mipLevel0Length);
            try
            {
                // Raw texture data carries every mip level; only mip 0 reaches the decoder.
                NativeArray<byte>.Copy(texture.GetRawTextureData<byte>(), 0, buffer, 0, mipLevel0Length);
                var frame = new Gray8Image(buffer, texture.width, texture.height);
                _rentedFrameBuffers.Add(buffer);
                return frame;
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(buffer);
                throw;
            }
        }

        private void OnScanCompleted(QRCodeScanCompletion completion)
        {
            // SubmitRawGray8 hands over caller-owned buffers, and a handler that resubmits can
            // rent another one before this fires, so the ledger decides what goes back.
            if (completion.Frame.Buffer == null || !_rentedFrameBuffers.Remove(completion.Frame.Buffer))
                return;

            ArrayPool<byte>.Shared.Return(completion.Frame.Buffer);
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
            _scanner.ScanCompleted -= OnScanCompleted;
            _scanner.Dispose();
            // In-flight buffers are dropped rather than returned: the decode still reads them.
            _rentedFrameBuffers.Clear();
        }
    }
}
