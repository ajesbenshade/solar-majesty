using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class FlagDecreeTitleMatchTests
    {
        [Test]
        public void TryMatchTitle_HitsBodyScopedTitle()
        {
            Assert.IsTrue(FlagDecreeIds.TryMatchTitle("Raise the Commons", CelestialBodyId.Earth, out var d));
            Assert.AreEqual(FlagDecreeIds.EarthRaiseTheCommons, d.Id);
            Assert.AreEqual(FlagType.Build, d.Type);
        }

        [Test]
        public void TryMatchTitle_IsCaseInsensitiveAndTrimmed()
        {
            Assert.IsTrue(FlagDecreeIds.TryMatchTitle("  root the hopper saboteurs  ", CelestialBodyId.Luna, out var d));
            Assert.AreEqual(FlagDecreeIds.LunaRootTheHopperSaboteurs, d.Id);
        }

        [Test]
        public void TryMatchTitle_DoesNotCrossBodies()
        {
            Assert.IsFalse(FlagDecreeIds.TryMatchTitle("Raise the Commons", CelestialBodyId.Luna, out _));
            Assert.IsFalse(FlagDecreeIds.TryMatchTitle("Raise the Compact Seat", CelestialBodyId.Earth, out _));
        }

        [Test]
        public void TryMatchPosted_UsesTitleBeforeUniqueType()
        {
            Assert.IsTrue(FlagDecreeIds.TryMatchPosted(
                FlagType.Build, "Dock the First HAB", CelestialBodyId.Earth, out var d));
            Assert.AreEqual(FlagDecreeIds.EarthDockTheFirstHab, d.Id);
        }

        [Test]
        public void TryMatchPosted_UniqueTypeWithoutTitle()
        {
            Assert.IsTrue(FlagDecreeIds.TryMatchPosted(FlagType.Explore, "Explore", CelestialBodyId.Earth, out var d));
            Assert.AreEqual(FlagDecreeIds.EarthSurveyTheClaim, d.Id);
        }

        [Test]
        public void TryMatchPosted_AmbiguousBuildWithoutTitle_Fails()
        {
            Assert.IsFalse(FlagDecreeIds.TryMatchPosted(FlagType.Build, "Build", CelestialBodyId.Earth, out _));
        }
    }

    public class CampaignCutsceneCatalogTests
    {
        [Test]
        public void Catalog_HasNineStableKeys()
        {
            Assert.AreEqual(9, CampaignCutsceneCatalog.All.Count);
            string[] keys =
            {
                CampaignCutsceneCatalog.EarthPrologue,
                CampaignCutsceneCatalog.EarthMidCourt,
                CampaignCutsceneCatalog.EarthToLuna,
                CampaignCutsceneCatalog.LunaArrival,
                CampaignCutsceneCatalog.LunaMidWarrant,
                CampaignCutsceneCatalog.LunaToMars,
                CampaignCutsceneCatalog.MarsArrival,
                CampaignCutsceneCatalog.MarsMidCrust,
                CampaignCutsceneCatalog.MarsBeltNamed
            };
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.IsTrue(CampaignCutsceneCatalog.TryGet(keys[i], out var cut), keys[i]);
                Assert.IsTrue(seen.Add(cut.Id));
                Assert.IsFalse(string.IsNullOrEmpty(cut.Title));
                Assert.GreaterOrEqual(cut.Body.Length, 3);
            }
        }

        [Test]
        public void ArrivalAndVictory_MapW2BodiesOnly()
        {
            Assert.AreEqual(CampaignCutsceneCatalog.EarthPrologue, CampaignCutsceneCatalog.ArrivalKey(CelestialBodyId.Earth));
            Assert.AreEqual(CampaignCutsceneCatalog.LunaArrival, CampaignCutsceneCatalog.ArrivalKey(CelestialBodyId.Luna));
            Assert.AreEqual(CampaignCutsceneCatalog.MarsArrival, CampaignCutsceneCatalog.ArrivalKey(CelestialBodyId.Mars));
            Assert.IsNull(CampaignCutsceneCatalog.ArrivalKey(CelestialBodyId.Belt));
            Assert.IsNull(CampaignCutsceneCatalog.ArrivalKey(CelestialBodyId.Europa));

            Assert.AreEqual(CampaignCutsceneCatalog.EarthToLuna, CampaignCutsceneCatalog.VictoryKey(CelestialBodyId.Earth));
            Assert.AreEqual(CampaignCutsceneCatalog.LunaToMars, CampaignCutsceneCatalog.VictoryKey(CelestialBodyId.Luna));
            Assert.AreEqual(CampaignCutsceneCatalog.MarsBeltNamed, CampaignCutsceneCatalog.VictoryKey(CelestialBodyId.Mars));
            Assert.IsNull(CampaignCutsceneCatalog.VictoryKey(CelestialBodyId.Belt));
        }

        [Test]
        public void TravelCuts_AreLogs_ArrivalAndMid_AreModals()
        {
            Assert.IsTrue(CampaignCutsceneCatalog.TryGet(CampaignCutsceneCatalog.EarthToLuna, out var hop));
            Assert.AreEqual(CutsceneKind.TravelLog, hop.Kind);
            Assert.IsTrue(CampaignCutsceneCatalog.TryGet(CampaignCutsceneCatalog.EarthPrologue, out var pro));
            Assert.AreEqual(CutsceneKind.Modal, pro.Kind);
            Assert.IsTrue(CampaignCutsceneCatalog.TryGet(CampaignCutsceneCatalog.EarthMidCourt, out var mid));
            Assert.AreEqual(CutsceneKind.Modal, mid.Kind);
        }
    }

    public class AdvisorToastCatalogTests
    {
        [Test]
        public void PrimaryTable_HasTwentyFourDecreeToasts()
        {
            Assert.AreEqual(24, AdvisorToastCatalog.AllPrimary.Count);
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < AdvisorToastCatalog.AllPrimary.Count; i++)
            {
                var t = AdvisorToastCatalog.AllPrimary[i];
                Assert.IsTrue(FlagDecreeIds.TryGet(t.DecreeId, out _), t.DecreeId);
                Assert.IsTrue(seen.Add(t.DecreeId), "duplicate primary toast: " + t.DecreeId);
                Assert.IsFalse(string.IsNullOrEmpty(t.Line));
            }
        }

        [Test]
        public void HopperWarrant_FiresOnClaim()
        {
            Assert.IsTrue(AdvisorToastCatalog.TryGetPrimary(FlagDecreeIds.LunaRootTheHopperSaboteurs, out var t));
            Assert.AreEqual(AdvisorFireWhen.Claim, t.When);
        }

        [Test]
        public void TravelKeys_ExistForW2Hops()
        {
            Assert.AreEqual(AdvisorToastCatalog.TravelEarthEmptyDrop, AdvisorToastCatalog.TravelKeyForArrival(CelestialBodyId.Earth));
            Assert.AreEqual(AdvisorToastCatalog.TravelEarthToLuna, AdvisorToastCatalog.TravelKeyForVictory(CelestialBodyId.Earth));
            Assert.IsTrue(AdvisorToastCatalog.TryGetTravel(AdvisorToastCatalog.TravelEarthEmptyDrop, out var toast));
            StringAssert.Contains("picnic", toast.Line);
        }
    }

    public class NarrativeBeatTrackerTests
    {
        [Test]
        public void MidCourt_FiresOnceAllThreeCivicDecreesComplete()
        {
            var n = new NarrativeBeatTracker();
            n.NoteCompleted(FlagDecreeIds.EarthRaiseTheCommons);
            n.NoteCompleted(FlagDecreeIds.EarthDockTheFirstHab);
            Assert.AreEqual(0, n.PendingCutCount);
            n.NoteCompleted(FlagDecreeIds.EarthCharterTheHall);
            Assert.IsTrue(n.PeekCut(out string id));
            Assert.AreEqual(CampaignCutsceneCatalog.EarthMidCourt, id);
            n.DismissCut();
            n.NoteCompleted(FlagDecreeIds.EarthCharterTheHall);
            Assert.AreEqual(0, n.PendingCutCount);
        }

        [Test]
        public void MidWarrant_FiresOnFirstLevyOrHopperClaim()
        {
            var levy = new NarrativeBeatTracker();
            levy.NoteCompleted(FlagDecreeIds.LunaWeighTheFreeholdOre);
            Assert.IsTrue(levy.PeekCut(out string levyId));
            Assert.AreEqual(CampaignCutsceneCatalog.LunaMidWarrant, levyId);

            var warrant = new NarrativeBeatTracker();
            warrant.NoteClaimed(FlagDecreeIds.LunaRootTheHopperSaboteurs);
            Assert.IsTrue(warrant.PeekCut(out string warrantId));
            Assert.AreEqual(CampaignCutsceneCatalog.LunaMidWarrant, warrantId);
        }

        [Test]
        public void MidCrust_FiresOnFirstOfSolarWispsOrCrust()
        {
            var n = new NarrativeBeatTracker();
            n.NoteCompleted(FlagDecreeIds.MarsHuntTheWisps);
            Assert.IsTrue(n.PeekCut(out string id));
            Assert.AreEqual(CampaignCutsceneCatalog.MarsMidCrust, id);
            n.DismissCut();
            n.NoteCompleted(FlagDecreeIds.MarsStringTheSolarField);
            Assert.AreEqual(0, n.PendingCutCount);
        }

        [Test]
        public void InferBuild_Earth_CommonsThenHabThenRocketOnlyWhenPadOrLaunchLive()
        {
            Assert.IsTrue(NarrativeBeatTracker.TryInferBuild(
                CelestialBodyId.Earth, new NarrativeWorldHint(), out string empty));
            Assert.AreEqual(FlagDecreeIds.EarthRaiseTheCommons, empty);

            Assert.IsTrue(NarrativeBeatTracker.TryInferBuild(
                CelestialBodyId.Earth,
                new NarrativeWorldHint { HasCommons = true },
                out string hab));
            Assert.AreEqual(FlagDecreeIds.EarthDockTheFirstHab, hab);

            var afterHab = new NarrativeWorldHint { HasCommons = true, HasHab = true };
            Assert.IsFalse(NarrativeBeatTracker.TryInferBuild(
                CelestialBodyId.Earth, afterHab, out _));

            afterHab.HasWorkshopOrder = true;
            Assert.IsFalse(NarrativeBeatTracker.TryInferBuild(
                CelestialBodyId.Earth, afterHab, out _),
                "workshop labour must not title-match Stage the Lunar Rocket");

            afterHab.HasWorkshopOrder = false;
            afterHab.HasPadOrder = true;
            Assert.IsTrue(NarrativeBeatTracker.TryInferBuild(
                CelestialBodyId.Earth, afterHab, out string padOrder));
            Assert.AreEqual(FlagDecreeIds.EarthStageTheLunarRocket, padOrder);

            afterHab.HasPadOrder = false;
            afterHab.HasPadPiece = true;
            Assert.IsTrue(NarrativeBeatTracker.TryInferBuild(
                CelestialBodyId.Earth, afterHab, out string padPiece));
            Assert.AreEqual(FlagDecreeIds.EarthStageTheLunarRocket, padPiece);

            afterHab.HasPadPiece = false;
            afterHab.LaunchPathLive = true;
            Assert.IsTrue(NarrativeBeatTracker.TryInferBuild(
                CelestialBodyId.Earth, afterHab, out string launch));
            Assert.AreEqual(FlagDecreeIds.EarthStageTheLunarRocket, launch);

            afterHab.HasWorkshopOrder = true;
            Assert.IsFalse(NarrativeBeatTracker.TryInferBuild(
                CelestialBodyId.Earth, afterHab, out _),
                "launch-path tee must not steal a live workshop Build");

            afterHab.HasPadOrder = true;
            Assert.IsTrue(NarrativeBeatTracker.TryInferBuild(
                CelestialBodyId.Earth, afterHab, out string padWins));
            Assert.AreEqual(FlagDecreeIds.EarthStageTheLunarRocket, padWins);

            afterHab.HasPadOrder = false;
            afterHab.HasWorkshopOrder = false;
            afterHab.HasPad = true;
            afterHab.HasPadPiece = true;
            afterHab.LaunchPathLive = true;
            Assert.IsFalse(NarrativeBeatTracker.TryInferBuild(
                CelestialBodyId.Earth, afterHab, out _),
                "completed pad is not more Stage Rocket labour");
        }

        [Test]
        public void TryResolvePosted_UntitledEarthBuildAfterHab_DoesNotStampRocket()
        {
            var data = UnityEngine.ScriptableObject.CreateInstance<FlagData>();
            data.flagType = FlagType.Build;
            data.displayName = "Build";
            var handle = new FlagHandle { Data = data, Title = "Build" };
            var hint = new NarrativeWorldHint { HasCommons = true, HasHab = true };

            Assert.IsFalse(NarrativeBeatTracker.TryResolvePosted(
                handle, CelestialBodyId.Earth, hint, out _));
            Assert.AreEqual("Build", handle.Title);

            hint.HasPadOrder = true;
            Assert.IsTrue(NarrativeBeatTracker.TryResolvePosted(
                handle, CelestialBodyId.Earth, hint, out var decree));
            Assert.AreEqual(FlagDecreeIds.EarthStageTheLunarRocket, decree.Id);
            Assert.AreEqual("Stage the Lunar Rocket", handle.Title);
        }

        [Test]
        public void PostToast_FiresOncePerDecree()
        {
            var n = new NarrativeBeatTracker();
            Assert.IsTrue(n.TryTakePostToast(FlagDecreeIds.EarthSurveyTheClaim, out var first));
            Assert.IsFalse(string.IsNullOrEmpty(first.Line));
            Assert.IsFalse(n.TryTakePostToast(FlagDecreeIds.EarthSurveyTheClaim, out _));
        }
    }

    public class StillCaptureNarrativeSuppressTests
    {
        [TearDown]
        public void TearDown() => StillCaptureHold.Disarm();

        [Test]
        public void HoldActive_EnqueueCut_DoesNotQueueModal()
        {
            StillCaptureHold.Arm();
            var n = new NarrativeBeatTracker();
            Assert.IsFalse(n.EnqueueCut(CampaignCutsceneCatalog.MarsArrival));
            Assert.AreEqual(0, n.PendingCutCount);
            Assert.IsFalse(n.PeekCut(out _));
        }

        [Test]
        public void HoldActive_QueuedCut_IsNotPresentableAsModal()
        {
            var n = new NarrativeBeatTracker();
            Assert.IsTrue(n.EnqueueCut(CampaignCutsceneCatalog.MarsArrival));
            StillCaptureHold.Arm();
            Assert.IsFalse(CanPresentCutModal(n),
                "DrawCutsceneModal / TryPeekCutscene must hide a queued arrival while the shutter hold is armed");
        }

        [Test]
        public void HoldOff_ArrivalCut_IsPresentableAsModal()
        {
            var n = new NarrativeBeatTracker();
            Assert.IsTrue(n.EnqueueCut(CampaignCutsceneCatalog.MarsArrival));
            Assert.IsTrue(CanPresentCutModal(n));
        }

        [Test]
        public void ClearPendingCuts_DropsQueuedModalWithoutRemembering()
        {
            var n = new NarrativeBeatTracker();
            n.EnqueueCut(CampaignCutsceneCatalog.MarsArrival);
            n.ClearPendingCuts();
            Assert.AreEqual(0, n.PendingCutCount);
            Assert.IsFalse(n.WasCutShown(CampaignCutsceneCatalog.MarsArrival));
            Assert.IsTrue(n.EnqueueCut(CampaignCutsceneCatalog.MarsArrival));
        }

        /// <summary>Mirrors GameLoop.TryPeekCutscene + OverseerHud.DrawCutsceneModal hold gate.</summary>
        private static bool CanPresentCutModal(NarrativeBeatTracker n)
        {
            if (StillCaptureHold.Active) return false;
            if (!n.PeekCut(out string id)) return false;
            return CampaignCutsceneCatalog.TryGet(id, out var cut) && cut.Kind == CutsceneKind.Modal;
        }
    }
}
