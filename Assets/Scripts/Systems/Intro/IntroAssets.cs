namespace SolarMajesty
{
    /// <summary>
    /// Shared names for the intro Timeline and sting. The editor builder writes the
    /// Timeline asset; the player loads it from Resources when it is present.
    /// </summary>
    public static class IntroAssets
    {
        public const string TimelineResource = "Intro/IntroTimeline";
        public const string StingResource = "Intro/IntroSting";
        public const string TimelineAssetPath = "Assets/Resources/Intro/IntroTimeline.playable";
        public const string StingAssetPath = "Assets/Resources/Intro/IntroSting.ogg";

        /// <summary>
        /// Authored 3D title. Not in this branch yet. The generator instances it into the
        /// title slot when the file exists; the player never loads it itself.
        /// </summary>
        public const string TitlePrefabPath = "Assets/Art/Intro/SM_Title_SolarMajesty.prefab";

        public const string PlaceholderGlyphs = "PlaceholderGlyphs";

        /// <summary>True when the editor should instance <see cref="TitlePrefabPath"/> instead of the placeholder.</summary>
        public static bool UseAuthoredTitle(bool prefabAssetExists) => prefabAssetExists;

        public const string CameraTrack = "Intro Camera";
        public const string TitleTrack = "Title Card";
        public const string StingTrack = "Intro Sting";
    }
}
