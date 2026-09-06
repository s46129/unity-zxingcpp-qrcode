using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace ZXingCpp.QRCode.Tests
{
    public sealed class QRCodeScannerTests
    {
        private static readonly Gray8Image Frame = new Gray8Image(new byte[16], 4, 4);

        [Test]
        public void TrySubmitFrame_Busy_DropsSecondFrame()
        {
            var decoder = new BlockingDecoder();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false))
            using (var completed = new ManualResetEventSlim())
            {
                scanner.ScanCompleted += _ => completed.Set();
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(Frame, 0d), Is.True);
                Assert.That(decoder.Entered.Wait(3000), Is.True);
                Assert.That(scanner.TrySubmitFrame(Frame, 0d), Is.False);

                decoder.Release.Set();
                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(scanner.IsBusy, Is.False);
            }
        }

        [Test]
        public void TrySubmitFrame_IntervalNotElapsed_DropsFrame()
        {
            var decoder = new SequenceDecoder(null, null);
            using (var scanner = CreateScanner(decoder, TimeSpan.FromSeconds(0.5), false))
            using (var completed = new CountdownEvent(2))
            {
                scanner.ScanCompleted += _ => completed.Signal();
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(Frame, 10d), Is.True);
                Assert.That(SpinWait.SpinUntil(() => !scanner.IsBusy, 3000), Is.True);
                Assert.That(scanner.TrySubmitFrame(Frame, 10.49d), Is.False);
                Assert.That(scanner.TrySubmitFrame(Frame, 10.5d), Is.True);
                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(decoder.CallCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void SuccessfulResult_StopOnSuccess_StopsAndRaisesDetected()
        {
            QRCodeResult expected = CreateResult("hello");
            var decoder = new SequenceDecoder(expected);
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, true))
            using (var detected = new ManualResetEventSlim())
            {
                QRCodeResult actual = null;
                scanner.Detected += result =>
                {
                    actual = result;
                    detected.Set();
                };
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(Frame, 0d), Is.True);
                Assert.That(detected.Wait(3000), Is.True);
                Assert.That(actual, Is.SameAs(expected));
                Assert.That(scanner.IsRunning, Is.False);
            }
        }

        [Test]
        public void Stop_InFlightResult_CompletesAsDiscarded()
        {
            var decoder = new BlockingDecoder(CreateResult("late"));
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false))
            using (var completed = new ManualResetEventSlim())
            {
                QRCodeScanCompletion completion = null;
                bool detected = false;
                scanner.ScanCompleted += value =>
                {
                    completion = value;
                    completed.Set();
                };
                scanner.Detected += _ => detected = true;
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(Frame, 0d), Is.True);
                Assert.That(decoder.Entered.Wait(3000), Is.True);
                scanner.Stop();
                decoder.Release.Set();

                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(completion.Discarded, Is.True);
                Assert.That(detected, Is.False);
            }
        }

        [Test]
        public void DecoderThrows_RaisesFailureAndReleasesFrame()
        {
            var decoder = new ThrowingDecoder();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false))
            using (var completed = new ManualResetEventSlim())
            {
                Exception failure = null;
                QRCodeScanCompletion completion = null;
                scanner.DecodeFailed += exception => failure = exception;
                scanner.ScanCompleted += value =>
                {
                    completion = value;
                    completed.Set();
                };
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(Frame, 0d), Is.True);
                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(failure, Is.TypeOf<InvalidOperationException>());
                Assert.That(completion.Error, Is.SameAs(failure));
                Assert.That(completion.Discarded, Is.False);
                Assert.That(scanner.IsBusy, Is.False);
            }
        }

        [Test]
        public void TrySubmitFrame_ProviderStopped_DoesNotInvokeProvider()
        {
            var decoder = new SequenceDecoder();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false))
            {
                int calls = 0;
                scanner.Start();
                scanner.Stop();

                Assert.That(scanner.TrySubmitFrame(() => CountingProvider(ref calls), 0d), Is.False);
                Assert.That(calls, Is.Zero);
                Assert.That(decoder.CallCount, Is.Zero);
            }
        }

        [Test]
        public void TrySubmitFrame_ProviderBusy_DoesNotInvokeProvider()
        {
            var decoder = new BlockingDecoder();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false))
            using (var completed = new ManualResetEventSlim())
            {
                int calls = 0;
                scanner.ScanCompleted += _ => completed.Set();
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(() => CountingProvider(ref calls), 0d), Is.True);
                Assert.That(decoder.Entered.Wait(3000), Is.True);
                Assert.That(scanner.TrySubmitFrame(() => CountingProvider(ref calls), 0d), Is.False);
                Assert.That(calls, Is.EqualTo(1));

                decoder.Release.Set();
                Assert.That(completed.Wait(3000), Is.True);
            }
        }

        [Test]
        public void TrySubmitFrame_ProviderIntervalNotElapsed_DoesNotInvokeProvider()
        {
            var decoder = new SequenceDecoder(null, null);
            using (var scanner = CreateScanner(decoder, TimeSpan.FromSeconds(0.5), false))
            {
                int calls = 0;
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(() => CountingProvider(ref calls), 10d), Is.True);
                Assert.That(SpinWait.SpinUntil(() => !scanner.IsBusy, 3000), Is.True);
                Assert.That(scanner.TrySubmitFrame(() => CountingProvider(ref calls), 10.49d), Is.False);
                Assert.That(calls, Is.EqualTo(1));
            }
        }

        [Test]
        public void TrySubmitFrame_ProviderAccepted_DecodesTheProvidedFrame()
        {
            var decoder = new SequenceDecoder();
            var provided = new Gray8Image(new byte[16], 4, 4);
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false))
            using (var completed = new ManualResetEventSlim())
            {
                QRCodeScanCompletion completion = null;
                scanner.ScanCompleted += value =>
                {
                    completion = value;
                    completed.Set();
                };
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(() => provided, 0d), Is.True);
                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(decoder.CallCount, Is.EqualTo(1));
                Assert.That(decoder.LastBuffer, Is.SameAs(provided.Buffer));
                Assert.That(completion.Frame.Buffer, Is.SameAs(provided.Buffer));
            }
        }

        [Test]
        public void TrySubmitFrame_ProviderThrows_ReleasesSlotWithoutConsumingInterval()
        {
            var decoder = new SequenceDecoder();
            using (var scanner = CreateScanner(decoder, TimeSpan.FromSeconds(0.5), false))
            using (var completed = new ManualResetEventSlim())
            {
                scanner.ScanCompleted += _ => completed.Set();
                scanner.Start();

                Assert.That(
                    () => scanner.TrySubmitFrame(() => throw new InvalidOperationException("provider"), 10d),
                    Throws.TypeOf<InvalidOperationException>());
                Assert.That(scanner.IsBusy, Is.False);

                Assert.That(scanner.TrySubmitFrame(Frame, 10d), Is.True);
                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(decoder.CallCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void TrySubmitFrame_NullProvider_Throws()
        {
            using (var scanner = CreateScanner(new SequenceDecoder(), TimeSpan.Zero, false))
            {
                scanner.Start();
                Assert.That(
                    () => scanner.TrySubmitFrame((Func<Gray8Image>)null, 0d),
                    Throws.TypeOf<ArgumentNullException>());
            }
        }

        [Test]
        public void TrySubmitFrame_ProviderFrame_BufferStaysCheckedOutUntilScanCompleted()
        {
            var decoder = new BlockingDecoder();
            var pool = new TrackingPool();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false))
            using (var completed = new ManualResetEventSlim())
            {
                byte[] borrowed = null;
                int rejectedProviderCalls = 0;
                scanner.ScanCompleted += completion =>
                {
                    pool.Return(completion.Frame.Buffer);
                    completed.Set();
                };
                scanner.Start();

                Assert.That(
                    scanner.TrySubmitFrame(
                        () =>
                        {
                            borrowed = pool.Rent(16);
                            return new Gray8Image(borrowed, 4, 4);
                        },
                        0d),
                    Is.True);
                Assert.That(decoder.Entered.Wait(3000), Is.True);
                Assert.That(pool.IsCheckedOut(borrowed), Is.True);

                Assert.That(scanner.TrySubmitFrame(() => CountingProvider(ref rejectedProviderCalls), 0d), Is.False);
                Assert.That(rejectedProviderCalls, Is.Zero);
                Assert.That(pool.RentCount, Is.EqualTo(1));
                Assert.That(pool.ReturnCount, Is.Zero);

                decoder.Release.Set();
                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(pool.IsCheckedOut(borrowed), Is.False);
                Assert.That(pool.ReturnCount, Is.EqualTo(1));
            }
        }

        private static Gray8Image CountingProvider(ref int calls)
        {
            Interlocked.Increment(ref calls);
            return Frame;
        }

        private static QRCodeScanner CreateScanner(IQRCodeDecoder decoder, TimeSpan interval, bool stopOnSuccess) =>
            new QRCodeScanner(
                decoder,
                new QRCodeScannerOptions
                {
                    ScanInterval = interval,
                    StopOnSuccess = stopOnSuccess
                },
                null);

        private static QRCodeResult CreateResult(string text) =>
            new QRCodeResult(
                text,
                Array.Empty<byte>(),
                new QRCodePoint(0, 0),
                new QRCodePoint(1, 0),
                new QRCodePoint(1, 1),
                new QRCodePoint(0, 1),
                0,
                false,
                false,
                "]Q1");

        private sealed class SequenceDecoder : IQRCodeDecoder
        {
            private readonly ConcurrentQueue<QRCodeResult> _results;
            private int _callCount;

            internal SequenceDecoder(params QRCodeResult[] results)
            {
                _results = new ConcurrentQueue<QRCodeResult>(results);
            }

            internal int CallCount => _callCount;

            internal byte[] LastBuffer { get; private set; }

            public bool TryDecode(Gray8Image image, out QRCodeResult result) =>
                TryDecode(image, new QRCodeDecodeOptions(), out result);

            public bool TryDecode(Gray8Image image, QRCodeDecodeOptions options, out QRCodeResult result)
            {
                Interlocked.Increment(ref _callCount);
                LastBuffer = image.Buffer;
                _results.TryDequeue(out result);
                return result != null;
            }
        }

        private sealed class TrackingPool
        {
            private readonly HashSet<byte[]> _checkedOut = new HashSet<byte[]>();

            internal int RentCount { get; private set; }

            internal int ReturnCount { get; private set; }

            internal byte[] Rent(int minimumLength)
            {
                var buffer = new byte[minimumLength];
                _checkedOut.Add(buffer);
                RentCount++;
                return buffer;
            }

            internal void Return(byte[] buffer)
            {
                if (!_checkedOut.Remove(buffer))
                    throw new InvalidOperationException("The buffer was returned while it was not checked out.");

                ReturnCount++;
            }

            internal bool IsCheckedOut(byte[] buffer) => _checkedOut.Contains(buffer);
        }

        private sealed class BlockingDecoder : IQRCodeDecoder
        {
            private readonly QRCodeResult _result;

            internal BlockingDecoder(QRCodeResult result = null)
            {
                _result = result;
            }

            internal ManualResetEventSlim Entered { get; } = new ManualResetEventSlim();

            internal ManualResetEventSlim Release { get; } = new ManualResetEventSlim();

            public bool TryDecode(Gray8Image image, out QRCodeResult result) =>
                TryDecode(image, new QRCodeDecodeOptions(), out result);

            public bool TryDecode(Gray8Image image, QRCodeDecodeOptions options, out QRCodeResult result)
            {
                Entered.Set();
                Release.Wait(3000);
                result = _result;
                return result != null;
            }
        }

        private sealed class ThrowingDecoder : IQRCodeDecoder
        {
            public bool TryDecode(Gray8Image image, out QRCodeResult result) =>
                TryDecode(image, new QRCodeDecodeOptions(), out result);

            public bool TryDecode(Gray8Image image, QRCodeDecodeOptions options, out QRCodeResult result)
            {
                result = null;
                throw new InvalidOperationException("Expected test failure.");
            }
        }
    }
}
