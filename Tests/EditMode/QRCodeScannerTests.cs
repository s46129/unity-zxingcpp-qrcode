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

        [TestCase(false)]
        [TestCase(true)]
        public void TrySubmitFrame_Busy_DropsSecondFrame(bool callbackContextThrows)
        {
            var decoder = new BlockingDecoder();
            SynchronizationContext context = callbackContextThrows ? new ThrowingSynchronizationContext() : null;
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false, context))
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
            var ledger = new BufferLedger();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false))
            using (var completed = new ManualResetEventSlim())
            {
                byte[] borrowed = null;
                int rejectedProviderCalls = 0;
                scanner.ScanCompleted += completion =>
                {
                    ledger.CheckIn(completion.Frame.Buffer);
                    completed.Set();
                };
                scanner.Start();

                Assert.That(
                    scanner.TrySubmitFrame(
                        () =>
                        {
                            borrowed = ledger.CheckOut(16);
                            return new Gray8Image(borrowed, 4, 4);
                        },
                        0d),
                    Is.True);
                Assert.That(decoder.Entered.Wait(3000), Is.True);
                Assert.That(ledger.IsCheckedOut(borrowed), Is.True);

                Assert.That(scanner.TrySubmitFrame(() => CountingProvider(ref rejectedProviderCalls), 0d), Is.False);
                Assert.That(rejectedProviderCalls, Is.Zero);
                Assert.That(ledger.CheckOutCount, Is.EqualTo(1));
                Assert.That(ledger.CheckInCount, Is.Zero);

                decoder.Release.Set();
                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(ledger.IsCheckedOut(borrowed), Is.False);
                Assert.That(ledger.CheckInCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void TrySubmitFrame_ProviderThrowsAfterRestart_DoesNotRestoreTheStaleScanTime()
        {
            var decoder = new SequenceDecoder();
            using (var scanner = CreateScanner(decoder, TimeSpan.FromSeconds(0.5), false))
            {
                scanner.Start();
                Assert.That(scanner.TrySubmitFrame(Frame, 10d), Is.True);
                Assert.That(SpinWait.SpinUntil(() => !scanner.IsBusy, 3000), Is.True);

                Assert.That(
                    () => scanner.TrySubmitFrame(
                        () =>
                        {
                            scanner.Stop();
                            scanner.Start();
                            throw new InvalidOperationException("provider");
                        },
                        11d),
                    Throws.TypeOf<InvalidOperationException>());

                Assert.That(scanner.IsBusy, Is.False);
                Assert.That(scanner.TrySubmitFrame(Frame, 0d), Is.True);
            }
        }

        [Test]
        public void TrySubmitFrame_SustainedSubmission_BuildsOneFramePerAcceptedScan()
        {
            var decoder = new BlockingDecoder();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false))
            using (var completed = new ManualResetEventSlim())
            {
                int calls = 0;
                int accepted = 0;
                scanner.ScanCompleted += _ => completed.Set();
                scanner.Start();

                for (int i = 0; i < 60; i++)
                {
                    if (scanner.TrySubmitFrame(() => CountingProvider(ref calls), i / 60d))
                        accepted++;
                }

                Assert.That(accepted, Is.EqualTo(1));
                Assert.That(calls, Is.EqualTo(1));

                decoder.Release.Set();
                Assert.That(completed.Wait(3000), Is.True);
            }
        }

        [Test]
        public void TrySubmitFrame_ProviderWithoutTimestamp_BuildsOnlyTheAcceptedFrame()
        {
            var decoder = new BlockingDecoder();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false))
            using (var completed = new ManualResetEventSlim())
            {
                int calls = 0;
                scanner.ScanCompleted += _ => completed.Set();
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(() => CountingProvider(ref calls)), Is.True);
                Assert.That(decoder.Entered.Wait(3000), Is.True);
                Assert.That(scanner.TrySubmitFrame(() => CountingProvider(ref calls)), Is.False);
                Assert.That(calls, Is.EqualTo(1));

                decoder.Release.Set();
                Assert.That(completed.Wait(3000), Is.True);
            }
        }

        [Test]
        public void TrySubmitFrame_CallbackContextThrows_CompletesFrameAndReturnsTheBuffer()
        {
            var decoder = new SequenceDecoder();
            var ledger = new BufferLedger();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false, new ThrowingSynchronizationContext()))
            using (var completed = new ManualResetEventSlim())
            {
                Exception failure = null;
                QRCodeScanCompletion completion = null;
                byte[] borrowed = null;
                scanner.DecodeFailed += exception => failure = exception;
                scanner.ScanCompleted += value =>
                {
                    completion = value;
                    ledger.CheckIn(value.Frame.Buffer);
                    completed.Set();
                };
                scanner.Start();

                Assert.That(
                    scanner.TrySubmitFrame(
                        () =>
                        {
                            borrowed = ledger.CheckOut(16);
                            return new Gray8Image(borrowed, 4, 4);
                        },
                        0d),
                    Is.True);

                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(ledger.IsCheckedOut(borrowed), Is.False);
                Assert.That(failure, Is.TypeOf<InvalidOperationException>());
                Assert.That(completion.Error, Is.SameAs(failure));
                Assert.That(completion.Discarded, Is.False);
                Assert.That(scanner.IsBusy, Is.False);
            }
        }

        [Test]
        public void TrySubmitFrame_AfterCallbackContextThrew_AcceptsTheNextFrame()
        {
            var decoder = new SequenceDecoder(null, null);
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false, new ThrowingSynchronizationContext()))
            using (var completed = new CountdownEvent(2))
            {
                scanner.ScanCompleted += _ => completed.Signal();
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(Frame, 0d), Is.True);
                Assert.That(SpinWait.SpinUntil(() => !scanner.IsBusy, 3000), Is.True);
                Assert.That(scanner.TrySubmitFrame(Frame, 0d), Is.True);
                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(decoder.CallCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void TrySubmitFrame_CallbackContextAvailable_CompletesThroughItExactlyOnce()
        {
            var decoder = new SequenceDecoder();
            var context = new RecordingSynchronizationContext();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false, context))
            using (var completed = new ManualResetEventSlim())
            {
                int completions = 0;
                scanner.ScanCompleted += _ =>
                {
                    Interlocked.Increment(ref completions);
                    completed.Set();
                };
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(Frame, 0d), Is.True);
                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(context.PostCount, Is.EqualTo(1));
                Assert.That(Volatile.Read(ref completions), Is.EqualTo(1));
            }
        }

        [Test]
        public void TrySubmitFrame_CallbackContextThrows_KeepsTheDecodedResultOnTheCompletion()
        {
            QRCodeResult expected = CreateResult("dispatch");
            var decoder = new SequenceDecoder(expected);
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false, new ThrowingSynchronizationContext()))
            using (var completed = new ManualResetEventSlim())
            {
                QRCodeScanCompletion completion = null;
                bool detected = false;
                scanner.Detected += _ => detected = true;
                scanner.ScanCompleted += value =>
                {
                    completion = value;
                    completed.Set();
                };
                scanner.Start();

                Assert.That(scanner.TrySubmitFrame(Frame, 0d), Is.True);
                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(completion.Result, Is.SameAs(expected));
                Assert.That(completion.Error, Is.TypeOf<InvalidOperationException>());
                Assert.That(detected, Is.False);
            }
        }

        [Test]
        public void TrySubmitFrame_CallbackContextPostsThenThrows_CompletesTheFrameOnce()
        {
            var decoder = new SequenceDecoder();
            var ledger = new BufferLedger();
            using (var scanner = CreateScanner(decoder, TimeSpan.Zero, false, new PostThenThrowSynchronizationContext()))
            using (var completed = new ManualResetEventSlim())
            {
                int completions = 0;
                byte[] borrowed = null;
                scanner.ScanCompleted += value =>
                {
                    Interlocked.Increment(ref completions);
                    ledger.CheckIn(value.Frame.Buffer);
                    completed.Set();
                };
                scanner.Start();

                Assert.That(
                    scanner.TrySubmitFrame(
                        () =>
                        {
                            borrowed = ledger.CheckOut(16);
                            return new Gray8Image(borrowed, 4, 4);
                        },
                        0d),
                    Is.True);

                Assert.That(completed.Wait(3000), Is.True);
                Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref completions) > 1, 200), Is.False);
                Assert.That(ledger.CheckInCount, Is.EqualTo(1));
                Assert.That(scanner.IsBusy, Is.False);
            }
        }

        private static Gray8Image CountingProvider(ref int calls)
        {
            Interlocked.Increment(ref calls);
            return Frame;
        }

        private static QRCodeScanner CreateScanner(
            IQRCodeDecoder decoder,
            TimeSpan interval,
            bool stopOnSuccess,
            SynchronizationContext callbackContext = null) =>
            new QRCodeScanner(
                decoder,
                new QRCodeScannerOptions
                {
                    ScanInterval = interval,
                    StopOnSuccess = stopOnSuccess
                },
                callbackContext);

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

        private sealed class BufferLedger
        {
            private readonly HashSet<byte[]> _checkedOut = new HashSet<byte[]>();

            internal int CheckOutCount { get; private set; }

            internal int CheckInCount { get; private set; }

            internal byte[] CheckOut(int length)
            {
                var buffer = new byte[length];
                _checkedOut.Add(buffer);
                CheckOutCount++;
                return buffer;
            }

            internal void CheckIn(byte[] buffer)
            {
                if (!_checkedOut.Remove(buffer))
                    throw new InvalidOperationException("The buffer was returned while it was not checked out.");

                CheckInCount++;
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

        private sealed class ThrowingSynchronizationContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state) =>
                throw new InvalidOperationException("Expected test dispatch failure.");
        }

        private sealed class RecordingSynchronizationContext : SynchronizationContext
        {
            private int _postCount;

            internal int PostCount => Volatile.Read(ref _postCount);

            public override void Post(SendOrPostCallback d, object state)
            {
                Interlocked.Increment(ref _postCount);
                d(state);
            }
        }

        private sealed class PostThenThrowSynchronizationContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state)
            {
                d(state);
                throw new InvalidOperationException("Expected test dispatch failure after queueing.");
            }
        }
    }
}
