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
        public const float DockW = 872f; // +12 with StockpileW so CHARGERS stays at 9pt or more
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

        /// <summary>Rows of text inside the dock at 100 % (colony header and two chip rows; verbs and levy line).</summary>
        public const float DockTextRows = 76f;

        /// <summary>Dock height: grows with TEXT SIZE so the chips' two lines and the clock column fit.</summary>
        public static float DockHeight => DockH + HudTextScale.Grow(DockTextRows);

        /// <summary>Objectives / alert column width; widens with TEXT SIZE.</summary>
        public static float ObjectivesWidth => HudTextScale.Width(ObjectivesW);

        public static float DockWidth(float viewW) =>
            Mathf.Min(HudTextScale.Width(DockW), viewW - Margin * 3f - MapSize - 12f);

        /// <summary>
        /// Dock columns: stockpile, verbs, clock. At 100 % they are 244 / the rest / 186; extra width
        /// from a larger TEXT SIZE goes to the text columns, never below the verbs' own width.
        /// </summary>
        public static void DockColumns(float contentW, out float stockpileW, out float clockW)
        {
            stockpileW = StockpileW;
            clockW = 186f;
            float extra = contentW - (DockW - Pad * 2f);
            if (extra <= 0f) return;
            stockpileW += extra * 0.55f;
            clockW += extra * 0.45f;
        }

        /// <summary>Centred, but never under the minimap on narrow screens.</summary>
        public static float DockLeft(float viewW)
        {
            float w = DockWidth(viewW);
            return Mathf.Max(Margin, Mathf.Min((viewW - w) * 0.5f, viewW - Margin - MapSize - 12f - w));
        }

        public static float DockTop(float viewH) => viewH - Margin - DockHeight;

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
            float w = ObjectivesWidth;
            return new Rect(viewW - Margin - w, Margin, w, 200f);
        }

        /// <summary>Stockpile chip height: two text lines (label over rate) beside the amount.</summary>
        public static float ChipHeight => HudTextScale.Line(28f);

        /// <summary>Label and rate share the left column; the amount keeps the right edge.</summary>
        public static void ChipText(Rect chip, out Rect label, out Rect number)
        {
            // The amount column follows the number font (17 px at 100 %).
            float numW = 36f * HudTextScale.Font(17) / 17f;
            number = new Rect(chip.xMax - numW - 4f, chip.y, numW, chip.height);
            float labelX = chip.x + 29f;
            label = new Rect(labelX, chip.y + 2f, number.xMin - TextGap - labelX, HudTextScale.Line(12f));
        }

        /// <summary>The rate line under a chip label.</summary>
        public static Rect ChipRate(Rect chip, Rect label) =>
            new Rect(label.x, chip.y + HudTextScale.Line(13f), label.width, label.height);

        /// <summary>Smallest chip label size that still reads at HUD scale 1.</summary>
        public const int ChipLabelMinSize = 8;

        /// <summary>
        /// Width the chip label may use: up to the right-aligned amount plus a gap, never less than
        /// the <see cref="ChipText"/> column. The amount is usually 1-3 digits, so this is the slack.
        /// </summary>
        public static float ChipLabelWidth(Rect labelRect, Rect numberRect, float amountWidth) =>
            Mathf.Max(labelRect.width, numberRect.xMax - Mathf.Min(numberRect.width, amountWidth) - TextGap - labelRect.x);

        /// <summary>
        /// Largest font size from <paramref name="baseSize"/> down to <paramref name="minSize"/> whose
        /// measured width fits; the text itself is never shortened. Returns minSize if nothing fits.
        /// </summary>
        public static int FitFontSize(System.Func<int, float> widthAtSize, float maxWidth, int baseSize, int minSize = ChipLabelMinSize)
        {
            for (int s = baseSize; s > minSize; s--)
                if (widthAtSize(s) <= maxWidth) return s;
            return minSize;
        }

        public const float StockpileW = 256f;

        /// <summary>Canon Watchtower blurb; the build list and the building card both use it.</summary>
        public const string WatchtowerBlurb = "Guard post and levy till. Arm lasers for EU.";
        /// <summary>Short stat appended to the Watchtower row when the row has room.</summary>
        public const string WatchtowerStat = "+1 Haul";

        /// <summary>
        /// Build-list row text, longest first: "name · blurb · stat" if it fits, else "name · blurb".
        /// The blurb always wins over the stat.
        /// </summary>
        public static string[] BuildRowVariants(string name, string description, string stat)
        {
            string row = string.IsNullOrEmpty(description) ? name : name + "  ·  " + description;
            return string.IsNullOrEmpty(stat) ? new[] { row } : new[] { row + "  ·  " + stat, row };
        }
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
            float labelW = Mathf.Min(HudTextScale.Width(72f), row.width * 0.45f);
            label = new Rect(row.x, row.y, labelW, row.height);
            float valueX = label.xMax + TextGap;
            value = new Rect(valueX, row.y, Mathf.Max(0f, row.xMax - valueX), row.height);
        }

        /// <summary>SOL and the replay tag stop before the speed buttons. Speed stays 116px so 0.75× fits.</summary>
        public static void ClockHeader(Rect panel, out Rect sol, out Rect tag, out Rect speed)
        {
            speed = new Rect(panel.xMax - 116f, panel.y - 2f, 116f, 28f);
            float solX = panel.x + 18f;
            float solH = HudTextScale.Line(15f);
            sol = new Rect(solX, panel.y - 1f, Mathf.Max(0f, speed.xMin - TextGap - solX), solH);
            tag = new Rect(panel.x, panel.y + solH, Mathf.Max(0f, speed.xMin - TextGap - panel.x), HudTextScale.Line(12f));
        }

        public static void Tutorial(float viewW, float barH, out Rect bar, out Rect text, out Rect skip)
        {
            float left = Margin;
            float right = viewW - Margin - ObjectivesWidth - 12f;
            float w = Mathf.Min(720f, Mathf.Max(240f, right - left));
            float x = Mathf.Clamp((viewW - w) * 0.5f, left, Mathf.Max(left, right - w));
            bar = new Rect(x, Margin, w, barH);
            float contentX = bar.x + Pad;
            float contentRight = bar.xMax - Pad;
            float skipW = HudTextScale.Width(72f);
            float skipH = HudTextScale.Line(26f);
            skip = new Rect(contentRight - skipW, bar.center.y - skipH * 0.5f, skipW, skipH);
            float textX = contentX + 42f;
            text = new Rect(textX, bar.y + Pad, Mathf.Max(0f, skip.xMin - PanelGap - textX), barH - 16f);
        }

        public static void Stake(Rect row, out Rect label, out Rect value)
        {
            float valueW = HudTextScale.Width(140f);
            value = new Rect(row.xMax - valueW, row.y, valueW, row.height);
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
