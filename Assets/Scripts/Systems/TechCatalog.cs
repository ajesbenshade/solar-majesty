using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Authored Alpha Centauri-style tech tree for the campaign spine.
    /// </summary>
    public static class TechCatalog
    {
        private static TechDef[] _all;

        public static IReadOnlyList<TechDef> All => _all ??= Build();

        public static TechDef Get(TechId id)
        {
            var list = All;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == id)
                    return list[i];
            }
            return null;
        }

        private static TechDef[] Build()
        {
            return new[]
            {
                new TechDef(
                    TechId.FieldSurvey,
                    "Field Survey",
                    "Map the apron. Root of the research tree.",
                    18f),

                new TechDef(
                    TechId.HabOps,
                    "Hab Ops",
                    "Pressure seals and crew bunks for growth.",
                    28f,
                    new[] { TechId.FieldSurvey }),

                new TechDef(
                    TechId.ExtractBasics,
                    "Extract Basics",
                    "Farm and mine doctrine for a living stockpile.",
                    28f,
                    new[] { TechId.FieldSurvey }),

                new TechDef(
                    TechId.LabScience,
                    "Lab Science",
                    "Protocols that speed further research.",
                    28f,
                    new[] { TechId.FieldSurvey },
                    researchRateBonus: 0.35f),

                new TechDef(
                    TechId.LifeSupport,
                    "Life Support",
                    "Closed-loop air and water for a lasting colony.",
                    40f,
                    new[] { TechId.HabOps }),

                new TechDef(
                    TechId.OreRefining,
                    "Ore Refining",
                    "Turn ore into structural metals at scale.",
                    40f,
                    new[] { TechId.ExtractBasics }),

                new TechDef(
                    TechId.PowerSystems,
                    "Power Systems",
                    "Stable power for launch prep and deep work.",
                    40f,
                    new[] { TechId.ExtractBasics }),

                new TechDef(
                    TechId.MedProtocols,
                    "Med Protocols",
                    "Field triage so parties return from the rim.",
                    36f,
                    new[] { TechId.LifeSupport }),

                new TechDef(
                    TechId.DeepSurvey,
                    "Deep Survey",
                    "Find richer nodes and quieter approaches.",
                    45f,
                    new[] { TechId.LabScience }),

                new TechDef(
                    TechId.LunarRocket,
                    "Lunar Rocket",
                    "Craft strong enough to leave this body.",
                    70f,
                    new[] { TechId.LifeSupport, TechId.OreRefining, TechId.PowerSystems },
                    new[]
                    {
                        new ResourceAmount(ResourceId.Metals, 40),
                        new ResourceAmount(ResourceId.WaterIce, 15)
                    },
                    unlocksLaunch: true),

                new TechDef(
                    TechId.MarsShip,
                    "Mars Ship",
                    "Heavy transfer ship for the next conquest.",
                    100f,
                    new[] { TechId.LunarRocket, TechId.DeepSurvey, TechId.MedProtocols },
                    new[]
                    {
                        new ResourceAmount(ResourceId.Metals, 80),
                        new ResourceAmount(ResourceId.WaterIce, 30),
                        new ResourceAmount(ResourceId.Power, 20)
                    },
                    unlocksLaunch: true)
            };
        }
    }
}
