using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>Waystation shop — permanent suits and temporary gene therapy.</summary>
    public static class ShopCatalog
    {
        private static readonly List<ShopItemDef> Items = new List<ShopItemDef>
        {
            new ShopItemDef
            {
                Id = ShopItemId.SuitFieldShell,
                Kind = ShopItemKind.PermanentSuit,
                DisplayName = "Field Shell",
                Description = "Light EVA plating. Permanent armor.",
                Cost = 45,
                ArmorMitigation = 0.18f,
                SpeedBonus = 0f
            },
            new ShopItemDef
            {
                Id = ShopItemId.SuitHardplate,
                Kind = ShopItemKind.PermanentSuit,
                DisplayName = "Hardplate Suit",
                Description = "Heavy armor. Permanent, slight speed cost.",
                Cost = 90,
                ArmorMitigation = 0.32f,
                SpeedBonus = -0.06f
            },
            new ShopItemDef
            {
                Id = ShopItemId.GeneCourage,
                Kind = ShopItemKind.ConsumableGene,
                DisplayName = "Gene: Valor",
                Description = "Temporary courage spike.",
                Cost = 35,
                CourageBonus = 0.22f,
                DurationSeconds = 90f
            },
            new ShopItemDef
            {
                Id = ShopItemId.GeneWork,
                Kind = ShopItemKind.ConsumableGene,
                DisplayName = "Gene: Focus",
                Description = "Temporary work-rate boost.",
                Cost = 30,
                WorkBonus = 0.28f,
                DurationSeconds = 75f
            },
            new ShopItemDef
            {
                Id = ShopItemId.GeneSwift,
                Kind = ShopItemKind.ConsumableGene,
                DisplayName = "Gene: Swift",
                Description = "Temporary move-speed boost.",
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

        public static ShopItemDef BestAffordableSuit(int credits, ShopItemId current)
        {
            ShopItemDef best = null;
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (it.Kind != ShopItemKind.PermanentSuit) continue;
                if (it.Cost > credits) continue;
                if (current == it.Id) continue;
                var cur = Get(current);
                if (cur != null && it.ArmorMitigation <= cur.ArmorMitigation + 0.001f) continue;
                if (best == null || it.ArmorMitigation > best.ArmorMitigation)
                    best = it;
            }
            return best;
        }

        public static ShopItemDef PreferredGene(SpecialistClass cls, int credits)
        {
            ShopItemId prefer = cls switch
            {
                SpecialistClass.DefenseMech => ShopItemId.GeneCourage,
                SpecialistClass.ScoutDrone => ShopItemId.GeneSwift,
                SpecialistClass.EngineerBot => ShopItemId.GeneWork,
                SpecialistClass.Medic => ShopItemId.GeneCourage,
                _ => ShopItemId.GeneWork
            };
            var def = Get(prefer);
            if (def != null && def.Cost <= credits) return def;

            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (it.Kind == ShopItemKind.ConsumableGene && it.Cost <= credits)
                    return it;
            }
            return null;
        }
    }
}
