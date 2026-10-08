using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class PurseWordingTests
    {
        [Test]
        public void SittingLevy_NamesThePooledPurses_NotOneHab()
        {
            string line = CompactGrok.PurseSitting(900);
            StringAssert.Contains("900 EU is sitting in building purses", line);
            StringAssert.DoesNotContain("napping on a HAB", line);
            StringAssert.DoesNotContain("napping on the HAB", line);
        }

        [Test]
        public void TheftLine_StillNamesTheBuildingAndTheAmount()
        {
            string line = CompactGrok.LevyStolen(20, "HAB-1");
            StringAssert.Contains("20 EU walked off HAB-1", line);
        }
    }
}
