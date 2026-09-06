using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ZXingCpp.QRCode
{
    /// <summary>Rate-limits Gray8 frames and decodes accepted frames on a background worker.</summary>
    public sealed class QRCodeScanner : IDisposable
    {
        private readonly object _gate = new object();
        private readonly IQRCodeDecoder _decoder;
        private readonly QRCodeScannerOptions _options;
        private readonly SynchronizationContext _callbackContext;
        private bool _running;
        private bool _busy;
        private bool _disposed;
        private double _nextScanTime;
        private int _generation;

        public QRCodeScanner(IQRCodeDecoder decoder, QRCodeScannerOptions options = null)
            : this(decoder, options, SynchronizationContext.Current)
        {
        }

        internal QRCodeScanner(
            IQRCodeDecoder decoder,
            QRCodeScannerOptions options,
            SynchronizationContext callbackContext)
        {
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _options = (options ?? new QRCodeScannerOptions()).Snapshot();
            _callbackContext = callbackContext;
            _nextScanTime = double.NegativeInfinity;
        }

        public event Action<QRCodeResult> Detected;

        public event Action<Exception> DecodeFailed;

        public event Action<QRCodeScanCompletion> ScanCompleted;

        public bool IsRunning
        {
            get
            {
                lock (_gate)
                    return _running;
            }
        }

        public bool IsBusy
        {
            get
            {
                lock (_gate)
                    return _busy;
            }
        }

        public void Start()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_running)
                    return;

                _running = true;
                _nextScanTime = double.NegativeInfinity;
                _generation++;
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                if (!_running)
                    return;

                _running = false;
                _generation++;
            }
        }

        public bool TrySubmitFrame(Gray8Image frame) => TrySubmitFrame(frame, CurrentTimeSeconds());

        public bool TrySubmitFrame(Gray8Image frame, double timestampSeconds)
        {
            ValidateTimestamp(timestampSeconds);

            if (!TryClaimScanSlot(timestampSeconds, out int generation, out _))
                return false;

            StartDecode(frame, generation);
            return true;
        }

        /// <summary>Submits a frame that <paramref name="frameProvider"/> builds only once the scan slot is claimed.</summary>
        public bool TrySubmitFrame(Func<Gray8Image> frameProvider) =>
            TrySubmitFrame(frameProvider, CurrentTimeSeconds());

        /// <summary>Submits a frame that <paramref name="frameProvider"/> builds only once the scan slot is claimed.</summary>
        public bool TrySubmitFrame(Func<Gray8Image> frameProvider, double timestampSeconds)
        {
            if (frameProvider == null)
                throw new ArgumentNullException(nameof(frameProvider));
            ValidateTimestamp(timestampSeconds);

            if (!TryClaimScanSlot(timestampSeconds, out int generation, out double previousScanTime))
                return false;

            Gray8Image frame;
            try
            {
                frame = frameProvider();
            }
            catch
            {
                ReleaseScanSlot(generation, previousScanTime);
                throw;
            }

            StartDecode(frame, generation);
            return true;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _running = false;
                _generation++;
            }
        }

        private bool TryClaimScanSlot(double timestampSeconds, out int generation, out double previousScanTime)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                generation = _generation;
                previousScanTime = _nextScanTime;
                if (!_running || _busy || timestampSeconds < _nextScanTime)
                    return false;

                _busy = true;
                _nextScanTime = timestampSeconds + _options.ScanInterval.TotalSeconds;
                return true;
            }
        }

        private void ReleaseScanSlot(int generation, double previousScanTime)
        {
            lock (_gate)
            {
                _busy = false;
                // A frame that never materialized must not push back the next scan; a newer
                // generation already rewrote _nextScanTime, so leave that one alone.
                if (generation == _generation)
                    _nextScanTime = previousScanTime;
            }
        }

        private void StartDecode(Gray8Image frame, int generation)
        {
            Task.Run(() => DecodeFrame(frame)).ContinueWith(
                task => Dispatch(() => CompleteFrame(frame, generation, task.Result)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static void ValidateTimestamp(double timestampSeconds)
        {
            if (double.IsNaN(timestampSeconds) || double.IsInfinity(timestampSeconds))
                throw new ArgumentOutOfRangeException(nameof(timestampSeconds), "Timestamp must be finite.");
        }

        private DecodeOutcome DecodeFrame(Gray8Image frame)
        {
            try
            {
                bool found = _decoder.TryDecode(frame, _options.DecodeOptions, out QRCodeResult result);
                return new DecodeOutcome(found ? result : null, null);
            }
            catch (Exception exception)
            {
                return new DecodeOutcome(null, exception);
            }
        }

        private void CompleteFrame(Gray8Image frame, int generation, DecodeOutcome outcome)
        {
            bool active;
            lock (_gate)
            {
                _busy = false;
                active = !_disposed && _running && generation == _generation;
                if (active && outcome.Result != null && _options.StopOnSuccess)
                    _running = false;
            }

            var completion = new QRCodeScanCompletion(frame, outcome.Result, outcome.Error, !active);
            try
            {
                if (!active)
                    return;
                if (outcome.Error != null)
                    DecodeFailed?.Invoke(outcome.Error);
                else if (outcome.Result != null)
                    Detected?.Invoke(outcome.Result);
            }
            finally
            {
                ScanCompleted?.Invoke(completion);
            }
        }

        private void Dispatch(Action callback)
        {
            if (_callbackContext == null)
                callback();
            else
                _callbackContext.Post(_ => callback(), null);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(QRCodeScanner));
        }

        private static double CurrentTimeSeconds() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        private readonly struct DecodeOutcome
        {
            internal DecodeOutcome(QRCodeResult result, Exception error)
            {
                Result = result;
                Error = error;
            }

            internal QRCodeResult Result { get; }

            internal Exception Error { get; }
        }
    }
}
