using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Designer-editable copy of the shop list. Optional: with no asset at
    /// <c>Resources/Data/ShopDatabase</c> the game uses the table in <see cref="ShopCatalog"/>.
    /// Create one with <b>Solar Majesty &gt; Data &gt; Export catalogs to assets</b>.
    /// Item ids are saved on heroes by number; never renumber <see cref="ShopItemId"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Solar Majesty/Data/Shop Database", fileName = "ShopDatabase")]
    public sealed class ShopDatabase : ScriptableObject
    {
        public const string ResourcePath = CatalogDatabase.ResourceFolder + "/ShopDatabase";

        public List<ShopEntry> items = new List<ShopEntry>();

        public static ShopDatabase FromDefs(IReadOnlyList<ShopItemDef> defs)
        {
            var db = CreateInstance<ShopDatabase>();
            db.items = new List<ShopEntry>(defs != null ? defs.Count : 0);
            if (defs == null) return db;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] != null)
                    db.items.Add(ShopEntry.From(defs[i]));
            }
            return db;
        }

        /// <summary>Entries as definitions. Blank text falls back to the built-in item of the same id.</summary>
        public List<ShopItemDef> ToDefs(Func<ShopItemId, ShopItemDef> fallback)
        {
            var list = new List<ShopItemDef>(items != null ? items.Count : 0);
            if (items == null) return list;
            for (int i = 0; i < items.Count; i++)
            {
                var e = items[i];
                if (e == null) continue;
                list.Add(e.ToDef(fallback?.Invoke(e.id)));
            }
            return list;
        }
    }

    /// <summary>Serializable mirror of <see cref="ShopItemDef"/>. Unity cannot serialize a nullable enum, hence <see cref="classOnly"/>.</summary>
    [Serializable]
    public sealed class ShopEntry
    {
        public ShopItemId id;
        public ShopItemKind kind;
        public ShopVendor vendor;
        [Tooltip("Only this class may buy it. Off = any class.")]
        public bool classOnly;
        public SpecialistClass forClass;
        public string displayName;
        [TextArea(1, 3)] public string description;
        public int cost;
        public float armorMitigation;
        public float speedBonus;
        public float workBonus;
        public float courageBonus;
        public float healAmount;
        public float regenPerSecond;
        public float durationSeconds;
        public float damageBonus;

        public static ShopEntry From(ShopItemDef def)
        {
            return new ShopEntry
            {
                id = def.Id,
                kind = def.Kind,
                vendor = def.Vendor,
                classOnly = def.ForClass.HasValue,
                forClass = def.ForClass ?? default,
                displayName = def.DisplayName,
                description = def.Description,
                cost = def.Cost,
                armorMitigation = def.ArmorMitigation,
                speedBonus = def.SpeedBonus,
                workBonus = def.WorkBonus,
                courageBonus = def.CourageBonus,
                healAmount = def.HealAmount,
                regenPerSecond = def.RegenPerSecond,
                durationSeconds = def.DurationSeconds,
                damageBonus = def.DamageBonus
            };
        }

        public ShopItemDef ToDef(ShopItemDef fallback)
        {
            return new ShopItemDef
            {
                Id = id,
                Kind = kind,
                Vendor = vendor,
                ForClass = classOnly ? forClass : (SpecialistClass?)null,
                DisplayName = CatalogDatabase.TextOr(displayName, fallback != null ? fallback.DisplayName : id.ToString()),
                Description = CatalogDatabase.TextOr(description, fallback != null ? fallback.Description : ""),
                Cost = Mathf.Max(0, cost),
                ArmorMitigation = armorMitigation,
                SpeedBonus = speedBonus,
                WorkBonus = workBonus,
                CourageBonus = courageBonus,
                HealAmount = healAmount,
                RegenPerSecond = regenPerSecond,
                DurationSeconds = durationSeconds,
                DamageBonus = damageBonus
            };
        }
    }
}
