using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
        /// BOXOPHOBIC Terrain Data Baker demo albedo tiles (Sand / Grass / Snow) wired
        /// from outside Resources. PlanetGround samples them as splat layers on Earth.
        /// Mars ignores these and uses SM_Ground_Mars_Albedo so highs stay rust.
    /// </summary>
    [CreateAssetMenu(menuName = "Solar Majesty/Terrain Splat Layers", fileName = "TerrainSplatLayers")]
    public sealed class TerrainSplatLayers : ScriptableObject
    {
        public const string ResourcePath = "Dressing/TerrainSplatLayers";

        public const string SandAssetPath =
            "Assets/BOXOPHOBIC/Terrain Data Baker/Demo/Terrain/Terrain - Sand.png";
        public const string GrassAssetPath =
            "Assets/BOXOPHOBIC/Terrain Data Baker/Demo/Terrain/Terrain - Grass.png";
        public const string SnowAssetPath =
            "Assets/BOXOPHOBIC/Terrain Data Baker/Demo/Terrain/Terrain - Snow.png";

        public Texture2D sand;
        public Texture2D grass;
        public Texture2D snow;

        private static TerrainSplatLayers _cached;

        public static TerrainSplatLayers Load()
        {
            if (_cached == null)
                _cached = Resources.Load<TerrainSplatLayers>(ResourcePath);
            return _cached;
        }

        public bool HasSand => sand != null;
        public bool HasGrass => grass != null;
        public bool HasAny => sand != null || grass != null;
    }
}
