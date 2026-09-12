using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Majesty shops. Guild halls sell class kits. The market sells potions
    /// and the regen necklace. The blacksmith sells guild arms and armor.
    /// The inn is rest only.
    /// </summary>
    public static class ShopCatalog
    {
        private static readonly List<ShopItemDef> Items = new List<ShopItemDef>
        {
            new ShopItemDef
            {
                Id = ShopItemId.HorizonOptics,
                Kind = ShopItemKind.PermanentSuit,
                Vendor = ShopVendor.GuildHall,
                ForClass = SpecialistClass.ScoutDrone,
                DisplayName = "Horizon Optics",
                Description = "Scout visor. Permanent speed.",
                Cost = 40,
                SpeedBonus = 0.18f
            },
            new ShopItemDef
            {
                Id = ShopItemId.AnvilRig,
                Kind = ShopItemKind.PermanentSuit,
                Vendor = ShopVendor.GuildHall,
                ForClass = SpecialistClass.EngineerBot,
                DisplayName = "Anvil Rig",
                Description = "Tool harness. Permanent work rate.",
                Cost = 48,
                WorkBonus = 0.18f,
                ArmorMitigation = 0.08f
            },
            new ShopItemDef
            {
                Id = ShopItemId.AegisShell,
                Kind = ShopItemKind.PermanentSuit,
                Vendor = ShopVendor.GuildHall,
                ForClass = SpecialistClass.DefenseMech,
                DisplayName = "Aegis Shell",
                Description = "Lodge plating. Permanent armor.",
                Cost = 55,
                ArmorMitigation = 0.22f
            },
            new ShopItemDef
            {
                Id = ShopItemId.TriagePack,
                Kind = ShopItemKind.PermanentSuit,
                Vendor = ShopVendor.GuildHall,
                ForClass = SpecialistClass.Medic,
                DisplayName = "Triage Pack",
                Description = "Field kit. Light armor, faster rest heal.",
                Cost = 42,
                ArmorMitigation = 0.10f,
                RegenPerSecond = 0.02f
            },
            new ShopItemDef
            {
                Id = ShopItemId.SuitFieldShell,
                Kind = ShopItemKind.PermanentSuit,
                Vendor = ShopVendor.GuildHall,
                ForClass = SpecialistClass.DefenseMech,
                DisplayName = "Field Shell",
                Description = "Light EVA plating. Permanent armor.",
                Cost = 45,
                ArmorMitigation = 0.18f
            },
            new ShopItemDef
            {
                Id = ShopItemId.SuitHardplate,
                Kind = ShopItemKind.PermanentSuit,
                Vendor = ShopVendor.GuildHall,
                ForClass = SpecialistClass.DefenseMech,
                DisplayName = "Hardplate Suit",
                Description = "Heavy armor. Permanent, slight speed cost.",
                Cost = 90,
                ArmorMitigation = 0.32f,
                SpeedBonus = -0.06f
            },
            new ShopItemDef
            {
                Id = ShopItemId.HealthPotion,
                Kind = ShopItemKind.ConsumablePotion,
                Vendor = ShopVendor.Market,
                DisplayName = "Health Potion",
                Description = "Instant chassis patch.",
                Cost = 18,
                HealAmount = 0.45f
            },
            new ShopItemDef
            {
                Id = ShopItemId.MagicPotion,
                Kind = ShopItemKind.ConsumablePotion,
                Vendor = ShopVendor.Market,
                DisplayName = "Magic Potion",
                Description = "Temporary work and courage.",
                Cost = 22,
                WorkBonus = 0.22f,
                CourageBonus = 0.16f,
                DurationSeconds = 50f
            },
            new ShopItemDef
            {
                Id = ShopItemId.RegenNecklace,
                Kind = ShopItemKind.Accessory,
                Vendor = ShopVendor.Market,
                DisplayName = "Regen Necklace",
                Description = "Slow HP regen while worn.",
                Cost = 80,
                RegenPerSecond = 0.035f
            },
            new ShopItemDef
            {
                Id = ShopItemId.HorizonNeedle,
                Kind = ShopItemKind.Weapon,
                Vendor = ShopVendor.Blacksmith,
                ForClass = SpecialistClass.ScoutDrone,
                DisplayName = "Horizon Needle",
                Description = "Lodge carbine. Permanent damage.",
                Cost = 70,
                DamageBonus = 0.22f,
                SpeedBonus = 0.06f
            },
            new ShopItemDef
            {
                Id = ShopItemId.HorizonWeave,
                Kind = ShopItemKind.PermanentSuit,
                Vendor = ShopVendor.Blacksmith,
                ForClass = SpecialistClass.ScoutDrone,
                DisplayName = "Horizon Weave",
                Description = "Light scout mail.",
                Cost = 62,
                ArmorMitigation = 0.16f,
                SpeedBonus = 0.08f
            },
            new ShopItemDef
            {
                Id = ShopItemId.AnvilSledge,
                Kind = ShopItemKind.Weapon,
                Vendor = ShopVendor.Blacksmith,
                ForClass = SpecialistClass.EngineerBot,
                DisplayName = "Anvil Sledge",
                Description = "Forge hammer. Damage and weld.",
                Cost = 75,
                DamageBonus = 0.18f,
                WorkBonus = 0.12f
            },
            new ShopItemDef
            {
                Id = ShopItemId.AnvilPlate,
                Kind = ShopItemKind.PermanentSuit,
                Vendor = ShopVendor.Blacksmith,
                ForClass = SpecialistClass.EngineerBot,
                DisplayName = "Anvil Plate",
                Description = "Welder plate. Heavy armor.",
                Cost = 80,
                ArmorMitigation = 0.28f,
                SpeedBonus = -0.04f
            },
            new ShopItemDef
            {
                Id = ShopItemId.AegisPike,
                Kind = ShopItemKind.Weapon,
                Vendor = ShopVendor.Blacksmith,
                ForClass = SpecialistClass.DefenseMech,
                DisplayName = "Aegis Pike",
                Description = "Lodge spear. Permanent damage.",
                Cost = 78,
                DamageBonus = 0.28f
            },
            new ShopItemDef
            {
                Id = ShopItemId.AegisMail,
                Kind = ShopItemKind.PermanentSuit,
                Vendor = ShopVendor.Blacksmith,
                ForClass = SpecialistClass.DefenseMech,
                DisplayName = "Aegis Mail",
                Description = "Watchmail. Heaviest lodge armor.",
                Cost = 95,
                ArmorMitigation = 0.38f,
                SpeedBonus = -0.08f
            },
            new ShopItemDef
            {
                Id = ShopItemId.TriageInjector,
                Kind = ShopItemKind.Weapon,
                Vendor = ShopVendor.Blacksmith,
                ForClass = SpecialistClass.Medic,
                DisplayName = "Triage Injector",
                Description = "Field lance. Light damage, extra regen.",
                Cost = 68,
                DamageBonus = 0.10f,
                RegenPerSecond = 0.03f
            },
            new ShopItemDef
            {
                Id = ShopItemId.TriageMail,
                Kind = ShopItemKind.PermanentSuit,
                Vendor = ShopVendor.Blacksmith,
                ForClass = SpecialistClass.Medic,
                DisplayName = "Triage Mail",
                Description = "Softmail. Armor and regen.",
                Cost = 70,
                ArmorMitigation = 0.18f,
                RegenPerSecond = 0.02f
            },
            new ShopItemDef
            {
                Id = ShopItemId.GeneCourage,
                Kind = ShopItemKind.ConsumableGene,
                Vendor = ShopVendor.Inn,
                DisplayName = "Gene: Valor",
                Description = "Legacy inn gene — unused.",
                Cost = 35,
                CourageBonus = 0.22f,
                DurationSeconds = 90f
            },
            new ShopItemDef
            {
                Id = ShopItemId.GeneWork,
                Kind = ShopItemKind.ConsumableGene,
                Vendor = ShopVendor.Inn,
                DisplayName = "Gene: Focus",
                Description = "Legacy inn gene — unused.",
                Cost = 30,
                WorkBonus = 0.28f,
                DurationSeconds = 75f
            },
            new ShopItemDef
            {
                Id = ShopItemId.GeneSwift,
                Kind = ShopItemKind.ConsumableGene,
                Vendor = ShopVendor.Inn,
                DisplayName = "Gene: Swift",
                Description = "Legacy inn gene — unused.",
                Cost = 28,
                SpeedBonus = 0.22f,
                DurationSeconds = 60f
            }
        };

        public static IReadOnlyList<ShopItemDef> All => Items;

        public static ShopItemDef Get(ShopItemId id)
        {
            for (int i = 0; i < Items.Count; i++)
                if (Items[i].Id == id) return Items[i];
            return null;
        }

        public static ShopItemDef BestAffordableSuit(int credits, ShopItemId current) =>
            BestGuildUpgrade(null, credits, current);

        public static ShopItemDef BestGuildUpgrade(SpecialistClass? cls, int credits, ShopItemId current)
        {
            ShopItemDef best = null;
            float bestScore = -1f;
            var cur = Get(current);
            float curScore = cur != null ? SuitScore(cur) : -1f;
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (it.Vendor != ShopVendor.GuildHall) continue;
                if (it.Kind != ShopItemKind.PermanentSuit) continue;
                if (cls.HasValue && it.ForClass.HasValue && it.ForClass.Value != cls.Value) continue;
                if (it.Cost > credits) continue;
                if (current == it.Id) continue;
                float score = SuitScore(it);
                if (score <= curScore + 0.001f) continue;
                if (best == null || score > bestScore)
                {
                    best = it;
                    bestScore = score;
                }
            }

            return best;
        }

        public static ShopItemDef PreferredMarketBuy(SpecialistClass cls, int credits, float hp, ShopItemId accessory)
        {
            if (hp < 0.72f)
            {
                var pot = Get(ShopItemId.HealthPotion);
                if (pot != null && pot.Cost <= credits) return pot;
            }

            if (accessory != ShopItemId.RegenNecklace)
            {
                var neck = Get(ShopItemId.RegenNecklace);
                if (neck != null && neck.Cost <= credits && hp < 0.92f) return neck;
            }

            var mag = Get(ShopItemId.MagicPotion);
            if (mag != null && mag.Cost <= credits) return mag;
            return null;
        }

        public static ShopItemDef PreferredGene(SpecialistClass cls, int credits)
        {
            return PreferredMarketBuy(cls, credits, 0.5f, ShopItemId.None);
        }

        public static ShopItemDef BestBlacksmithBuy(
            SpecialistClass cls, int credits, ShopItemId armor, ShopItemId weapon)
        {
            ShopItemDef bestWeapon = null;
            ShopItemDef bestArmor = null;
            var curArmor = Get(armor);
            float curArmorScore = curArmor != null ? SuitScore(curArmor) : -1f;
            var curWeapon = Get(weapon);
            float curDmg = curWeapon != null ? curWeapon.DamageBonus : -0.01f;

            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (it.Vendor != ShopVendor.Blacksmith) continue;
                if (it.ForClass.HasValue && it.ForClass.Value != cls) continue;
                if (it.Cost > credits) continue;
                if (it.Kind == ShopItemKind.Weapon)
                {
                    if (it.Id == weapon) continue;
                    if (it.DamageBonus <= curDmg + 0.001f) continue;
                    if (bestWeapon == null || it.DamageBonus > bestWeapon.DamageBonus)
                        bestWeapon = it;
                }
                else if (it.Kind == ShopItemKind.PermanentSuit)
                {
                    if (it.Id == armor) continue;
                    if (SuitScore(it) <= curArmorScore + 0.001f) continue;
                    if (bestArmor == null || SuitScore(it) > SuitScore(bestArmor))
                        bestArmor = it;
                }
            }

            if (weapon == ShopItemId.None && bestWeapon != null) return bestWeapon;
            if (bestArmor != null && (bestWeapon == null || bestArmor.Cost <= bestWeapon.Cost))
                return bestArmor;
            return bestWeapon ?? bestArmor;
        }

        private static float SuitScore(ShopItemDef it)
        {
            if (it == null) return 0f;
            return it.ArmorMitigation * 2f + it.WorkBonus + it.SpeedBonus + it.RegenPerSecond * 4f;
        }
    }
}
