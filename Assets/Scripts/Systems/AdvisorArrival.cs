namespace SolarMajesty
{
    /// <summary>
    /// Arrival copy that assumes a first drop. Continue and Load already have a world;
    /// those lines are skipped when the loaded campus makes them false.
    /// </summary>
    public static class AdvisorArrival
    {
        /// <summary>
        /// The empty-court lecture ("the meadow is empty… raise a Commons", and the Mars
        /// "raise the seat" arrival) is only true when that civic is not already standing.
        /// </summary>
        public static bool ShouldSpeak(string travelKey, bool hasCommons)
        {
            if (string.IsNullOrEmpty(travelKey)) return false;
            if (!hasCommons) return true;
            if (travelKey == AdvisorToastCatalog.TravelEarthEmptyDrop) return false;
            if (travelKey == AdvisorToastCatalog.TravelMarsArrival) return false;
            return true;
        }
    }
}
