using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Phase 4 CaptureStill campus: Commons→airlock→HAB plus pad / PWR-1 /
    /// water/regolith extractors when they <see cref="BuildingPlacer.CanFitRect"/>.
    /// Leftover Workshop / Inn / wonder may stamp when they CanFit — they are not
    /// required to max-pack the frame. Forward yards use CanFit only.
    /// Empty dirt is OK (spaced Majesty-2 overseer still). Do not fill every
    /// interior 4×4 or crop the AABB to chase retired VisualTarget density.
    /// Standalone yards keep <see cref="SpacedYardGapCells"/> of dirt around them, and the still
    /// ortho fits the campus plus headroom with <see cref="PlayCampusOrthoSize"/> as the floor.
    /// Does not flip spawnShowcaseColony. Does not reference Runtime ColonyLayout.
    /// </summary>
    public static class StillCampusDensity
    {
        public const int PadSize = 6;
        public const int YardSize = 4;

        /// <summary>
        /// Keep yards inside the campus ortho. still20 leftover Inn / wonder /
        /// extraSolar / Defense sit ~11 cells out after extra HAB + pad; 9
        /// dropped those sockets. Raised to 15 for <see cref="SpacedYardGapCells"/>: a spaced pad
        /// pushes its centre a gap further out, and the still ortho now fits the AABB rather than
        /// cropping it, so a wider spread costs nothing.
        /// </summary>
        public const float MaxCenterSeparationCells = 15f;

        /// <summary>
        /// CaptureStill Game-tab is short-wide (~2.4). Play snap stays
        /// <see cref="PlayCampusOrthoSize"/> (10) so the player can place yards.
        /// </summary>
        public const float GameTabAspect = 2.4f;

        /// <summary>
        /// Must match IsoGrid default so still AABB math uses the same meter grid.
        /// Lives here (Systems) — Runtime <c>ColonyLayout</c> is not visible to this assembly.
        /// </summary>
        public const float DefaultCellSize = 1.5f;

        /// <summary>
        /// Play snap (Runtime aliases this as ColonyLayout.CampusOrthoSize), and the **floor** for
        /// the still ortho — see <see cref="FitStillOrtho"/>. The still may pull back past this to
        /// keep the whole campus in frame; it may never come inside it.
        /// </summary>
        public const float PlayCampusOrthoSize = 10f;

        /// <summary>
        /// No AABB crop — empty dirt around the cluster stays in frame.
        /// Play snap stays <see cref="PlayCampusOrthoSize"/>.
        /// </summary>
        public const int StillFrameInsetCells = 0;

        /// <summary>Floor so a Commons-only AABB cannot punch through minZoom.</summary>
        public const float StillMinOrtho = 7.25f;

        /// <summary>
        /// How much wider than the campus AABB the still frames. The concept is mostly regolith
        /// with the campus sitting in it, so the fit gets headroom rather than hugging the yards.
        /// </summary>
        public const float StillDirtHeadroom = 1.35f;

        /// <summary>
        /// Gap in cells the still leaves around standalone yards — pad, power, extractors,
        /// Defense — so they read as separate pads with dirt between them the way the concept
        /// does. HAB chains still dock flush; those are tube-linked, not free-standing.
        /// </summary>
        public const int SpacedYardGapCells = 2;

        /// <summary>
        /// Planner cap only. CaptureStill does not force-fill interior dirt.
        /// </summary>
        public const int MaxInteriorHabSockets = 6;

        /// <summary>
        /// Ceiling so a runaway AABB cannot shrink the campus to a speck in the middle of a
        /// dirt field. Above this the still stops pulling back.
        /// </summary>
        public const float StillMaxOrtho = 26f;

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
            public bool ExtraHab;
            public bool ExtraSolar;
            public bool Defense;
            public string Leftover;

            public override string ToString() =>
                $"commons={Commons} airlock={Airlock} hab={Hab} " +
                $"pad={Pad} pwr={Power} water={Water} regolith={Regolith} " +
                $"workshop={Workshop} inn={Inn} wonder={Wonder} leftover={Leftover ?? "none"} " +
                $"extraHab={ExtraHab} extraSolar={ExtraSolar} defense={Defense}";

            public static StampLog FromPieces(BuildingPlacer placer, string leftover = null)
            {
                var log = new StampLog();
                if (placer == null)
                {
                    log.Leftover = leftover ?? "none";
                    return log;
                }

                var pieces = placer.Pieces;
                int habs = 0;
                int pwrs = 0;
                for (int i = 0; i < pieces.Count; i++)
                {
                    switch (pieces[i].Category)
                    {
                        case BuildingCategory.Commons: log.Commons = true; break;
                        case BuildingCategory.Utility:
                            log.Airlock = true;
                            break;
                        case BuildingCategory.Habitat:
                            habs++;
                            log.Hab = true;
                            break;
                        case BuildingCategory.LandingPad: log.Pad = true; break;
                        case BuildingCategory.Power:
                            pwrs++;
                            log.Power = true;
                            break;
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
                        case BuildingCategory.Defense: log.Defense = true; break;
                    }
                }

                log.ExtraHab = habs > 1;
                log.ExtraSolar = pwrs > 1;
                log.Leftover = leftover ?? LeftoverLabel(log);
                return log;
            }

            public static string LeftoverLabel(bool workshop, bool inn, bool wonder)
            {
                if (workshop && inn && wonder) return "workshop+inn+wonder";
                if (workshop && inn) return "workshop+inn";
                if (workshop && wonder) return "workshop+wonder";
                if (workshop) return "workshop";
                if (inn && wonder) return "inn+wonder";
                if (inn) return "inn";
                if (wonder) return "wonder";
                return "none";
            }

            private static string LeftoverLabel(StampLog log) =>
                LeftoverLabel(log.Workshop, log.Inn, log.Wonder);
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

        public struct CuePlan
        {
            public bool ExtraAirlock;
            public Vector2Int ExtraAirlockOrigin;
            public bool ExtraHab;
            public Vector2Int ExtraHabOrigin;
            public bool ExtraSolar;
            public Vector2Int ExtraSolarOrigin;
            public bool Defense;
            public Vector2Int DefenseOrigin;
            public bool HabSocket;
            public Vector2Int HabSocketOrigin;
            public int HabSocketCount;

            public int PlacedCount =>
                (ExtraAirlock ? 1 : 0) + (ExtraHab ? 1 : 0) + (ExtraSolar ? 1 : 0) +
                (Defense ? 1 : 0) + HabSocketCount;
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

        /// <summary>
        /// Remaining Commons cardinals for a second HAB chain. South first so the
        /// still19 empty-dirt apron fills; skip the live HAB face.
        /// </summary>
        public static BuildingPlacer.Cardinal[] ExtraHabFaces(BuildingPlacer.Cardinal habFace)
        {
            var raw = new[]
            {
                BuildingPlacer.Cardinal.South,
                BuildingPlacer.Cardinal.North,
                Opposite(habFace),
                habFace
            };
            var unique = new BuildingPlacer.Cardinal[4];
            int n = 0;
            for (int i = 0; i < raw.Length && n < 4; i++)
            {
                if (raw[i] == habFace) continue;
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

            var trimmed = new BuildingPlacer.Cardinal[n];
            for (int i = 0; i < n; i++)
                trimmed[i] = unique[i];
            return trimmed;
        }

        public static bool TryDockOnAirlock(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            int width,
            int height,
            BoundsOk bounds,
            out Vector2Int origin)
        {
            origin = default;
            if (placer == null) return false;
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            var pieces = placer.Pieces;
            if (pieces == null) return false;

            var faces = new[]
            {
                BuildingPlacer.Cardinal.North,
                BuildingPlacer.Cardinal.East,
                BuildingPlacer.Cardinal.West,
                BuildingPlacer.Cardinal.South
            };
            for (int i = 0; i < pieces.Count; i++)
            {
                if (!pieces[i].IsAirlock) continue;
                for (int f = 0; f < faces.Length; f++)
                {
                    Vector2Int cell = BuildingPlacer.ModuleOriginOnAirlockFace(
                        pieces[i], width, height, faces[f]);
                    if (!NearCommons(commons, cell, width, height)) continue;
                    if (!placer.CanFitRect(cell, width, height)) continue;
                    if (bounds != null && !bounds(cell, width, height)) continue;
                    origin = cell;
                    return true;
                }
            }

            return false;
        }

        public static bool TryDockOrNext(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal habFace,
            int width,
            int height,
            BoundsOk bounds,
            out Vector2Int origin)
        {
            if (TryDockOnAirlock(placer, commons, width, height, bounds, out origin))
                return true;
            return TryNext(placer, commons, habFace, width, height, bounds, out origin);
        }

        public static bool TryExtraHabChain(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal habFace,
            BoundsOk bounds,
            out Vector2Int airlock,
            out Vector2Int hab)
        {
            airlock = default;
            hab = default;
            if (placer == null) return false;

            var faces = ExtraHabFaces(habFace);
            for (int i = 0; i < faces.Length; i++)
            {
                BuildingPlacer.CardinalExpansionOrigins(
                    commons, faces[i], YardSize, YardSize, out Vector2Int a, out Vector2Int h);
                if (!placer.CanFitRect(a, 2, 2) || !placer.CanFitRect(h, YardSize, YardSize))
                    continue;
                if (bounds != null && (!bounds(a, 2, 2) || !bounds(h, YardSize, YardSize)))
                    continue;
                if (!NearCommons(commons, h, YardSize, YardSize))
                    continue;
                airlock = a;
                hab = h;
                return true;
            }

            return false;
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
            var candidates = new List<Vector2Int>(96);
            CollectCandidates(commons, width, height, faces, candidates);
            CollectAroundPieces(placer, width, height, candidates);
            CollectSpiral(commons, width, height, candidates);
            return FirstFit(placer, commons, candidates, width, height, bounds, out origin);
        }

        /// <summary>
        /// Free-standing yard placement: try the spaced ring first so the yard lands with dirt
        /// around it, and only fall back to flush docking if nothing spaced fits.
        ///
        /// Every candidate list in <see cref="CollectCandidates"/> leads with gap 0, so the still
        /// campus came out as one contiguous mass butted against the Commons. The concept's
        /// defining quality is six pads with open regolith between them.
        /// </summary>
        public static bool TryNextSpaced(
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
            var spaced = new List<Vector2Int>(96);
            for (int gap = SpacedYardGapCells; gap <= SpacedYardGapCells + 2; gap++)
            {
                AddFlushFaces(commons, width, height, faces, spaced, skipSouth: false, gap: gap);
                AddDiagonals(commons, width, height, spaced, gap);
            }

            var pieces = placer.Pieces;
            if (pieces != null)
            {
                var allFaces = new[]
                {
                    BuildingPlacer.Cardinal.East,
                    BuildingPlacer.Cardinal.West,
                    BuildingPlacer.Cardinal.North,
                    BuildingPlacer.Cardinal.South
                };
                for (int gap = SpacedYardGapCells; gap <= SpacedYardGapCells + 1; gap++)
                {
                    for (int i = 0; i < pieces.Count; i++)
                    {
                        AddFlushFaces(pieces[i], width, height, allFaces, spaced,
                            skipSouth: false, gap: gap);
                    }
                }
            }

            if (FirstFit(placer, commons, spaced, width, height, bounds, out origin))
                return true;
            return TryNext(placer, commons, habFace, width, height, bounds, out origin);
        }

        private static bool FirstFit(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            List<Vector2Int> candidates,
            int width,
            int height,
            BoundsOk bounds,
            out Vector2Int origin)
        {
            origin = default;
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

        private static void AddDiagonals(
            BuildingPlacer.CampusPiece anchor, int width, int height, List<Vector2Int> dest, int gap)
        {
            int ax = anchor.Origin.x;
            int ay = anchor.Origin.y;
            int aw = anchor.Width;
            int ah = anchor.Height;
            gap = Mathf.Max(0, gap);
            dest.Add(new Vector2Int(ax + aw + gap, ay + ah + gap));
            dest.Add(new Vector2Int(ax - width - gap, ay + ah + gap));
            dest.Add(new Vector2Int(ax + aw + gap, ay - height - gap));
            dest.Add(new Vector2Int(ax - width - gap, ay - height - gap));
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

            if (TryNextSpaced(placer, commons, habFace, PadSize, PadSize, bounds, out Vector2Int pad))
            {
                placer.MarkCampusRect(pad, PadSize, PadSize);
                placer.RegisterPiece(pad, PadSize, PadSize, BuildingCategory.LandingPad);
                plan.Pad = true;
                plan.PadOrigin = pad;
            }

            if (TryNextSpaced(placer, commons, habFace, YardSize, YardSize, bounds, out Vector2Int pwr))
            {
                placer.MarkCampusRect(pwr, YardSize, YardSize);
                placer.RegisterPiece(pwr, YardSize, YardSize, BuildingCategory.Power);
                plan.Power = true;
                plan.PowerOrigin = pwr;
            }

            if (TryNextSpaced(placer, commons, habFace, YardSize, YardSize, bounds, out Vector2Int water))
            {
                placer.MarkCampusRect(water, YardSize, YardSize);
                placer.RegisterPiece(water, YardSize, YardSize, BuildingCategory.Farm);
                plan.Water = true;
                plan.WaterOrigin = water;
            }

            if (TryNextSpaced(placer, commons, habFace, YardSize, YardSize, bounds, out Vector2Int reg))
            {
                placer.MarkCampusRect(reg, YardSize, YardSize);
                placer.RegisterPiece(reg, YardSize, YardSize, BuildingCategory.RegolithCamp);
                plan.Regolith = true;
                plan.RegolithOrigin = reg;
            }

            return plan;
        }

        /// <summary>
        /// Workshop hangar / Inn / one 6×6 wonder when they CanFit. Workshop and
        /// wonder prefer a free airlock face so the hub reads as a multi-face joint.
        /// Occupies like <see cref="Plan"/> so later leftover slots do not collide.
        /// still20 leftover=workshop: a second hangar must not eat the Inn / wonder
        /// sockets — skip workshop when one is already registered, then still place
        /// Village Inn and a wonder independently.
        /// </summary>
        public static LeftoverPlan PlanLeftovers(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal habFace,
            BoundsOk bounds = null,
            float cellSize = DefaultCellSize,
            float aspect = GameTabAspect)
        {
            var leftovers = new LeftoverPlan { SkipReason = "none" };
            if (placer == null) return leftovers;
            _ = cellSize;
            _ = aspect;

            bool canFitYard = CanFitNear(placer, commons, habFace, YardSize, bounds);
            bool canFitWonder = CanFitNear(placer, commons, habFace, PadSize, bounds);
            leftovers.Workshop = HasWorkshop(placer);

            // Inn + wonder first — still20 leftover=workshop ate those sockets.
            if (TryNextSpaced(placer, commons, habFace, YardSize, YardSize, bounds, out leftovers.InnOrigin))
            {
                placer.MarkCampusRect(leftovers.InnOrigin, YardSize, YardSize);
                placer.RegisterPiece(leftovers.InnOrigin, YardSize, YardSize, BuildingCategory.Inn);
                leftovers.Inn = true;
            }

            if (TryNextSpaced(placer, commons, habFace, PadSize, PadSize, bounds, out leftovers.WonderOrigin))
            {
                placer.MarkCampusRect(leftovers.WonderOrigin, PadSize, PadSize);
                placer.RegisterPiece(
                    leftovers.WonderOrigin, PadSize, PadSize, BuildingCategory.AegisSpire);
                leftovers.Wonder = true;
            }

            // Extra HAB packs are already tight — a leftover hangar yard would
            // steal extraSolar / Defense Battery (still20). Dock-or-next only
            // when the first HAB chain is the only housing.
            if (!leftovers.Workshop &&
                CountCategory(placer, BuildingCategory.Habitat) < 2 &&
                TryDockOrNext(placer, commons, habFace, YardSize, YardSize, bounds, out leftovers.WorkshopOrigin))
            {
                placer.MarkCampusRect(leftovers.WorkshopOrigin, YardSize, YardSize);
                placer.RegisterPiece(
                    leftovers.WorkshopOrigin, YardSize, YardSize, BuildingCategory.EngineerWorkshop);
                leftovers.Workshop = true;
            }

            leftovers.SkipReason = leftovers.PlacedCount > 0 || leftovers.Workshop
                ? StampLog.LeftoverLabel(leftovers.Workshop, leftovers.Inn, leftovers.Wonder)
                : (canFitYard || canFitWonder) ? "CanFit" : "none";
            return leftovers;
        }

        /// <summary>
        /// Extra HAB chain (new airlock + HAB), optional second solar bank and
        /// Defense Battery when they CanFit. Does <b>not</b> fill interior dirt
        /// with HAB sockets — empty ground is the spaced-overseer look.
        /// Occupies like <see cref="Plan"/>.
        /// </summary>
        public static CuePlan PlanCues(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            BuildingPlacer.Cardinal habFace,
            BoundsOk bounds = null)
        {
            var cues = new CuePlan();
            if (placer == null) return cues;

            // still20 extraHab=True already filled south — a third HAB chain
            // would steal extraSolar / Defense Battery sockets.
            if (CountCategory(placer, BuildingCategory.Habitat) < 2 &&
                TryExtraHabChain(placer, commons, habFace, bounds, out Vector2Int airlock, out Vector2Int hab))
            {
                placer.MarkCampusRect(airlock, 2, 2);
                placer.RegisterPiece(airlock, 2, 2, BuildingCategory.Utility);
                placer.MarkCampusRect(hab, YardSize, YardSize);
                placer.RegisterPiece(hab, YardSize, YardSize, BuildingCategory.Habitat);
                cues.ExtraAirlock = true;
                cues.ExtraAirlockOrigin = airlock;
                cues.ExtraHab = true;
                cues.ExtraHabOrigin = hab;
            }

            if (TryNextSpaced(placer, commons, habFace, YardSize, YardSize, bounds, out Vector2Int solar))
            {
                placer.MarkCampusRect(solar, YardSize, YardSize);
                placer.RegisterPiece(solar, YardSize, YardSize, BuildingCategory.Power);
                cues.ExtraSolar = true;
                cues.ExtraSolarOrigin = solar;
            }

            if (TryNextSpaced(placer, commons, habFace, YardSize, YardSize, bounds, out Vector2Int def))
            {
                placer.MarkCampusRect(def, YardSize, YardSize);
                placer.RegisterPiece(def, YardSize, YardSize, BuildingCategory.Defense);
                cues.Defense = true;
                cues.DefenseOrigin = def;
            }

            cues.HabSocketCount = 0;
            cues.HabSocket = false;
            return cues;
        }

        /// <summary>
        /// still21 empty dirt between pad and extractors: occupy 4×4 HAB sockets
        /// that sit entirely inside the current campus AABB. Does not grow the
        /// still frame. Prefers cells boxed in by existing yards.
        /// </summary>
        public static int FillInteriorSockets(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            int max,
            BoundsOk bounds,
            out Vector2Int firstOrigin)
        {
            firstOrigin = default;
            int n = 0;
            if (placer == null || max <= 0) return 0;

            while (n < max && TryInteriorSocket(placer, commons, bounds, out Vector2Int origin))
            {
                placer.MarkCampusRect(origin, YardSize, YardSize);
                placer.RegisterPiece(origin, YardSize, YardSize, BuildingCategory.Habitat);
                if (n == 0) firstOrigin = origin;
                n++;
            }

            return n;
        }

        /// <summary>
        /// Next 4×4 that CanFit inside the live AABB. Prefer cells with occupied
        /// neighbors (dirt between pad / extractors / HAB) over rim pockets.
        /// </summary>
        public static bool TryInteriorSocket(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece commons,
            BoundsOk bounds,
            out Vector2Int origin)
        {
            origin = default;
            if (placer == null || !TryCampusAabb(placer, out Vector2Int min, out Vector2Int max))
                return false;

            int bestScore = -1;
            float bestDist = float.MaxValue;
            Vector2Int best = default;
            Vector2 center = AabbCenterCells(min, max);

            for (int y = min.y; y <= max.y - YardSize; y++)
            {
                for (int x = min.x; x <= max.x - YardSize; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!placer.CanFitRect(cell, YardSize, YardSize)) continue;
                    if (bounds != null && !bounds(cell, YardSize, YardSize)) continue;
                    if (!NearCommons(commons, cell, YardSize, YardSize)) continue;
                    int score = EnclosedSides(placer, cell, YardSize, YardSize);
                    float px = cell.x + YardSize * 0.5f;
                    float py = cell.y + YardSize * 0.5f;
                    float dx = px - center.x;
                    float dy = py - center.y;
                    float dist = dx * dx + dy * dy;
                    if (score > bestScore || (score == bestScore && dist < bestDist))
                    {
                        bestScore = score;
                        bestDist = dist;
                        best = cell;
                    }
                }
            }

            if (bestScore < 0) return false;
            origin = best;
            return true;
        }

        /// <summary>
        /// True when an airlock already joins the pair (RefreshTubes docked arms).
        /// Tube-run dressing must skip these so it does not stack in the HAB gap.
        /// </summary>
        public static bool AreAirlockLinked(
            BuildingPlacer placer,
            BuildingPlacer.CampusPiece a,
            BuildingPlacer.CampusPiece b)
        {
            if (placer == null) return false;
            if (a.IsAirlock && ModuleDocksAirlock(b, a)) return true;
            if (b.IsAirlock && ModuleDocksAirlock(a, b)) return true;
            var pieces = placer.Pieces;
            if (pieces == null) return false;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (!pieces[i].IsAirlock) continue;
                if (ModuleDocksAirlock(a, pieces[i]) && ModuleDocksAirlock(b, pieces[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Cardinal neighbor with a short gap (still21 tube runs). Overlap keeps
        /// the corridor on the square grid; gap 0 is flush yards.
        /// </summary>
        public static bool TryCardinalNeighbor(
            BuildingPlacer.CampusPiece a,
            BuildingPlacer.CampusPiece b,
            int maxGapCells,
            out int gapCells,
            out bool eastWest)
        {
            gapCells = 0;
            eastWest = true;
            maxGapCells = Mathf.Max(0, maxGapCells);

            int ax0 = a.Origin.x, ax1 = a.Origin.x + a.Width;
            int ay0 = a.Origin.y, ay1 = a.Origin.y + a.Height;
            int bx0 = b.Origin.x, bx1 = b.Origin.x + b.Width;
            int by0 = b.Origin.y, by1 = b.Origin.y + b.Height;

            int overlapY = Mathf.Min(ay1, by1) - Mathf.Max(ay0, by0);
            int overlapX = Mathf.Min(ax1, bx1) - Mathf.Max(ax0, bx0);

            if (overlapY > 0)
            {
                int gap = bx0 >= ax1 ? bx0 - ax1 : ax0 >= bx1 ? ax0 - bx1 : int.MaxValue;
                if (gap <= maxGapCells)
                {
                    gapCells = gap;
                    eastWest = true;
                    return true;
                }
            }

            if (overlapX > 0)
            {
                int gap = by0 >= ay1 ? by0 - ay1 : ay0 >= by1 ? ay0 - by1 : int.MaxValue;
                if (gap <= maxGapCells)
                {
                    gapCells = gap;
                    eastWest = false;
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// Still frame is the live campus AABB. No dirt crop — <see cref="FitStillOrtho"/> adds
        /// headroom around it so empty ground stays in shot.
        /// </summary>
        public static bool TryStillFrameAabb(
            BuildingPlacer placer, out Vector2Int min, out Vector2Int maxExclusive)
        {
            if (!TryCampusAabb(placer, out min, out maxExclusive))
                return false;
            InsetAabb(ref min, ref maxExclusive, StillFrameInsetCells);
            return true;
        }

        public static void InsetAabb(ref Vector2Int min, ref Vector2Int maxExclusive, int inset)
        {
            inset = Mathf.Max(0, inset);
            if (maxExclusive.x - min.x > inset * 2 + 4)
            {
                min.x += inset;
                maxExclusive.x -= inset;
            }

            if (maxExclusive.y - min.y > inset * 2 + 4)
            {
                min.y += inset;
                maxExclusive.y -= inset;
            }
        }

        public static Vector2 AabbCenterCells(Vector2Int min, Vector2Int maxExclusive) =>
            new Vector2((min.x + maxExclusive.x) * 0.5f, (min.y + maxExclusive.y) * 0.5f);

        /// <summary>
        /// Spaced overseer still: frame the whole campus with regolith around it.
        ///
        /// This used to return <see cref="PlayCampusOrthoSize"/> outright, which inverted its own
        /// intent. The rule is "do not zoom **in** to pack the AABB" — but pinning the ortho while
        /// the campus grows past 20 m of frame does not preserve empty dirt, it crops the campus
        /// and squeezes every last pixel of dirt out. The Capture came back a close-up of the
        /// Commons dome with the pad and rocket falling off the edge behind the HUD.
        ///
        /// So: fit the AABB, add <see cref="StillDirtHeadroom"/> so ground frames it, and floor at
        /// the play ortho. The floor is what actually enforces the rule — the still can never be
        /// tighter than what the player sees, only wider.
        /// </summary>
        public static float FitStillOrtho(
            Vector2Int min,
            Vector2Int maxExclusive,
            float cellSize,
            float aspect)
        {
            HalfExtents(min, maxExclusive, cellSize, out float halfX, out float halfZ);
            float fit = RawStillOrtho(halfX, halfZ, aspect) * StillDirtHeadroom;
            return Mathf.Clamp(fit, PlayCampusOrthoSize, StillMaxOrtho);
        }

        public static float FitStillOrtho(BuildingPlacer placer, float cellSize, float aspect)
        {
            if (!TryStillFrameAabb(placer, out Vector2Int min, out Vector2Int maxExclusive))
                return PlayCampusOrthoSize;
            return FitStillOrtho(min, maxExclusive, cellSize, aspect);
        }

        /// <summary>Unclamped iso fit — tests use this to describe leftover AABB growth.</summary>
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

            int[] slides = { 1, -1, 2, -2, 3, -3 };
            AddSlides(commons, width, height, faces, dest, slides, skipSouth: true);
            AddSlides(commons, width, height, faces, dest, slides, skipSouth: false, onlySouth: true);
        }

        /// <summary>
        /// Pockets flush to already-stamped yards (HAB / pad / workshop) so leftover
        /// Inn / wonder / extra solar / Defense stay inside the packed AABB.
        /// Appended after Commons-first candidates so <see cref="Plan"/> origins stay put.
        /// </summary>
        private static void CollectAroundPieces(
            BuildingPlacer placer, int width, int height, List<Vector2Int> dest)
        {
            if (placer?.Pieces == null || dest == null) return;
            var faces = new[]
            {
                BuildingPlacer.Cardinal.East,
                BuildingPlacer.Cardinal.West,
                BuildingPlacer.Cardinal.North,
                BuildingPlacer.Cardinal.South
            };
            var pieces = placer.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                CollectCandidates(piece, width, height, faces, dest);
            }
        }

        /// <summary>Last-resort packed ring so a CanFit leftover is not missed.</summary>
        private static void CollectSpiral(
            BuildingPlacer.CampusPiece commons, int width, int height, List<Vector2Int> dest)
        {
            int ox = commons.Origin.x + CenterOffset(commons.Width, width);
            int oy = commons.Origin.y + CenterOffset(commons.Height, height);
            for (int r = 3; r <= 8; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    dest.Add(new Vector2Int(ox + dx, oy + r));
                    dest.Add(new Vector2Int(ox + dx, oy - r));
                }

                for (int dy = -r + 1; dy <= r - 1; dy++)
                {
                    dest.Add(new Vector2Int(ox + r, oy + dy));
                    dest.Add(new Vector2Int(ox - r, oy + dy));
                }
            }
        }

        public static bool HasWorkshop(BuildingPlacer placer) =>
            CountCategory(placer, BuildingCategory.EngineerWorkshop) > 0 ||
            CountCategory(placer, BuildingCategory.ScoutWorkshop) > 0 ||
            CountCategory(placer, BuildingCategory.DefenseWorkshop) > 0;

        public static int CountCategory(BuildingPlacer placer, BuildingCategory cat)
        {
            if (placer?.Pieces == null) return 0;
            int n = 0;
            var pieces = placer.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Category == cat)
                    n++;
            }

            return n;
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

        private static int EnclosedSides(
            BuildingPlacer placer, Vector2Int origin, int width, int height)
        {
            int n = 0;
            if (EdgeOccupied(placer, origin.x - 1, origin.y, 1, height)) n++;
            if (EdgeOccupied(placer, origin.x + width, origin.y, 1, height)) n++;
            if (EdgeOccupied(placer, origin.x, origin.y - 1, width, 1)) n++;
            if (EdgeOccupied(placer, origin.x, origin.y + height, width, 1)) n++;
            return n;
        }

        private static bool EdgeOccupied(
            BuildingPlacer placer, int x, int y, int width, int height)
        {
            for (int dx = 0; dx < width; dx++)
            {
                for (int dy = 0; dy < height; dy++)
                {
                    if (placer.IsCellOccupied(new Vector2Int(x + dx, y + dy)))
                        return true;
                }
            }

            return false;
        }

        private static bool ModuleDocksAirlock(
            BuildingPlacer.CampusPiece module, BuildingPlacer.CampusPiece airlock)
        {
            if (!airlock.IsAirlock || !module.IsModule) return false;
            for (int f = 0; f < 4; f++)
            {
                if (BuildingPlacer.AirlockOriginOnModuleFace(
                        module, (BuildingPlacer.Cardinal)f) == airlock.Origin)
                    return true;
            }

            return false;
        }

        private static int CenterOffset(int parentSpan, int childSpan) =>
            (parentSpan - childSpan) / 2;

        private static long Pack(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
