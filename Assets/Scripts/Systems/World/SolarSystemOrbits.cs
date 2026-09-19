using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Playable (not to-scale) orrery layout for the title picker.
    /// Radii are metres in the title rig, parked far above the colony mesh.
    /// </summary>
    public static class SolarSystemOrbits
    {
        public const float TitleAltitude = 8500f;

        public const float EarthOrbit = 8f;
        public const float MarsOrbit = 13.5f;
        public const float BeltOrbit = 18.5f;
        public const float JupiterOrbit = 24.2f;

        public const float LunaOrbit = 2.35f;
        public const float EuropaOrbit = 3.15f;

        public const float SunScale = 2.8f;
        public const float EarthScale = 1.7f;
        public const float LunaScale = 0.68f;
        public const float MarsScale = 1.28f;
        public const float BeltHubScale = 1.12f;
        public const float JupiterScale = 2.45f;
        public const float EuropaScale = 0.78f;

        public static readonly Vector3 CameraLocal = new Vector3(11.8f, 6.6f, -15.2f);
        public static readonly Vector3 LookLocal = new Vector3(2.4f, 0.35f, 0.9f);

        public static Vector3 OnOrbit(float radius, float angleRadians) =>
            new Vector3(Mathf.Cos(angleRadians) * radius, 0f, Mathf.Sin(angleRadians) * radius);
    }
}
