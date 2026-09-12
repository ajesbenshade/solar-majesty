using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Continue-slot specialist persist (level / XP / purse / revive count / corpse).
    /// One record per class — workshops fabricate at most one outdoor robot.
    /// </summary>
    public struct SpecialistRecord
    {
        public SpecialistClass Class;
        public int Level;
        public int Xp;
        public int Credits;
        public int ReviveCount;
        public ShopItemId Suit;
        public ShopItemId Accessory;
        public ShopItemId Weapon;
        public bool Corpse;
    }

    public static class SpecialistRoster
    {
        public const int Version = 1;

        public static string Encode(IReadOnlyList<SpecialistRecord> records)
        {
            var sb = new StringBuilder(96);
            sb.Append(Version);
            sb.Append('|');
            if (records == null || records.Count == 0)
                return sb.ToString();

            for (int i = 0; i < records.Count; i++)
            {
                if (i > 0) sb.Append(';');
                var r = records[i];
                sb.Append((int)r.Class);
                sb.Append(':');
                sb.Append(Mathf.Clamp(r.Level, 1, OverseerRules.LevelCap));
                sb.Append(':');
                sb.Append(Mathf.Max(0, r.Xp));
                sb.Append(':');
                sb.Append(Mathf.Max(0, r.Credits));
                sb.Append(':');
                sb.Append(Mathf.Max(0, r.ReviveCount));
                sb.Append(':');
                sb.Append((int)r.Suit);
                sb.Append(':');
                sb.Append(r.Corpse ? 1 : 0);
                sb.Append(':');
                sb.Append((int)r.Accessory);
                sb.Append(':');
                sb.Append((int)r.Weapon);
            }

            return sb.ToString();
        }

        public static bool TryDecode(string raw, List<SpecialistRecord> records)
        {
            records?.Clear();
            if (string.IsNullOrEmpty(raw) || records == null)
                return false;

            var parts = raw.Split('|');
            if (parts.Length < 2) return false;
            if (!int.TryParse(parts[0], out int ver) || ver != Version)
                return false;
            if (string.IsNullOrEmpty(parts[1]))
                return true;

            var entries = parts[1].Split(';');
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.IsNullOrEmpty(entries[i])) continue;
                var f = entries[i].Split(':');
                if (f.Length < 7) continue;
                if (!int.TryParse(f[0], out int cls)) continue;
                if (!int.TryParse(f[1], out int level)) continue;
                if (!int.TryParse(f[2], out int xp)) continue;
                if (!int.TryParse(f[3], out int credits)) continue;
                if (!int.TryParse(f[4], out int revives)) continue;
                if (!int.TryParse(f[5], out int suit)) continue;
                int.TryParse(f[6], out int corpse);
                int accessory = 0;
                if (f.Length >= 8)
                    int.TryParse(f[7], out accessory);
                int weapon = 0;
                if (f.Length >= 9)
                    int.TryParse(f[8], out weapon);
                records.Add(new SpecialistRecord
                {
                    Class = (SpecialistClass)cls,
                    Level = Mathf.Clamp(level, 1, OverseerRules.LevelCap),
                    Xp = Mathf.Max(0, xp),
                    Credits = Mathf.Max(0, credits),
                    ReviveCount = Mathf.Max(0, revives),
                    Suit = (ShopItemId)suit,
                    Accessory = (ShopItemId)accessory,
                    Weapon = (ShopItemId)weapon,
                    Corpse = corpse == 1
                });
            }

            return true;
        }

        public static bool TryGet(IReadOnlyList<SpecialistRecord> records, SpecialistClass cls, out SpecialistRecord record)
        {
            record = default;
            if (records == null) return false;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Class != cls) continue;
                record = records[i];
                return true;
            }
            return false;
        }
    }
}
