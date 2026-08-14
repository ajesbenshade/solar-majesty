using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Compact per-body campus blob for the continue slot (pieces + build progress).
    /// Not a full world snapshot — flags, fauna, and specialist HP are not stored.
    /// </summary>
    public struct CampusSlot
    {
        public BuildingCategory Category;
        public int X;
        public int Y;
        public int W;
        public int H;
        public int ProgressMilli;
        public bool VillageHab;
    }

    public static class CampusSnapshot
    {
        public const int Version = 1;

        public static string Encode(int population, List<CampusSlot> slots)
        {
            var sb = new StringBuilder(64);
            sb.Append(Version);
            sb.Append('|');
            sb.Append(Mathf.Max(0, population));
            sb.Append('|');
            if (slots == null || slots.Count == 0)
                return sb.ToString();

            for (int i = 0; i < slots.Count; i++)
            {
                if (i > 0) sb.Append(';');
                var s = slots[i];
                sb.Append((int)s.Category);
                sb.Append(':');
                sb.Append(s.X);
                sb.Append(':');
                sb.Append(s.Y);
                sb.Append(':');
                sb.Append(Mathf.Max(1, s.W));
                sb.Append(':');
                sb.Append(Mathf.Max(1, s.H));
                sb.Append(':');
                sb.Append(Mathf.Clamp(s.ProgressMilli, 0, 1000));
                sb.Append(':');
                sb.Append(s.VillageHab ? 1 : 0);
            }

            return sb.ToString();
        }

        public static bool TryDecode(string raw, out int population, List<CampusSlot> slots)
        {
            population = 0;
            slots?.Clear();
            if (string.IsNullOrEmpty(raw) || slots == null)
                return false;

            var parts = raw.Split('|');
            if (parts.Length < 3) return false;
            if (!int.TryParse(parts[0], out int ver) || ver != Version)
                return false;
            int.TryParse(parts[1], out population);
            if (string.IsNullOrEmpty(parts[2]))
                return true;

            var entries = parts[2].Split(';');
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.IsNullOrEmpty(entries[i])) continue;
                var f = entries[i].Split(':');
                if (f.Length < 7) continue;
                if (!int.TryParse(f[0], out int cat)) continue;
                if (!int.TryParse(f[1], out int x)) continue;
                if (!int.TryParse(f[2], out int y)) continue;
                if (!int.TryParse(f[3], out int w)) continue;
                if (!int.TryParse(f[4], out int h)) continue;
                if (!int.TryParse(f[5], out int milli)) continue;
                int.TryParse(f[6], out int village);
                slots.Add(new CampusSlot
                {
                    Category = (BuildingCategory)cat,
                    X = x,
                    Y = y,
                    W = Mathf.Max(1, w),
                    H = Mathf.Max(1, h),
                    ProgressMilli = Mathf.Clamp(milli, 0, 1000),
                    VillageHab = village == 1
                });
            }

            return true;
        }

        public static int SlotCount(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return 0;
            var scratch = new List<CampusSlot>(16);
            return TryDecode(raw, out _, scratch) ? scratch.Count : 0;
        }

        public static int Rank(BuildingCategory cat)
        {
            if (cat == BuildingCategory.Palace) return 0;
            if (cat == BuildingCategory.Utility) return 1;
            return 2;
        }
    }
}
