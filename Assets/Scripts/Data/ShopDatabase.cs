using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// The shop catalog as an asset. When one sits at <c>Resources/Data/ShopDatabase</c>,
    /// <see cref="ShopCatalog"/> reads it in place of the built-in list, so prices and bonuses can
    /// be tuned without a code change. With no asset the built-in list is used.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopDatabase", menuName = "Solar Majesty/Shop Database", order = 42)]
    public class ShopDatabase : ScriptableObject
    {
        public const string ResourcePath = "Data/ShopDatabase";

        [Serializable]
        public sealed class Entry
        {
            public ShopItemId id;
            public ShopItemKind kind;
            public ShopVendor vendor;
            [Tooltip("Off: any class may buy it.")]
            public bool classOnly;
            public SpecialistClass forClass;
            public string displayName;
            [TextArea] public string description;
            [Min(0)] public int cost;
            public float armorMitigation;
            public float speedBonus;
            public float workBonus;
            public float courageBonus;
            public float healAmount;
            public float regenPerSecond;
            public float durationSeconds;
            public float damageBonus;

            public ShopItemDef ToDef() => new ShopItemDef
            {
                Id = id,
                Kind = kind,
                Vendor = vendor,
                ForClass = classOnly ? forClass : (SpecialistClass?)null,
                DisplayName = displayName,
                Description = description,
                Cost = cost,
                ArmorMitigation = armorMitigation,
                SpeedBonus = speedBonus,
                WorkBonus = workBonus,
                CourageBonus = courageBonus,
                HealAmount = healAmount,
                RegenPerSecond = regenPerSecond,
                DurationSeconds = durationSeconds,
                DamageBonus = damageBonus
            };

            public static Entry From(ShopItemDef def) => new Entry
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

        public List<Entry> entries = new List<Entry>();

        public List<ShopItemDef> ToDefs()
        {
            var defs = new List<ShopItemDef>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
                defs.Add(entries[i].ToDef());
            return defs;
        }

        /// <summary>Fill this asset from a catalog (the exporter uses the built-in one).</summary>
        public void CopyFrom(IReadOnlyList<ShopItemDef> defs)
        {
            entries.Clear();
            for (int i = 0; i < defs.Count; i++)
                entries.Add(Entry.From(defs[i]));
        }
    }
}
