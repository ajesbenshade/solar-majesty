// W2 title-match + mid-act cut gates. Pure C#. Copy stays in the catalogs.

using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>Campus facts used only to disambiguate multi-Build titles. Not a quest graph.</summary>
    public struct NarrativeWorldHint
    {
        public bool HasCommons;
        public bool HasHab;
        public bool HasPad;
        public bool HasPower;
        /// <summary>Landing Pad footprint on campus (construction or complete).</summary>
        public bool HasPadPiece;
        /// <summary>Live Landing Pad construction order — the Stage Rocket Build target.</summary>
        public bool HasPadOrder;
        /// <summary>Live workshop construction / refab. Must not steal the rocket toast.</summary>
        public bool HasWorkshopOrder;
        /// <summary>Body launch tech is researching or unlocked.</summary>
        public bool LaunchPathLive;
    }

    /// <summary>
    /// Fire-once advisor keys and mid-act cut gates.
    /// Title + body is the posted-flag hook; civic notes fill gaps the player never posts
    /// (auto-drop Commons, Guild Charter tech, craft staged).
    /// </summary>
    public sealed class NarrativeBeatTracker
    {
        private readonly HashSet<string> _firedKeys = new HashSet<string>();
        private readonly HashSet<string> _completed = new HashSet<string>();
        private readonly HashSet<string> _claimed = new HashSet<string>();
        private readonly HashSet<string> _shownCuts = new HashSet<string>();
        private readonly List<string> _pendingCuts = new List<string>(4);

        public IReadOnlyCollection<string> CompletedDecrees => _completed;
        public int PendingCutCount => _pendingCuts.Count;

        public void RememberShownCut(string id)
        {
            if (!string.IsNullOrEmpty(id))
                _shownCuts.Add(id);
        }

        public bool WasCutShown(string id) =>
            !string.IsNullOrEmpty(id) && _shownCuts.Contains(id);

        public bool TryConsumeCut(out string id)
        {
            if (_pendingCuts.Count == 0)
            {
                id = null;
                return false;
            }

            id = _pendingCuts[0];
            _pendingCuts.RemoveAt(0);
            return !string.IsNullOrEmpty(id);
        }

        public bool PeekCut(out string id)
        {
            if (_pendingCuts.Count == 0)
            {
                id = null;
                return false;
            }

            id = _pendingCuts[0];
            return !string.IsNullOrEmpty(id);
        }

        public void DismissCut()
        {
            if (_pendingCuts.Count > 0)
            {
                RememberShownCut(_pendingCuts[0]);
                _pendingCuts.RemoveAt(0);
            }
        }

        public void ClearPendingCuts() => _pendingCuts.Clear();

        public bool EnqueueCut(string id)
        {
            if (StillCaptureHold.Active) return false;
            if (string.IsNullOrEmpty(id) || _shownCuts.Contains(id))
                return false;
            for (int i = 0; i < _pendingCuts.Count; i++)
            {
                if (_pendingCuts[i] == id)
                    return false;
            }

            if (!CampaignCutsceneCatalog.TryGet(id, out _))
                return false;

            _pendingCuts.Add(id);
            return true;
        }

        /// <summary>
        /// Resolve a posted flag to a decree: stamped title, then unique type, then Build context.
        /// Stamps <see cref="FlagHandle.Title"/> when a decree hits so claim/complete reuse it.
        /// </summary>
        public static bool TryResolvePosted(
            FlagHandle handle,
            CelestialBodyId body,
            NarrativeWorldHint hint,
            out FlagDecree decree)
        {
            decree = default;
            if (handle?.Data == null) return false;

            string title = !string.IsNullOrEmpty(handle.Title) ? handle.Title : handle.Data.displayName;
            if (FlagDecreeIds.TryMatchPosted(handle.Data.flagType, title, body, out decree))
            {
                handle.Title = decree.Title;
                return true;
            }

            if (handle.Data.flagType == FlagType.Build &&
                TryInferBuild(body, hint, out string id) &&
                FlagDecreeIds.TryGet(id, out decree))
            {
                handle.Title = decree.Title;
                return true;
            }

            return false;
        }

        public static bool TryInferBuild(CelestialBodyId body, NarrativeWorldHint hint, out string id)
        {
            switch (body)
            {
                case CelestialBodyId.Earth:
                    if (!hint.HasCommons)
                    {
                        id = FlagDecreeIds.EarthRaiseTheCommons;
                        return true;
                    }
                    if (!hint.HasHab)
                    {
                        id = FlagDecreeIds.EarthDockTheFirstHab;
                        return true;
                    }
                    // Commons → HAB → rocket only when pad labour or the launch path is live.
                    // Untitled workshop / other Builds stay unmatched until a title stamps.
                    if (EarthRocketBuildLive(hint))
                    {
                        id = FlagDecreeIds.EarthStageTheLunarRocket;
                        return true;
                    }
                    id = null;
                    return false;
                case CelestialBodyId.Luna:
                    if (!hint.HasCommons)
                    {
                        id = FlagDecreeIds.LunaRaiseTheCraterCommons;
                        return true;
                    }
                    id = FlagDecreeIds.LunaFortifyTheAirlocks;
                    return true;
                case CelestialBodyId.Mars:
                    if (!hint.HasCommons)
                    {
                        id = FlagDecreeIds.MarsRaiseTheCompactSeat;
                        return true;
                    }
                    id = FlagDecreeIds.MarsStringTheSolarField;
                    return true;
                default:
                    id = null;
                    return false;
            }
        }

        /// <summary>
        /// Pad construction (order or unfinished piece) is the Stage Rocket Build.
        /// Launch tech in flight / unlocked tees the same toast only when no pad stands
        /// and a workshop order is not the live target.
        /// </summary>
        public static bool EarthRocketBuildLive(NarrativeWorldHint hint)
        {
            bool padLabour = hint.HasPadOrder || (hint.HasPadPiece && !hint.HasPad);
            if (padLabour)
                return true;
            if (hint.HasWorkshopOrder)
                return false;
            return hint.LaunchPathLive && !hint.HasPad;
        }

        public bool TryTakePostToast(string decreeId, out AdvisorToast toast)
        {
            toast = default;
            if (!AdvisorToastCatalog.TryGetPrimary(decreeId, out toast))
                return false;
            if (toast.When != AdvisorFireWhen.Post)
                return false;
            return TryFire(toast.Key);
        }

        public bool TryTakeClaimToast(string decreeId, out AdvisorToast toast)
        {
            toast = default;
            if (string.IsNullOrEmpty(decreeId)) return false;
            _claimed.Add(decreeId);
            if (!AdvisorToastCatalog.TryGetPrimary(decreeId, out toast))
                return false;
            if (toast.When != AdvisorFireWhen.Claim)
                return false;
            return TryFire(toast.Key);
        }

        public bool TryTakeClaimAside(string decreeId, out string line)
        {
            line = null;
            if (!AdvisorToastCatalog.TryGetClaimAside(decreeId, out line))
                return false;
            return TryFire("aside." + decreeId);
        }

        public bool TryTakeCompleteToast(string key, out AdvisorToast toast)
        {
            toast = default;
            if (!AdvisorToastCatalog.TryGetComplete(key, out toast))
                return false;
            return TryFire(toast.Key);
        }

        public bool TryTakeTravelToast(string key, out AdvisorToast toast)
        {
            toast = default;
            if (!AdvisorToastCatalog.TryGetTravel(key, out toast))
                return false;
            return TryFire(toast.Key);
        }

        public bool TryTakeGreedToast(out AdvisorToast toast)
        {
            return TryTakeTravelToast(AdvisorToastCatalog.LessonEarthGreedBuild, out toast);
        }

        public void NoteCompleted(string decreeId)
        {
            if (string.IsNullOrEmpty(decreeId)) return;
            if (!_completed.Add(decreeId)) return;
            QueueMidCuts();
        }

        public void NoteClaimed(string decreeId)
        {
            if (string.IsNullOrEmpty(decreeId)) return;
            _claimed.Add(decreeId);
            QueueMidCuts();
        }

        public void SyncCivic(CelestialBodyId body, NarrativeWorldHint hint, bool guildCharter, bool launchStaged)
        {
            if (body == CelestialBodyId.Earth)
            {
                if (hint.HasCommons) NoteCompleted(FlagDecreeIds.EarthRaiseTheCommons);
                if (hint.HasHab) NoteCompleted(FlagDecreeIds.EarthDockTheFirstHab);
                if (guildCharter) NoteCompleted(FlagDecreeIds.EarthCharterTheHall);
                if (launchStaged) NoteCompleted(FlagDecreeIds.EarthStageTheLunarRocket);
            }
            else if (body == CelestialBodyId.Luna)
            {
                if (hint.HasCommons) NoteCompleted(FlagDecreeIds.LunaRaiseTheCraterCommons);
                if (launchStaged) NoteCompleted(FlagDecreeIds.LunaCommissionTheMarsShip);
            }
            else if (body == CelestialBodyId.Mars)
            {
                if (hint.HasCommons) NoteCompleted(FlagDecreeIds.MarsRaiseTheCompactSeat);
                if (hint.HasPower) NoteCompleted(FlagDecreeIds.MarsStringTheSolarField);
                if (launchStaged) NoteCompleted(FlagDecreeIds.MarsCommissionTheBeltHauler);
            }
        }

        private bool TryFire(string key)
        {
            if (string.IsNullOrEmpty(key) || _firedKeys.Contains(key))
                return false;
            _firedKeys.Add(key);
            return true;
        }

        private void QueueMidCuts()
        {
            if (_completed.Contains(FlagDecreeIds.EarthRaiseTheCommons) &&
                _completed.Contains(FlagDecreeIds.EarthDockTheFirstHab) &&
                _completed.Contains(FlagDecreeIds.EarthCharterTheHall))
            {
                EnqueueCut(CampaignCutsceneCatalog.EarthMidCourt);
            }

            // First hit: levy complete or hopper warrant claimed (stakes checklist).
            if (_completed.Contains(FlagDecreeIds.LunaWeighTheFreeholdOre) ||
                _claimed.Contains(FlagDecreeIds.LunaRootTheHopperSaboteurs) ||
                _completed.Contains(FlagDecreeIds.LunaRootTheHopperSaboteurs))
            {
                EnqueueCut(CampaignCutsceneCatalog.LunaMidWarrant);
            }

            if (_completed.Contains(FlagDecreeIds.MarsStringTheSolarField) ||
                _completed.Contains(FlagDecreeIds.MarsHuntTheWisps) ||
                _completed.Contains(FlagDecreeIds.MarsWeaveTheCrust))
            {
                EnqueueCut(CampaignCutsceneCatalog.MarsMidCrust);
            }
        }
    }
}
