namespace SolarMajesty
{
    /// <summary>Compatibility facade — prefer <see cref="BodySeed"/>.</summary>
    public static class MoonSeed
    {
        public static int Current => BodySeed.Current;

        public static void Ensure(int inspectorOverride = 0, bool randomizeIfMissing = false) =>
            BodySeed.Ensure(BodySeed.LoadSavedBody(), inspectorOverride, randomizeIfMissing);

        public static void AdvanceForNextConquest() => BodySeed.AdvanceForNextConquest();

        public static void SetAndPersist(int seed) => BodySeed.SetAndPersist(seed);
    }
}
