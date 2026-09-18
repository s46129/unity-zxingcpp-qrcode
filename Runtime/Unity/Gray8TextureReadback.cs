using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ZXingCpp.QRCode.Unity
{
    /// <summary>Converts any <see cref="Texture"/> to a top-left-origin Gray8 frame on the GPU and reads it back asynchronously.</summary>
    public sealed class Gray8TextureReadback : IDisposable
    {
        private const string ShaderResourcePath = "ZXingCpp.QRCode/Gray8Readback";
        private static readonly int DimensionsId = Shader.PropertyToID("_Dimensions");
        private static readonly int FactorId = Shader.PropertyToID("_Factor");
        private static readonly int FlipVerticallyId = Shader.PropertyToID("_FlipVertically");

        private readonly Action<AsyncGPUReadbackRequest> _onReadback;
        private Material _material;
        private RenderTexture _target;
        private NativeArray<byte> _pixels;
        private bool _pending;
        private bool _hasFrame;
        private bool _disposed;
        private int _frameWidth;
        private int _frameHeight;
        private int _frameRowStride;

        public Gray8TextureReadback(int downscaleFactor = 1)
        {
            if (downscaleFactor < 1 || downscaleFactor > 8)
                throw new ArgumentOutOfRangeException(nameof(downscaleFactor), "Downscale factor must be between 1 and 8.");

            DownscaleFactor = downscaleFactor;
            _onReadback = OnReadback;
        }

        /// <summary>Raised on the main thread once a requested frame can be copied with <see cref="CopyFrame"/>.</summary>
        public event Action<Gray8TextureReadback> FrameReady;

        /// <summary>Raised on the main thread when the GPU reported an error for a requested frame.</summary>
        public event Action<Gray8TextureReadback> ReadbackFailed;

        /// <summary>True when this device can render to R8 and read it back asynchronously; otherwise use a CPU path.</summary>
        public static bool IsSupported =>
            SystemInfo.supportsAsyncGPUReadback && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8);

        public int DownscaleFactor { get; }

        public bool IsPending => _pending;

        /// <summary>True between <see cref="FrameReady"/> and the next <see cref="TryRequest"/>.</summary>
        public bool HasFrame => _hasFrame;

        public int Width => _frameWidth;

        public int Height => _frameHeight;

        public int RowStride => _frameRowStride;

        /// <summary>The smallest buffer <see cref="CopyFrame"/> accepts for the completed frame.</summary>
        public int FrameByteLength => _frameRowStride * _frameHeight;

        /// <summary>Starts a GPU pass over <paramref name="source"/>; returns false while a previous request is still in flight.</summary>
        /// <param name="flipVertically">True for Unity's bottom-up textures; false when the source rows already start at the top.</param>
        public bool TryRequest(Texture source, bool flipVertically = true)
        {
            ThrowIfDisposed();
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!IsSupported)
                throw new NotSupportedException("This device cannot render to R8 or read it back asynchronously; check IsSupported and use a CPU path.");
            if (_pending)
                return false;

            int width = source.width;
            int height = source.height;
            if (width <= 0 || height <= 0)
                return false;

            int outputWidth = (width + DownscaleFactor - 1) / DownscaleFactor;
            int outputHeight = (height + DownscaleFactor - 1) / DownscaleFactor;
            EnsureResources(outputWidth, outputHeight);

            _material.SetVector(DimensionsId, new Vector4(width, height, outputWidth, outputHeight));
            _material.SetFloat(FactorId, DownscaleFactor);
            _material.SetFloat(FlipVerticallyId, flipVertically ? 1f : 0f);
            Graphics.Blit(source, _target, _material);

            _hasFrame = false;
            _pending = true;
            try
            {
                AsyncGPUReadback.RequestIntoNativeArray(ref _pixels, _target, 0, TextureFormat.R8, _onReadback);
            }
            catch
            {
                _pending = false;
                throw;
            }

            return true;
        }

        /// <summary>Copies the completed frame into <paramref name="destination"/> and wraps it as a top-left-origin image.</summary>
        public Gray8Image CopyFrame(byte[] destination)
        {
            ThrowIfDisposed();
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!_hasFrame)
                throw new InvalidOperationException("No completed frame is available; wait for FrameReady after TryRequest.");

            int length = FrameByteLength;
            if (destination.Length < length)
                throw new ArgumentException($"The destination holds {destination.Length} bytes but the frame needs {length}.", nameof(destination));

            NativeArray<byte>.Copy(_pixels, 0, destination, 0, length);
            return new Gray8Image(destination, _frameWidth, _frameHeight, _frameRowStride);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _hasFrame = false;
            // The NativeArray is locked while a request is in flight, so it cannot be freed before the GPU is done.
            if (_pending)
                AsyncGPUReadback.WaitAllRequests();
            _pending = false;

            if (_pixels.IsCreated)
                _pixels.Dispose();
            if (_target != null)
            {
                _target.Release();
                DestroyObject(_target);
                _target = null;
            }
            if (_material != null)
            {
                DestroyObject(_material);
                _material = null;
            }
        }

        private void OnReadback(AsyncGPUReadbackRequest request)
        {
            _pending = false;
            if (_disposed)
                return;

            int rowStride = request.height > 0 ? request.layerDataSize / request.height : 0;
            if (request.hasError || rowStride < request.width || request.layerDataSize > _pixels.Length)
            {
                ReadbackFailed?.Invoke(this);
                return;
            }

            _frameWidth = request.width;
            _frameHeight = request.height;
            _frameRowStride = rowStride;
            _hasFrame = true;
            FrameReady?.Invoke(this);
        }

        private void EnsureResources(int outputWidth, int outputHeight)
        {
            if (_material == null)
            {
                Shader shader = Resources.Load<Shader>(ShaderResourcePath);
                if (shader == null)
                    throw new InvalidOperationException($"The shader resource '{ShaderResourcePath}' is missing from the package.");
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (_target == null || _target.width != outputWidth || _target.height != outputHeight)
            {
                if (_target != null)
                {
                    _target.Release();
                    DestroyObject(_target);
                }

                _target = new RenderTexture(outputWidth, outputHeight, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
                {
                    filterMode = FilterMode.Point,
                    useMipMap = false,
                    autoGenerateMips = false,
                    antiAliasing = 1,
                    hideFlags = HideFlags.HideAndDontSave
                };
                _target.Create();
            }

            int length = outputWidth * outputHeight;
            if (!_pixels.IsCreated || _pixels.Length != length)
            {
                if (_pixels.IsCreated)
                    _pixels.Dispose();
                _pixels = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Gray8TextureReadback));
        }
    }
}
