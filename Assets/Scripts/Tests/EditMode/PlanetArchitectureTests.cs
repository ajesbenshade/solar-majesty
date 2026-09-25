using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Per-world building architecture: every world dresses every archetype, the four doorway
    /// lanes stay clear, and Mars keeps its tuned kit colours.
    /// </summary>
    public class PlanetArchitectureTests
    {
        private static readonly CelestialBodyId[] Bodies =
            { CelestialBodyId.Earth, CelestialBodyId.Luna, CelestialBodyId.Mars, CelestialBodyId.Belt, CelestialBodyId.Europa };

        private static readonly ArchArchetype[] Archetypes =
        {
            ArchArchetype.Dwelling, ArchArchetype.Hub, ArchArchetype.Power, ArchArchetype.Extractor,
            ArchArchetype.Workshop, ArchArchetype.Defense, ArchArchetype.Pad, ArchArchetype.Wonder
        };

        // Doorway lane on each face centre: ~1.4 m wide, from knee to head height.
        private const float LaneHalfWidth = 0.72f;
        private const float LaneBottom = 0.35f;
        private const float LaneTop = 1.9f;
        private const float LaneReach = 3f;

        private static float Side(ArchArchetype a) => a == ArchArchetype.Hub || a == ArchArchetype.Pad || a == ArchArchetype.Wonder ? 9f : 6f;
        private static float Height(ArchArchetype a) => a == ArchArchetype.Pad ? 0.4f : a == ArchArchetype.Wonder ? 5f : 2.8f;

        [Test]
        public void EveryWorldDressesEveryArchetype()
        {
            foreach (var id in Bodies)
            foreach (var a in Archetypes)
            {
                var parts = PlanetArchitecture.Adapt(id, a, Side(a), Side(a), Height(a), 7);
                Assert.GreaterOrEqual(parts.Count, 6, $"{id} {a}");
                foreach (var p in parts)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(p.Name), $"{id} {a} unnamed part");
                    Assert.IsTrue(Finite(p.Position) && Finite(p.Size) && Finite(p.Euler), $"{id} {a} {p.Name} not finite");
                    Assert.Greater(Mathf.Min(p.Size.x, Mathf.Min(p.Size.y, p.Size.z)), 0f, $"{id} {a} {p.Name} size");
                    Assert.Less(p.Position.y + p.Size.y, 20f, $"{id} {a} {p.Name} absurdly tall");
                }
            }
        }

        [Test]
        public void DoorwayLanesStayClear()
        {
            var blocked = new List<string>();
            foreach (var id in Bodies)
            foreach (var a in Archetypes)
            foreach (int seed in new[] { 1, 7, 12345 })
            {
                float w = Side(a), d = Side(a);
                var parts = PlanetArchitecture.Adapt(id, a, w, d, Height(a), seed);
                foreach (var p in parts)
                {
                    Bounds(p, out Vector3 min, out Vector3 max);
                    if (max.y < LaneBottom || min.y > LaneTop) continue;
                    // ±X faces: lane runs along x, narrow in z.
                    bool xLane = Overlap(min.z, max.z, -LaneHalfWidth, LaneHalfWidth) &&
                                 (Overlap(min.x, max.x, w * 0.5f, w * 0.5f + LaneReach) ||
                                  Overlap(min.x, max.x, -w * 0.5f - LaneReach, -w * 0.5f));
                    bool zLane = Overlap(min.x, max.x, -LaneHalfWidth, LaneHalfWidth) &&
                                 (Overlap(min.z, max.z, d * 0.5f, d * 0.5f + LaneReach) ||
                                  Overlap(min.z, max.z, -d * 0.5f - LaneReach, -d * 0.5f));
                    if (xLane || zLane)
                        blocked.Add($"{id} {a} seed {seed}: {p.Name} x[{min.x:0.00},{max.x:0.00}] y[{min.y:0.00},{max.y:0.00}] z[{min.z:0.00},{max.z:0.00}]");
                }
            }
            Assert.IsEmpty(blocked, "Parts block doorway lanes:\n" + string.Join("\n", blocked));
        }

        [Test]
        public void SameSeedSameParts_DifferentWorldsDifferentBuildings()
        {
            var a1 = PlanetArchitecture.Adapt(CelestialBodyId.Luna, ArchArchetype.Hub, 9f, 9f, 3f, 42);
            var a2 = PlanetArchitecture.Adapt(CelestialBodyId.Luna, ArchArchetype.Hub, 9f, 9f, 3f, 42);
            Assert.AreEqual(a1.Count, a2.Count);
            for (int i = 0; i < a1.Count; i++)
            {
                Assert.AreEqual(a1[i].Name, a2[i].Name);
                Assert.AreEqual(a1[i].Position.x, a2[i].Position.x, 1e-5f);
                Assert.AreEqual(a1[i].Size.z, a2[i].Size.z, 1e-5f);
            }

            foreach (var a in Archetypes)
            {
                var names = new HashSet<string>();
                foreach (var id in Bodies)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var p in PlanetArchitecture.Adapt(id, a, Side(a), Side(a), Height(a), 3)) sb.Append(p.Name).Append(',');
                    Assert.IsTrue(names.Add(sb.ToString()), $"{id} {a} repeats another world's set");
                }
            }
        }

        [Test]
        public void EachWorldHasItsOwnPalette()
        {
            for (int i = 0; i < Bodies.Length; i++)
            {
                var s = PlanetArchitecture.Style(Bodies[i]);
                Assert.IsFalse(string.IsNullOrEmpty(s.Name), $"{Bodies[i]} name");
                Assert.IsFalse(string.IsNullOrEmpty(s.Rationale), $"{Bodies[i]} rationale");
                Assert.Greater(s.GlowEmission.maxColorComponent, 0.2f, $"{Bodies[i]} glow");
                for (int j = i + 1; j < Bodies.Length; j++)
                {
                    var o = PlanetArchitecture.Style(Bodies[j]);
                    float dh = Dist(s.Hull, o.Hull) + Dist(s.Trim, o.Trim) + Dist(s.Shell, o.Shell);
                    Assert.Greater(dh, 0.05f, $"{Bodies[i]} vs {Bodies[j]} look alike");
                }
            }
            Assert.IsTrue(ArchStyle.IsTranslucent(ArchRole.Ice));
            Assert.IsFalse(ArchStyle.IsTranslucent(ArchRole.Shell));
        }

        [Test]
        public void MarsKeepsItsTunedKit_OthersRegrade()
        {
            var orange = new Color(0.96f, 0.42f, 0.08f);
            Assert.IsFalse(PlanetArchitecture.TryRemap(CelestialBodyId.Mars, orange, out _));
            Assert.IsFalse(PlanetArchitecture.TryRemapMaterial(CelestialBodyId.Mars, "SM_Art_Orange", orange, out _));

            Assert.IsTrue(PlanetArchitecture.TryRemap(CelestialBodyId.Europa, orange, out var eu));
            Assert.AreEqual(PlanetArchitecture.Style(CelestialBodyId.Europa).Trim.b, eu.b, 1e-5f);

            Assert.IsTrue(PlanetArchitecture.TryRemapMaterial(CelestialBodyId.Luna, "SM_Art_WhiteHull", Color.white, out var lu));
            Assert.AreEqual(PlanetArchitecture.Style(CelestialBodyId.Luna).Hull.r, lu.r, 1e-5f);
            Assert.IsTrue(PlanetArchitecture.TryRemapMaterial(CelestialBodyId.Belt, "SM_Art_BlackCarbon", Color.black, out _));
            Assert.IsFalse(PlanetArchitecture.TryRemapMaterial(CelestialBodyId.Earth, "SM_Art_Steel", Color.grey, out _));
            // Unrelated colours stay.
            Assert.IsFalse(PlanetArchitecture.TryRemap(CelestialBodyId.Earth, new Color(0.1f, 0.5f, 0.9f), out _));
        }

        [Test]
        public void EveryCategoryHasAnArchetype()
        {
            Assert.AreEqual(ArchArchetype.Dwelling, PlanetArchitecture.ArchetypeOf(BuildingCategory.Habitat));
            Assert.AreEqual(ArchArchetype.Hub, PlanetArchitecture.ArchetypeOf(BuildingCategory.Commons));
            Assert.AreEqual(ArchArchetype.Power, PlanetArchitecture.ArchetypeOf(BuildingCategory.Power));
            Assert.AreEqual(ArchArchetype.Pad, PlanetArchitecture.ArchetypeOf(BuildingCategory.LandingPad));
            Assert.AreEqual(ArchArchetype.Wonder, PlanetArchitecture.ArchetypeOf(BuildingCategory.DeepArchive));
            foreach (BuildingCategory c in System.Enum.GetValues(typeof(BuildingCategory)))
                Assert.DoesNotThrow(() => PlanetArchitecture.ArchetypeOf(c));
        }

        // ---------------------------------------------------------------- helpers

        private static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                                                   float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        private static float Dist(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        private static bool Overlap(float a0, float a1, float b0, float b1) => a0 < b1 && b0 < a1;

        /// <summary>Exact world-axis bounds of a rotated box, ellipsoid or elliptic cylinder.</summary>
        private static void Bounds(in ArchPart p, out Vector3 min, out Vector3 max)
        {
            Quaternion q = Quaternion.Euler(p.Euler);
            Vector3 h = p.Size * 0.5f;
            Vector3 ax = q * new Vector3(h.x, 0, 0), ay = q * new Vector3(0, h.y, 0), az = q * new Vector3(0, 0, h.z);
            Vector3 e;
            if (p.Shape == ArchShape.Box)
                e = new Vector3(
                    Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x),
                    Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y),
                    Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            else if (p.Shape == ArchShape.Sphere)
                e = new Vector3(
                    Mathf.Sqrt(ax.x * ax.x + ay.x * ay.x + az.x * az.x),
                    Mathf.Sqrt(ax.y * ax.y + ay.y * ay.y + az.y * az.y),
                    Mathf.Sqrt(ax.z * ax.z + ay.z * ay.z + az.z * az.z));
            else // cylinder: round in local x/z, flat along local y
                e = new Vector3(
                    Mathf.Abs(ay.x) + Mathf.Sqrt(ax.x * ax.x + az.x * az.x),
                    Mathf.Abs(ay.y) + Mathf.Sqrt(ax.y * ax.y + az.y * az.y),
                    Mathf.Abs(ay.z) + Mathf.Sqrt(ax.z * ax.z + az.z * az.z));
            min = p.Position - e;
            max = p.Position + e;
        }
    }
}
