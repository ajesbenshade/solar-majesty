using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Majesty shops. Guild halls sell class upgrades. The market sells potions
    /// and the regen necklace. The inn is rest only. Blacksmith is parked.
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

        private static float SuitScore(ShopItemDef it)
        {
            if (it == null) return 0f;
            return it.ArmorMitigation * 2f + it.WorkBonus + it.SpeedBonus + it.RegenPerSecond * 4f;
        }
    }
}
