using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class SfxVariantPickerTests
    {
        /// <summary>The whole point: the same take never plays twice in a row.</summary>
        [Test]
        public void Next_NeverRepeatsThePreviousVariant()
        {
            var picker = new SfxVariantPicker();
            var rng = new System.Random(7);
            int prev = picker.Next("bite", 3, (float)rng.NextDouble());
            for (int i = 0; i < 2000; i++)
            {
                int next = picker.Next("bite", 3, (float)rng.NextDouble());
                Assert.AreNotEqual(prev, next);
                prev = next;
            }
        }

        [Test]
        public void Next_StillReachesEveryVariant()
        {
            var picker = new SfxVariantPicker();
            var rng = new System.Random(11);
            var seen = new bool[3];
            for (int i = 0; i < 300; i++)
                seen[picker.Next("claim", 3, (float)rng.NextDouble())] = true;

            Assert.IsTrue(seen[0] && seen[1] && seen[2]);
        }

        [Test]
        public void Next_EdgeRandomValuesStayInRange()
        {
            var picker = new SfxVariantPicker();
            for (int i = 0; i < 20; i++)
            {
                int a = picker.Next("x", 3, 0f);
                int b = picker.Next("x", 3, 1f);
                int c = picker.Next("x", 3, 0.99999f);
                Assert.That(a, Is.InRange(0, 2));
                Assert.That(b, Is.InRange(0, 2));
                Assert.That(c, Is.InRange(0, 2));
            }
        }

        [Test]
        public void Next_SingleVariantAlwaysZero()
        {
            var picker = new SfxVariantPicker();
            Assert.AreEqual(0, picker.Next("legacy", 1, 0.7f));
            Assert.AreEqual(0, picker.Next("legacy", 1, 0.7f));
        }

        /// <summary>Keys are independent: one event's history must not constrain another.</summary>
        [Test]
        public void Next_KeysDoNotShareHistory()
        {
            var picker = new SfxVariantPicker();
            picker.Next("a", 2, 0f);
            Assert.AreEqual(0, picker.Next("b", 2, 0f));
        }

        /// <summary>A shrinking clip set (a variant deleted) must not index past the end.</summary>
        [Test]
        public void Next_StaleLastOutsideNewCountIsIgnored()
        {
            var picker = new SfxVariantPicker();
            picker.Next("k", 5, 0.95f);
            Assert.AreEqual(4, picker.Last("k"));
            Assert.That(picker.Next("k", 2, 0.6f), Is.InRange(0, 1));
        }
    }

    public class SfxRateLimiterTests
    {
        /// <summary>Twenty robots claiming in the same frame must not stack twenty clips.</summary>
        [Test]
        public void TryAcquire_SameInstantPlaysOnce()
        {
            var limiter = new SfxRateLimiter();
            var limit = new SfxLimit(0.05f, 3, 4f);
            int played = 0;
            for (int i = 0; i < 20; i++)
                if (limiter.TryAcquire("claim", 10f, limit)) played++;

            Assert.AreEqual(1, played);
        }

        [Test]
        public void TryAcquire_BurstThenThrottles()
        {
            var limiter = new SfxRateLimiter();
            var limit = new SfxLimit(0.05f, 3, 2f);
            int played = 0;
            // A report every frame for one second, like ApplyDamage during a bite.
            for (int frame = 0; frame < 60; frame++)
                if (limiter.TryAcquire("bite", frame / 60f, limit)) played++;

            // 3 from the bucket plus ~2 refilled during the second; far fewer than 60.
            Assert.That(played, Is.InRange(3, 6));
        }

        [Test]
        public void TryAcquire_RecoversAfterQuiet()
        {
            var limiter = new SfxRateLimiter();
            var limit = new SfxLimit(0.1f, 1, 1f);
            Assert.IsTrue(limiter.TryAcquire("hit", 0f, limit));
            Assert.IsFalse(limiter.TryAcquire("hit", 0.5f, limit));
            Assert.IsTrue(limiter.TryAcquire("hit", 1.2f, limit));
        }

        [Test]
        public void TryAcquire_KeysAreIndependent()
        {
            var limiter = new SfxRateLimiter();
            var limit = new SfxLimit(1f, 1, 0.1f);
            Assert.IsTrue(limiter.TryAcquire("a", 0f, limit));
            Assert.IsTrue(limiter.TryAcquire("b", 0f, limit));
            Assert.IsFalse(limiter.TryAcquire("a", 0.2f, limit));
        }

        [Test]
        public void TryAcquire_EmptyKeyIsNeverLimited()
        {
            var limiter = new SfxRateLimiter();
            var limit = new SfxLimit(10f, 1, 0f);
            Assert.IsTrue(limiter.TryAcquire(null, 0f, limit));
            Assert.IsTrue(limiter.TryAcquire(null, 0f, limit));
        }
    }
}
