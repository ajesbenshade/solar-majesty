using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Phase 4 CaptureStill density pack: pad + PWR-1 + water/regolith extractors
    /// around Commons→airlock→HAB. Forward yards use <see cref="BuildingPlacer.CanFitRect"/>
    /// only (same as the still HAB stamp) — ExtraPlacementRule would send pad/solar/extract
    /// to Campus B (~30 m) or demand an airlock dock, both of which miss CampusOrthoSize ~10.
    /// Does not flip spawnShowcaseColony.
    /// </summary>
    public static class StillCampusDensity
    {
        public const int PadSize = 6;
        public const int YardSize = 4;

        /// <summary>Keep yards inside the campus ortho: ~9 cells × 1.5 m ≈ 13.5 m from Commons.</summary>
        public const float MaxCenterSeparationCells = 9f;

        public delegate bool BoundsOk(Vector2Int origin, int width, int height);

        public struct StampLog
        {
            public bool Commons;
            public bool Airlock;
            public bool Hab;
            public bool Pad;
            public bool Power;
            public bool Water;
            public bool Regolith;

            public override string ToString() =>
                $"commons={Commons} airlock={Airlock} hab={Hab} " +
                $"pad={Pad} pwr={Power} water={Water} regolith={Regolith}";

            public static StampLog FromPieces(BuildingPlacer placer)
            {
                var log = new StampLog();
                if (placer == null) return log;
                var pieces = placer.Pieces;
                for (int i = 0; i < pieces.Count; i++)
                {
                    switch (pieces[i].Category)
                    {
                        case BuildingCategory.Commons: log.Commons = true; break;
                        case BuildingCategory.Utility: log.Airlock = true; break;
                        case BuildingCategory.Habitat: log.Hab = true; break;
                        case BuildingCategory.LandingPad: log.Pad = true; break;
                        case BuildingCategory.Power: log.Power = true; break;
                        case BuildingCategory.Farm: log.Water = true; break;
                        case BuildingCategory.RegolithCamp: log.Regolith = true; break;
                    }
                }

                return log;
            }
        }

        public struct PackPlan
        {
            public bool Pad;
            public Vector2Int PadOrigin;
            public bool Power;
            public Vector2Int PowerOrigin;
            public bool Water;
            public Vector2Int WaterOrigin;
            public bool Regolith;
            public Vector2Int RegolithOrigin;

            public int PlacedCount =>
                (Pad ? 1 : 0) + (Power ? 1 : 0) + (Water ? 1 : 0) + (Regolith ? 1 : 0);
        }

        public static bool TryGetCommons(BuildingPlacer placer, out BuildingPlacer.CampusPiece commons)
        {
            commons = default;
            if (placer == null) return false;
            var pieces = placer.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Category == BuildingCategory.Commons)
                {
                    commons = pieces[i];
                    return true;
                }
            }

            return false;
        }

        public static BuildingPlacer.Cardinal InferHabFace(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons)
        {
            if (placer == null) return BuildingPlacer.Cardinal.East;
            var pieces = placer.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p.Category != BuildingCategory.Habitat) continue;
                for (int f = 0; f < 4; f++)
                {
                    var face = (BuildingPlacer.Cardinal)f;
                    BuildingPlacer.CardinalExpansionOrigins(
                        commons, face, p.Width, p.Height, out _, out Vector2Int hab);
                    if (hab == p.Origin)
                        return face;
                }
            }

            return BuildingPlacer.Cardinal.East;
        }

        public static BuildingPlacer.Cardinal Opposite(BuildingPlacer.Cardinal face)
        {
            switch (face)
            {
                case BuildingPlacer.Cardinal.East: return BuildingPlacer.Cardinal.West;
                case BuildingPlacer.Cardinal.West: return BuildingPlacer.Cardinal.East;
                case BuildingPlacer.Cardinal.North: return BuildingPlacer.Cardinal.South;
                default: return BuildingPlacer.Cardinal.North;
            }
        }

        /// <summary>Opposite of HAB first, then E / W / N, South last (Inn / empty-drop south).</summary>
        public static BuildingPlacer.Cardinal[] PreferFaces(BuildingPlacer.Cardinal habFace)
        {
            var raw = new[]
            {
                Opposite(habFace),
                BuildingPlacer.Cardinal.East,
                BuildingPlacer.Cardinal.West,
                BuildingPlacer.Cardinal.North,
                BuildingPlacer.Cardinal.South
            };
            var unique = new BuildingPlacer.Cardinal[4];
            int n = 0;
            for (int i = 0; i < raw.Length && n < 4; i++)
            {
                bool seen = false;
                for (int j = 0; j < n; j++)
                {
                    if (unique[j] == raw[i])
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                    unique[n++] = raw[i];
            }

            return unique;
        }

        public static bool TryNext(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal habFace,
            int width,
            int height,
            BoundsOk bounds,
            out Vector2Int origin)
        {
            origin = default;
            if (placer == null) return false;
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            var faces = PreferFaces(habFace);
            var candidates = new List<Vector2Int>(32);
            CollectCandidates(commons, width, height, faces, candidates);

            var seen = new HashSet<long>();
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector2Int cell = candidates[i];
                long key = Pack(cell.x, cell.y);
                if (!seen.Add(key)) continue;
                if (!NearCommons(commons, cell, width, height)) continue;
                if (!placer.CanFitRect(cell, width, height)) continue;
                if (bounds != null && !bounds(cell, width, height)) continue;
                origin = cell;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Occupies pad / power / water / regolith in order so later yards see earlier footprints.
        /// Tests use this; GameLoop stamps visuals after <see cref="TryNext"/> + TryRestore.
        /// </summary>
        public static PackPlan Plan(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal habFace,
            BoundsOk bounds = null)
        {
            var plan = new PackPlan();
            if (placer == null) return plan;

            if (TryNext(placer, commons, habFace, PadSize, PadSize, bounds, out Vector2Int pad))
            {
                placer.MarkCampusRect(pad, PadSize, PadSize);
                placer.RegisterPiece(pad, PadSize, PadSize, BuildingCategory.LandingPad);
                plan.Pad = true;
                plan.PadOrigin = pad;
            }

            if (TryNext(placer, commons, habFace, YardSize, YardSize, bounds, out Vector2Int pwr))
            {
                placer.MarkCampusRect(pwr, YardSize, YardSize);
                placer.RegisterPiece(pwr, YardSize, YardSize, BuildingCategory.Power);
                plan.Power = true;
                plan.PowerOrigin = pwr;
            }

            if (TryNext(placer, commons, habFace, YardSize, YardSize, bounds, out Vector2Int water))
            {
                placer.MarkCampusRect(water, YardSize, YardSize);
                placer.RegisterPiece(water, YardSize, YardSize, BuildingCategory.Farm);
                plan.Water = true;
                plan.WaterOrigin = water;
            }

            if (TryNext(placer, commons, habFace, YardSize, YardSize, bounds, out Vector2Int reg))
            {
                placer.MarkCampusRect(reg, YardSize, YardSize);
                placer.RegisterPiece(reg, YardSize, YardSize, BuildingCategory.RegolithCamp);
                plan.Regolith = true;
                plan.RegolithOrigin = reg;
            }

            return plan;
        }

        public static Vector2Int FlushOrigin(
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal face,
            int width,
            int height,
            int gap = 0)
        {
            int cx = commons.Origin.x;
            int cy = commons.Origin.y;
            int cw = commons.Width;
            int ch = commons.Height;
            gap = Mathf.Max(0, gap);
            switch (face)
            {
                case BuildingPlacer.Cardinal.East:
                    return new Vector2Int(cx + cw + gap, cy + CenterOffset(ch, height));
                case BuildingPlacer.Cardinal.West:
                    return new Vector2Int(cx - width - gap, cy + CenterOffset(ch, height));
                case BuildingPlacer.Cardinal.North:
                    return new Vector2Int(cx + CenterOffset(cw, width), cy + ch + gap);
                default:
                    return new Vector2Int(cx + CenterOffset(cw, width), cy - height - gap);
            }
        }

        private static void CollectCandidates(
            BuildingPlacer.CampusPiece commons,
            int width,
            int height,
            BuildingPlacer.Cardinal[] faces,
            List<Vector2Int> dest)
        {
            int cx = commons.Origin.x;
            int cy = commons.Origin.y;
            int cw = commons.Width;
            int ch = commons.Height;

            // E/W/N flush first, then corners, South last — keep the still in CampusOrthoSize.
            AddFlushFaces(commons, width, height, faces, dest, skipSouth: true, gap: 0);

            dest.Add(new Vector2Int(cx + cw, cy + ch));
            dest.Add(new Vector2Int(cx - width, cy + ch));
            dest.Add(new Vector2Int(cx + cw, cy - height));
            dest.Add(new Vector2Int(cx - width, cy - height));

            dest.Add(FlushOrigin(commons, BuildingPlacer.Cardinal.South, width, height, 0));

            for (int gap = 1; gap <= 2; gap++)
            {
                AddFlushFaces(commons, width, height, faces, dest, skipSouth: true, gap: gap);
                dest.Add(FlushOrigin(commons, BuildingPlacer.Cardinal.South, width, height, gap));
            }

            int[] slides = { 1, -1, 2, -2 };
            AddSlides(commons, width, height, faces, dest, slides, skipSouth: true);
            AddSlides(commons, width, height, faces, dest, slides, skipSouth: false, onlySouth: true);
        }

        private static void AddFlushFaces(
            BuildingPlacer.CampusPiece commons,
            int width,
            int height,
            BuildingPlacer.Cardinal[] faces,
            List<Vector2Int> dest,
            bool skipSouth,
            int gap)
        {
            for (int i = 0; i < faces.Length; i++)
            {
                if (skipSouth && faces[i] == BuildingPlacer.Cardinal.South) continue;
                dest.Add(FlushOrigin(commons, faces[i], width, height, gap));
            }
        }

        private static void AddSlides(
            BuildingPlacer.CampusPiece commons,
            int width,
            int height,
            BuildingPlacer.Cardinal[] faces,
            List<Vector2Int> dest,
            int[] slides,
            bool skipSouth,
            bool onlySouth = false)
        {
            int cx = commons.Origin.x;
            int cy = commons.Origin.y;
            int cw = commons.Width;
            int ch = commons.Height;
            for (int i = 0; i < faces.Length; i++)
            {
                var face = faces[i];
                bool south = face == BuildingPlacer.Cardinal.South;
                if (skipSouth && south) continue;
                if (onlySouth && !south) continue;
                for (int s = 0; s < slides.Length; s++)
                {
                    int slide = slides[s];
                    switch (face)
                    {
                        case BuildingPlacer.Cardinal.East:
                            dest.Add(new Vector2Int(cx + cw, cy + CenterOffset(ch, height) + slide));
                            break;
                        case BuildingPlacer.Cardinal.West:
                            dest.Add(new Vector2Int(cx - width, cy + CenterOffset(ch, height) + slide));
                            break;
                        case BuildingPlacer.Cardinal.North:
                            dest.Add(new Vector2Int(cx + CenterOffset(cw, width) + slide, cy + ch));
                            break;
                        default:
                            dest.Add(new Vector2Int(cx + CenterOffset(cw, width) + slide, cy - height));
                            break;
                    }
                }
            }
        }

        private static bool NearCommons(
            BuildingPlacer.CampusPiece commons,
            Vector2Int origin,
            int width,
            int height)
        {
            float cx = commons.Origin.x + commons.Width * 0.5f;
            float cy = commons.Origin.y + commons.Height * 0.5f;
            float px = origin.x + width * 0.5f;
            float py = origin.y + height * 0.5f;
            float dx = px - cx;
            float dy = py - cy;
            return dx * dx + dy * dy <= MaxCenterSeparationCells * MaxCenterSeparationCells;
        }

        private static int CenterOffset(int parentSpan, int childSpan) =>
            (parentSpan - childSpan) / 2;

        private static long Pack(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
