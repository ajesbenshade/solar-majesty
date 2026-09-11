using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Alignment knobs for the vendored Mars heightmap. Heights are encoded so
    /// 0.5 = campus grade (0 m) and the full 0–1 range is <see cref="heightRange"/> metres.
    /// Credit: NASA/JPL-Caltech/UArizona HiRISE DTEEC_001414_1780 (Victoria crater sample).
    /// </summary>
    [CreateAssetMenu(menuName = "Solar Majesty/Mars DEM Settings", fileName = "MarsDemSettings")]
    public sealed class MarsDemSettings : ScriptableObject
    {
        public const string ResourcePath = "World/MarsDem/MarsDemSettings";
        public const string HeightResourcePath = "World/MarsDem/MarsHeight";

        public Texture2D height;
        public Vector2 campusUv = new Vector2(0.5f, 0.5f);
        public float yawDegrees;
        public float metresPerPixel = 80f / 512f;
        public float verticalScale = 1f;
        public float coverageMetres = 80f;
        public float heightRange = 32f;

        private static MarsDemSettings _cached;

        public static MarsDemSettings Load()
        {
            if (_cached == null)
                _cached = Resources.Load<MarsDemSettings>(ResourcePath);
            return _cached;
        }

        public Texture2D HeightTexture
        {
            get
            {
                if (height != null) return height;
                return Resources.Load<Texture2D>(HeightResourcePath);
            }
        }
    }
}
