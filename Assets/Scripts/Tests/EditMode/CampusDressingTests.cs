using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Campus kits and the still-capture layout. Buildings stand alone: no airlocks, tubes,
    /// ports or sleeves on any kit, live or ghost. Locked HAB still: boxy tan hull + roof solar;
    /// graphite-rim HAB-1 cylinder is rejected.
    /// </summary>
    public class CampusDressingTests
    {
        private GameObject _root;
        private IsoGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CampusDressingTestRoot");
            var gridGo = new GameObject("IsoGrid");
            gridGo.transform.SetParent(_root.transform);
            _grid = gridGo.AddComponent<IsoGrid>();
            _grid.Resize(64, 64);
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void LiveCommons_LockedCommandDome_NotGeodesic()
        {
            var commons = ModularBuildingFactory.Spawn(
                BuildingCategory.Commons, Vector3.zero, _root.transform);
            Assert.IsNotNull(FindChild(commons.transform, "CommonsDome"),
                "locked command-dome citadel — smooth sphere, not geodesic lattice");
            Assert.IsNotNull(FindChild(commons.transform, "CommonsStripe"),
                "orange equatorial band");
            Assert.IsNotNull(FindChild(commons.transform, "Dress_CommonsCupolaBand"),
                "orange cupola band");
            Assert.IsNull(FindChild(commons.transform, "Dress_CommonsGeo_0"),
                "geodesic lattice is retired — do not restore");
            Assert.IsNull(FindChild(commons.transform, "Dress_CommonsDomeUnder"));
            Assert.IsNull(FindChild(commons.transform, "CommonsVisor_1"),
                "cyan waist visors washed the sheet white — removed");
            Assert.IsNull(FindChild(commons.transform, "CommonsVisor_3"));
            Color stripe = Albedo(FindChild(commons.transform, "CommonsStripe"));
            Assert.Greater(stripe.r, 0.85f);
            Assert.Less(stripe.g, 0.55f);
        }

        [Test]
        public void LiveHab_BoxyTanHull_RoofSolar_NoGraphiteRim()
        {
            var hab = ModularBuildingFactory.Spawn(
                BuildingCategory.Habitat, Vector3.zero, _root.transform);
            Transform shell = FindChild(hab.transform, "Dress_HabShell");
            Assert.IsNotNull(shell, "locked still HAB is a boxy hull, not HAB-1 cylinder");
            Color tan = Albedo(shell);
            Assert.Greater(tan.r, 0.70f, "HAB hull stays beige/tan");
            Assert.Greater(tan.g, 0.50f);
            Assert.Less(tan.g, 0.72f);
            Assert.Less(tan.b, 0.55f);
            Assert.Less(tan.grayscale, 0.80f, "tan must not flatten to sheet-white");
            Assert.Greater(shell.localScale.x, shell.localScale.y,
                "HAB reads as a box fill of the 4×4, not a lying cylinder");
            Assert.AreEqual(shell.localScale.x, shell.localScale.z, 0.08f);
            Assert.IsNotNull(FindChild(hab.transform, "Dress_HabSolarCell_0_0"),
                "roof solar array from the locked still");
            Assert.IsNotNull(FindChild(hab.transform, "Dress_HabSolarFrame_0_0"));
            Assert.IsNull(FindChild(hab.transform, "HabCarbonBand"),
                "graphite-rim HAB-1 mid-band is rejected");
            Assert.IsNull(FindChild(hab.transform, "HabCarbonBandCore"));
            Assert.IsNull(FindChild(hab.transform, "HabFrontRim"),
                "graphite/orange rim hatches are the rejected variant");
            Assert.IsNull(FindChild(hab.transform, "HabRearRim"));
            Assert.IsNull(FindChild(hab.transform, "DockSleeve_S_Tube"), "HABs stand alone — no dock sleeves");
        }

        [Test]
        public void LivePad_StarshipTwoCarbonBands_SoftTerminator()
        {
            var pad = ModularBuildingFactory.Spawn(
                BuildingCategory.LandingPad, Vector3.zero, _root.transform);
            Transform lo = FindChild(pad.transform, "Dress_ShipBand_0");
            Transform hi = FindChild(pad.transform, "Dress_ShipBand_1");
            Transform body = FindChild(pad.transform, "Dress_StarshipBody");
            Assert.IsNotNull(lo, "lower carbon band");
            Assert.IsNotNull(hi, "upper carbon band");
            Assert.IsNotNull(body, "procedural cream hull — placeholder FBX skipped");
            Assert.IsNull(FindChild(pad.transform, "Dress_StarshipStripe"),
                "orange hull stripes are not the concept rocket");
            Assert.IsNull(FindChild(pad.transform, "Dress_StarshipBandHi"));
            Assert.IsNull(FindChild(pad.transform, "Dress_Starship"),
                "SM_Starship_Placeholder stays off the pad (LaunchSite still uses it)");

            float h = HeroBuildingKits.StarshipStackHeight;
            float loLen = lo.localScale.y * 2f;
            float hiLen = hi.localScale.y * 2f;
            Assert.Greater(loLen / h, 0.08f, "lower band must read thick at ortho 10");
            Assert.Less(loLen / h, 0.16f);
            Assert.Greater(hiLen / h, 0.06f, "upper band stays a readable ring");
            Assert.Less(hiLen / h, 0.14f);
            Assert.Less(Albedo(lo).grayscale, 0.20f, "bands stay near-black, not orange");
            Assert.Less(Albedo(hi).grayscale, 0.20f);
            Assert.AreEqual(h * HeroBuildingKits.StarshipBandLoT, lo.localPosition.y, 0.02f);
            Assert.AreEqual(h * HeroBuildingKits.StarshipBandHiT, hi.localPosition.y, 0.02f);

            Color hull = Albedo(body);
            Assert.Greater(hull.r - hull.b, 0.08f, "hull is warm cream, not cool white");
            Assert.Greater(hull.grayscale, 0.70f);
            var mat = body.GetComponent<Renderer>().sharedMaterial;
            Assert.IsTrue(mat.HasProperty("_EmissionColor"), "warm fill lifts the shaded flank");
            Color emit = mat.GetColor("_EmissionColor");
            Assert.Greater(emit.maxColorComponent, 0.08f,
                "hull fill keeps the terminator near 55 % of lit");
            Assert.Less(emit.maxColorComponent, 0.22f, "fill is a hint, not a glow");
            if (mat.HasProperty("_Smoothness"))
                Assert.Less(mat.GetFloat("_Smoothness"), 0.28f, "cream hull stays matte");
            if (mat.HasProperty("_Metallic"))
                Assert.Less(mat.GetFloat("_Metallic"), 0.10f, "dielectric — no hard metal terminator");
        }

        [Test]
        public void MarsCatalog_SalmonHorizonHaze()
        {
            var mars = CelestialBodyCatalog.Get(CelestialBodyId.Mars);
            // Pale dusty haze, concept far-ground edge ~RGB 222/140/80 (dream-loop round 6).
            Assert.AreEqual(0.98f, mars.FogColor.r, 0.03f);
            Assert.AreEqual(0.61f, mars.FogColor.g, 0.03f);
            Assert.AreEqual(0.40f, mars.FogColor.b, 0.03f);
            // Play-ortho 10: start past campus/focus (~44 m), FogEnd well past the 61 m far
            // edge so the top of frame is a salmon hint (~28 %), not an opaque wall.
            Assert.Greater(mars.FogStart, 30f);
            Assert.Greater(mars.FogEnd, 80f);
            Assert.Less(mars.FogEnd, 160f);
            float playFarHint = (DemoAtmosphere.MarsPlayFarDepth - mars.FogStart)
                / (mars.FogEnd - mars.FogStart);
            Assert.Less(playFarHint, 0.45f);
            Assert.Greater(playFarHint, 0.10f);
            // Dusty brown-orange dirt, not blood-red: concept lit dirt G/R ~0.45.
            Assert.Greater(mars.GroundLight.g / mars.GroundLight.r, 0.55f);
        }

        [Test]
        public void MarsFog_ZoomedOutKeepsFarGroundAHint()
        {
            var mars = CelestialBodyCatalog.Get(CelestialBodyId.Mars);
            float pitch = 30f * Mathf.Deg2Rad;
            float sinP = Mathf.Sin(pitch);
            float cosP = Mathf.Cos(pitch);
            float ortho = IsometricCameraController.MaxOrthoSize;
            float camY = ortho * cosP + 1.5f;
            DemoAtmosphere.ComputeMarsLinearFog(
                mars, camY, -sinP, cosP, ortho, out float start, out float end);
            float depthFar = (camY + cosP * ortho) / sinP;
            float depthFocus = camY / sinP;
            float farHint = Mathf.InverseLerp(start, end, depthFar);
            float focusHint = Mathf.InverseLerp(start, end, depthFocus);
            Assert.Less(farHint, 0.45f, "zoomed-out far ground must stay a haze hint, not FogColor");
            Assert.Greater(farHint, 0.10f, "zoomed-out far ground must still recede");
            Assert.Less(focusHint, 0.08f, "zoomed-out focus must stay near-clear");
            Assert.Greater(end, depthFar + 20f);
        }

        [Test]
        public void FaunaHide_DoesNotUseCampusCream()
        {
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("RegolithMite"));
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("WattLeech"));
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("DustStalker"));
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("AshHopper"));
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("DustHopper"));
            Assert.IsTrue(IndustrialArtDressing.IsFaunaAgentName("DustWisp"));
            Assert.IsFalse(IndustrialArtDressing.IsFaunaAgentName("ColonyCommons"));
            Assert.IsFalse(IndustrialArtDressing.IsFaunaAgentName("Visual"));

            GameObject mite = MakeFaunaStub("RegolithMite", new Vector3(0.45f, 0.9f, 0.4f));
            AddAccentToken(mite.transform.Find("Visual"), "SM_Cyan");
            IndustrialArtDressing.Apply(mite);
            var hideMat = mite.transform.Find("Visual").GetComponent<Renderer>().sharedMaterial;
            Assert.IsNotNull(hideMat);
            Assert.IsFalse(hideMat.name.Contains("WhiteHull"), hideMat.name);
            Assert.IsTrue(hideMat.name.Contains("MiteHide"), hideMat.name);
            Transform cyan = FindChild(mite.transform, "SM_CyanToken");
            Assert.IsNotNull(cyan);
            Assert.IsTrue(cyan.GetComponent<Renderer>().sharedMaterial.name.Contains("Cyan"),
                cyan.GetComponent<Renderer>().sharedMaterial.name);

            GameObject hopper = MakeFaunaStub("DustHopper", new Vector3(0.4f, 1.1f, 0.35f));
            IndustrialArtDressing.Apply(hopper);
            string hopperMat = hopper.transform.Find("Visual").GetComponent<Renderer>().sharedMaterial.name;
            Assert.IsTrue(hopperMat.Contains("HopperHide"), hopperMat);
            Assert.IsFalse(hopperMat.Contains("MiteHide"), hopperMat);

            GameObject wisp = MakeFaunaStub("DustWisp", Vector3.one * 0.6f);
            IndustrialArtDressing.Apply(wisp);
            string wispMat = wisp.transform.Find("Visual").GetComponent<Renderer>().sharedMaterial.name;
            Assert.IsTrue(wispMat.Contains("WispHide"), wispMat);
            Assert.IsFalse(wispMat.Contains("MiteHide"), wispMat);
        }

        [Test]
        public void FaunaMotion_HopperHops_LeechHovers_MiteScuttles()
        {
            Assert.AreEqual(LocomotionKind.Hop, UnitMotion.KindFor(FaunaKind.Hopper));
            Assert.AreEqual(LocomotionKind.Hover, UnitMotion.KindFor(FaunaKind.Leech));
            Assert.AreEqual(LocomotionKind.Hover, UnitMotion.KindFor(FaunaKind.Wisp));
            Assert.AreEqual(LocomotionKind.Scuttle, UnitMotion.KindFor(FaunaKind.Mite));
            Assert.AreEqual(LocomotionKind.Scuttle, UnitMotion.KindFor(FaunaKind.Stalker));
            Assert.AreEqual(6, DustStalkerAgent.LegCountFor(FaunaKind.Hopper));
            Assert.AreEqual(6, DustStalkerAgent.LegCountFor(FaunaKind.Mite));
            Assert.AreEqual(6, DustStalkerAgent.LegCountFor(FaunaKind.Stalker));
            Assert.AreEqual(8, DustStalkerAgent.LegCountFor(FaunaKind.Tick));
            Assert.AreEqual(0, DustStalkerAgent.LegCountFor(FaunaKind.Leech));
            Assert.AreEqual(0, DustStalkerAgent.LegCountFor(FaunaKind.Wisp));
        }

        [Test]
        public void FaunaHasArt_KeepsProceduralLegs()
        {
            GameObject mite = MakeFaunaStub("RegolithMite", new Vector3(0.45f, 0.9f, 0.4f));
            InitFauna(mite, FaunaKind.Mite);
            Assert.IsTrue(IndustrialArtDressing.HasArt(mite));
            Assert.IsNotNull(mite.GetComponent<ProceduralLegs>(), "mite IK legs even when HasArt");
            Assert.AreEqual(6, mite.GetComponent<ProceduralLegs>().LegCount);

            GameObject hopper = MakeFaunaStub("AshHopper", new Vector3(0.4f, 1.2f, 0.35f));
            InitFauna(hopper, FaunaKind.Hopper);
            Assert.IsTrue(IndustrialArtDressing.HasArt(hopper));
            Assert.IsNotNull(hopper.GetComponent<ProceduralLegs>());
            Assert.AreEqual(6, hopper.GetComponent<ProceduralLegs>().LegCount);

            GameObject stalker = MakeFaunaStub("DustStalker", new Vector3(0.5f, 1.1f, 0.7f));
            InitFauna(stalker, FaunaKind.Stalker);
            Assert.IsTrue(IndustrialArtDressing.HasArt(stalker));
            Assert.IsNotNull(stalker.GetComponent<ProceduralLegs>());
            Assert.AreEqual(6, stalker.GetComponent<ProceduralLegs>().LegCount);

            GameObject leech = MakeFaunaStub("WattLeech", new Vector3(0.35f, 1.0f, 0.25f));
            InitFauna(leech, FaunaKind.Leech);
            Assert.IsNull(leech.GetComponent<ProceduralLegs>(), "leech hovers — no IK legs");
        }

        [Test]
        public void FaunaMite_PaintAddsCyanOrangeDress()
        {
            GameObject mite = MakeFaunaStub("RegolithMite", new Vector3(0.45f, 0.9f, 0.4f));
            InitFauna(mite, FaunaKind.Mite);
            Transform eye = FindChild(mite.transform, "Dress_EyeL");
            Transform nub = FindChild(mite.transform, "Dress_NubL");
            Assert.IsNotNull(eye, "cyan eye dress");
            Assert.IsNotNull(nub, "orange nub dress");
            Color eyeC = Albedo(eye);
            Assert.Greater(eyeC.b, 0.70f, "eye stays cyan");
            Assert.Greater(eyeC.b, eyeC.r);
            Color nubC = Albedo(nub);
            Assert.Greater(nubC.r, 0.80f, "nub stays safety orange");
            Assert.Less(nubC.b, 0.25f);
        }

        [Test]
        public void FaunaAlignHead_LongestAxisOnPlusZ()
        {
            var go = new GameObject("RegolithMite");
            go.transform.SetParent(_root.transform, false);
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "Visual";
            vis.transform.SetParent(go.transform, false);
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = new Vector3(2f, 0.3f, 0.4f);
            var col = vis.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            FaunaDressing.AlignHead(go);

            Bounds b = vis.GetComponent<Renderer>().bounds;
            Assert.Greater(b.size.z, b.size.x * 1.2f,
                "longest horizontal mesh axis must map to parent +Z");
        }

        [Test]
        public void NeighborOrigin_CentresHabOnFace_WithWalkwayGap()
        {
            var commons = new BuildingPlacer.CampusPiece(
                new Vector2Int(10, 10), 6, 6, BuildingCategory.Commons);
            Assert.AreEqual(new Vector2Int(18, 11),
                BuildingPlacer.NeighborOrigin(commons, BuildingPlacer.Cardinal.East, 4, 4));
            Assert.AreEqual(new Vector2Int(4, 11),
                BuildingPlacer.NeighborOrigin(commons, BuildingPlacer.Cardinal.West, 4, 4));
            Assert.AreEqual(new Vector2Int(11, 18),
                BuildingPlacer.NeighborOrigin(commons, BuildingPlacer.Cardinal.North, 4, 4));
            Assert.AreEqual(new Vector2Int(11, 4),
                BuildingPlacer.NeighborOrigin(commons, BuildingPlacer.Cardinal.South, 4, 4));

            var faces = new[]
            {
                BuildingPlacer.Cardinal.East,
                BuildingPlacer.Cardinal.West,
                BuildingPlacer.Cardinal.North,
                BuildingPlacer.Cardinal.South
            };
            for (int i = 0; i < faces.Length; i++)
            {
                Vector2Int hab = BuildingPlacer.NeighborOrigin(commons, faces[i], 4, 4);
                Assert.IsFalse(RectsOverlap(hab, 4, 4, commons.Origin, 6, 6),
                    $"{faces[i]} HAB {hab} must not sit on Commons");
                Assert.AreEqual(BuildingPlacer.NeighborGap,
                    StillCampusDensity.RectGapCells(hab, 4, 4, commons.Origin, 6, 6),
                    $"{faces[i]} HAB keeps a {BuildingPlacer.NeighborGap}-cell walkway");
            }
        }

        [Test]
        public void NeighborOrigin_EveryFaceFitsOnAnEmptyCommonsDrop()
        {
            var placer = new BuildingPlacer(new ResourceManager());
            var commonsOrigin = new Vector2Int(10, 10);
            placer.MarkCampusRect(commonsOrigin, 6, 6);
            placer.RegisterPiece(commonsOrigin, 6, 6, BuildingCategory.Commons);
            var commons = placer.Pieces[0];

            for (int f = 0; f < 4; f++)
            {
                var face = (BuildingPlacer.Cardinal)f;
                Vector2Int hab = BuildingPlacer.NeighborOrigin(commons, face, 4, 4);
                Assert.IsTrue(placer.CanFitRect(hab, 4, 4), $"{face} HAB {hab} must CanFit");
            }
        }

        [Test]
        public void Placer_RefusesRetiredAirlocks()
        {
            var placer = new BuildingPlacer(new ResourceManager());
            placer.RegisterPiece(new Vector2Int(4, 4), 2, 2, BuildingCategory.Utility);
            Assert.AreEqual(0, placer.Pieces.Count, "old-save airlock pieces are dropped");

            var data = ScriptableObject.CreateInstance<BuildingData>();
            try
            {
                data.category = BuildingCategory.Utility;
                data.footprintWidth = 2;
                data.footprintHeight = 2;
                Assert.IsFalse(placer.TryPlace(data, new Vector2Int(8, 8), Vector3.zero, out _, out string why));
                Assert.AreEqual("retired_building", why);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void Buildings_HaveNoPortsSleevesOrConnectors()
        {
            string[] banned =
            {
                "DockSleeve", "Dress_TubeArm", "CommonsStub", "CommonsPort", "HabPort", "LabPort",
                "PwrPort", "HullPort", "DockPort", "DrumPort", "ModulePort", "CardinalPort",
                "PlusConnector", "AirlockHub", "Dress_Hub"
            };
            var cats = new[]
            {
                BuildingCategory.Commons, BuildingCategory.Habitat, BuildingCategory.Laboratory,
                BuildingCategory.Power, BuildingCategory.EngineerWorkshop, BuildingCategory.LandingPad
            };
            foreach (bool ghost in new[] { false, true })
            foreach (var cat in cats)
            {
                var go = ModularBuildingFactory.Spawn(cat, Vector3.zero, _root.transform, 4, 4,
                    ColonyLayout.DefaultCellSize, ghost);
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    foreach (var b in banned)
                        Assert.IsFalse(t.name.StartsWith(b),
                            $"{cat}{(ghost ? " ghost" : "")} still has '{t.name}'");
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EmptyCommonsDrop_EastHabFitsAndDoesNotOverlap()
        {
            var placer = new BuildingPlacer(new ResourceManager());
            var commonsOrigin = new Vector2Int(10, 10);
            placer.MarkCampusRect(commonsOrigin, 6, 6);
            placer.RegisterPiece(commonsOrigin, 6, 6, BuildingCategory.Commons);

            Vector2Int hab = BuildingPlacer.NeighborOrigin(
                placer.Pieces[0], BuildingPlacer.Cardinal.East, 4, 4);

            Assert.AreEqual(new Vector2Int(18, 11), hab);
            Assert.IsTrue(placer.CanFitRect(hab, 4, 4));

            placer.MarkCampusRect(hab, 4, 4);
            placer.RegisterPiece(hab, 4, 4, BuildingCategory.Habitat);

            Assert.AreEqual(2, placer.Pieces.Count);
            Assert.IsTrue(placer.HasCommonsModule);
            Assert.IsFalse(placer.CanFitRect(hab, 4, 4));
        }

        [Test]
        public void DensePack_EastHab_PlacesPadPowerWaterRegolith_WithoutOverlap()
        {
            var placer = StampEastChain(out var commons);

            Vector2Int westPad = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6,
                StillCampusDensity.MinYardGapCells);
            Assert.AreEqual(
                StillCampusDensity.FlushOrigin(
                    commons, BuildingPlacer.Cardinal.West, 6, 6,
                    StillCampusDensity.MinYardGapCells),
                westPad);
            Assert.IsTrue(placer.CanFitRect(westPad, 6, 6), "gapped west pad must CanFit");

            var plan = StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);

            Assert.IsTrue(plan.Pad, "pad");
            Assert.IsTrue(plan.Power, "pwr");
            Assert.IsTrue(plan.Water, "water");
            Assert.IsTrue(plan.Regolith, "regolith");
            Assert.AreEqual(4, plan.PlacedCount);
            Assert.AreEqual(westPad, plan.PadOrigin, "HAB east → pad west with yard gap");
            Assert.GreaterOrEqual(
                StillCampusDensity.RectGapCells(plan.PadOrigin, 6, 6, commons.Origin, 6, 6),
                StillCampusDensity.MinYardGapCells);
            Assert.GreaterOrEqual(
                StillCampusDensity.RectGapCells(plan.PadOrigin, 6, 6, new Vector2Int(18, 11), 4, 4),
                StillCampusDensity.MinYardGapCells,
                "pad must not hug east HAB");
            Assert.GreaterOrEqual(
                StillCampusDensity.RectGapCells(plan.PowerOrigin, 4, 4, commons.Origin, 6, 6),
                StillCampusDensity.MinYardGapCells);
            Assert.GreaterOrEqual(
                StillCampusDensity.RectGapCells(plan.WaterOrigin, 4, 4, commons.Origin, 6, 6),
                StillCampusDensity.MinYardGapCells);
            Assert.GreaterOrEqual(
                StillCampusDensity.RectGapCells(plan.RegolithOrigin, 4, 4, commons.Origin, 6, 6),
                StillCampusDensity.MinYardGapCells);

            var log = StillCampusDensity.StampLog.FromPieces(placer);
            Assert.IsTrue(log.Commons && log.Hab);
            Assert.IsTrue(log.Pad && log.Power && log.Water && log.Regolith);
            Assert.AreEqual(
                "commons=True hab=True pad=True pwr=True water=True regolith=True " +
                "workshop=False inn=False wonder=False leftover=none extraHab=False extraSolar=False defense=False",
                log.ToString());

            Assert.IsFalse(
                RectsOverlap(plan.PadOrigin, 6, 6, commons.Origin, 6, 6));
            Assert.IsFalse(
                RectsOverlap(plan.PowerOrigin, 4, 4, plan.PadOrigin, 6, 6));
            Assert.IsFalse(
                RectsOverlap(plan.WaterOrigin, 4, 4, plan.PowerOrigin, 4, 4));
            Assert.IsFalse(
                RectsOverlap(plan.RegolithOrigin, 4, 4, plan.WaterOrigin, 4, 4));
            Assert.IsFalse(
                RectsOverlap(plan.WaterOrigin, 4, 4, new Vector2Int(18, 11), 4, 4),
                "water must not sit on east HAB");
            Assert.AreEqual(6, placer.Pieces.Count);
        }

        [Test]
        public void DensePack_ForwardYards_CanFitNearCommons_ButExtraRuleWouldReject()
        {
            var placer = StampEastChain(out var commons);
            Vector2Int westPad = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6);

            Assert.IsTrue(placer.CanFitRect(westPad, 6, 6));
            Assert.IsFalse(
                placer.OverlapsOutpostClaim(westPad, 6, 6),
                "Campus B claim is ~30 m off — still pack must not require it");
        }

        [Test]
        public void DensePack_SkipsHabFace_ThenTakesNorthWhenWestBlocked()
        {
            var placer = StampEastChain(out var commons);

            Vector2Int westPad = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6,
                StillCampusDensity.MinYardGapCells);
            placer.MarkCampusRect(westPad, 6, 6);
            placer.RegisterPiece(westPad, 6, 6, BuildingCategory.Defense);

            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 6, 6, null, out Vector2Int picked));
            Assert.AreNotEqual(westPad, picked);
            Assert.GreaterOrEqual(
                StillCampusDensity.RectGapCells(picked, 6, 6, commons.Origin, 6, 6),
                StillCampusDensity.MinYardGapCells,
                "spaced west blocked → next island still keeps the yard gap");
            Assert.GreaterOrEqual(
                StillCampusDensity.RectGapCells(picked, 6, 6, new Vector2Int(18, 11), 4, 4),
                StillCampusDensity.MinYardGapCells,
                "replacement pad must not hug east HAB");
        }

        [Test]
        public void DensePack_StillFrame_UsesPlayCampusOrtho10()
        {
            var placer = StampEastChain(out var commons);
            StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);

            Assert.IsTrue(StillCampusDensity.TryCampusAabb(placer, out var min, out var max));
            float ortho = StillCampusDensity.FitStillOrtho(
                placer, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect);
            Assert.GreaterOrEqual(ortho, StillCampusDensity.PlayCampusOrthoSize,
                "never zoom inside play ortho 10 — that packs the AABB");
            Assert.LessOrEqual(ortho, StillCampusDensity.StillMaxOrtho);
            Assert.Greater(max.x - min.x, 10, "AABB must span pad→HAB");
            Assert.Greater(max.y - min.y, 6, "AABB must span Commons→north yards");
        }

        [Test]
        public void DensePack_LeftoverKits_DoNotStampWhenTheyCanFit()
        {
            var placer = StampEastChain(out var commons);
            StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);

            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 4, 4, null,
                out _, enforceIslandGap: false),
                "workshop 4×4 still CanFit after pad+yards");
            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 6, 6, null,
                out _, enforceIslandGap: false),
                "a 6×6 wonder still CanFit near Commons");

            var leftovers = StillCampusDensity.PlanLeftovers(
                placer, commons, BuildingPlacer.Cardinal.East);
            Assert.IsFalse(leftovers.Inn, "Aaron 2026-09-07: leftover Inn is not a still density gate");
            Assert.IsFalse(leftovers.Wonder, "Aaron 2026-09-07: leftover wonder is not a still density gate");
            Assert.IsFalse(leftovers.Workshop, "Aaron 2026-09-07: leftover hangar is not a still density gate");
            Assert.AreEqual("spaced", leftovers.SkipReason);
            Assert.AreEqual(6, placer.Pieces.Count, "PlanLeftovers must not occupy leftover footprints");

            var log = StillCampusDensity.StampLog.FromPieces(placer, leftovers.SkipReason);
            Assert.IsFalse(log.Workshop);
            Assert.IsFalse(log.Inn);
            Assert.IsFalse(log.Wonder);
            Assert.AreEqual("spaced", log.Leftover);

            Assert.IsTrue(StillCampusDensity.TryCampusAabb(placer, out _, out _));
            float ortho = StillCampusDensity.FitStillOrtho(
                placer, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect);
            Assert.GreaterOrEqual(ortho, StillCampusDensity.StillMinOrtho);
            Assert.LessOrEqual(ortho, StillCampusDensity.StillMaxOrtho);
        }

        [Test]
        public void LandmarkGap_IsNotFlushToCommons()
        {
            var placer = StampEastChain(out var commons);
            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 6, 6, null,
                out Vector2Int spaced, StillCampusDensity.LandmarkGapCells));
            Vector2Int flush = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6, 0);
            Assert.AreNotEqual(flush, spaced, "pad must sit off the Commons apron");
            Vector2Int gapped = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6,
                Mathf.Max(StillCampusDensity.LandmarkGapCells, StillCampusDensity.MinYardGapCells));
            Assert.AreEqual(gapped, spaced);
        }

        [Test]
        public void ConceptPad_SitsPastTheHabNotOnCommonsWest()
        {
            var placer = StampEastChain(out var commons);
            Assert.IsTrue(StillCampusDensity.TryConceptPad(
                placer, commons, BuildingPlacer.Cardinal.East, null, out Vector2Int pad));
            Assert.IsTrue(StillCampusDensity.TryFindHab(placer, out var hab));
            Vector2Int west = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6, 0);
            Assert.AreNotEqual(west, pad);
            Vector2Int pastHab = StillCampusDensity.FlushBeyond(
                hab, BuildingPlacer.Cardinal.East, 6, 6, StillCampusDensity.LandmarkGapCells);
            Assert.AreEqual(pastHab, pad);
        }

        [Test]
        public void DensePack_Leftovers_AfterWorkshop_SkipInnAndWonder()
        {
            var placer = StampEastChain(out var commons);
            StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);
            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 4, 4, null, out Vector2Int shop));
            placer.MarkCampusRect(shop, 4, 4);
            placer.RegisterPiece(shop, 4, 4, BuildingCategory.EngineerWorkshop);

            var leftovers = StillCampusDensity.PlanLeftovers(
                placer, commons, BuildingPlacer.Cardinal.East);
            Assert.IsTrue(leftovers.Workshop, "existing hangar stays on the leftover label");
            Assert.IsFalse(leftovers.Inn, "Aaron 2026-09-07: do not pack leftover Inn");
            Assert.IsFalse(leftovers.Wonder, "Aaron 2026-09-07: do not pack leftover wonder");
            Assert.AreEqual("spaced+workshop", leftovers.SkipReason);
        }

        [Test]
        public void DensePack_Still20Order_SkipsLeftoverDensityPressure()
        {
            var placer = StampStill20Order(out _, out var leftovers, out var cues);

            Assert.IsFalse(leftovers.Inn, "Aaron 2026-09-07: leftover Inn is not a still density gate");
            Assert.IsFalse(leftovers.Wonder, "Aaron 2026-09-07: leftover wonder is not a still density gate");
            Assert.IsTrue(leftovers.SkipReason.StartsWith("spaced"), leftovers.SkipReason);
            Assert.IsFalse(cues.ExtraSolar, "do not pack a second solar bank on the still");
            Assert.IsFalse(cues.Defense, "do not pack Defense Battery leftover on the still");
            Assert.AreEqual(0, cues.HabSocketCount);
            Assert.AreEqual(1, StillCampusDensity.CountCategory(placer, BuildingCategory.Power));
            Assert.AreEqual(0, StillCampusDensity.CountCategory(placer, BuildingCategory.Defense));

            var log = StillCampusDensity.StampLog.FromPieces(placer, leftovers.SkipReason);
            Assert.IsFalse(log.Inn);
            Assert.IsFalse(log.Wonder);
            Assert.IsFalse(log.ExtraSolar);
            Assert.IsFalse(log.Defense);

            Assert.IsTrue(StillCampusDensity.TryCampusAabb(placer, out _, out _));
            float ortho = StillCampusDensity.FitStillOrtho(
                placer, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect);
            Assert.GreaterOrEqual(ortho, StillCampusDensity.StillMinOrtho);
            Assert.LessOrEqual(ortho, StillCampusDensity.StillMaxOrtho);
        }

        [Test]
        public void DensePack_RejectsSiteOverlappingCommons_AndFindsFreeSite()
        {
            var placer = StampEastChain(out var commons);
            Assert.IsFalse(placer.CanFitRect(new Vector2Int(15, 14), 4, 4),
                "a site at (15,14) overlaps the Commons");

            Assert.IsTrue(StillCampusDensity.TryNext(
                placer, commons, BuildingPlacer.Cardinal.East, 4, 4, null, out Vector2Int picked));
            Assert.IsTrue(placer.CanFitRect(picked, 4, 4));
        }

        [Test]
        public void DensePack_ExtraHab_CanFitSouth_ButPlanCuesSkips()
        {
            var placer = StampEastChain(out var commons);
            Assert.IsTrue(StillCampusDensity.TryExtraHab(
                placer, commons, BuildingPlacer.Cardinal.East, null, out Vector2Int hab));
            Assert.AreEqual(
                BuildingPlacer.NeighborOrigin(commons, BuildingPlacer.Cardinal.South, 4, 4), hab);
            Assert.IsFalse(RectsOverlap(hab, 4, 4, commons.Origin, 6, 6));
            Assert.IsTrue(placer.CanFitRect(hab, 4, 4));

            var cues = StillCampusDensity.PlanCues(
                placer, commons, BuildingPlacer.Cardinal.East);
            Assert.IsFalse(cues.ExtraHab, "Aaron 2026-09-07: extra HAB is leftover density, not a still gate");
            Assert.AreEqual(0, cues.PlacedCount);
        }

        [Test]
        public void StillHabSocket_SpawnsFoundationAndCrane()
        {
            var go = CampusDressing.SpawnStillHabSocket(Vector3.zero, _root.transform);
            Assert.IsNotNull(go);
            Assert.AreEqual("Site_StillHabSocket", go.name);
            Assert.IsNotNull(FindChild(go.transform, "SocketDisc"));
            Assert.IsNotNull(FindChild(go.transform, "SocketCraneMast"));
            Assert.IsNotNull(FindChild(go.transform, "SocketClad_0"));
            Assert.Greater(Albedo(FindChild(go.transform, "SocketDisc")).grayscale, 0.88f);
        }

        [Test]
        public void DensePack_InteriorDirt_MayStayEmpty_StillUsesPlayOrtho()
        {
            var placer = StampEastChain(out var commons);
            Vector2Int pad = StillCampusDensity.FlushOrigin(
                commons, BuildingPlacer.Cardinal.West, 6, 6);
            placer.MarkCampusRect(pad, 6, 6);
            placer.RegisterPiece(pad, 6, 6, BuildingCategory.LandingPad);
            var extractor = new Vector2Int(pad.x, pad.y + 6 + 4);
            placer.MarkCampusRect(extractor, 4, 4);
            placer.RegisterPiece(extractor, 4, 4, BuildingCategory.Farm);

            var cues = StillCampusDensity.PlanCues(placer, commons, BuildingPlacer.Cardinal.East);
            Assert.AreEqual(0, cues.HabSocketCount, "PlanCues must not fill interior dirt");
            Assert.IsFalse(cues.HabSocket);
            float stillOrtho = StillCampusDensity.FitStillOrtho(
                placer, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect);
            Assert.GreaterOrEqual(stillOrtho, StillCampusDensity.StillMinOrtho);
            Assert.LessOrEqual(stillOrtho, StillCampusDensity.StillMaxOrtho);
        }

        [Test]
        public void DensePack_Still21Order_SkipsLeftoversWithoutInteriorFill()
        {
            var placer = StampStill20Order(out _, out var leftovers, out var cues);
            Assert.IsFalse(leftovers.Inn, "do not pack leftover Inn");
            Assert.IsFalse(leftovers.Wonder, "do not pack leftover wonder");
            Assert.IsTrue(leftovers.SkipReason.StartsWith("spaced"), leftovers.SkipReason);
            Assert.AreEqual(0, cues.HabSocketCount, "do not force-fill interior sockets");
            float stillOrtho = StillCampusDensity.FitStillOrtho(
                placer, ColonyLayout.DefaultCellSize, StillCampusDensity.GameTabAspect);
            Assert.GreaterOrEqual(stillOrtho, StillCampusDensity.StillMinOrtho);
            Assert.LessOrEqual(stillOrtho, StillCampusDensity.StillMaxOrtho);
            Assert.AreEqual(10f, StillCampusDensity.PlayCampusOrthoSize);
        }

        [Test]
        public void GeodesicDomeMesh_InsetShrinksFacets()
        {
            var tight = GeodesicDomeMesh.Build(2, Vector3.one, -1f, 0f);
            var open = GeodesicDomeMesh.Build(
                HeroBuildingKits.CommonsGeodesicFrequency, Vector3.one, -1f,
                HeroBuildingKits.CommonsGeodesicInset);
            Assert.AreEqual(0, tight.vertexCount % 3);
            Assert.AreEqual(0, open.vertexCount % 3);
            Assert.Greater(tight.bounds.size.x, open.bounds.size.x,
                "inset must open a seam so the undershell lattice reads");
            Assert.Greater(open.vertexCount, 80);
            Assert.Less(open.vertexCount, 650, "frequency 3, not a vanishing frequency-4 shell");
        }

        [Test]
        public void DensePack_YardsKeepMinGap_NoFlushIslands()
        {
            var placer = StampEastChain(out var commons);
            var plan = StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);
            Assert.IsTrue(plan.Pad && plan.Power && plan.Water && plan.Regolith);
            Assert.GreaterOrEqual(
                StillCampusDensity.RectGapCells(plan.PadOrigin, 6, 6, plan.PowerOrigin, 4, 4),
                StillCampusDensity.MinYardGapCells);
            Assert.GreaterOrEqual(
                StillCampusDensity.RectGapCells(plan.WaterOrigin, 4, 4, plan.RegolithOrigin, 4, 4),
                StillCampusDensity.MinYardGapCells);
            Assert.GreaterOrEqual(
                StillCampusDensity.RectGapCells(plan.PowerOrigin, 4, 4, plan.WaterOrigin, 4, 4),
                StillCampusDensity.MinYardGapCells);
            Assert.IsFalse(
                StillCampusDensity.TryCardinalNeighbor(
                    new BuildingPlacer.CampusPiece(commons.Origin, 6, 6, BuildingCategory.Commons),
                    new BuildingPlacer.CampusPiece(
                        plan.PadOrigin, 6, 6, BuildingCategory.LandingPad),
                    1, out _, out _),
                "pad must not sit flush (gap 0–1) against Commons");
        }

        private Vector3 FootprintCenter(Vector2Int origin, int side)
        {
            Vector3 a = _grid.CellToWorld(origin);
            Vector3 b = _grid.CellToWorld(origin + new Vector2Int(side - 1, side - 1));
            return (a + b) * 0.5f;
        }

        [Test]
        public void InferHabFace_ReadsEastChain()
        {
            var placer = StampEastChain(out var commons);
            Assert.AreEqual(
                BuildingPlacer.Cardinal.East,
                StillCampusDensity.InferHabFace(placer, commons));
            Assert.AreEqual(
                BuildingPlacer.Cardinal.West,
                StillCampusDensity.Opposite(BuildingPlacer.Cardinal.East));
        }

        [Test]
        public void StillCampusDensity_GridConstants_MatchColonyLayout()
        {
            Assert.AreEqual(ColonyLayout.DefaultCellSize, StillCampusDensity.DefaultCellSize);
            Assert.AreEqual(ColonyLayout.CampusOrthoSize, StillCampusDensity.PlayCampusOrthoSize);
            Assert.AreEqual(1.5f, StillCampusDensity.DefaultCellSize);
            Assert.AreEqual(10f, StillCampusDensity.PlayCampusOrthoSize);
            Assert.AreEqual(0, StillCampusDensity.StillFrameInsetCells);
            Assert.AreEqual(6, StillCampusDensity.MaxInteriorHabSockets);
            Assert.AreEqual(
                StillCampusDensity.PlayCampusOrthoSize, StillCampusDensity.StillMinOrtho,
                "FitStillOrtho floor must not crop inside play ortho");
            Assert.AreEqual(4, StillCampusDensity.MinYardGapCells);
            Assert.AreEqual(16f, StillCampusDensity.MaxCenterSeparationCells);
        }

        [Test]
        public void FitStillOrtho_EmptyPlacer_FallsBackToPlayCampusOrtho()
        {
            Assert.AreEqual(
                StillCampusDensity.PlayCampusOrthoSize,
                StillCampusDensity.FitStillOrtho(
                    null, StillCampusDensity.DefaultCellSize, StillCampusDensity.GameTabAspect));
        }

        private static BuildingPlacer StampEastChain(out BuildingPlacer.CampusPiece commons)
        {
            var placer = new BuildingPlacer(new ResourceManager());
            var commonsOrigin = new Vector2Int(10, 10);
            placer.MarkCampusRect(commonsOrigin, 6, 6);
            placer.RegisterPiece(commonsOrigin, 6, 6, BuildingCategory.Commons);
            commons = placer.Pieces[0];

            Vector2Int hab = BuildingPlacer.NeighborOrigin(commons, BuildingPlacer.Cardinal.East, 4, 4);
            placer.MarkCampusRect(hab, 4, 4);
            placer.RegisterPiece(hab, 4, 4, BuildingCategory.Habitat);
            return placer;
        }

        /// <summary>
        /// still20 shutter order: extra HAB + workshop, then pad/yards, leftovers, cues.
        /// </summary>
        private static BuildingPlacer StampStill20Order(
            out BuildingPlacer.CampusPiece commons,
            out StillCampusDensity.LeftoverPlan leftovers,
            out StillCampusDensity.CuePlan cues)
        {
            var placer = StampEastChain(out commons);
            Assert.IsTrue(StillCampusDensity.TryExtraHab(
                placer, commons, BuildingPlacer.Cardinal.East, null, out Vector2Int extraHab));
            placer.MarkCampusRect(extraHab, 4, 4);
            placer.RegisterPiece(extraHab, 4, 4, BuildingCategory.Habitat);

            if (StillCampusDensity.TryNext(
                    placer, commons, BuildingPlacer.Cardinal.East, 4, 4, null, out Vector2Int shop))
            {
                placer.MarkCampusRect(shop, 4, 4);
                placer.RegisterPiece(shop, 4, 4, BuildingCategory.EngineerWorkshop);
            }

            StillCampusDensity.Plan(placer, commons, BuildingPlacer.Cardinal.East);
            leftovers = StillCampusDensity.PlanLeftovers(
                placer, commons, BuildingPlacer.Cardinal.East);
            cues = StillCampusDensity.PlanCues(placer, commons, BuildingPlacer.Cardinal.East);
            return placer;
        }
        private static bool RectsOverlap(
            Vector2Int a, int aw, int ah, Vector2Int b, int bw, int bh)
        {
            return a.x < b.x + bw && a.x + aw > b.x && a.y < b.y + bh && a.y + ah > b.y;
        }
        private static Transform FindChild(Transform root, string name)
        {
            var ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
            {
                if (ts[i] != null && ts[i].name == name)
                    return ts[i];
            }
            return null;
        }

        private GameObject MakeFaunaStub(string name, Vector3 visualScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vis.name = "Visual";
            vis.transform.SetParent(go.transform, false);
            vis.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            vis.transform.localScale = visualScale;
            var col = vis.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "SM_White" };
            vis.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void AddAccentToken(Transform visual, string token)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = token + "Token";
            eye.transform.SetParent(visual, false);
            eye.transform.localScale = Vector3.one * 0.15f;
            var col = eye.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = token };
            eye.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void InitFauna(GameObject go, FaunaKind kind)
        {
            IndustrialArtDressing.Apply(go);
            var agent = go.AddComponent<DustStalkerAgent>();
            agent.Initialize(null, null, Vector3.zero);
            agent.SetKind(kind);
        }
        private static void AssertNoSquareHullPanels(Transform t, string message)
        {
            Assert.IsNotNull(t);
            var rend = t.GetComponent<Renderer>();
            Assert.IsNotNull(rend, t.name + " renderer");
            var mat = rend.sharedMaterial;
            Assert.IsNotNull(mat, t.name + " material");
            if (mat.HasProperty("_PanelDarken"))
                Assert.AreEqual(0f, mat.GetFloat("_PanelDarken"), 0.01f, message);
            if (mat.HasProperty("_PanelBevel"))
                Assert.AreEqual(0f, mat.GetFloat("_PanelBevel"), 0.01f, message);
        }

        private static bool UsesHullShader(Transform t)
        {
            var rend = t.GetComponent<Renderer>();
            if (rend == null || rend.sharedMaterial == null || rend.sharedMaterial.shader == null)
                return false;
            string n = rend.sharedMaterial.shader.name;
            return n.ToLowerInvariant().Contains("hull");
        }

        private static Color Albedo(Transform t)
        {
            Assert.IsNotNull(t);
            var rend = t.GetComponent<Renderer>();
            Assert.IsNotNull(rend, t.name + " renderer");
            var mat = rend.sharedMaterial;
            Assert.IsNotNull(mat, t.name + " material");
            if (mat.HasProperty("_BaseColor"))
                return mat.GetColor("_BaseColor");
            return mat.color;
        }
    }
}
