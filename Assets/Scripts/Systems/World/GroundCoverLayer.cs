using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>Procedural ground-cover meshes (built by GroundCoverMeshes, drawn instanced).</summary>
    public enum GroundCoverKind
    {
        GrassTuft = 0,
        TallGrass = 1,
        Flowers = 2,
        Pebbles = 3,
        Bush = 4,
        Fern = 5,
        Mushrooms = 6,
        Rubble = 7,
        Boulder = 8,
        Crystals = 9,
        IceShards = 10,
        Reeds = 12,
        Twigs = 13
    }

    /// <summary>
    /// One layer of instanced ground cover on a world: what to plant, how thick, where it is
    /// welcome (terrain splat channels and rockiness) and how patchy it grows. Profiles carry a
    /// list of these, so a new world gets its own undergrowth from catalog data alone.
    /// </summary>
    [Serializable]
    public struct GroundCoverLayer
    {
        public GroundCoverKind Kind;
        [Tooltip("Instances per 100 m² where the layer is fully welcome.")]
        public float Density;
        public float ScaleMin;
        public float ScaleMax;
        [Tooltip("Instances pick a colour between A and B.")]
        public Color ColorA;
        public Color ColorB;
        [Tooltip("Welcome per terrain splat channel: R dust/soil, G rock, B vegetation/feature, A wet/floor.")]
        public Vector4 SplatAffinity;
        [Tooltip("Size of the patches in metres (0 = even cover).")]
        public float ClusterScale;
        [Tooltip("0..1. Higher leaves more bare ground between patches.")]
        public float ClusterThreshold;
        public bool CastShadows;
        [Tooltip("Sink below the surface, as a fraction of the instance scale.")]
        public float Sink;

        public static GroundCoverLayer Of(GroundCoverKind kind, float density, float scaleMin, float scaleMax,
            Color a, Color b, Vector4 affinity, float clusterScale = 0f, float clusterThreshold = 0f,
            bool castShadows = false, float sink = 0f)
        {
            return new GroundCoverLayer
            {
                Kind = kind,
                Density = density,
                ScaleMin = scaleMin,
                ScaleMax = scaleMax,
                ColorA = a,
                ColorB = b,
                SplatAffinity = affinity,
                ClusterScale = clusterScale,
                ClusterThreshold = clusterThreshold,
                CastShadows = castShadows,
                Sink = sink
            };
        }

        /// <summary>How welcome the layer is on ground with this splat (0..1).</summary>
        public float Welcome(Color splat)
        {
            float w = splat.r * SplatAffinity.x + splat.g * SplatAffinity.y +
                      splat.b * SplatAffinity.z + splat.a * SplatAffinity.w;
            return Mathf.Clamp01(w);
        }

        /// <summary>Patch mask from a 0..1 noise value (1 everywhere when unclustered).</summary>
        public float Patch(float noise01)
        {
            if (ClusterScale <= 0f || ClusterThreshold <= 0f) return 1f;
            float lo = ClusterThreshold - 0.12f;
            float hi = ClusterThreshold + 0.12f;
            float t = Mathf.Clamp01((noise01 - lo) / (hi - lo));
            return t * t * (3f - 2f * t);
        }
    }
}
