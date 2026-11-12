using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Team marks use the civ's second colour, the same colour a castle paints on its TeamColour
    /// slot. Hired classes and military units wear it. Scouts are left for the stripe pass and
    /// engineers keep the authored orange. The neutral civ (guided Earth start, old saves) keeps
    /// today's orange.
    /// </summary>
    public class CivTeamMarkTests
    {
        private static readonly Color Slate = new Color(0.22f, 0.32f, 0.50f);

        private static readonly CivId[] Playable =
        {
            CivId.LumenConcord, CivId.TetherExchange, CivId.BastionMandate, CivId.PathfinderAccord
        };

        private readonly List<Object> _trash = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            ModularBuildingFactory.BindCiv(null);
            for (int i = _trash.Count - 1; i >= 0; i--)
                if (_trash[i] != null) Object.DestroyImmediate(_trash[i]);
            _trash.Clear();
        }

        [Test]
        public void TeamMark_IsTheCivSecondColour_AndMatchesTheCastle()
        {
            foreach (var civ in Playable)
            {
                Assert.IsTrue(CivAccents.CanUse(Slate, civ, null), civ + " accent is legal");
                var mods = CivModifiers.For(civ, Slate);
                Color mark = CivTeamMark.Colour(mods);
                Color civColour = CivCatalog.Get(civ).teamSecondary;
                AssertColour(civColour, mark, civ + " mark");
                AssertColour(mark, CivTeamMark.Colour(civ), civ + " id");
                AssertColour(mark, CivCastleLoader.SlotColour(
                        CivCastleLoader.Slot.TeamColour, CivCastleCatalog.For(civ), mods),
                    civ + " matches the castle TeamColour slot");
                AssertColour(CivAccents.ForCastle(Slate, civ),
                    CivCastleLoader.SlotColour(CivCastleLoader.Slot.Accent, CivCastleCatalog.For(civ), mods),
                    civ + " accent slot is still the player accent");
                Assert.Greater(CivPalette.Distance(mark, Slate), 0.2f, civ + " mark is not the accent");
                AssertColour(mark, CivTeamMark.Owned(mods, new Color(0.96f, 0.42f, 0.08f, 0.85f)), civ + " owned rgb");
                Assert.AreEqual(0.85f, CivTeamMark.Owned(mods, new Color(0.96f, 0.42f, 0.08f, 0.85f)).a, 0.001f,
                    civ + " owned keeps the marker alpha");
            }
        }

        [Test]
        public void Neutral_IsTodaysOrange_AndDoesNotMarkAHero()
        {
            Color orange = CivCatalog.Get(CivId.None).teamSecondary;
            AssertColour(orange, CivTeamMark.Colour(CivId.None), "neutral");
            AssertColour(orange, CivTeamMark.Colour((CivModifiers)null), "no owner");
            var guided = CivModifiers.For(CivId.None, Slate);
            AssertColour(orange, CivTeamMark.Colour(guided), "guided start ignores the accent");
            AssertColour(orange, CivCastleLoader.SlotColour(
                    CivCastleLoader.Slot.TeamColour, null, guided),
                "castle formula for the neutral civ is the same orange");
            ModularBuildingFactory.BindCiv(null);
            AssertColour(orange, CivTeamMark.ColourBound(), "an unbound colony is neutral");

            Color ringToday = new Color(0.96f, 0.42f, 0.08f, 0.85f);
            AssertColour(ringToday, CivTeamMark.Owned(guided, ringToday), "neutral ring stays today's orange");
            Assert.AreEqual(ringToday.a, CivTeamMark.Owned(null, ringToday).a, 0.001f, "neutral alpha");

            var mech = LoadUnit(SpecialistClass.DefenseMech);
            Color authored = CivTeamMark.BaseColour(FirstOrange(mech));
            CivTeamMark.ApplyToHero(mech, SpecialistClass.DefenseMech, guided);
            Assert.IsNull(Find(mech.transform, CivTeamMark.MarkName), "a neutral mech grows no band");
            AssertOrangeBlocks(mech, null, "neutral mech keeps the authored orange");
            AssertColour(authored, CivTeamMark.BaseColour(FirstOrange(mech)), "shared orange material is not rewritten");

            var flag = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            CivTeamMark.AttachFlagRim(flag.transform, guided);
            Assert.IsNull(Find(flag.transform, CivTeamMark.MarkName), "a neutral flag grows no rim");

            var ghost = Ghost();
            var orange = FirstOrange(ghost);
            Color ghostAuthored = CivTeamMark.BaseColour(orange);
            CivTeamMark.PaintGhost(ghost, true, CivId.None);
            Assert.IsNull(Find(ghost.transform, CivTeamMark.MarkName), "a neutral ghost grows no rim");
            AssertColour(new Color(0.25f, 1f, 0.45f, 0.38f), GhostColour(ghost, orange: true), "neutral ghost orange follows the valid tint");
            Assert.AreEqual(0.38f, GhostColour(ghost, orange: true).a, 0.01f, "neutral ghost alpha");
            AssertColour(new Color(0.25f, 1f, 0.45f, 0.38f), GhostColour(ghost, orange: false), "neutral ghost hull follows the valid tint");
            AssertColour(ghostAuthored, CivTeamMark.BaseColour(orange), "ghost tint does not rewrite the shared orange");
        }

        [Test]
        public void HeroesAndMilitary_WearTheCivColour_EngineerAndScoutDoNot()
        {
            var concord = CivModifiers.For(CivId.LumenConcord, Slate);
            foreach (SpecialistClass cls in System.Enum.GetValues(typeof(SpecialistClass)))
            {
                var unit = LoadUnit(cls);
                var orange = FirstOrange(unit);
                Color authored = CivTeamMark.BaseColour(orange);
                CivTeamMark.ApplyToHero(unit, cls, concord);

                if (!CivTeamMark.Wears(cls))
                {
                    Assert.IsNull(Find(unit.transform, CivTeamMark.MarkName), cls + " grows no band");
                    AssertOrangeBlocks(unit, null, cls + " orange stays authored");
                    AssertColour(authored, CivTeamMark.BaseColour(orange), cls + " material");
                    continue;
                }

                AssertColour(concord.TeamSecondary, BlockColour(Find(unit.transform, CivTeamMark.MarkName)), cls + " band");
                AssertOrangeBlocks(unit, concord.TeamSecondary, cls + " orange slot");
                AssertColour(authored, CivTeamMark.BaseColour(orange), cls + " shared orange material stays orange");
                AssertUntinted(unit, cls + " non-orange slots");

                var mat = Find(unit.transform, CivTeamMark.MarkName).GetComponent<Renderer>().sharedMaterial;
                IndustrialArtDressing.Apply(unit);
                Assert.AreSame(mat, Find(unit.transform, CivTeamMark.MarkName).GetComponent<Renderer>().sharedMaterial,
                    cls + " dressing does not remap the mark");
            }

            Assert.IsFalse(CivTeamMark.Wears(SpecialistClass.EngineerBot), "an engineer keeps the authored orange");
            Assert.IsFalse(CivTeamMark.Wears(SpecialistClass.ScoutDrone), "a scout is not painted by this pass");
            Assert.IsTrue(CivTeamMark.Wears(SpecialistClass.DefenseMech), "a military unit wears the mark");
            Assert.IsTrue(CivTeamMark.Wears(SpecialistClass.Medic), "a hired class wears the mark");
        }

        [Test]
        public void MarkerFlagAndGhost_UseTheCivColour()
        {
            var exchange = CivModifiers.For(CivId.TetherExchange, Slate);
            var ring = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            Color today = new Color(0.96f, 0.42f, 0.08f, 0.85f);
            CivTeamMark.PaintMarker(ring.GetComponent<Renderer>(), exchange, today);
            AssertColour(exchange.TeamSecondary, CivTeamMark.BaseColour(ring.GetComponent<Renderer>().sharedMaterial), "select ring");
            Assert.AreEqual(0.85f, CivTeamMark.BaseColour(ring.GetComponent<Renderer>().sharedMaterial).a, 0.001f, "ring alpha");
            Assert.Greater(CivPalette.Distance(exchange.TeamSecondary, Slate), 0.2f, "the ring is not the accent");

            var flag = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            var banner = flag.GetComponent<Renderer>();
            var bannerMat = new Material(banner.sharedMaterial) { name = "FlagBanner" };
            Color explore = new Color(0.3f, 0.85f, 1f, 1f);
            if (bannerMat.HasProperty("_BaseColor")) bannerMat.SetColor("_BaseColor", explore);
            if (bannerMat.HasProperty("_Color")) bannerMat.SetColor("_Color", explore);
            banner.sharedMaterial = bannerMat;
            CivTeamMark.AttachFlagRim(flag.transform, exchange);
            AssertColour(exchange.TeamSecondary, BlockColour(Find(flag.transform, CivTeamMark.MarkName)), "flag rim");
            AssertColour(explore, CivTeamMark.BaseColour(banner.sharedMaterial), "banner colour stays the flag type");
            AssertColour(exchange.TeamSecondary,
                CivCastleLoader.SlotColour(CivCastleLoader.Slot.TeamColour, CivCastleCatalog.For(CivId.TetherExchange), exchange),
                "flag rim matches the castle");

            var ghost = Ghost();
            var orange = FirstOrange(ghost);
            Color authored = CivTeamMark.BaseColour(orange);
            CivTeamMark.PaintGhost(ghost, true, CivId.BastionMandate);
            Color oxide = CivCatalog.Get(CivId.BastionMandate).teamSecondary;
            AssertColour(oxide, BlockColour(Find(ghost.transform, CivTeamMark.MarkName)), "ghost rim");
            Color slot = GhostColour(ghost, orange: true);
            AssertColour(new Color(oxide.r, oxide.g, oxide.b, 0.38f), slot, "ghost orange slot");
            Assert.AreEqual(0.38f, slot.a, 0.01f, "ghost orange alpha");
            AssertColour(new Color(0.25f, 1f, 0.45f, 0.38f), GhostColour(ghost, orange: false), "ghost hull stays the valid tint");
            AssertColour(authored, CivTeamMark.BaseColour(orange), "shared orange material is not rewritten");

            CivTeamMark.PaintGhost(ghost, false, CivId.BastionMandate);
            Color invalid = GhostColour(ghost, orange: true);
            AssertColour(new Color(oxide.r, oxide.g, oxide.b, 0.42f), invalid, "invalid ghost keeps the team hue");
            Assert.AreEqual(0.42f, invalid.a, 0.01f, "invalid ghost alpha");
            Assert.AreEqual(1, Count(ghost.transform, CivTeamMark.MarkName), "a second tint does not stack rims");
        }

        private GameObject LoadUnit(SpecialistClass cls)
        {
            var prefab = UnitMeshCatalog.LoadForClass(cls);
            Assert.IsNotNull(prefab, cls + " mesh");
            return Track(UnitMeshCatalog.InstantiateClean(prefab, "Unit_" + cls));
        }

        private GameObject Ghost()
        {
            var root = Track(new GameObject("Ghost_Habitat"));
            var orange = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            orange.name = "Trim";
            orange.transform.SetParent(root.transform, false);
            orange.transform.localScale = new Vector3(2f, 1f, 2f);
            var orangeMat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                                         ?? Shader.Find("Standard"))
            { name = "SM_Art_Orange" };
            Color kit = CivCatalog.Get(CivId.None).teamSecondary;
            if (orangeMat.HasProperty("_BaseColor")) orangeMat.SetColor("_BaseColor", kit);
            if (orangeMat.HasProperty("_Color")) orangeMat.SetColor("_Color", kit);
            if (orangeMat.HasProperty("_EmissionColor"))
                orangeMat.SetColor("_EmissionColor", new Color(1.4f, 0.42f, 0.06f));
            orange.GetComponent<Renderer>().sharedMaterial = orangeMat;

            var hull = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            hull.name = "Hull";
            hull.transform.SetParent(root.transform, false);
            hull.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var hullMat = new Material(orangeMat) { name = "SM_Art_WhiteHull" };
            if (hullMat.HasProperty("_BaseColor")) hullMat.SetColor("_BaseColor", new Color(0.9f, 0.89f, 0.86f));
            if (hullMat.HasProperty("_Color")) hullMat.SetColor("_Color", new Color(0.9f, 0.89f, 0.86f));
            hull.GetComponent<Renderer>().sharedMaterial = hullMat;
            return root;
        }

        private GameObject Track(GameObject go)
        {
            _trash.Add(go);
            return go;
        }

        private static Transform Find(Transform root, string name)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        private static int Count(Transform root, string name)
        {
            int n = 0;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) n++;
            return n;
        }

        private static Material FirstOrange(GameObject root)
        {
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                if (rends[r].gameObject.name == CivTeamMark.MarkName) continue;
                var mats = rends[r].sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                    if (CivTeamMark.IsKitOrange(mats[i]) &&
                        !mats[i].name.StartsWith("SM_GhostCopy_", System.StringComparison.Ordinal))
                        return mats[i];
            }
            Assert.Fail(root.name + " has no orange slot");
            return null;
        }

        private static Color GhostColour(GameObject root, bool orange)
        {
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                if (rends[r].gameObject.name == CivTeamMark.MarkName) continue;
                var mats = rends[r].sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    bool isOrange = mats[i].name.IndexOf("orange", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (isOrange != orange) continue;
                    return CivTeamMark.BaseColour(mats[i]);
                }
            }
            Assert.Fail(root.name + " missing ghost " + (orange ? "orange" : "hull"));
            return Color.clear;
        }

        private static void AssertOrangeBlocks(GameObject root, Color? expected, string what)
        {
            int n = 0;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                var rend = rends[r];
                if (rend.gameObject.name == CivTeamMark.MarkName) continue;
                var mats = rend.sharedMaterials;
                int count = mats != null ? mats.Length : 0;
                for (int i = 0; i < count; i++)
                {
                    if (!CivTeamMark.IsKitOrange(mats[i])) continue;
                    n++;
                    var block = new MaterialPropertyBlock();
                    if (count <= 1) rend.GetPropertyBlock(block);
                    else rend.GetPropertyBlock(block, i);
                    if (expected.HasValue)
                    {
                        Assert.IsFalse(block.isEmpty, what);
                        AssertColour(expected.Value, block.GetColor("_BaseColor"), what);
                    }
                    else
                        Assert.IsTrue(block.isEmpty, what);
                }
            }
            Assert.Greater(n, 0, what + " found an orange slot");
        }

        private static void AssertUntinted(GameObject root, string what)
        {
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                var rend = rends[r];
                if (rend.gameObject.name == CivTeamMark.MarkName) continue;
                var mats = rend.sharedMaterials;
                int count = mats != null ? mats.Length : 0;
                for (int i = 0; i < count; i++)
                {
                    if (CivTeamMark.IsKitOrange(mats[i])) continue;
                    var block = new MaterialPropertyBlock();
                    if (count <= 1) rend.GetPropertyBlock(block);
                    else rend.GetPropertyBlock(block, i);
                    Assert.IsTrue(block.isEmpty, what + " " + rend.name);
                }
            }
        }

        private static Color BlockColour(Transform mark)
        {
            Assert.IsNotNull(mark);
            return CivTeamMark.BaseColour(mark.GetComponent<Renderer>().sharedMaterial);
        }

        private static void AssertColour(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, what + " r");
            Assert.AreEqual(expected.g, actual.g, 0.01f, what + " g");
            Assert.AreEqual(expected.b, actual.b, 0.01f, what + " b");
        }
    }
}
