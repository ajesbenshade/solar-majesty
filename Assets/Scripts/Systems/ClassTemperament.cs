using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Per-class flag appeal (Majesty 2 hero AllureFactors). Multiplies parts of the flag score.
    /// 1 everywhere = no change.
    /// </summary>
    [Serializable]
    public struct ClassAllure
    {
        public SpecialistClass specialistClass;
        [Tooltip("Distance penalty multiplier. >1 = homebody (Majesty mage), <1 = roams far (ranger).")]
        public float distanceMul;
        [Tooltip("Risk / fear penalty multiplier. <1 = shrugs off danger (warrior), >1 = cautious (rogue, cleric).")]
        public float dangerMul;
        [Tooltip("Preference multiplier for Clear Threat flags.")]
        public float attackMul;
        [Tooltip("Preference multiplier for Defend Area flags.")]
        public float defendMul;
        [Tooltip("Preference multiplier for Explore flags.")]
        public float exploreMul;

        public static ClassAllure Neutral(SpecialistClass cls) => new ClassAllure
        {
            specialistClass = cls, distanceMul = 1f, dangerMul = 1f, attackMul = 1f, defendMul = 1f, exploreMul = 1f
        };

        public float KindMul(FlagType type)
        {
            switch (type)
            {
                case FlagType.ClearThreat: return attackMul;
                case FlagType.DefendArea: return defendMul;
                case FlagType.Explore: return exploreMul;
                default: return 1f;
            }
        }

        /// <summary>
        /// Convert a Majesty 2 hero's raw AllureFactors into softened multipliers around 1.
        /// cl_dist runs 2-8 (median 5), cl_danger 0.05-1, flag factors are tilted against the
        /// class's own mean so they adjust our per-class preferences instead of replacing them.
        /// </summary>
        public static ClassAllure FromMajesty(
            SpecialistClass cls, float clDist, float clDanger, float atk, float prot, float expl)
        {
            float mean = Mathf.Max(0.01f, (atk + prot + expl) / 3f);
            float Tilt(float v) => Mathf.Clamp(1f + 0.5f * (v / mean - 1f), 0.5f, 1.5f);
            return new ClassAllure
            {
                specialistClass = cls,
                distanceMul = Mathf.Clamp(0.5f + 0.5f * clDist / 5f, 0.5f, 1.5f),
                dangerMul = Mathf.Clamp(0.6f + 0.8f * clDanger, 0.5f, 1.5f),
                attackMul = Tilt(atk),
                defendMul = Tilt(prot),
                exploreMul = Tilt(expl),
            };
        }

        /// <summary>Majesty 2 heroes mapped onto our classes. Engineer and Terraformer stay neutral.</summary>
        public static ClassAllure[] Defaults() => new[]
        {
            FromMajesty(SpecialistClass.ScoutDrone, 2f, 0.3f, 0.6f, 0.3f, 1.4f),     // ranger
            FromMajesty(SpecialistClass.DefenseMech, 6f, 0.1f, 1.9f, 1.5f, 0.9f),    // warrior
            FromMajesty(SpecialistClass.Medic, 5f, 0.6f, 1.32f, 1.5f, 0.6f),         // cleric
            FromMajesty(SpecialistClass.HarvesterBot, 3f, 0.6f, 1.3f, 0.8f, 1.4f),   // rogue
            FromMajesty(SpecialistClass.SentinelMech, 8f, 1f, 2.2f, 0.8f, 1f),       // dwarf
            FromMajesty(SpecialistClass.SurveyorBot, 2f, 0.8f, 1.7f, 0.7f, 1f),      // elf
            FromMajesty(SpecialistClass.GeologistBot, 2f, 1f, 0.8f, 1f, 1.2f),       // marksman
            FromMajesty(SpecialistClass.CourierBot, 3f, 1f, 1.3f, 1f, 1.3f),         // beastmaster
        };
    }

    /// <summary>How badly a class wants a kind of purchase (Majesty 2 PurchaseManager TypeNecessity).</summary>
    public enum ShopNeed
    {
        Min = 0,     // never buys
        Lower = 1,   // only with money to spare
        Medium = 2,  // the default
        High = 3,
        Max = 4      // first in line
    }

    /// <summary>Per-class shopping priorities. Medium everywhere = the original shop behaviour.</summary>
    [Serializable]
    public struct ClassShopPriority
    {
        public SpecialistClass specialistClass;
        [Tooltip("Guild and blacksmith armor.")]
        public ShopNeed armor;
        [Tooltip("Blacksmith weapons.")]
        public ShopNeed weapon;
        [Tooltip("Health potions (Market).")]
        public ShopNeed healthPotion;
        [Tooltip("Magic potions (Market). Majesty 2 mana potions: casters max, fighters never.")]
        public ShopNeed magicPotion;
        [Tooltip("Regen necklace and other accessories (Majesty 2 artefacts).")]
        public ShopNeed accessory;

        public static ClassShopPriority Neutral(SpecialistClass cls) => new ClassShopPriority
        {
            specialistClass = cls, armor = ShopNeed.Medium, weapon = ShopNeed.Medium,
            healthPotion = ShopNeed.Medium, magicPotion = ShopNeed.Medium, accessory = ShopNeed.Medium
        };

        private static ClassShopPriority P(SpecialistClass c, ShopNeed armor, ShopNeed weapon,
            ShopNeed accessory, ShopNeed health, ShopNeed magic) => new ClassShopPriority
        {
            specialistClass = c, armor = armor, weapon = weapon,
            accessory = accessory, healthPotion = health, magicPotion = magic
        };

        /// <summary>Majesty 2 TypeNecessity (armour, weapon, artefact, potion_health, potion_mana).</summary>
        public static ClassShopPriority[] Defaults() => new[]
        {
            P(SpecialistClass.ScoutDrone, ShopNeed.High, ShopNeed.High, ShopNeed.Max, ShopNeed.Max, ShopNeed.Min),       // ranger
            P(SpecialistClass.DefenseMech, ShopNeed.Max, ShopNeed.High, ShopNeed.High, ShopNeed.High, ShopNeed.Min),     // warrior
            P(SpecialistClass.Medic, ShopNeed.High, ShopNeed.High, ShopNeed.Medium, ShopNeed.Medium, ShopNeed.Max),      // cleric
            P(SpecialistClass.HarvesterBot, ShopNeed.High, ShopNeed.Max, ShopNeed.Medium, ShopNeed.Max, ShopNeed.Min),   // rogue
            P(SpecialistClass.SentinelMech, ShopNeed.Max, ShopNeed.Max, ShopNeed.High, ShopNeed.Max, ShopNeed.Min),      // dwarf
            P(SpecialistClass.SurveyorBot, ShopNeed.High, ShopNeed.High, ShopNeed.Medium, ShopNeed.Max, ShopNeed.High),  // elf
            P(SpecialistClass.GeologistBot, ShopNeed.High, ShopNeed.Max, ShopNeed.Medium, ShopNeed.Max, ShopNeed.Min),   // marksman
            P(SpecialistClass.CourierBot, ShopNeed.High, ShopNeed.High, ShopNeed.Max, ShopNeed.Max, ShopNeed.Min),       // beastmaster
        };
    }
}
