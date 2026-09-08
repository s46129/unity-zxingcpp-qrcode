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
        private string _lastDetectedText;
        private int _missStreak;

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
                _lastDetectedText = null;
                _missStreak = 0;
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

        /// <summary>Submits a frame the caller already holds; returns false when the scanner drops it.</summary>
        public bool TrySubmitFrame(Gray8Image frame) => TrySubmitFrame(frame, CurrentTimeSeconds());

        /// <summary>Submits a frame the caller already holds; returns false when the scanner drops it.</summary>
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
                // A frame that never materialized must not push back the next scan.
                if (generation == _generation)
                    _nextScanTime = previousScanTime;
            }
        }

        private void StartDecode(Gray8Image frame, int generation)
        {
            var accepted = new AcceptedFrame(frame, generation);
            try
            {
                // Dispatching from inside the worker is what makes a scheduling failure mean "no decode is running".
                Task.Run(() => CompleteOnCallbackContext(accepted, DecodeFrame(frame)));
            }
            catch (Exception exception)
            {
                CompleteOnCallbackContext(accepted, new DecodeOutcome(null, exception));
            }
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

        private void CompleteFrame(AcceptedFrame accepted, DecodeOutcome outcome)
        {
            // A callback context may queue the completion and still throw, so the frame decides who completes it.
            if (!accepted.TryClaimCompletion())
                return;

            bool active;
            bool raiseDetected;
            lock (_gate)
            {
                _busy = false;
                active = !_disposed && _running && accepted.Generation == _generation;
                if (active && outcome.Result != null && _options.StopOnSuccess)
                    _running = false;
                raiseDetected = active && outcome.Error == null && outcome.Result != null;
                if (active && outcome.Error == null && _options.SuppressRepeats)
                    raiseDetected = TrackRepeat(outcome.Result);
            }

            var completion = new QRCodeScanCompletion(accepted.Frame, outcome.Result, outcome.Error, !active);
            try
            {
                if (!active)
                    return;
                if (outcome.Error != null)
                    DecodeFailed?.Invoke(outcome.Error);
                else if (raiseDetected)
                    Detected?.Invoke(outcome.Result);
            }
            finally
            {
                ScanCompleted?.Invoke(completion);
            }
        }

        // Runs under _gate: a refused callback context completes frames on the worker thread.
        private bool TrackRepeat(QRCodeResult result)
        {
            if (result == null)
            {
                if (++_missStreak >= _options.MissesBeforeReset)
                {
                    _lastDetectedText = null;
                    _missStreak = 0;
                }
                return false;
            }

            _missStreak = 0;
            if (result.Text == _lastDetectedText)
                return false;

            _lastDetectedText = result.Text;
            return true;
        }

        private void CompleteOnCallbackContext(AcceptedFrame accepted, DecodeOutcome outcome)
        {
            if (_callbackContext == null)
            {
                CompleteFrame(accepted, outcome);
                return;
            }

            try
            {
                _callbackContext.Post(_ => CompleteFrame(accepted, outcome), null);
            }
            catch (Exception exception)
            {
                // A completion the context refuses would strand _busy and the caller's borrowed buffer.
                CompleteFrame(accepted, new DecodeOutcome(outcome.Result, outcome.Error ?? exception));
            }
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

        private sealed class AcceptedFrame
        {
            private int _completed;

            internal AcceptedFrame(Gray8Image frame, int generation)
            {
                Frame = frame;
                Generation = generation;
            }

            internal Gray8Image Frame { get; }

            internal int Generation { get; }

            internal bool TryClaimCompletion() => Interlocked.Exchange(ref _completed, 1) == 0;
        }
    }
}
