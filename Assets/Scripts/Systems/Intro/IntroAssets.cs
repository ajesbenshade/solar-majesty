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
        /// Authored 3D title. The generator instances it into the title slot when the file
        /// exists. The player never loads this path; a missing scene slot still gets the placeholder.
        /// </summary>
        public const string TitlePrefabPath = "Assets/Art/Intro/SM_Title_SolarMajesty.prefab";

        public const string PlaceholderGlyphs = "PlaceholderGlyphs";

        public const string WordSolar = "Word_SOLAR";
        public const string WordMajesty = "Word_MAJESTY";
        public const string EmblemPlanet = "Emblem_Planet";
        public const string TrimRoot = "Trim";
        public const string GoldMaterialName = "SM_Title_Gold";

        /// <summary>True when the editor should instance <see cref="TitlePrefabPath"/> instead of the placeholder.</summary>
        public static bool UseAuthoredTitle(bool prefabAssetExists) => prefabAssetExists;

        public const string CameraTrack = "Intro Camera";
        public const string TitleTrack = "Title Card";
        public const string StingTrack = "Intro Sting";
        public const string RevealTrack = "Title Reveal";

        /// <summary>
        /// The 3D artist marks a hand-edited camera by renaming that track so the name ends
        /// with this suffix, for example "Intro Camera (Authored)". BuildAll then leaves the
        /// track, its clips, curves, and offsets untouched and only rebuilds the other tracks.
        /// </summary>
        public const string AuthoredCameraSuffix = " (Authored)";

        public static bool IsAuthoredCameraTrack(string trackName) =>
            !string.IsNullOrEmpty(trackName) && trackName.EndsWith(AuthoredCameraSuffix);

        public static bool IsCameraTrackName(string trackName) =>
            trackName == CameraTrack || IsAuthoredCameraTrack(trackName);

        /// <summary>An existing authored camera is kept. A missing or generated camera is rebuilt.</summary>
        public static bool ShouldRebuildCameraTrack(bool trackExists, string trackName) =>
            !trackExists || !IsAuthoredCameraTrack(trackName);
    }
}
