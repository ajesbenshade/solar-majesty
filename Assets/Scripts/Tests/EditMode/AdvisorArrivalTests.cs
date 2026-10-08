using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class AdvisorArrivalTests
    {
        [Test]
        public void Continue_WithCommons_DoesNotRepeatTheEmptyMeadowHint()
        {
            Assert.IsTrue(AdvisorToastCatalog.TryGetTravel(
                AdvisorToastCatalog.TravelEarthEmptyDrop, out var toast));
            StringAssert.Contains("The meadow is empty", toast.Line);
            StringAssert.Contains("Raise a Commons", toast.Line);

            Assert.IsFalse(AdvisorArrival.ShouldSpeak(AdvisorToastCatalog.TravelEarthEmptyDrop, hasCommons: true),
                "a loaded Commons means the meadow lecture is already false");
            Assert.IsFalse(AdvisorArrival.ShouldSpeak(AdvisorToastCatalog.TravelMarsArrival, hasCommons: true));
        }

        [Test]
        public void EmptyDrop_StillSpeaksWhenNoCommonsStands()
        {
            Assert.IsTrue(AdvisorArrival.ShouldSpeak(AdvisorToastCatalog.TravelEarthEmptyDrop, hasCommons: false));
            Assert.IsTrue(AdvisorArrival.ShouldSpeak(AdvisorToastCatalog.TravelLunaArrival, hasCommons: true),
                "Luna's arrival is not an empty-court lecture");
            Assert.IsFalse(AdvisorArrival.ShouldSpeak(null, hasCommons: false));
        }
    }
}
