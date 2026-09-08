using System;
using System.Buffers;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ZXingCpp.QRCode.Samples
{
    public sealed class QRCodeSampleRunner : MonoBehaviour
    {
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private TMP_Text resultLabel;
        [SerializeField] private TMP_Text statsLabel;
        [SerializeField, Min(0f)] private float scanIntervalSeconds = 0.2f;
        [SerializeField] private bool stopOnSuccess = true;
        [SerializeField] private bool suppressRepeats;
        [SerializeField, Min(1)] private int missesBeforeReset = 3;
        [SerializeField, Range(1, 4)] private int downscaleFactor = 1;

        [Header("Webcam")]
        [SerializeField] private bool useWebcam;
        [SerializeField] private string webcamDeviceName = "";
        [SerializeField, Min(16)] private int webcamWidth = 640;
        [SerializeField, Min(16)] private int webcamHeight = 480;
        [SerializeField, Min(1)] private int webcamFps = 30;
        [SerializeField] private RawImage webcamPreview;

        private readonly HashSet<byte[]> _rentedFrameBuffers = new HashSet<byte[]>();

        private QRCodeScanner _scanner;
        private WebCamTexture _webcam;
        private Color32[] _webcamPixels;
        private Func<Gray8Image> _webcamProvider;
        private int _framesSinceStats;
        private int _scansSinceStats;
        private float _statsElapsed;

        private void Start()
        {
            var decoder = new ZXingCppQRCodeDecoder();
            _scanner = new QRCodeScanner(decoder, new QRCodeScannerOptions
            {
                ScanInterval = TimeSpan.FromSeconds(scanIntervalSeconds),
                StopOnSuccess = stopOnSuccess,
                SuppressRepeats = suppressRepeats,
                MissesBeforeReset = missesBeforeReset,
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

            if (useWebcam)
                StartWebcam();
            else if (sourceTexture != null)
                SubmitTexture(sourceTexture);
        }

        private void Update()
        {
            // WebCamTexture reports 16x16 until the first real frame arrives.
            if (_webcam != null && _webcam.didUpdateThisFrame && _webcam.width > 16)
                _scanner.TrySubmitFrame(_webcamProvider);

            if (statsLabel == null)
                return;

            _framesSinceStats++;
            _statsElapsed += Time.unscaledDeltaTime;
            if (_statsElapsed < 0.5f)
                return;

            statsLabel.text = $"FPS {_framesSinceStats / _statsElapsed:F0}   scans/s {_scansSinceStats / _statsElapsed:F1}";
            _framesSinceStats = 0;
            _scansSinceStats = 0;
            _statsElapsed = 0f;
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

        private void StartWebcam()
        {
            _webcam = string.IsNullOrEmpty(webcamDeviceName)
                ? new WebCamTexture(webcamWidth, webcamHeight, webcamFps)
                : new WebCamTexture(webcamDeviceName, webcamWidth, webcamHeight, webcamFps);
            _webcam.Play();
            if (webcamPreview != null)
                webcamPreview.texture = _webcam;
            _webcamProvider = RentWebcamFrame;
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
                // Texture rows start at the displayed bottom; decode results are top-left-origin.
                Gray8RowOrder.FlipVertically(frame);
                _rentedFrameBuffers.Add(buffer);
                return frame;
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(buffer);
                throw;
            }
        }

        private Gray8Image RentWebcamFrame()
        {
            int width = _webcam.width;
            int height = _webcam.height;
            int pixelCount = width * height;
            if (_webcamPixels == null || _webcamPixels.Length != pixelCount)
                _webcamPixels = new Color32[pixelCount];
            _webcam.GetPixels32(_webcamPixels);

            byte[] buffer = ArrayPool<byte>.Shared.Rent(pixelCount);
            try
            {
                // Integer Rec. 601 luma; the weights sum to 256 so the shift keeps the result in byte range.
                for (int i = 0; i < pixelCount; i++)
                {
                    Color32 c = _webcamPixels[i];
                    buffer[i] = (byte)((c.r * 77 + c.g * 151 + c.b * 28) >> 8);
                }
                var frame = new Gray8Image(buffer, width, height);
                // WebCamTexture rows start at the displayed bottom like any Unity texture.
                Gray8RowOrder.FlipVertically(frame);
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
            if (!completion.Discarded)
                _scansSinceStats++;

            // SubmitRawGray8 hands over caller-owned buffers, and a handler that resubmits can
            // rent another one before this fires, so the ledger decides what goes back.
            if (completion.Frame.Buffer == null || !_rentedFrameBuffers.Remove(completion.Frame.Buffer))
                return;

            ArrayPool<byte>.Shared.Return(completion.Frame.Buffer);
        }

        private void OnDetected(QRCodeResult result)
        {
            Debug.Log($"QR decoded: {result.Text}");
            if (resultLabel != null)
                resultLabel.text = $"{result.Text}{Environment.NewLine}TL {result.TopLeft}  TR {result.TopRight}{Environment.NewLine}BL {result.BottomLeft}  BR {result.BottomRight}{Environment.NewLine}orientation {result.Orientation}  mirrored {result.IsMirrored}";
        }

        private static void OnDecodeFailed(Exception exception)
        {
            Debug.LogException(exception);
        }

        private void OnDestroy()
        {
            if (_webcam != null)
            {
                _webcam.Stop();
                Destroy(_webcam);
                _webcam = null;
            }

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
