using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    public class CatalogRowFitTests
    {
        // Short-hint catalog rows, with the authored names they start from before normalising.
        static readonly (BuildingCategory cat, string name)[] Rows =
        {
            (BuildingCategory.Market, "Market Stall"),
            (BuildingCategory.Mine, "Nuclear Mine"),
            (BuildingCategory.Farm, "Greenhouse Farm"),
            (BuildingCategory.LandingPad, "Landing Pad"),
            (BuildingCategory.Laboratory, "Lab Module"),
            (BuildingCategory.Defense, "Laser Turret"),
            (BuildingCategory.Blacksmith, "Blacksmith"),
        };

        [Test]
        public void CatalogRows_NamePlusHint_FitTheRow()
        {
            var data = new BuildingData[Rows.Length];
            for (int i = 0; i < Rows.Length; i++)
            {
                data[i] = ScriptableObject.CreateInstance<BuildingData>();
                data[i].category = Rows[i].cat;
                data[i].displayName = Rows[i].name;
            }
            GameLoop.NormalizeCatalogNames(data);
            foreach (var b in data)
            {
                var variants = HudLayout.BuildRowVariants(b.displayName, b.description, null);
                string row = variants[variants.Length - 1];
                Assert.LessOrEqual(row.Length, HudLayout.MaxBuildRowChars, row);
                Object.DestroyImmediate(b);
            }
            Assert.AreEqual("Greenhouse  ·  50/day",
                HudLayout.BuildRowVariants("Greenhouse", $"{MajestyEconomy.FarmDailyTax:N0}/day", null)[0]);
        }
    }
}
