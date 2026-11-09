using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class SimRngTests
    {
        [Test]
        public void SameSeed_GivesTheSameSequence()
        {
            var a = new SimRng(7717);
            var b = new SimRng(7717);

            for (int i = 0; i < 64; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void NeighbouringSeeds_Diverge()
        {
            var a = new SimRng(1);
            var b = new SimRng(2);

            int same = 0;
            for (int i = 0; i < 64; i++)
                if (a.NextUInt() == b.NextUInt()) same++;

            Assert.Less(same, 4);
        }

        /// <summary>Save and reload must continue the stream, not restart or fork it.</summary>
        [Test]
        public void State_RestoredIntoAnotherStream_ContinuesTheSequence()
        {
            var a = new SimRng(42);
            for (int i = 0; i < 10; i++) a.NextUInt();

            var b = new SimRng(999) { State = a.State };

            for (int i = 0; i < 32; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void ZeroSeedAndZeroState_StillProduceNumbers()
        {
            var seeded = new SimRng(0);
            var restored = new SimRng(5) { State = 0 };

            Assert.AreNotEqual(0L, seeded.State);
            Assert.AreNotEqual(0L, restored.State);
            Assert.AreNotEqual(seeded.NextUInt(), seeded.NextUInt());
        }

        [Test]
        public void Value_StaysInZeroToOneExclusive()
        {
            var rng = new SimRng(3);
            for (int i = 0; i < 5000; i++)
            {
                float v = rng.Value();
                Assert.GreaterOrEqual(v, 0f);
                Assert.Less(v, 1f);
            }
        }

        [Test]
        public void RangeWithBounds_StaysBetweenThem()
        {
            var rng = new SimRng(21);
            for (int i = 0; i < 2000; i++)
            {
                float v = rng.Range(0.7f, 1.3f);
                Assert.GreaterOrEqual(v, 0.7f);
                Assert.Less(v, 1.3f);
            }
        }

        [Test]
        public void InsideUnitCircle_StaysInsideAndCoversAllFourQuadrants()
        {
            var rng = new SimRng(8);
            int quadrants = 0;
            for (int i = 0; i < 2000; i++)
            {
                UnityEngine.Vector2 p = rng.InsideUnitCircle();
                Assert.LessOrEqual(p.sqrMagnitude, 1f + 1e-5f);
                quadrants |= 1 << ((p.x >= 0f ? 1 : 0) + (p.y >= 0f ? 2 : 0));
            }

            Assert.AreEqual(0b1111, quadrants);
        }

        [Test]
        public void Next_StaysInRangeAndReachesBothEnds()
        {
            var rng = new SimRng(11);
            bool low = false, high = false;
            for (int i = 0; i < 2000; i++)
            {
                int n = rng.Next(2, 6);
                Assert.GreaterOrEqual(n, 2);
                Assert.Less(n, 6);
                low |= n == 2;
                high |= n == 5;
            }

            Assert.IsTrue(low && high);
            Assert.AreEqual(4, rng.Next(4, 4), "an empty range returns its lower bound");
        }
    }
}
