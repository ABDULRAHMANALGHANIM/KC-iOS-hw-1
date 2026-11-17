using System;
using NeonRequiem.Core;
using NUnit.Framework;

namespace NeonRequiem.Tests
{
    public sealed class CircularBufferTests
    {
        [Test]
        public void Constructor_RejectsNonPositiveCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<int>(-3));
        }

        [Test]
        public void NewBuffer_IsEmpty()
        {
            var buffer = new CircularBuffer<int>(4);
            Assert.AreEqual(4, buffer.Capacity);
            Assert.AreEqual(0, buffer.Count);
            Assert.IsTrue(buffer.IsEmpty);
            Assert.IsFalse(buffer.IsFull);
        }

        [Test]
        public void PushBack_KeepsInsertionOrder()
        {
            var buffer = new CircularBuffer<int>(4);
            buffer.PushBack(10);
            buffer.PushBack(20);
            buffer.PushBack(30);

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(10, buffer[0]);
            Assert.AreEqual(20, buffer[1]);
            Assert.AreEqual(30, buffer[2]);
            Assert.AreEqual(10, buffer.Oldest);
            Assert.AreEqual(30, buffer.Newest);
        }

        [Test]
        public void PushBack_WhenFull_OverwritesOldest()
        {
            var buffer = new CircularBuffer<int>(3);
            Assert.IsFalse(buffer.PushBack(1));
            Assert.IsFalse(buffer.PushBack(2));
            Assert.IsFalse(buffer.PushBack(3));
            Assert.IsTrue(buffer.IsFull);

            Assert.IsTrue(buffer.PushBack(4));
            Assert.IsTrue(buffer.PushBack(5));

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(3, buffer[0]);
            Assert.AreEqual(4, buffer[1]);
            Assert.AreEqual(5, buffer[2]);
        }

        [Test]
        public void Count_NeverExceedsCapacity_AfterManyPushes()
        {
            var buffer = new CircularBuffer<int>(8);
            for (int i = 0; i < 1000; i++)
            {
                buffer.PushBack(i);
            }

            Assert.AreEqual(8, buffer.Count);
            Assert.AreEqual(992, buffer.Oldest);
            Assert.AreEqual(999, buffer.Newest);
        }

        [Test]
        public void Indexer_ThrowsOutsideRange()
        {
            var buffer = new CircularBuffer<int>(2);
            buffer.PushBack(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => { int unused = buffer[1]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { int unused = buffer[-1]; });
        }

        [Test]
        public void NewestAndOldest_ThrowWhenEmpty()
        {
            var buffer = new CircularBuffer<int>(2);
            Assert.Throws<InvalidOperationException>(() => { int unused = buffer.Newest; });
            Assert.Throws<InvalidOperationException>(() => { int unused = buffer.Oldest; });
        }

        [Test]
        public void TryPopFront_RemovesOldestFirst()
        {
            var buffer = new CircularBuffer<int>(3);
            buffer.PushBack(1);
            buffer.PushBack(2);
            buffer.PushBack(3);
            buffer.PushBack(4);

            Assert.IsTrue(buffer.TryPopFront(out int first));
            Assert.AreEqual(2, first);
            Assert.IsTrue(buffer.TryPopFront(out int second));
            Assert.AreEqual(3, second);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(4, buffer.Oldest);
        }

        [Test]
        public void TryPopBack_RemovesNewestFirst()
        {
            var buffer = new CircularBuffer<int>(3);
            buffer.PushBack(1);
            buffer.PushBack(2);

            Assert.IsTrue(buffer.TryPopBack(out int value));
            Assert.AreEqual(2, value);
            Assert.AreEqual(1, buffer.Newest);
        }

        [Test]
        public void TryPop_OnEmpty_ReturnsFalse()
        {
            var buffer = new CircularBuffer<int>(3);
            Assert.IsFalse(buffer.TryPopFront(out int _));
            Assert.IsFalse(buffer.TryPopBack(out int _));
        }

        [Test]
        public void Clear_ResetsBuffer_AndAllowsReuse()
        {
            var buffer = new CircularBuffer<int>(3);
            buffer.PushBack(1);
            buffer.PushBack(2);
            buffer.PushBack(3);
            buffer.PushBack(4);
            buffer.Clear();

            Assert.IsTrue(buffer.IsEmpty);
            buffer.PushBack(7);
            Assert.AreEqual(7, buffer[0]);
            Assert.AreEqual(1, buffer.Count);
        }

        [Test]
        public void CopyTo_CopiesOldestFirst_UpToDestinationLength()
        {
            var buffer = new CircularBuffer<int>(4);
            for (int i = 1; i <= 6; i++)
            {
                buffer.PushBack(i);
            }

            var all = new int[4];
            Assert.AreEqual(4, buffer.CopyTo(all));
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6 }, all);

            var partial = new int[2];
            Assert.AreEqual(2, buffer.CopyTo(partial));
            CollectionAssert.AreEqual(new[] { 3, 4 }, partial);

            Assert.Throws<ArgumentNullException>(() => buffer.CopyTo(null));
        }
    }

    public sealed class FrameTimeTrackerTests
    {
        [Test]
        public void EmptyTracker_ReportsZero()
        {
            var tracker = new FrameTimeTracker(16);
            Assert.AreEqual(0, tracker.SampleCount);
            Assert.AreEqual(0f, tracker.AverageSeconds);
            Assert.AreEqual(0f, tracker.WorstSeconds);
            Assert.AreEqual(0f, tracker.GetPercentileSeconds(0.95f));
        }

        [Test]
        public void AddSample_RejectsInvalidAndHitchSamples()
        {
            var tracker = new FrameTimeTracker(16);
            Assert.IsFalse(tracker.AddSample(0f));
            Assert.IsFalse(tracker.AddSample(-0.016f));
            Assert.IsFalse(tracker.AddSample(float.NaN));
            Assert.IsFalse(tracker.AddSample(FrameTimeTracker.MaxAcceptedSampleSeconds + 0.01f));
            Assert.IsTrue(tracker.AddSample(0.016f));
            Assert.AreEqual(1, tracker.SampleCount);
        }

        [Test]
        public void Average_IsArithmeticMean()
        {
            var tracker = new FrameTimeTracker(16);
            tracker.AddSample(0.010f);
            tracker.AddSample(0.020f);
            tracker.AddSample(0.030f);
            Assert.That(tracker.AverageSeconds, Is.EqualTo(0.020f).Within(1e-6f));
            Assert.That(tracker.WorstSeconds, Is.EqualTo(0.030f).Within(1e-6f));
        }

        [Test]
        public void Percentile_UsesNearestRank()
        {
            var tracker = new FrameTimeTracker(128);
            for (int i = 1; i <= 100; i++)
            {
                tracker.AddSample(i * 0.001f);
            }

            Assert.That(tracker.GetPercentileSeconds(0.95f), Is.EqualTo(0.095f).Within(1e-6f));
            Assert.That(tracker.GetPercentileSeconds(0.50f), Is.EqualTo(0.050f).Within(1e-6f));
            Assert.That(tracker.GetPercentileSeconds(1f), Is.EqualTo(0.100f).Within(1e-6f));
            Assert.That(tracker.GetPercentileSeconds(0f), Is.EqualTo(0.001f).Within(1e-6f));
        }

        [Test]
        public void Percentile_DoesNotReorderStoredSamples()
        {
            var tracker = new FrameTimeTracker(8);
            tracker.AddSample(0.030f);
            tracker.AddSample(0.010f);
            tracker.GetPercentileSeconds(0.95f);
            tracker.AddSample(0.020f);

            // The average is order independent, but the window must still drop the OLDEST sample.
            for (int i = 0; i < 7; i++)
            {
                tracker.AddSample(0.010f);
            }

            Assert.That(tracker.WorstSeconds, Is.EqualTo(0.020f).Within(1e-6f));
        }

        [Test]
        public void Window_DropsOldestSamples()
        {
            var tracker = new FrameTimeTracker(4);
            tracker.AddSample(0.100f);
            for (int i = 0; i < 4; i++)
            {
                tracker.AddSample(0.010f);
            }

            Assert.AreEqual(4, tracker.SampleCount);
            Assert.That(tracker.WorstSeconds, Is.EqualTo(0.010f).Within(1e-6f));
        }

        [Test]
        public void Clear_RemovesAllSamples()
        {
            var tracker = new FrameTimeTracker(4);
            tracker.AddSample(0.016f);
            tracker.Clear();
            Assert.AreEqual(0, tracker.SampleCount);
        }
    }
}
