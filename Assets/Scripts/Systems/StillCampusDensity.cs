using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Phase 4 CaptureStill density pack: pad + PWR-1 + water/regolith extractors
    /// around Commons→airlock→HAB. Forward yards use <see cref="BuildingPlacer.CanFitRect"/>
    /// only (same as the still HAB stamp) — ExtraPlacementRule would send pad/solar/extract
    /// to Campus B (~30 m) or demand an airlock dock, both of which miss CampusOrthoSize ~10.
    /// still18 signed that density gate; leftover Workshop / Inn / 6×6 wonders CanFit
    /// south of Commons but grow the AABB past <see cref="StillMaxOrtho"/>, so they stay
    /// off the still pack (see <see cref="PlanLeftovers"/>). Does not flip spawnShowcaseColony.
    /// </summary>
    public static class StillCampusDensity
    {
        public const int PadSize = 6;
        public const int YardSize = 4;

        /// <summary>Keep yards inside the campus ortho: ~9 cells × 1.5 m ≈ 13.5 m from Commons.</summary>
        public const float MaxCenterSeparationCells = 9f;

        /// <summary>
        /// CaptureStill Game-tab is short-wide (~2.4). Play snap stays
        /// <see cref="ColonyLayout.CampusOrthoSize"/> (10) so the player can place yards.
        /// </summary>
        public const float GameTabAspect = 2.4f;

        /// <summary>Floor so a Commons-only AABB cannot punch through minZoom.</summary>
        public const float StillMinOrtho = 7.25f;

        /// <summary>
        /// Ceiling for the still snap — tighter than play ortho 10 so still18-class
        /// dirt margins close. Leftover 4×4 / 6×6 yards that push RawStillOrtho
        /// above this are skipped (south of Commons grows the AABB).
        /// </summary>
        public const float StillMaxOrtho = 9f;

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
            public bool Workshop;
            public bool Inn;
            public bool Wonder;
            public string Leftover;

            public override string ToString() =>
                $"commons={Commons} airlock={Airlock} hab={Hab} " +
                $"pad={Pad} pwr={Power} water={Water} regolith={Regolith} " +
                $"workshop={Workshop} inn={Inn} wonder={Wonder} leftover={Leftover ?? "none"}";

            public static StampLog FromPieces(BuildingPlacer placer, string leftover = null)
            {
                var log = new StampLog();
                if (placer == null)
                {
                    log.Leftover = leftover ?? "none";
                    return log;
                }

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
                        case BuildingCategory.EngineerWorkshop:
                        case BuildingCategory.ScoutWorkshop:
                        case BuildingCategory.DefenseWorkshop:
                            log.Workshop = true; break;
                        case BuildingCategory.Inn: log.Inn = true; break;
                        case BuildingCategory.ClimateLoom:
                        case BuildingCategory.AegisSpire:
                        case BuildingCategory.DeepArchive:
                            log.Wonder = true; break;
                    }
                }

                log.Leftover = leftover ?? LeftoverLabel(log);
                return log;
            }

            private static string LeftoverLabel(StampLog log)
            {
                if (log.Workshop && log.Inn && log.Wonder) return "workshop+inn+wonder";
                if (log.Workshop && log.Inn) return "workshop+inn";
                if (log.Workshop && log.Wonder) return "workshop+wonder";
                if (log.Workshop) return "workshop";
                if (log.Inn && log.Wonder) return "inn+wonder";
                if (log.Inn) return "inn";
                if (log.Wonder) return "wonder";
                return "none";
            }
        }

        public struct LeftoverPlan
        {
            public bool Workshop;
            public Vector2Int WorkshopOrigin;
            public bool Inn;
            public Vector2Int InnOrigin;
            public bool Wonder;
            public Vector2Int WonderOrigin;
            public string SkipReason;

            public int PlacedCount =>
                (Workshop ? 1 : 0) + (Inn ? 1 : 0) + (Wonder ? 1 : 0);
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

        /// <summary>
        /// Workshop hangar / Inn / one 6×6 wonder only when they CanFit AND the
        /// still-fit ortho stays ≤ <see cref="StillMaxOrtho"/>. still18-class packs
        /// already fill that budget — leftover yards sit south of Commons and
        /// re-open empty-dirt framing if we zoom back out to hold them.
        /// </summary>
        public static LeftoverPlan PlanLeftovers(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal habFace,
            BoundsOk bounds = null,
            float cellSize = ColonyLayout.DefaultCellSize,
            float aspect = GameTabAspect)
        {
            var leftovers = new LeftoverPlan { SkipReason = "none" };
            if (placer == null) return leftovers;

            // Dry — do not Mark/Register. GameLoop InstantStamps only when a slot
            // passes the frame gate. still18-class packs skip every leftover.
            bool canFitYard = CanFitNear(placer, commons, habFace, YardSize, bounds);
            bool canFitWonder = CanFitNear(placer, commons, habFace, PadSize, bounds);

            leftovers.Workshop = TryLeftoverSlot(
                placer, commons, habFace, YardSize, bounds, cellSize, aspect, out leftovers.WorkshopOrigin);
            leftovers.Inn = TryLeftoverSlot(
                placer, commons, habFace, YardSize, bounds, cellSize, aspect, out leftovers.InnOrigin);
            leftovers.Wonder = TryLeftoverSlot(
                placer, commons, habFace, PadSize, bounds, cellSize, aspect, out leftovers.WonderOrigin);

            if (leftovers.PlacedCount == 0)
                leftovers.SkipReason = (canFitYard || canFitWonder) ? "skip-frame" : "CanFit";
            return leftovers;
        }

        /// <summary>Inclusive min / exclusive max cell of every registered piece.</summary>
        public static bool TryCampusAabb(
            BuildingPlacer placer, out Vector2Int min, out Vector2Int maxExclusive)
        {
            min = default;
            maxExclusive = default;
            if (placer == null || placer.Pieces == null || placer.Pieces.Count == 0)
                return false;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            var pieces = placer.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                minX = Mathf.Min(minX, p.Origin.x);
                minY = Mathf.Min(minY, p.Origin.y);
                maxX = Mathf.Max(maxX, p.Origin.x + p.Width);
                maxY = Mathf.Max(maxY, p.Origin.y + p.Height);
            }

            min = new Vector2Int(minX, minY);
            maxExclusive = new Vector2Int(maxX, maxY);
            return true;
        }

        public static Vector2 AabbCenterCells(Vector2Int min, Vector2Int maxExclusive) =>
            new Vector2((min.x + maxExclusive.x) * 0.5f, (min.y + maxExclusive.y) * 0.5f);

        /// <summary>
        /// still18 at play ortho 10 left a ~27×15 m pack in a ~48×20 m Game tab.
        /// Fit the iso 30°/45° ground AABB with a tight pad; clamp so leftovers
        /// cannot yank the shutter back to empty-drop dirt.
        /// </summary>
        public static float FitStillOrtho(
            Vector2Int min,
            Vector2Int maxExclusive,
            float cellSize,
            float aspect)
        {
            HalfExtents(min, maxExclusive, cellSize, out float halfX, out float halfZ);
            return Mathf.Clamp(RawStillOrtho(halfX, halfZ, aspect), StillMinOrtho, StillMaxOrtho);
        }

        public static float FitStillOrtho(BuildingPlacer placer, float cellSize, float aspect)
        {
            if (!TryCampusAabb(placer, out Vector2Int min, out Vector2Int max))
                return ColonyLayout.CampusOrthoSize;
            return FitStillOrtho(min, max, cellSize, aspect);
        }

        /// <summary>Unclamped iso fit — leftover gate uses this so a south 6×6 cannot sneak in.</summary>
        public static float RawStillOrtho(
            Vector2Int min,
            Vector2Int maxExclusive,
            float cellSize,
            float aspect)
        {
            HalfExtents(min, maxExclusive, cellSize, out float halfX, out float halfZ);
            return RawStillOrtho(halfX, halfZ, aspect);
        }

        public static float RawStillOrthoIfAdded(
            BuildingPlacer placer,
            Vector2Int origin,
            int width,
            int height,
            float cellSize,
            float aspect)
        {
            if (!TryCampusAabb(placer, out Vector2Int min, out Vector2Int max))
            {
                min = origin;
                max = origin + new Vector2Int(Mathf.Max(1, width), Mathf.Max(1, height));
            }
            else
            {
                min = new Vector2Int(Mathf.Min(min.x, origin.x), Mathf.Min(min.y, origin.y));
                max = new Vector2Int(
                    Mathf.Max(max.x, origin.x + Mathf.Max(1, width)),
                    Mathf.Max(max.y, origin.y + Mathf.Max(1, height)));
            }

            return RawStillOrtho(min, max, cellSize, aspect);
        }

        public static float RawStillOrtho(float halfX, float halfZ, float aspect)
        {
            // Unity iso (30, 45, 0): camera.up.xz ≈ 0.35, camera.right.xz ≈ 0.71.
            const float isoUp = 0.36f;
            const float isoRight = 0.707f;
            const float pad = 0.85f;
            float byHeight = isoUp * (halfX + halfZ) + pad;
            aspect = Mathf.Max(1.05f, aspect);
            float byWidth = (isoRight * (halfX + halfZ) + pad) / aspect;
            return Mathf.Max(byHeight, byWidth);
        }

        public static Vector3 FocusWorld(Vector2Int min, Vector2Int maxExclusive, float cellSize)
        {
            Vector2 c = AabbCenterCells(min, maxExclusive);
            return new Vector3((c.x - 0.5f) * cellSize, 0f, (c.y - 0.5f) * cellSize);
        }

        private static void HalfExtents(
            Vector2Int min, Vector2Int maxExclusive, float cellSize, out float halfX, out float halfZ)
        {
            cellSize = Mathf.Max(0.01f, cellSize);
            halfX = Mathf.Max(1, maxExclusive.x - min.x) * cellSize * 0.5f;
            halfZ = Mathf.Max(1, maxExclusive.y - min.y) * cellSize * 0.5f;
        }

        private static bool TryLeftoverSlot(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal habFace,
            int side,
            BoundsOk bounds,
            float cellSize,
            float aspect,
            out Vector2Int origin)
        {
            origin = default;
            if (!TryNext(placer, commons, habFace, side, side, bounds, out origin))
                return false;
            float raw = RawStillOrthoIfAdded(placer, origin, side, side, cellSize, aspect);
            return raw <= StillMaxOrtho;
        }

        private static bool CanFitNear(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal habFace,
            int side,
            BoundsOk bounds)
        {
            return TryNext(placer, commons, habFace, side, side, bounds, out _);
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
