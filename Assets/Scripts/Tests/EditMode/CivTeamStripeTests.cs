using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Team stripes use the civ's second colour, the same colour a castle paints on its TeamColour
    /// slot. Scout drones, tax collectors and companions wear it. Other units keep their orange.
    /// The neutral civ (guided Earth start, old saves) keeps today's orange, and a scout keeps the
    /// authored orange slot with no extra band.
    /// </summary>
    public class CivTeamStripeTests
    {
        private static readonly Color Slate = new Color(0.22f, 0.32f, 0.50f);

        private static readonly CivId[] Playable =
        {
            CivId.LumenConcord, CivId.TetherExchange, CivId.BastionMandate, CivId.PathfinderAccord
        };

        private readonly List<Object> _trash = new List<Object>();
        private GameLoop _loop;

        [TearDown]
        public void TearDown()
        {
            if (_loop != null) _loop.ReleaseRosterForTests();
            for (int i = _trash.Count - 1; i >= 0; i--)
                if (_trash[i] != null) Object.DestroyImmediate(_trash[i]);
            _trash.Clear();
            _loop = null;
        }

        [Test]
        public void TeamStripe_IsTheCivSecondColour_NotThePlayerAccent()
        {
            foreach (var civ in Playable)
            {
                Assert.IsTrue(CivAccents.CanUse(Slate, civ, null), civ + " accent is legal");
                var mods = CivModifiers.For(civ, Slate);
                Color stripe = CivAccents.TeamStripe(mods);
                Color civColour = CivCatalog.Get(civ).teamSecondary;
                AssertColour(civColour, stripe, civ + " stripe");
                AssertColour(stripe, CivAccents.TeamStripe(civ), civ + " id");
                AssertColour(stripe, CivCastleLoader.SlotColour(CivCastleLoader.Slot.TeamColour, CivCastleCatalog.For(civ), mods),
                    civ + " matches the castle TeamColour slot");
                AssertColour(CivAccents.ForCastle(Slate, civ),
                    CivCastleLoader.SlotColour(CivCastleLoader.Slot.Accent, CivCastleCatalog.For(civ), mods),
                    civ + " accent slot is still the player accent");
                Assert.Greater(CivPalette.Distance(stripe, Slate), 0.2f, civ + " stripe is not the accent");
            }
        }

        [Test]
        public void Neutral_IsTodaysOrange_AndDoesNotPaintAScoutBand()
        {
            Color orange = CivCatalog.Get(CivId.None).teamSecondary;
            AssertColour(orange, CivAccents.TeamStripe(CivId.None), "neutral");
            AssertColour(orange, CivAccents.TeamStripe((CivModifiers)null), "no owner");
            var guided = CivModifiers.For(CivId.None, Slate);
            AssertColour(orange, CivAccents.TeamStripe(guided), "guided start ignores the accent");
            AssertColour(orange, CivCastleLoader.SlotColour(CivCastleLoader.Slot.TeamColour, null, guided),
                "castle formula for the neutral civ is the same orange");

            var scout = LoadUnit(UnitMeshCatalog.LoadScout(), "Unit_ScoutDrone");
            Color authored = CivTeamStripe.BaseColour(FirstOrange(scout));
            CivTeamStripe.ApplyToUnit(scout, SpecialistClass.ScoutDrone, guided);
            Assert.IsNull(Find(scout.transform, CivTeamStripe.StripeName), "neutral scout grows no band");
            AssertOrangeBlocks(scout, null, "neutral scout keeps the authored orange");
            AssertColour(authored, CivTeamStripe.BaseColour(FirstOrange(scout)), "shared orange material is not rewritten");
        }

        [Test]
        public void Scout_WearsTheCivColour_OtherClassesDoNot()
        {
            var concord = CivModifiers.For(CivId.LumenConcord, Slate);
            var scout = LoadUnit(UnitMeshCatalog.LoadScout(), "Unit_ScoutDrone");
            var orange = FirstOrange(scout);
            Color authored = CivTeamStripe.BaseColour(orange);

            CivTeamStripe.ApplyToUnit(scout, SpecialistClass.ScoutDrone, concord);

            AssertColour(concord.TeamSecondary, BlockColour(Find(scout.transform, CivTeamStripe.StripeName)), "scout band");
            AssertOrangeBlocks(scout, concord.TeamSecondary, "scout orange slot");
            AssertColour(authored, CivTeamStripe.BaseColour(orange), "the shared orange material stays orange for everyone else");
            AssertUntinted(scout, "non-orange scout slots");

            var engineer = LoadUnit(UnitMeshCatalog.LoadEngineer(), "Unit_EngineerBot");
            var engineerOrange = FirstOrange(engineer);
            Color engineerAuthored = CivTeamStripe.BaseColour(engineerOrange);
            CivTeamStripe.ApplyToUnit(engineer, SpecialistClass.EngineerBot, concord);
            Assert.IsNull(Find(engineer.transform, CivTeamStripe.StripeName), "an engineer is not a drone");
            AssertOrangeBlocks(engineer, null, "engineer orange stays authored");
            AssertColour(engineerAuthored, CivTeamStripe.BaseColour(engineerOrange), "engineer material");
        }

        [Test]
        public void Collector_WearsTheCivColour_AndKeepsGold()
        {
            var exchange = Arm(CivId.TetherExchange);
            var collector = SpawnCollector();
            var stripe = Find(collector.transform, CivTeamStripe.StripeName);
            Assert.IsNotNull(stripe, "collector rim");
            AssertColour(exchange.TeamSecondary, CivTeamStripe.BaseColour(stripe.GetComponent<Renderer>().sharedMaterial), "exchange violet");
            Assert.Greater(CivPalette.Distance(CivTeamStripe.BaseColour(stripe.GetComponent<Renderer>().sharedMaterial), Slate), 0.2f,
                "the rim is not the player accent");

            AssertColour(new Color(0.98f, 0.78f, 0.2f), Albedo(Find(collector.transform, "Ring")), "gold skirt");
            AssertColour(new Color(1f, 0.72f, 0.2f), Albedo(Find(collector.transform, "Eye")), "eye");
            AssertColour(new Color(0.22f, 0.24f, 0.27f), Albedo(Find(collector.transform, "Body")), "body");

            var mat = stripe.GetComponent<Renderer>().sharedMaterial;
            IndustrialArtDressing.Apply(collector.gameObject);
            Assert.AreSame(mat, stripe.GetComponent<Renderer>().sharedMaterial, "dressing does not remap the stripe");
            AssertColour(exchange.TeamSecondary, CivTeamStripe.BaseColour(mat), "stripe colour after dressing");

            _loop.SetCivForTests(CivModifiers.For(CivId.None));
            var neutral = SpawnCollector();
            var neutralStripe = Find(neutral.transform, CivTeamStripe.StripeName);
            AssertColour(CivCatalog.Get(CivId.None).teamSecondary,
                CivTeamStripe.BaseColour(neutralStripe.GetComponent<Renderer>().sharedMaterial),
                "a neutral collector wears today's orange");
        }

        [Test]
        public void Companion_WearsTheCivColour_AndKeepsItsOwnColour()
        {
            var pathfinder = Arm(CivId.PathfinderAccord);
            var owner = Track(new GameObject("owner")).AddComponent<SpecialistAgent>();
            var tuning = new AbilityTuning();
            Assert.GreaterOrEqual(tuning.companions.Length, 2);

            var escort = CompanionDrone.Spawn(_loop, owner, tuning.companions[0]);
            var hauler = CompanionDrone.Spawn(_loop, owner, tuning.companions[1]);
            Track(escort.gameObject);
            Track(hauler.gameObject);

            AssertColour(pathfinder.TeamSecondary, StripeColour(escort.transform), "escort rim");
            AssertColour(pathfinder.TeamSecondary, StripeColour(hauler.transform), "hauler rim");
            AssertColour(tuning.companions[0].color, BodyColour(escort), "escort body");
            AssertColour(tuning.companions[1].color, BodyColour(hauler), "hauler body");
            Assert.Greater(CivPalette.Distance(tuning.companions[0].color, tuning.companions[1].color), 0.2f,
                "the two companions still read as different bodies");
        }

        [Test]
        public void Band_SitsAtTheWaist_AndStandsProudOfTheBody()
        {
            var pathfinder = Arm(CivId.PathfinderAccord);
            var collector = SpawnCollector();
            AssertReadable(collector.transform, pathfinder.TeamSecondary, "Pathfinder collector");

            var owner = Track(new GameObject("owner")).AddComponent<SpecialistAgent>();
            var tuning = new AbilityTuning();
            var escort = CompanionDrone.Spawn(_loop, owner, tuning.companions[0]);
            var hauler = CompanionDrone.Spawn(_loop, owner, tuning.companions[1]);
            Track(escort.gameObject);
            Track(hauler.gameObject);
            AssertReadable(escort.transform, pathfinder.TeamSecondary, "Pathfinder escort");
            AssertReadable(hauler.transform, pathfinder.TeamSecondary, "Pathfinder hauler");

            var scout = LoadUnit(UnitMeshCatalog.LoadScout(), "Unit_ScoutDrone");
            CivTeamStripe.ApplyToUnit(scout, SpecialistClass.ScoutDrone, pathfinder);
            AssertReadable(scout.transform, pathfinder.TeamSecondary, "Pathfinder scout");
        }

        [Test]
        public void Attach_UpdatesColour_AndDoesNotLeaveASecondBand()
        {
            Arm(CivId.PathfinderAccord);
            var collector = SpawnCollector();
            var visual = Find(collector.transform, "Drone");
            Assert.IsNotNull(visual, "collector visual");
            var concord = CivModifiers.For(CivId.LumenConcord, Slate);
            CivTeamStripe.AttachToCollector(visual, concord);
            var bands = 0;
            var all = collector.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == CivTeamStripe.StripeName) bands++;
            Assert.AreEqual(1, bands, "still one rim");
            AssertColour(concord.TeamSecondary,
                CivTeamStripe.BaseColour(Find(collector.transform, CivTeamStripe.StripeName).GetComponent<Renderer>().sharedMaterial),
                "re-attach picks up the new civ");
            AssertReadable(collector.transform, concord.TeamSecondary, "re-sized rim");
        }

        private CivModifiers Arm(CivId civ)
        {
            var go = Track(new GameObject("stripe-loop"));
            go.SetActive(false);
            _loop = go.AddComponent<GameLoop>();
            var mods = CivModifiers.For(civ, Slate);
            _loop.ArmRosterForTests(mods);
            return mods;
        }

        private LevyCollector SpawnCollector()
        {
            var home = Track(new GameObject("home")).AddComponent<ColonyStructure>();
            var director = new LevyCollectorDirector(null, _loop);
            var collector = LevyCollector.Spawn(Track(new GameObject("collectors")).transform, director, home, _trash.Count);
            Assert.IsNotNull(collector);
            Track(collector.gameObject);
            return collector;
        }

        private GameObject LoadUnit(GameObject prefab, string name)
        {
            Assert.IsNotNull(prefab, name + " mesh");
            return Track(UnitMeshCatalog.InstantiateClean(prefab, name));
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

        private static Material FirstOrange(GameObject root)
        {
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                var mats = rends[r].sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                    if (IsOrange(mats[i])) return mats[i];
            }
            Assert.Fail(root.name + " has no orange slot");
            return null;
        }

        private static void AssertOrangeBlocks(GameObject root, Color? expected, string what)
        {
            int n = 0;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                var rend = rends[r];
                if (rend.gameObject.name == CivTeamStripe.StripeName) continue;
                var mats = rend.sharedMaterials;
                int count = mats != null ? mats.Length : 0;
                for (int i = 0; i < count; i++)
                {
                    if (!IsOrange(mats[i])) continue;
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
                if (rend.gameObject.name == CivTeamStripe.StripeName) continue;
                var mats = rend.sharedMaterials;
                int count = mats != null ? mats.Length : 0;
                for (int i = 0; i < count; i++)
                {
                    if (IsOrange(mats[i])) continue;
                    var block = new MaterialPropertyBlock();
                    if (count <= 1) rend.GetPropertyBlock(block);
                    else rend.GetPropertyBlock(block, i);
                    Assert.IsTrue(block.isEmpty, what + " " + rend.name);
                }
            }
        }

        private static bool IsOrange(Material mat) =>
            mat != null && mat.name.IndexOf("orange", System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static Color StripeColour(Transform root)
        {
            var stripe = Find(root, CivTeamStripe.StripeName);
            Assert.IsNotNull(stripe, root.name + " stripe");
            return CivTeamStripe.BaseColour(stripe.GetComponent<Renderer>().sharedMaterial);
        }

        private static Color BodyColour(CompanionDrone drone)
        {
            var block = new MaterialPropertyBlock();
            drone.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        private static Color BlockColour(Transform stripe)
        {
            Assert.IsNotNull(stripe);
            return CivTeamStripe.BaseColour(stripe.GetComponent<Renderer>().sharedMaterial);
        }

        private static Color Albedo(Transform t)
        {
            Assert.IsNotNull(t);
            return CivTeamStripe.BaseColour(t.GetComponent<Renderer>().sharedMaterial);
        }

        private static void AssertReadable(Transform root, Color expected, string what)
        {
            var stripe = Find(root, CivTeamStripe.StripeName);
            Assert.IsNotNull(stripe, what + " has a rim");
            var rend = stripe.GetComponent<Renderer>();
            Assert.IsNotNull(rend, what + " renderer");
            AssertColour(expected, CivTeamStripe.BaseColour(rend.sharedMaterial), what + " colour");

            Bounds body = default;
            bool any = false;
            var rends = root.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null || rends[i].gameObject.name == CivTeamStripe.StripeName) continue;
                if (!any) { body = rends[i].bounds; any = true; }
                else body.Encapsulate(rends[i].bounds);
            }
            Assert.IsTrue(any, what + " body bounds");

            Bounds sb = rend.bounds;
            float bodyXz = Mathf.Max(body.size.x, body.size.z);
            float stripeXz = Mathf.Max(sb.size.x, sb.size.z);
            Assert.GreaterOrEqual(stripeXz, bodyXz + 2f * CivTeamStripe.MinReadableRim - 0.02f,
                what + " stands proud of the body (stripe xz " + stripeXz + " body xz " + bodyXz + ")");
            Assert.GreaterOrEqual(sb.size.y, CivTeamStripe.MinReadableHeight - 0.01f,
                what + " is tall enough to read from the play camera (height " + sb.size.y + ")");
            Assert.Less(Mathf.Abs(sb.center.y - body.center.y), Mathf.Max(0.08f, body.size.y * 0.25f),
                what + " sits at the waist, not as a cap (stripe y " + sb.center.y + " body y " + body.center.y + ")");
        }

        private static void AssertColour(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, what + " r");
            Assert.AreEqual(expected.g, actual.g, 0.01f, what + " g");
            Assert.AreEqual(expected.b, actual.b, 0.01f, what + " b");
        }
    }
}
