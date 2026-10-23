using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// The ScriptableObject databases are optional overrides. With none, every catalog must hand
    /// back exactly the built-in tables; with one, its entries win and missing ids keep their default.
    /// </summary>
    public class CatalogDatabaseTests
    {
        private readonly List<ScriptableObject> _made = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            // Back to "whatever Resources holds", which is how the game starts.
            TechCatalog.ReloadDatabase();
            ShopCatalog.ReloadDatabase();
            CelestialBodyCatalog.ReloadDatabase();
            foreach (var so in _made)
            {
                if (so != null) UnityEngine.Object.DestroyImmediate(so);
            }
            _made.Clear();
        }

        private T Track<T>(T so) where T : ScriptableObject
        {
            _made.Add(so);
            return so;
        }

        // ---- Default path ------------------------------------------------------------------

        [Test]
        public void NoTechDatabase_AllIsTheBuiltInTable()
        {
            TechCatalog.UseDatabase(null);
            Assert.IsFalse(TechCatalog.UsingDatabase);
            Assert.AreSame(TechCatalog.Defaults, TechCatalog.All);

            var field = TechCatalog.Get(TechId.FieldSurvey);
            Assert.AreEqual("Field Survey", field.DisplayName);
            Assert.AreEqual(18f, field.ScienceCost, 1e-4f);
            Assert.AreEqual(0, field.Prerequisites.Length);

            var lab = TechCatalog.Get(TechId.LabScience);
            Assert.AreEqual(0.35f, lab.ResearchRateBonus, 1e-4f);
            CollectionAssert.AreEqual(new[] { TechId.FieldSurvey }, lab.Prerequisites);

            Assert.IsTrue(TechCatalog.Get(TechId.MarsShip).UnlocksLaunch);
            Assert.IsNull(TechCatalog.Get(TechId.None));
        }

        [Test]
        public void NoTechDatabase_EverySavedTechIdHasExactlyOneEntry()
        {
            TechCatalog.UseDatabase(null);
            AssertEveryIdOnce<TechId, TechDef>(TechCatalog.All, d => d.Id, TechId.None);
        }

        [Test]
        public void NoShopDatabase_AllIsTheBuiltInList()
        {
            ShopCatalog.UseDatabase(null);
            Assert.IsFalse(ShopCatalog.UsingDatabase);
            Assert.AreSame(ShopCatalog.Defaults, ShopCatalog.All);

            var potion = ShopCatalog.Get(ShopItemId.HealthPotion);
            Assert.AreEqual(20, potion.Cost);
            Assert.AreEqual(ShopVendor.Market, potion.Vendor);
            Assert.IsFalse(potion.ForClass.HasValue);

            var optics = ShopCatalog.Get(ShopItemId.HorizonOptics);
            Assert.AreEqual(SpecialistClass.ScoutDrone, optics.ForClass);
            Assert.AreEqual(150, optics.Cost);
            Assert.AreEqual(0.18f, optics.SpeedBonus, 1e-4f);
            AssertEveryIdOnce<ShopItemId, ShopItemDef>(ShopCatalog.All, d => d.Id, ShopItemId.None);
        }

        [Test]
        public void NoBodyDatabase_GetReturnsTheBuiltInProfiles()
        {
            CelestialBodyCatalog.UseDatabase(null);
            Assert.IsFalse(CelestialBodyCatalog.UsingDatabase);
            foreach (var id in CelestialBodyCatalog.All)
            {
                Assert.AreSame(CelestialBodyCatalog.Default(id), CelestialBodyCatalog.Get(id), id.ToString());
                Assert.AreEqual(id, CelestialBodyCatalog.Get(id).Id);
            }
            Assert.AreSame(CelestialBodyCatalog.Default(CelestialBodyId.Mars), CelestialBodyCatalog.Mars());
            Assert.AreEqual("Earth", CelestialBodyCatalog.Earth().DisplayName);
            Assert.AreEqual(TechId.LunarRocket, CelestialBodyCatalog.Earth().LaunchTech);
            Assert.AreEqual(8, CelestialBodyCatalog.Earth().PopulationGoal);
            // Unknown ids still fall back to Luna, as before.
            Assert.AreSame(CelestialBodyCatalog.Luna(), CelestialBodyCatalog.Get((CelestialBodyId)99));
        }

        // ---- Export round trip (what the editor generator writes) --------------------------

        [Test]
        public void ExportedTechDatabase_ReproducesTheBuiltInTree()
        {
            var db = Track(TechDatabase.FromDefs(TechCatalog.Defaults));
            TechCatalog.UseDatabase(db);
            Assert.IsTrue(TechCatalog.UsingDatabase);
            Assert.AreNotSame(TechCatalog.Defaults, TechCatalog.All);
            Assert.AreEqual(TechCatalog.Defaults.Count, TechCatalog.All.Count);
            for (int i = 0; i < TechCatalog.Defaults.Count; i++)
                AssertSameTech(TechCatalog.Defaults[i], TechCatalog.All[i]);
        }

        [Test]
        public void ExportedShopDatabase_ReproducesTheBuiltInList()
        {
            var db = Track(ShopDatabase.FromDefs(ShopCatalog.Defaults));
            ShopCatalog.UseDatabase(db);
            Assert.IsTrue(ShopCatalog.UsingDatabase);
            Assert.AreEqual(ShopCatalog.Defaults.Count, ShopCatalog.All.Count);
            for (int i = 0; i < ShopCatalog.Defaults.Count; i++)
                AssertSameFields(ShopCatalog.Defaults[i], ShopCatalog.All[i], ShopCatalog.Defaults[i].Id.ToString());
        }

        [Test]
        public void ExportedBodyDatabase_ReproducesTheBuiltInProfiles()
        {
            var list = new List<CelestialBodyProfile>();
            foreach (var id in CelestialBodyCatalog.All) list.Add(CelestialBodyCatalog.Default(id));
            var db = Track(CelestialBodyDatabase.FromProfiles(list));
            CelestialBodyCatalog.UseDatabase(db);
            Assert.IsTrue(CelestialBodyCatalog.UsingDatabase);
            foreach (var id in CelestialBodyCatalog.All)
            {
                var live = CelestialBodyCatalog.Get(id);
                Assert.AreNotSame(CelestialBodyCatalog.Default(id), live, "override hands back a copy");
                AssertSameFields(CelestialBodyCatalog.Default(id), live, id.ToString());
            }
        }

        // ---- Overrides are honoured ---------------------------------------------------------

        [Test]
        public void TechOverride_ChangesOnlyWhatItLists_AndKeepsMissingIds()
        {
            var db = Track(ScriptableObject.CreateInstance<TechDatabase>());
            db.techs.Add(new TechEntry
            {
                id = TechId.HabOps,
                displayName = "Hab Ops II",
                description = "",
                scienceCost = 99f,
                prerequisites = new[] { TechId.FieldSurvey },
                completeCost = new[] { new ResourceAmount(ResourceId.Metals, 7) }
            });
            TechCatalog.UseDatabase(db);

            var hab = TechCatalog.Get(TechId.HabOps);
            Assert.AreEqual("Hab Ops II", hab.DisplayName);
            Assert.AreEqual(99f, hab.ScienceCost, 1e-4f);
            Assert.AreEqual(7, hab.CompleteCost[0].amount);
            Assert.AreEqual(TechCatalog.Defaults[1].Description, hab.Description, "blank text keeps the built-in line");
            Assert.AreSame(TechCatalog.All[0], hab, "asset entries come first");

            // A save that references a tech the asset forgot must still resolve.
            Assert.AreEqual(TechCatalog.Defaults.Count, TechCatalog.All.Count);
            Assert.AreSame(DefaultTech(TechId.OrbitalConstellation), TechCatalog.Get(TechId.OrbitalConstellation));
        }

        [Test]
        public void TechOverride_SkipsNoneAndDuplicateIds()
        {
            var db = Track(ScriptableObject.CreateInstance<TechDatabase>());
            db.techs.Add(new TechEntry { id = TechId.None, displayName = "Nothing", scienceCost = 5f });
            db.techs.Add(new TechEntry { id = TechId.HabOps, displayName = "First", scienceCost = 5f });
            db.techs.Add(new TechEntry { id = TechId.HabOps, displayName = "Second", scienceCost = 6f });
            TechCatalog.UseDatabase(db);

            Assert.AreEqual("First", TechCatalog.Get(TechId.HabOps).DisplayName);
            Assert.IsNull(TechCatalog.Get(TechId.None));
            AssertEveryIdOnce<TechId, TechDef>(TechCatalog.All, d => d.Id, TechId.None);
        }

        [Test]
        public void EmptyTechDatabase_IsIgnored()
        {
            TechCatalog.UseDatabase(Track(ScriptableObject.CreateInstance<TechDatabase>()));
            Assert.IsFalse(TechCatalog.UsingDatabase);
            Assert.AreSame(TechCatalog.Defaults, TechCatalog.All);
        }

        [Test]
        public void ShopOverride_IsHonouredByTheBuyingRules()
        {
            var db = Track(ScriptableObject.CreateInstance<ShopDatabase>());
            var potion = ShopEntry.From(ShopCatalog.Defaults[0]);
            foreach (var d in ShopCatalog.Defaults)
            {
                if (d.Id == ShopItemId.HealthPotion) potion = ShopEntry.From(d);
            }
            potion.cost = 5;
            db.items.Add(potion);
            ShopCatalog.UseDatabase(db);

            Assert.AreEqual(5, ShopCatalog.Get(ShopItemId.HealthPotion).Cost);
            // The buying rules read the live list, so a hurt hero is sold the edited potion.
            var bought = ShopCatalog.PreferredMarketBuy(SpecialistClass.DefenseMech, 600, 0.1f, ShopItemId.RegenNecklace);
            Assert.AreEqual(ShopItemId.HealthPotion, bought?.Id);
            Assert.AreEqual(5, bought.Cost);
            Assert.AreEqual(ShopCatalog.Defaults.Count, ShopCatalog.All.Count);
        }

        [Test]
        public void ShopOverride_ClassOnlyFlagMapsToNullableClass()
        {
            var db = Track(ScriptableObject.CreateInstance<ShopDatabase>());
            db.items.Add(new ShopEntry
            {
                id = ShopItemId.RegenNecklace,
                kind = ShopItemKind.Accessory,
                vendor = ShopVendor.Market,
                classOnly = true,
                forClass = SpecialistClass.Medic,
                displayName = "Medic Necklace",
                cost = 300
            });
            ShopCatalog.UseDatabase(db);
            Assert.AreEqual(SpecialistClass.Medic, ShopCatalog.Get(ShopItemId.RegenNecklace).ForClass);
            Assert.AreEqual("Slow HP regen while worn.", ShopCatalog.Get(ShopItemId.RegenNecklace).Description);
        }

        [Test]
        public void BodyOverride_RetunesOneBody_AndLeavesTheRest()
        {
            var mars = CelestialBodyDatabase.Copy(CelestialBodyCatalog.Default(CelestialBodyId.Mars));
            mars.PopulationGoal = 3;
            mars.Briefing = "";
            mars.ResourceWeights = new[] { 1, 2 }; // malformed: must be four long
            var db = Track(ScriptableObject.CreateInstance<CelestialBodyDatabase>());
            db.bodies.Add(mars);
            CelestialBodyCatalog.UseDatabase(db);

            var live = CelestialBodyCatalog.Mars();
            Assert.AreEqual(3, live.PopulationGoal);
            Assert.AreEqual(CelestialBodyCatalog.Default(CelestialBodyId.Mars).Briefing, live.Briefing);
            CollectionAssert.AreEqual(CelestialBodyCatalog.Default(CelestialBodyId.Mars).ResourceWeights, live.ResourceWeights);
            Assert.AreSame(live, CelestialBodyCatalog.Get(CelestialBodyId.Mars), "stable instance per session");
            Assert.AreSame(CelestialBodyCatalog.Default(CelestialBodyId.Luna), CelestialBodyCatalog.Luna());
            Assert.AreNotEqual(3, CelestialBodyCatalog.Default(CelestialBodyId.Mars).PopulationGoal,
                "editing the override never touches the built-in profile");
        }

        [Test]
        public void BodyCopy_DoesNotAliasArrays()
        {
            var src = CelestialBodyCatalog.Default(CelestialBodyId.Earth);
            var copy = CelestialBodyDatabase.Copy(src);
            Assert.AreNotSame(src.ResourceWeights, copy.ResourceWeights);
            Assert.AreNotSame(src.GroundCover, copy.GroundCover);
            copy.ResourceWeights[0] = 999;
            Assert.AreNotEqual(999, src.ResourceWeights[0]);
        }

        [Test]
        public void Merge_AssetOrderFirstThenMissingDefaults()
        {
            var defaults = new List<string> { "a", "b", "c" };
            var asset = new List<string> { "c", null, "", "c", "x" };
            var merged = CatalogDatabase.Merge<string, string>(defaults, asset, s => s, s => s.Length > 0, "test");
            CollectionAssert.AreEqual(new[] { "c", "x", "a", "b" }, merged);
        }

        // ---- helpers ----------------------------------------------------------------------

        private static TechDef DefaultTech(TechId id)
        {
            foreach (var d in TechCatalog.Defaults)
            {
                if (d.Id == id) return d;
            }
            return null;
        }

        private static void AssertEveryIdOnce<TId, TDef>(IReadOnlyList<TDef> list, Func<TDef, TId> idOf, TId none)
            where TId : struct, Enum
        {
            var counts = new Dictionary<TId, int>();
            foreach (var d in list)
            {
                TId id = idOf(d);
                counts.TryGetValue(id, out int n);
                counts[id] = n + 1;
            }
            foreach (TId id in Enum.GetValues(typeof(TId)))
            {
                if (id.Equals(none)) continue;
                counts.TryGetValue(id, out int n);
                Assert.AreEqual(1, n, $"{typeof(TId).Name}.{id}");
            }
            Assert.IsFalse(counts.ContainsKey(none));
        }

        private static void AssertSameTech(TechDef a, TechDef b)
        {
            string at = a.Id.ToString();
            Assert.AreEqual(a.Id, b.Id, at);
            Assert.AreEqual(a.DisplayName, b.DisplayName, at);
            Assert.AreEqual(a.Description, b.Description, at);
            Assert.AreEqual(a.ScienceCost, b.ScienceCost, at);
            CollectionAssert.AreEqual(a.Prerequisites, b.Prerequisites, at);
            // Null and empty both mean "free" to ResearchManager; Unity cannot store the difference.
            CollectionAssert.AreEqual(a.CompleteCost ?? new ResourceAmount[0], b.CompleteCost ?? new ResourceAmount[0], at);
            Assert.AreEqual(a.UnlocksLaunch, b.UnlocksLaunch, at);
            Assert.AreEqual(a.ResearchRateBonus, b.ResearchRateBonus, at);
            Assert.AreEqual(a.SecretProject, b.SecretProject, at);
        }

        /// <summary>Every public field equal; arrays compared element by element.</summary>
        private static void AssertSameFields(object a, object b, string context)
        {
            Assert.AreEqual(a.GetType(), b.GetType(), context);
            foreach (var f in a.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object va = f.GetValue(a);
                object vb = f.GetValue(b);
                if (va is Array arrA && vb is Array arrB)
                    CollectionAssert.AreEqual(arrA, arrB, $"{context}.{f.Name}");
                else
                    Assert.AreEqual(va, vb, $"{context}.{f.Name}");
            }
        }
    }
}
