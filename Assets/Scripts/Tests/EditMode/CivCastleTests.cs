using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Civ castles (3D Artist's models, PR #130) as the Commons: the civ-to-castle mapping, the
    /// named anchors, the gate direction, the 18 m plot, the slot colours, and the neutral civ
    /// keeping the procedural Commons. Plus the accent rules that keep the civ colour first.
    /// </summary>
    public class CivCastleTests
    {
        private GameObject _root;

        private static readonly CivId[] CastleCivs =
        {
            CivId.LumenConcord, CivId.TetherExchange, CivId.BastionMandate, CivId.PathfinderAccord
        };

        /// <summary>
        /// A legal accent for every civ: a muted slate blue, dimmer and less saturated than each civ
        /// colour, clear of the team colours, cyan, amber, violet and the threat reds.
        /// </summary>
        private static readonly Color Slate = new Color(0.22f, 0.32f, 0.50f);

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CivCastleTestRoot");
            LogAssert.ignoreFailingMessages = true;
            ModularBuildingFactory.BindCiv(null);
        }

        [TearDown]
        public void TearDown()
        {
            ModularBuildingFactory.BindCiv(null);
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private GameObject SpawnCommons(CivId civ, Color? accent = null, bool ghost = false)
        {
            ModularBuildingFactory.BindCiv(CivModifiers.For(civ, accent));
            return ModularBuildingFactory.Spawn(BuildingCategory.Commons, Vector3.zero, _root.transform, ghost);
        }

        private static Transform Find(Transform root, string name) => CivCastleLoader.FindDeep(root, name);

        // ---- mapping and assets -----------------------------------------------------------

        [Test]
        public void EveryCiv_MapsToItsCastle()
        {
            Assert.AreEqual("Citadels/SM_Citadel_Ziggurat", CivCastleCatalog.For(CivId.LumenConcord).ResourcePath);
            Assert.AreEqual("Citadels/SM_Citadel_Ring", CivCastleCatalog.For(CivId.TetherExchange).ResourcePath);
            Assert.AreEqual("Citadels/SM_Citadel_Dome", CivCastleCatalog.For(CivId.BastionMandate).ResourcePath);
            Assert.AreEqual("Citadels/SM_Citadel_Arcology", CivCastleCatalog.For(CivId.PathfinderAccord).ResourcePath);
            Assert.IsNull(CivCastleCatalog.For(CivId.None), "the neutral civ has no castle");
            foreach (var civ in CastleCivs)
            {
                var spec = CivCastleCatalog.For(civ);
                Assert.IsNotNull(Resources.Load<GameObject>(spec.ResourcePath), spec.ResourcePath + " imports");
            }
            Assert.IsNotNull(Resources.Load<GameObject>(CivCastleCatalog.CoreModulePath), "shared Core module imports");
            Assert.IsNotNull(Resources.Load<Texture2D>(CivCastleCatalog.TrimTexturePrefix + "BaseColor"), "trim sheet");
        }

        // ---- (a) anchors -------------------------------------------------------------------

        [Test]
        public void EachCiv_SpawnsItsCastle_WithEveryAnchor()
        {
            foreach (var civ in CastleCivs)
            {
                var spec = CivCastleCatalog.For(civ);
                var go = SpawnCommons(civ);
                var castle = go.GetComponent<CivCastle>();
                Assert.IsNotNull(castle, civ + " draws a castle");
                Assert.AreEqual(spec.Key, castle.CastleKey, civ + " castle key");
                Assert.IsNull(Find(go.transform, "CommonsGateLintel"), civ + ": no procedural Commons underneath");
                Assert.IsNull(Find(go.transform, "CommonsPlinth"), civ + ": no procedural Commons underneath");

                foreach (var anchor in CivCastleCatalog.CommonAnchors)
                    Assert.IsNotNull(Find(go.transform, anchor), civ + " anchor " + anchor);
                for (int i = 0; i < spec.TowerCount; i++)
                    Assert.IsNotNull(Find(go.transform, "CommonsTower_" + i), civ + " anchor CommonsTower_" + i);
                foreach (var part in CivCastleCatalog.CoreParts)
                    Assert.IsNotNull(Find(go.transform, part), civ + " Core part " + part);

                Assert.Greater(castle.Lod0.Length, 0, civ + " LOD0");
                Assert.Greater(castle.Lod1.Length, 0, civ + " LOD1");
                Assert.Greater(castle.Lod2.Length, 0, civ + " LOD2");
                var group = castle.Model.GetComponent<LODGroup>();
                Assert.IsNotNull(group, civ + ": the castle keeps its own LOD group");
                Assert.AreSame(group, castle.Group, civ + ": CivCastle.Group is the model's group");
                Assert.AreEqual(3, group.lodCount, civ + ": the castle's own three LODs");
                Assert.IsNull(go.GetComponent<LODGroup>(), civ + ": no LODGroup on the Commons root");
                Assert.AreEqual(1, go.GetComponentsInChildren<LODGroup>(true).Length, civ + ": exactly one LODGroup");

                // The crown sits on the model, not at the procedural Commons' height.
                var crown = Find(go.transform, "CitadelCrownY");
                Assert.AreEqual(go.transform.InverseTransformPoint(crown.position).y,
                    CivCastleLoader.CrownHeight(go.transform), 1e-3f, civ + " crown height from the anchor");
                Assert.Greater(CivCastleLoader.CrownHeight(go.transform), 5f, civ + " crown is up top");

                // The Core idles: crystal and both halos registered with BuildingLife.
                Assert.IsNotNull(go.GetComponent<BuildingLife>(), civ + " Core spin registered");
            }
        }

        /// <summary>
        /// Session 33: on New Campaign the castle renderers were also registered on the Commons'
        /// own LODGroup (the imported group was only destroyed at end of frame in play mode), and
        /// Unity warned once per renderer. Only the castle's group may hold castle renderers, a
        /// second Apply (or a second Commons) changes nothing, and spawning logs no LOD warning.
        /// </summary>
        [Test]
        public void Castle_OnlyItsOwnLodGroupHoldsItsRenderers_AndSpawnLogsNoLodWarnings()
        {
            var logged = new List<string>();
            void Capture(string message, string stack, LogType type)
            {
                if (type != LogType.Log) logged.Add(type + ": " + message);
            }
            Application.logMessageReceived += Capture;
            var spawned = new List<GameObject>();
            try
            {
                foreach (var civ in CastleCivs)
                {
                    // Twice, like a New Campaign after a campaign, then re-apply the LODs.
                    spawned.Add(SpawnCommons(civ, Slate));
                    var go = SpawnCommons(civ, Slate);
                    spawned.Add(go);
                    BuildingLod.Apply(go);
                }
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }

            foreach (var go in spawned)
            {
                var castle = go.GetComponent<CivCastle>();
                Assert.IsNotNull(castle, go.name);
                Assert.IsNull(go.GetComponent<LODGroup>(), castle.Civ + ": the Commons root carries no LODGroup");

                var owned = new HashSet<Renderer>();
                foreach (var lod in castle.Group.GetLODs())
                    foreach (var r in lod.renderers) if (r != null) owned.Add(r);
                foreach (var r in castle.Lod0) Assert.IsTrue(owned.Contains(r), castle.Civ + " castle group holds " + r.name);
                foreach (var r in castle.Lod1) Assert.IsTrue(owned.Contains(r), castle.Civ + " castle group holds " + r.name);
                foreach (var r in castle.Lod2) Assert.IsTrue(owned.Contains(r), castle.Civ + " castle group holds " + r.name);
                foreach (var r in owned) Assert.IsTrue(castle.Owns(r), castle.Civ + ": castle group holds only castle parts, not " + r.name);
            }

            // No LODGroup anywhere under the test root, other than each castle's own, references a castle renderer.
            var castleRenderers = new HashSet<Renderer>();
            var castleGroups = new HashSet<LODGroup>();
            foreach (var go in spawned)
            {
                var castle = go.GetComponent<CivCastle>();
                castleGroups.Add(castle.Group);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (castle.Owns(r)) castleRenderers.Add(r);
            }
            foreach (var group in _root.GetComponentsInChildren<LODGroup>(true))
            {
                if (castleGroups.Contains(group)) continue;
                foreach (var lod in group.GetLODs())
                    foreach (var r in lod.renderers)
                        Assert.IsFalse(r != null && castleRenderers.Contains(r),
                            group.name + " (not a castle group) holds castle renderer " + (r != null ? r.name : ""));
            }

            var lodWarnings = logged.FindAll(m => m.IndexOf("LOD", System.StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(lodWarnings, Is.Empty, "LOD warnings while spawning castles:\n" + string.Join("\n", lodWarnings));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CastleGhost_ShowsOnlyTheFullDetailBody()
        {
            var ghost = SpawnCommons(CivId.TetherExchange, null, ghost: true);
            var castle = ghost.GetComponent<CivCastle>();
            Assert.IsNotNull(castle);
            foreach (var r in castle.Lod0) Assert.IsTrue(r.enabled, r.name);
            foreach (var r in castle.Lod1) Assert.IsFalse(r.enabled, r.name);
            foreach (var r in castle.Lod2) Assert.IsFalse(r.enabled, r.name);
            Assert.AreEqual(0, ghost.GetComponentsInChildren<Collider>(true).Length);
        }

        // ---- (b) gate direction ------------------------------------------------------------

        /// <summary>
        /// How the gate is measured: each castle names a tower anchor on its gate side (authored at
        /// Blender -Y; <see cref="CivCastleSpec.FrontAnchor"/>). In the spawned Commons, in the
        /// building root's space, that anchor must sit at z &lt; -1 m (the gate faces -Z, toward the
        /// play camera at yaw 45°) and on the expected side in x, which also catches a mirrored
        /// import (a 180° turn alone flips both signs).
        /// </summary>
        [Test]
        public void Castle_GateFacesMinusZ()
        {
            foreach (var civ in CastleCivs)
            {
                var spec = CivCastleCatalog.For(civ);
                var go = SpawnCommons(civ);
                var front = Find(go.transform, spec.FrontAnchor);
                Assert.IsNotNull(front, civ + " " + spec.FrontAnchor);
                Vector3 p = go.transform.InverseTransformPoint(front.position);
                Assert.Less(p.z, -1f, $"{civ}: gate-side anchor {spec.FrontAnchor} at {p} should be on -Z " +
                                      $"(CivCastleLoader.ImportYawDegrees = {CivCastleLoader.ImportYawDegrees})");
                if (spec.FrontAnchorSignX == 0)
                    Assert.Less(Mathf.Abs(p.x), 0.5f, $"{civ}: {spec.FrontAnchor} on the centre line, at {p}");
                else
                    Assert.AreEqual(spec.FrontAnchorSignX, (int)Mathf.Sign(p.x), $"{civ}: {spec.FrontAnchor} side in x, at {p} (mirrored?)");
            }
        }

        // ---- (c) footprint -----------------------------------------------------------------

        [Test]
        public void Castle_FitsTheCommonsPlot()
        {
            float plot = ColonyLayout.FootprintMeters(BuildingCategory.Commons);
            Assert.AreEqual(18f, plot, 1e-4f, "12 cells x 1.5 m");
            const float slack = 0.1f;
            foreach (var civ in CastleCivs)
            {
                var go = SpawnCommons(civ);
                var castle = go.GetComponent<CivCastle>();
                Assert.IsNotNull(castle, civ.ToString());
                var parts = new List<Renderer>(castle.Lod0);
                parts.AddRange(castle.Core);
                Bounds b = default;
                bool any = false;
                foreach (var r in parts)
                {
                    if (r == null) continue;
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
                Assert.IsTrue(any, civ + " has renderers");
                Vector3 o = go.transform.position;
                Vector3 min = b.min - o, max = b.max - o;
                Assert.LessOrEqual(b.size.x, plot + slack, $"{civ} width {b.size.x:F2} m");
                Assert.LessOrEqual(b.size.z, plot + slack, $"{civ} depth {b.size.z:F2} m");
                Assert.GreaterOrEqual(min.x, -plot * 0.5f - slack, $"{civ} min x {min.x:F2}");
                Assert.LessOrEqual(max.x, plot * 0.5f + slack, $"{civ} max x {max.x:F2}");
                Assert.GreaterOrEqual(min.z, -plot * 0.5f - slack, $"{civ} min z {min.z:F2}");
                Assert.LessOrEqual(max.z, plot * 0.5f + slack, $"{civ} max z {max.z:F2}");
                Assert.Greater(Mathf.Max(b.size.x, b.size.z), plot * 0.6f, $"{civ} fills the plot (imported 1:1, not shrunk)");
                Assert.Greater(b.size.y, 8f, $"{civ} stands tall");
            }
        }

        // ---- (d) slot colours --------------------------------------------------------------

        [Test]
        public void Castle_TeamColourIsTheCivColour_AccentIsThePlayerAccent()
        {
            foreach (var civ in CastleCivs)
            {
                Assert.IsTrue(CivAccents.CanUse(Slate, civ, null), civ + ": the test accent is legal");
                var spec = CivCastleCatalog.For(civ);
                var go = SpawnCommons(civ, Slate);
                var castle = go.GetComponent<CivCastle>();
                bool team = false, accent = false, trim = false;
                foreach (var r in castle.Lod0)
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        Assert.IsNotNull(m, civ + " " + r.name + " material");
                        StringAssert.StartsWith(CivCastleLoader.MaterialPrefix, m.name,
                            civ + ": every castle slot is painted (FBX slot names resolved)");
                        Color c = CivCastleLoader.BaseColour(m);
                        switch (CivCastleLoader.SlotOf(m))
                        {
                            case CivCastleLoader.Slot.TeamColour:
                                team = true;
                                AssertColour(CivCatalog.Get(civ).teamSecondary, c, civ + " TeamColour = civ colour");
                                break;
                            case CivCastleLoader.Slot.Accent:
                                accent = true;
                                AssertColour(Slate, c, civ + " Accent = player accent");
                                break;
                            case CivCastleLoader.Slot.Trim:
                                trim = true;
                                AssertColour(spec.TrimTint, c, civ + " Trim tint");
                                break;
                        }
                    }
                }
                Assert.IsTrue(team, civ + " has a TeamColour slot");
                Assert.IsTrue(accent, civ + " has an Accent slot");
                Assert.IsTrue(trim, civ + " has a Trim slot");

                bool coreGlow = false;
                foreach (var r in castle.Core)
                    foreach (var m in r.sharedMaterials)
                        if (CivCastleLoader.SlotOf(m) == CivCastleLoader.Slot.CoreEmission)
                            coreGlow = m.IsKeywordEnabled("_EMISSION");
                Assert.IsTrue(coreGlow, civ + ": the Core glows");
            }

            // The imported model's own materials are shared assets and stay untouched.
            var prefab = Resources.Load<GameObject>(CivCastleCatalog.For(CivId.LumenConcord).ResourcePath);
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null)
                        Assert.IsFalse(m.name.StartsWith(CivCastleLoader.MaterialPrefix), "FBX material " + m.name + " not replaced");
        }

        [Test]
        public void Castle_NoAccentPicked_PaintsADarkerCivColour()
        {
            var go = SpawnCommons(CivId.BastionMandate);
            var castle = go.GetComponent<CivCastle>();
            Color civColour = CivCatalog.Get(CivId.BastionMandate).teamSecondary;
            foreach (var r in castle.Lod0)
                foreach (var m in r.sharedMaterials)
                    if (CivCastleLoader.SlotOf(m) == CivCastleLoader.Slot.Accent)
                    {
                        Color c = CivCastleLoader.BaseColour(m);
                        Assert.IsFalse(CivAccents.OutshinesCiv(c, CivId.BastionMandate));
                        Assert.Less(c.maxColorComponent, civColour.maxColorComponent);
                    }
        }

        // ---- (e) the neutral civ -----------------------------------------------------------

        [Test]
        public void NeutralCiv_KeepsTheProceduralCommons()
        {
            foreach (var bound in new[] { null, CivModifiers.For(CivId.None) })
            {
                ModularBuildingFactory.BindCiv(bound);
                var go = ModularBuildingFactory.Spawn(BuildingCategory.Commons, Vector3.zero, _root.transform);
                Assert.IsNull(go.GetComponent<CivCastle>(), "no castle for None");
                Assert.IsNotNull(Find(go.transform, "CommonsGateLintel"), "procedural gatehouse");
                Assert.IsNotNull(Find(go.transform, "CommonsPlinth"), "procedural plinth");
                Assert.IsNotNull(Find(go.transform, "CommonsDome").GetComponent<MeshFilter>(), "the dome tier is a mesh");
                Assert.AreEqual(HeroBuildingKits.CitadelCrownY, CivCastleLoader.CrownHeight(go.transform), 1e-4f);
            }
        }

        // ---- accent rules ------------------------------------------------------------------

        [Test]
        public void Accent_MayNotOutshineTheCivColour()
        {
            foreach (var civ in CastleCivs)
            {
                Color civColour = CivCatalog.Get(civ).teamSecondary;
                Assert.AreEqual(civColour, CivAccents.CivColour(civ).Value, civ + ": the castle's TeamColour is the civ colour");
                CivPalette.Hsv(civColour, out float h, out float s, out float v);
                CivPalette.Hsv(Slate, out float ah, out float asat, out float av);

                var brighter = Color.HSVToRGB(ah / 360f, Mathf.Min(asat, s) * 0.9f, Mathf.Min(1f, v + 0.12f));
                var richer = Color.HSVToRGB(ah / 360f, Mathf.Min(1f, s + 0.08f), v * 0.9f);
                Assert.IsTrue(CivAccents.OutshinesCiv(brighter, civ), civ + ": brighter than the civ colour");
                Assert.IsTrue(CivAccents.OutshinesCiv(richer, civ), civ + ": more saturated than the civ colour");
                Assert.IsFalse(CivAccents.CanUse(brighter, civ, null), civ + ": brighter accent refused");
                Assert.IsFalse(CivAccents.CanUse(richer, civ, null), civ + ": more saturated accent refused");
                Assert.IsTrue(CivAccents.CanUse(Slate, civ, null), civ + ": a muted accent is fine");

                Color capped = CivAccents.ForCastle(brighter, civ);
                Assert.IsFalse(CivAccents.OutshinesCiv(capped, civ), civ + ": an outshining accent is capped when painted");
                CivPalette.Hsv(capped, out float ch, out _, out _);
                Assert.Less(CivPalette.HueGap(ch, ah), 1f, civ + ": capping keeps the hue");
            }
            Assert.IsFalse(CivAccents.OutshinesCiv(Color.white, CivId.None), "the neutral civ has no colour to protect");
            Assert.IsTrue(CivAccents.CanUse(new Color(0.85f, 0.15f, 0.65f), null), "the civ-less check is unchanged for magenta");
        }

        [Test]
        public void Accent_ExchangeVioletAndThreatRedsAreBlocked()
        {
            Color violet = CivCatalog.Get(CivId.TetherExchange).teamSecondary;
            Assert.IsTrue(CivAccents.ReadsAsExchangeViolet(violet));
            Assert.IsFalse(CivAccents.CanUse(violet, null), "the Exchange violet");
            Assert.IsFalse(CivAccents.CanUse(new Color(0.36f, 0.22f, 0.58f), null), "a darker violet");
            Assert.IsFalse(CivAccents.CanUse(new Color(0.62f, 0.50f, 0.90f), null), "a lighter violet");
            Assert.IsFalse(CivAccents.CanUse(new Color(0.42f, 0.30f, 0.62f), CivId.TetherExchange, null), "violet on the Exchange itself");

            Assert.IsFalse(CivAccents.CanUse(CivPalette.ThreatFlagRed, null), "threat flag red");
            Assert.IsFalse(CivAccents.CanUse(CivPalette.DangerMapRed, null), "map danger red");
            Assert.IsFalse(CivAccents.CanUse(CivPalette.AlertIconRed, null), "alert icon red");
            Assert.IsFalse(CivAccents.CanUse(new Color(0.80f, 0.12f, 0.22f), null), "a crimson reads as a warning");
            Assert.IsFalse(CivAccents.CanUse(new Color(0.90f, 0.20f, 0.08f), null), "an orange-red reads as a warning");

            Assert.IsTrue(CivAccents.CanUse(Slate, null), "slate blue is fine");
            Assert.IsTrue(CivAccents.CanUse(new Color(0.85f, 0.15f, 0.65f), null), "magenta is fine");
            Assert.IsTrue(CivAccents.CanUse(new Color(0.20f, 0.30f, 0.90f), null), "blue is fine");
            Assert.IsFalse(CivAccents.ReadsAsThreatRed(CivCatalog.Get(CivId.BastionMandate).teamSecondary),
                "the Mandate's oxide red is dark enough not to read as a warning");
        }

        private static void AssertColour(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, what + " (r)");
            Assert.AreEqual(expected.g, actual.g, 0.01f, what + " (g)");
            Assert.AreEqual(expected.b, actual.b, 0.01f, what + " (b)");
        }
    }
}
