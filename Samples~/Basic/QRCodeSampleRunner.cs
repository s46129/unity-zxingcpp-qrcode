using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using ZXingCpp.QRCode.Unity;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
#if QRCODE_SAMPLE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        [SerializeField, Min(16)] private int webcamWidth = 1280;
        [SerializeField, Min(16)] private int webcamHeight = 720;
        [SerializeField, Min(1)] private int webcamFps = 30;
        [SerializeField] private RawImage webcamPreview;
        [SerializeField, Min(0f)] private float tapFocusHoldSeconds = 2f;
        [Tooltip("Convert, flip and downscale webcam frames on the GPU and read them back asynchronously; falls back to GetPixels32 where AsyncGPUReadback is unsupported.")]
        [SerializeField] private bool useGpuReadback = true;

        private readonly HashSet<byte[]> _rentedFrameBuffers = new HashSet<byte[]>();

        private QRCodeScanner _scanner;
        private WebCamTexture _webcam;
        private Color32[] _webcamPixels;
        private Func<Gray8Image> _webcamProvider;
        private Gray8TextureReadback _readback;
        private Func<Gray8Image> _readbackProvider;
        private int _cornerScale = 1;
        private bool _readbackFailureLogged;
        private bool _tapFocusSupported;
        private float _tapFocusResetTime = float.PositiveInfinity;
        private string _focusStatus = "";
        private Vector2 _previewBounds;
        private int _framesSinceStats;
        private int _scansSinceStats;
        private float _statsElapsed;

        private void Start()
        {
            // On the GPU path the readback already downscales, so the decoder must not do it again;
            // the corners it reports are then in the downscaled frame and get scaled back for display.
            bool gpuReadback = useWebcam && useGpuReadback && Gray8TextureReadback.IsSupported;
            if (gpuReadback)
            {
                _readback = new Gray8TextureReadback(downscaleFactor);
                _readback.FrameReady += OnReadbackFrameReady;
                _readback.ReadbackFailed += OnReadbackFailed;
                _readbackProvider = RentReadbackFrame;
                _cornerScale = downscaleFactor;
            }

            var decoder = new ZXingCppQRCodeDecoder();
            _scanner = new QRCodeScanner(decoder, new QRCodeScannerOptions
            {
                ScanInterval = TimeSpan.FromSeconds(scanIntervalSeconds),
                StopOnSuccess = stopOnSuccess,
                SuppressRepeats = suppressRepeats,
                MissesBeforeReset = missesBeforeReset,
                DecodeOptions = new QRCodeDecodeOptions
                {
                    DownscaleFactor = gpuReadback ? 1 : downscaleFactor,
                    TryRotate = true
                }
            });
            _scanner.Detected += OnDetected;
            _scanner.DecodeFailed += OnDecodeFailed;
            _scanner.ScanCompleted += OnScanCompleted;
            _scanner.Start();

            if (useWebcam)
                StartCoroutine(StartWebcamWhenAuthorized());
            else if (sourceTexture != null)
                SubmitTexture(sourceTexture);
        }

        private void Update()
        {
            // WebCamTexture reports 16x16 until the first real frame arrives.
            if (_webcam != null && _webcam.didUpdateThisFrame && _webcam.width > 16)
            {
                if (_readback != null)
                {
                    // The readback lands a frame or two later, so ask first: a frame the scanner
                    // would drop anyway is not worth a GPU pass. TrySubmitFrame still decides.
                    if (_scanner.CanAcceptFrame())
                        _readback.TryRequest(_webcam);
                }
                else
                {
                    _scanner.TrySubmitFrame(_webcamProvider);
                }
                AlignWebcamPreview();
            }

            if (_webcam != null)
                UpdateTapToFocus();

            if (statsLabel == null)
                return;

            _framesSinceStats++;
            _statsElapsed += Time.unscaledDeltaTime;
            if (_statsElapsed < 0.5f)
                return;

            string path = _webcam == null ? "" : _readback != null ? "   gpu" : "   cpu";
            statsLabel.text = $"FPS {_framesSinceStats / _statsElapsed:F0}   scans/s {_scansSinceStats / _statsElapsed:F1}{path}{_focusStatus}";
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

        private IEnumerator StartWebcamWhenAuthorized()
        {
            // A WebCamTexture created before the permission dialog is answered stays black for the rest of the run.
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                Permission.RequestUserPermission(Permission.Camera);
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                yield return new WaitForSecondsRealtime(0.5f);
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogWarning("QRCodeSampleRunner: camera permission was denied, so the webcam stays off.");
                yield break;
            }
#endif
            StartWebcam();
        }

        private void StartWebcam()
        {
            _webcam = string.IsNullOrEmpty(webcamDeviceName)
                ? new WebCamTexture(webcamWidth, webcamHeight, webcamFps)
                : new WebCamTexture(webcamDeviceName, webcamWidth, webcamHeight, webcamFps);
            _webcam.Play();
            if (webcamPreview != null)
            {
                webcamPreview.texture = _webcam;
                // The scene size is the box the preview must fit into; the actual size follows the camera's aspect.
                _previewBounds = webcamPreview.rectTransform.sizeDelta;
            }
            _webcamProvider = RentWebcamFrame;

            foreach (WebCamDevice device in WebCamTexture.devices)
                if (device.name == _webcam.deviceName)
                    _tapFocusSupported = device.isAutoFocusPointSupported;
            _focusStatus = _tapFocusSupported ? "" : "   tap focus unsupported";
        }

        // A tap on the preview focuses the camera there once; after the hold time the camera
        // goes back to continuous auto focus. Desktop cameras report no support and ignore taps.
        private void UpdateTapToFocus()
        {
            if (Time.unscaledTime >= _tapFocusResetTime)
            {
                _webcam.autoFocusPoint = null;
                _tapFocusResetTime = float.PositiveInfinity;
                _focusStatus = "";
            }

            if (webcamPreview == null || !TryGetTap(out Vector2 screenPosition))
                return;

            Canvas canvas = webcamPreview.canvas;
            Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransform rectTransform = webcamPreview.rectTransform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, canvasCamera) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, canvasCamera, out Vector2 local))
                return;

            // The local point is inside the rotated RectTransform, so it already lives in texture space; only the mirror flip remains.
            Rect rect = rectTransform.rect;
            float u = Mathf.Clamp01((local.x - rect.x) / rect.width);
            float v = Mathf.Clamp01((local.y - rect.y) / rect.height);
            if (_webcam.videoVerticallyMirrored)
                v = 1f - v;

            // The label reports every tap so an unsupported camera is distinguishable from a missed tap.
            if (!_tapFocusSupported)
            {
                _focusStatus = $"   tap ({u:F2}, {v:F2}) focus unsupported";
                return;
            }

            _webcam.autoFocusPoint = new Vector2(u, v);
            _tapFocusResetTime = Time.unscaledTime + tapFocusHoldSeconds;
            _focusStatus = $"   focus ({u:F2}, {v:F2})";
            Debug.Log($"QRCodeSampleRunner: focus point ({u:F2}, {v:F2})");
        }

        private static bool TryGetTap(out Vector2 screenPosition)
        {
#if QRCODE_SAMPLE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                screenPosition = pointer.position.ReadValue();
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
#endif
            screenPosition = default;
            return false;
        }

        // Phone cameras deliver sensor-oriented frames; the preview is turned to match while the
        // decoder keeps reading the raw frame, so result corners stay in the frame's own coordinates.
        private void AlignWebcamPreview()
        {
            if (webcamPreview == null)
                return;

            int angle = _webcam.videoRotationAngle;
            RectTransform rectTransform = webcamPreview.rectTransform;
            rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);
            webcamPreview.uvRect = _webcam.videoVerticallyMirrored ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);

            // Letterbox the camera's aspect into the scene box; a quarter turn swaps the box's sides.
            bool quarterTurn = angle % 180 != 0;
            float boxWidth = quarterTurn ? _previewBounds.y : _previewBounds.x;
            float boxHeight = quarterTurn ? _previewBounds.x : _previewBounds.y;
            float aspect = (float)_webcam.width / _webcam.height;
            float width = boxWidth;
            float height = width / aspect;
            if (height > boxHeight)
            {
                height = boxHeight;
                width = height * aspect;
            }
            rectTransform.sizeDelta = new Vector2(width, height);
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

        private void OnReadbackFrameReady(Gray8TextureReadback readback)
        {
            // Rejected here means the scanner stopped or got busy meanwhile; the provider then never runs.
            _scanner.TrySubmitFrame(_readbackProvider);
        }

        private void OnReadbackFailed(Gray8TextureReadback readback)
        {
            if (_readbackFailureLogged)
                return;

            _readbackFailureLogged = true;
            Debug.LogWarning("QRCodeSampleRunner: the GPU reported a readback error; frames from this readback are skipped.");
        }

        private Gray8Image RentReadbackFrame()
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(_readback.FrameByteLength);
            try
            {
                // One memcpy of the R8 frame; luma, flip and downscale already happened on the GPU.
                Gray8Image frame = _readback.CopyFrame(buffer);
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
                resultLabel.text = $"{result.Text}{Environment.NewLine}TL {Scale(result.TopLeft)}  TR {Scale(result.TopRight)}{Environment.NewLine}BL {Scale(result.BottomLeft)}  BR {Scale(result.BottomRight)}{Environment.NewLine}orientation {result.Orientation}  mirrored {result.IsMirrored}";
        }

        private QRCodePoint Scale(QRCodePoint point) =>
            _cornerScale == 1 ? point : new QRCodePoint(point.X * _cornerScale, point.Y * _cornerScale);

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

            if (_readback != null)
            {
                _readback.FrameReady -= OnReadbackFrameReady;
                _readback.ReadbackFailed -= OnReadbackFailed;
                _readback.Dispose();
                _readback = null;
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
