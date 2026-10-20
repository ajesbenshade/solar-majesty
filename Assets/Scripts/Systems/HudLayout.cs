using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Screen-space rectangles for the overseer console. The dock, crest, catalogs, chips, and
    /// tutorial bar share one set of gaps so labels do not paint on top of each other at 1280×800
    /// or 1920×1080 (including a 1.25 HUD scale, where the dock narrows).
    /// </summary>
    public static class HudLayout
    {
        public const float Margin = 16f;
        public const float Pad = 12f;
        public const float DockH = 100f;
        public const float DockW = 860f;
        public const float CrestW = 240f;
        public const float CrestH = 52f;
        public const float CrestRise = 30f;
        /// <summary>Matches HudSkin.CrestPad. The drawn plate is larger than the crest rect.</summary>
        public const float CrestPad = 8f;
        public const float MapSize = 176f;
        public const float ObjectivesW = 300f;
        public const float PanelGap = 8f;
        public const float TextGap = 6f;
        public const int LevyBudget = 46;
        public const int TreasuryBudget = 26;

        public static float DockWidth(float viewW) =>
            Mathf.Min(DockW, viewW - Margin * 3f - MapSize - 12f);

        /// <summary>Centred, but never under the minimap on narrow screens.</summary>
        public static float DockLeft(float viewW)
        {
            float w = DockWidth(viewW);
            return Mathf.Max(Margin, Mathf.Min((viewW - w) * 0.5f, viewW - Margin - MapSize - 12f - w));
        }

        public static float DockTop(float viewH) => viewH - Margin - DockH;

        public static Rect Crest(float viewW, float viewH)
        {
            float w = DockWidth(viewW);
            float left = DockLeft(viewW);
            return new Rect(left + (w - CrestW) * 0.5f, DockTop(viewH) - CrestRise, CrestW, CrestH);
        }

        /// <summary>Catalog / flag / orbital sheet. Sits left of the treasury plate, above it.</summary>
        public static Rect ToolPopup(float viewW, float viewH, float preferredW, float preferredH)
        {
            float left = DockLeft(viewW);
            Rect crest = Crest(viewW, viewH);
            float plateLeft = crest.xMin - CrestPad;
            float plateTop = crest.yMin - CrestPad;
            float width = Mathf.Min(preferredW, Mathf.Max(160f, plateLeft - left - PanelGap));
            float bottom = plateTop - PanelGap;
            float y = bottom - preferredH;
            float h = preferredH;
            if (y < Margin)
            {
                y = Margin;
                h = Mathf.Max(36f, bottom - y);
            }
            return new Rect(left, y, width, h);
        }

        public static Rect Objectives(float viewW)
        {
            return new Rect(viewW - Margin - ObjectivesW, Margin, ObjectivesW, 200f);
        }

        /// <summary>Label and rate share the left column; the amount keeps the right edge.</summary>
        public static void ChipText(Rect chip, out Rect label, out Rect number)
        {
            number = new Rect(chip.xMax - 40f, chip.y, 36f, chip.height);
            float labelX = chip.x + 29f;
            label = new Rect(labelX, chip.y + 2f, number.xMin - TextGap - labelX, 12f);
        }

        public const float StockpileW = 244f;
        public const float StockpileChipGap = 6f;

        /// <summary>Width of one chip in the 2×2 stockpile grid.</summary>
        public static float StockpileChipWidth(float stockpileWidth = StockpileW) =>
            (stockpileWidth - StockpileChipGap) * 0.5f;

        /// <summary>
        /// CHARGERS chip rate: worker drones housed / berths ("12/16 drones"); heroes are counted
        /// on their own chip. With no berths yet it falls back to the solar farm count.
        /// </summary>
        public static string ChargersRate(int drones, int capacity, int solarFarms)
        {
            if (capacity > 0) return Loc.F("{0}/{1} drones", drones, capacity);
            return solarFarms > 0 ? Loc.F("{0} solar", solarFarms) : null;
        }

        /// <summary>The chip rate wording, most to least verbose: full, first word + abbreviated second, first token.</summary>
        public static string[] ChipRateVariants(string rate)
        {
            if (string.IsNullOrEmpty(rate)) return new[] { "" };
            var list = new System.Collections.Generic.List<string> { rate };
            var words = rate.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            // Only counts compact ("330 carried" -> "330"); a unit like "% built" stays whole.
            bool count = words.Length >= 2 && words[0].Length > 0
                && (char.IsDigit(words[0][0]) || (words[0].Length > 1 && (words[0][0] == '-' || words[0][0] == '+') && char.IsDigit(words[0][1])));
            if (!count) return new[] { rate };
            {
                string second = words[1];
                string abbr = second.Length > 5 ? second.Substring(0, 4) + "." : second;
                list.Add(words[0] + " " + abbr);
            }
            list.Add(words[0]);
            var result = new System.Collections.Generic.List<string>();
            foreach (var s in list)
                if (!result.Contains(s)) result.Add(s);
            return result.ToArray();
        }

        /// <summary>Building or power row: the price column starts after the name.</summary>
        public static void SplitRow(float rowX, float rowY, float rowWidth, float rowHeight, float nameX, float costWidth, out Rect name, out Rect cost)
        {
            cost = new Rect(rowX + rowWidth - TextGap - costWidth, rowY, costWidth, rowHeight);
            float nameLeft = rowX + nameX;
            name = new Rect(nameLeft, rowY, Mathf.Max(0f, cost.xMin - PanelGap - nameLeft), rowHeight);
        }

        public static void StatLine(Rect row, out Rect label, out Rect value)
        {
            float labelW = Mathf.Min(72f, row.width * 0.45f);
            label = new Rect(row.x, row.y, labelW, row.height);
            float valueX = label.xMax + TextGap;
            value = new Rect(valueX, row.y, Mathf.Max(0f, row.xMax - valueX), row.height);
        }

        /// <summary>SOL and the replay tag stop before the speed buttons. Speed stays 116px so 0.75× fits.</summary>
        public static void ClockHeader(Rect panel, out Rect sol, out Rect tag, out Rect speed)
        {
            speed = new Rect(panel.xMax - 116f, panel.y - 2f, 116f, 28f);
            float solX = panel.x + 18f;
            sol = new Rect(solX, panel.y - 1f, Mathf.Max(0f, speed.xMin - TextGap - solX), 15f);
            tag = new Rect(panel.x, panel.y + 15f, Mathf.Max(0f, speed.xMin - TextGap - panel.x), 12f);
        }

        public static void Tutorial(float viewW, float barH, out Rect bar, out Rect text, out Rect skip)
        {
            float left = Margin;
            float right = viewW - Margin - ObjectivesW - 12f;
            float w = Mathf.Min(720f, Mathf.Max(240f, right - left));
            float x = Mathf.Clamp((viewW - w) * 0.5f, left, Mathf.Max(left, right - w));
            bar = new Rect(x, Margin, w, barH);
            float contentX = bar.x + Pad;
            float contentRight = bar.xMax - Pad;
            skip = new Rect(contentRight - 72f, bar.center.y - 13f, 72f, 26f);
            float textX = contentX + 42f;
            text = new Rect(textX, bar.y + Pad, Mathf.Max(0f, skip.xMin - PanelGap - textX), barH - 16f);
        }

        public static void Stake(Rect row, out Rect label, out Rect value)
        {
            value = new Rect(row.xMax - 140f, row.y, 140f, row.height);
            float labelX = row.x + 19f;
            label = new Rect(labelX, row.y, Mathf.Max(0f, value.xMin - TextGap - labelX), row.height);
        }

        /// <summary>
        /// Tills, collectors, and daily tax. Shortens until it fits the dock's middle column
        /// (about 370px at 1280 and 1920, narrower when the HUD scale shrinks the dock).
        /// </summary>
        public static string LevyLine(int tills, int collectors, int bags, int daily)
        {
            var v = LevyVariants(tills, collectors, bags, daily);
            for (int i = 0; i < v.Length - 1; i++)
                if (v[i].Length <= LevyBudget) return v[i];
            return v[v.Length - 1];
        }

        /// <summary>The levy wordings, most to least verbose.</summary>
        public static string[] LevyVariants(int tills, int collectors, int bags, int daily) => new[]
        {
            Loc.F("TILLS {0:N0} · COLLECTORS {1} carrying {2:N0} · EU {3:N0}/day", tills, collectors, bags, daily),
            Loc.F("TILLS {0:N0} · {1} HAUL · {2:N0} BAGS · EU {3:N0}/day", tills, collectors, bags, daily),
            Loc.F("TILLS {0:N0} · HAUL {1} · EU {2:N0}/day", tills, collectors, daily),
            Loc.F("T {0:N0} · C {1} · EU {2:N0}", tills, collectors, daily),
        };

        /// <summary>
        /// First variant whose measured width fits <paramref name="maxWidth"/>; otherwise the shortest
        /// variant cut with an ellipsis. <paramref name="measure"/> and <paramref name="maxWidth"/> may be
        /// in different units: <paramref name="scale"/> converts measured width into maxWidth's units
        /// (leave 1 when both are in the same GUI space).
        /// </summary>
        public static string FitText(string[] variants, System.Func<string, float> measure, float maxWidth, float scale = 1f)
        {
            if (variants == null || variants.Length == 0) return "";
            float limit = maxWidth / Mathf.Max(0.01f, scale);
            for (int i = 0; i < variants.Length; i++)
                if (measure(variants[i]) <= limit) return variants[i];

            const string ellipsis = "…";
            string s = variants[variants.Length - 1];
            for (int n = s.Length - 1; n > 0; n--)
            {
                string cut = s.Substring(0, n).TrimEnd() + ellipsis;
                if (measure(cut) <= limit) return cut;
            }
            return measure(ellipsis) <= limit ? ellipsis : "";
        }

        /// <summary>Levy line that fits <paramref name="maxWidth"/>; identical wording to <see cref="LevyLine"/> when it fits.</summary>
        public static string LevyLineFit(int tills, int collectors, int bags, int daily,
            System.Func<string, float> measure, float maxWidth, float scale = 1f)
        {
            var v = LevyVariants(tills, collectors, bags, daily);
            // Keep the char-budget pick as the first choice so wording never changes when it fits.
            string preferred = LevyLine(tills, collectors, bags, daily);
            int start = System.Array.IndexOf(v, preferred);
            if (start > 0) v = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Skip(v, start));
            return FitText(v, measure, maxWidth, scale);
        }

        /// <summary>Income under the treasury number. Stays inside the crest plate.</summary>
        public static string TreasuryLine(float perMin, int escrow, bool thin)
        {
            string head = perMin >= 1f ? Loc.F("+{0:F0}/MIN", perMin) : Loc.S("NO INCOME");
            if (thin) head = Loc.F("LOW · {0}", head);
            if (escrow <= 0) return head;
            string withBounties = Loc.F("{0} · {1:N0} BOUNTIES", head, escrow);
            if (withBounties.Length <= TreasuryBudget) return withBounties;
            string withOut = Loc.F("{0} · {1:N0} OUT", head, escrow);
            if (withOut.Length <= TreasuryBudget) return withOut;
            return head;
        }
    }
}
