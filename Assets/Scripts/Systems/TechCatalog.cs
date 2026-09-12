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
                    Wallet.Credits(55),
                    unlocksLaunch: true),

                new TechDef(
                    TechId.MarsShip,
                    "Mars Ship",
                    "Heavy transfer ship for the next conquest.",
                    100f,
                    new[] { TechId.LunarRocket, TechId.DeepSurvey, TechId.MedProtocols },
                    Wallet.Credits(110),
                    unlocksLaunch: true),

                new TechDef(
                    TechId.BeltHauler,
                    "Belt Hauler",
                    "Tethered ore barge for the asteroid belt.",
                    120f,
                    new[] { TechId.MarsShip, TechId.OreRefining },
                    Wallet.Credits(90),
                    unlocksLaunch: true),

                new TechDef(
                    TechId.Icebreaker,
                    "Icebreaker",
                    "Radiation-hardened lander for Europa's crust.",
                    140f,
                    new[] { TechId.BeltHauler, TechId.LifeSupport, TechId.PowerSystems },
                    Wallet.Credits(120),
                    unlocksLaunch: true),

                new TechDef(
                    TechId.GuildCharter,
                    "Guild Charter",
                    "License Horizon Lodge, Anvil Compact, Aegis Lodge, and Triage Compact. Flags near a hall pull that class.",
                    32f,
                    new[] { TechId.HabOps }),

                new TechDef(
                    TechId.HarvestDoctrine,
                    "Harvest Doctrine",
                    "Strip-mine doctrine. Better haul and mine ticks. Unlocks Harvester shop.",
                    48f,
                    new[] { TechId.OreRefining }),

                new TechDef(
                    TechId.AegisDoctrine,
                    "Aegis Doctrine",
                    "Hardened grid. Colony power draw drops 15%. Pairs with Aegis Watch and Rim Watch.",
                    48f,
                    new[] { TechId.PowerSystems }),

                new TechDef(
                    TechId.SurveyDoctrine,
                    "Survey Doctrine",
                    "Deep mapping. Labs tick faster. Unlocks Surveyor shop.",
                    48f,
                    new[] { TechId.DeepSurvey },
                    researchRateBonus: 0.4f),

                new TechDef(
                    TechId.PlanetaryAnvil,
                    "Planetary Anvil",
                    "Secret Project. A foundry that never cools — mines and haul surge. Extract/haul rush.",
                    160f,
                    new[] { TechId.HarvestDoctrine, TechId.OreRefining },
                    Wallet.Credits(120),
                    secretProject: true),

                new TechDef(
                    TechId.OrbitalSkyhook,
                    "Orbital Skyhook",
                    "Secret Project. Cheap freight and free Earth dockings. Extract/haul rush.",
                    160f,
                    new[] { TechId.AegisDoctrine, TechId.LunarRocket },
                    Wallet.Credits(100),
                    secretProject: true),

                new TechDef(
                    TechId.GeneVault,
                    "Gene Vault",
                    "Secret Project. Spare beds, faster births, greener farms. Growth path.",
                    150f,
                    new[] { TechId.LifeSupport, TechId.MedProtocols },
                    Wallet.Credits(140),
                    secretProject: true),

                new TechDef(
                    TechId.TerraformCharter,
                    "Terraform Charter",
                    "License a Terraformer shop. Greener farms when they work Terraform flags.",
                    44f,
                    new[] { TechId.LifeSupport, TechId.ExtractBasics }),

                new TechDef(
                    TechId.FreightDoctrine,
                    "Freight Doctrine",
                    "Haul claims between campuses. Unlocks Courier shop.",
                    44f,
                    new[] { TechId.HabOps, TechId.ExtractBasics }),

                new TechDef(
                    TechId.CoreSampling,
                    "Core Sampling",
                    "Read the crust. Better mine ticks. Unlocks Geologist shop.",
                    42f,
                    new[] { TechId.ExtractBasics, TechId.LabScience }),

                new TechDef(
                    TechId.PerimeterDoctrine,
                    "Perimeter Doctrine",
                    "Watch the rim. Unlocks Sentinel shop — they take Defend cheap.",
                    52f,
                    new[] { TechId.AegisDoctrine }),

                new TechDef(
                    TechId.ClimateLoom,
                    "Climate Loom",
                    "Secret Project. Weave weather for the crust — farms surge. Place the Loom landmark.",
                    155f,
                    new[] { TechId.TerraformCharter, TechId.LifeSupport },
                    Wallet.Credits(160),
                    secretProject: true),

                new TechDef(
                    TechId.AegisSpire,
                    "Aegis Spire",
                    "Secret Project. A tower that calms the grid and the rim. Place the Spire landmark.",
                    165f,
                    new[] { TechId.PerimeterDoctrine, TechId.AegisDoctrine },
                    Wallet.Credits(110),
                    secretProject: true),

                new TechDef(
                    TechId.DeepArchive,
                    "Deep Archive",
                    "Secret Project. Labs remember every sample. Place the Archive landmark.",
                    150f,
                    new[] { TechId.SurveyDoctrine, TechId.DeepSurvey },
                    Wallet.Credits(80),
                    researchRateBonus: 0.55f,
                    secretProject: true),

                new TechDef(
                    TechId.HorizonPulse,
                    "Horizon Pulse",
                    "Activate at Horizon Lodge: scouts run hot and mark dens. Costs CRED.",
                    36f,
                    new[] { TechId.GuildCharter },
                    Wallet.Credits(20)),

                new TechDef(
                    TechId.AnvilOvertime,
                    "Anvil Overtime",
                    "Activate at Anvil Compact: engineers weld faster. Costs CRED.",
                    36f,
                    new[] { TechId.GuildCharter },
                    Wallet.Credits(24)),

                new TechDef(
                    TechId.AegisWatchfire,
                    "Aegis Watchfire",
                    "Activate at Aegis Lodge: robots shrug bites, batteries hit harder. Costs CRED.",
                    40f,
                    new[] { TechId.GuildCharter },
                    Wallet.Credits(28)),

                new TechDef(
                    TechId.TriageFieldAid,
                    "Triage Field Aid",
                    "Activate at Triage Compact: everyone on the dirt patches HP. Costs CRED.",
                    36f,
                    new[] { TechId.GuildCharter },
                    Wallet.Credits(22))
            };
        }
    }
}
