using System.Collections.Generic;
using System.Text;

namespace SolarMajesty
{
    /// <summary>
    /// How Earth leaves for Luna. The Earth demo keeps the launch objective, and says
    /// where Full Campaign lives, instead of sending the player hunting a hidden hop.
    /// </summary>
    public static class LaunchPath
    {
        /// <summary>Settings panel, GUIDED START section, SKIP GUIDED START chip.</summary>
        public const string FullCampaignWhere = "Settings → Guided Start → Skip";

        public static string Chain(string craft) => Loc.F("{0} research → launch", craft);

        public static string ToggleNote =>
            Loc.F("Clear Earth to open Luna, or skip the guided start ({0}).", Loc.S(FullCampaignWhere));

        /// <summary>Objective column, title toast, and the win-banner note share this sentence.</summary>
        public static string ObjectiveText(bool fullCampaignOn, bool launchReady, string craft)
        {
            string name = string.IsNullOrEmpty(craft) ? Loc.S("Lunar Rocket") : craft;
            if (fullCampaignOn)
                return launchReady ? Loc.S("ready on pad") : Chain(name);
            if (launchReady)
                return Loc.F("ready on pad. {0}", ToggleNote);
            return Loc.F("{0}. {1}", Chain(name), ToggleNote);
        }

        /// <summary>
        /// True when the tech is on the research list and its prerequisites are already unlocked.
        /// Full Campaign is <see cref="DemoSettings.FirstHourDemo"/> off.
        /// </summary>
        public static bool CanResearch(TechId id, ICollection<TechId> unlocked)
        {
            if (!DemoSlice.ShowTech(id)) return false;
            var def = TechCatalog.Get(id);
            if (def == null) return false;
            if (unlocked != null && unlocked.Contains(id)) return false;
            if (def.Prerequisites == null) return true;
            for (int i = 0; i < def.Prerequisites.Length; i++)
            {
                if (unlocked == null || !unlocked.Contains(def.Prerequisites[i]))
                    return false;
            }
            return true;
        }

        public static string UnmetPrerequisites(TechId id, ICollection<TechId> unlocked)
        {
            var def = TechCatalog.Get(id);
            if (def?.Prerequisites == null || def.Prerequisites.Length == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < def.Prerequisites.Length; i++)
            {
                var need = def.Prerequisites[i];
                if (unlocked != null && unlocked.Contains(need)) continue;
                var pre = TechCatalog.Get(need);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(pre != null ? pre.DisplayNameText : need.ToString());
            }
            return sb.ToString();
        }
    }
}
