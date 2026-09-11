using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Alignment knobs for the vendored Luna heightmap. Heights are encoded so
    /// 0.5 = campus grade (0 m) and the full 0–1 range is <see cref="heightRange"/> metres.
    /// Credit: NASA/GSFC/Arizona State University LROC NAC_DTM_LINNECRATER (Linné crater).
    /// </summary>
    [CreateAssetMenu(menuName = "Solar Majesty/Luna DEM Settings", fileName = "LunaDemSettings")]
    public sealed class LunaDemSettings : ScriptableObject
    {
        public const string ResourcePath = "World/LunaDem/LunaDemSettings";
        public const string HeightResourcePath = "World/LunaDem/LunaHeight";

        public Texture2D height;
        public Vector2 campusUv = new Vector2(0.5f, 0.5f);
        public float yawDegrees;
        public float metresPerPixel = 80f / 512f;
        public float verticalScale = 1f;
        public float coverageMetres = 80f;
        public float heightRange = 32f;

        private static LunaDemSettings _cached;

        public static LunaDemSettings Load()
        {
            if (_cached == null)
                _cached = Resources.Load<LunaDemSettings>(ResourcePath);
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
