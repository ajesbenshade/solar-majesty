using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class EntityIdsTests
    {
        [Test]
        public void Next_StartsAtOneAndNeverRepeats()
        {
            var ids = new EntityIds();

            Assert.AreEqual(1, ids.Next());
            Assert.AreEqual(2, ids.Next());
            Assert.AreEqual(3, ids.NextId);
        }

        /// <summary>A robot restored with id 7 must not share it with the next robot built.</summary>
        [Test]
        public void Reserve_SkipsPastARestoredId()
        {
            var ids = new EntityIds();
            ids.Next();

            ids.Reserve(7);

            Assert.AreEqual(8, ids.Next());
        }

        [Test]
        public void Reserve_OfAnOlderId_ChangesNothing()
        {
            var ids = new EntityIds();
            ids.Next();
            ids.Next();

            ids.Reserve(1);
            ids.Reserve(0);
            ids.Reserve(-1); // an older save: nextEntityId 0, minus one

            Assert.AreEqual(3, ids.Next());
        }

        [Test]
        public void NextId_RoundTripsAndRejectsNonPositiveValues()
        {
            var ids = new EntityIds { NextId = 40 };
            Assert.AreEqual(40, ids.Next());

            ids.NextId = 0;
            Assert.AreEqual(1, ids.Next());
        }
    }
}
