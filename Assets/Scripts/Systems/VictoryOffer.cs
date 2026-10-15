using UnityEngine;
using UnityEngine.SceneManagement;

namespace SolarMajesty
{
    /// <summary>
    /// What the player sees when the three campaign gates are met.
    /// Earth, Luna, and the later bodies share one scene
    /// (<see cref="CampaignScenePath"/>). A missing build scene or a missing
    /// catalog profile means that body is not a level yet.
    /// </summary>
    public static class VictoryOffer
    {
        /// <summary>Only scene in the build. Bodies swap inside it via <c>BodySeed</c>.</summary>
        public const string CampaignScenePath = "Assets/Scenes/LunarOutpost_Sandbox.unity";

        public const string ContinueToLuna = "Continue to Luna";
        public const string LunaComingSoon = "Luna coming soon";
        public const string MainMenu = "Main Menu";

        public readonly struct View
        {
            public readonly bool HoldSim;
            public readonly string Headline;
            public readonly string Body;
            public readonly string ContinueLabel;
            public readonly string ComingSoon;
            public readonly bool ShowMainMenu;

            public View(
                bool holdSim,
                string headline,
                string body,
                string continueLabel,
                string comingSoon,
                bool showMainMenu)
            {
                HoldSim = holdSim;
                Headline = headline ?? "";
                Body = body ?? "";
                ContinueLabel = continueLabel;
                ComingSoon = comingSoon;
                ShowMainMenu = showMainMenu;
            }

            public bool CanContinue => !string.IsNullOrEmpty(ContinueLabel);
        }

        /// <summary>
        /// The Earth demo used to swallow the hop. A victory depart ignores that flag.
        /// Other hops (title click, debug) still honor it.
        /// </summary>
        public static bool DemoBlocksDepart(bool firstHourDemo, bool fromVictory) =>
            firstHourDemo && !fromVictory;

        public static bool NextIsBuilt(CelestialBodyId current)
        {
            var next = CampaignProgress.NextAfter(current);
            if (!next.HasValue) return false;
            return IsBuilt(next.Value);
        }

        public static bool IsBuilt(CelestialBodyId id) =>
            IsBuilt(id, SceneManager.sceneCountInBuildSettings);

        /// <summary>
        /// A spine body with a catalog profile, and at least one scene in the build.
        /// Luna is that profile inside <see cref="CampaignScenePath"/>, not a second scene.
        /// </summary>
        public static bool IsBuilt(CelestialBodyId id, int scenesInBuild)
        {
            if (scenesInBuild <= 0) return false;
            if (!CampaignProgress.IsOnSpine(id)) return false;
            var profile = CelestialBodyCatalog.Get(id);
            return profile != null
                && profile.Id == id
                && !string.IsNullOrEmpty(profile.DisplayName);
        }

        public static View For(CelestialBodyId current, bool nextBuilt, bool endless)
        {
            if (endless)
            {
                return new View(
                    false,
                    "KEEP COLONIZING",
                    "Gates are met. Worker bots stay on this claim. The treasury keeps ticking in EU.",
                    null,
                    null,
                    false);
            }

            string headline = HeadlineFor(current);
            const string body =
                "You are the central AI. Worker bots secured this claim: dens are quiet, the treasury is holding in EU, and the craft is on the pad.";

            var next = CampaignProgress.NextAfter(current);
            if (!next.HasValue || !nextBuilt)
            {
                string soon = null;
                if (next.HasValue && !nextBuilt)
                {
                    soon = next.Value == CelestialBodyId.Luna
                        ? LunaComingSoon
                        : NameOf(next.Value) + " coming soon";
                }
                return new View(true, headline, body, null, soon, true);
            }

            string label = next.Value == CelestialBodyId.Luna
                ? ContinueToLuna
                : "Continue to " + NameOf(next.Value);
            return new View(true, headline, body, label, null, true);
        }

        /// <summary>
        /// Virtual HUD rect. Callers pass width/height already divided by the UI Scale,
        /// the same space <c>OverseerHud</c> draws in.
        /// </summary>
        public static Rect PanelRect(float viewW, float viewH)
        {
            const float preferredW = 520f;
            const float preferredH = 248f;
            float width = Mathf.Min(preferredW, Mathf.Max(280f, viewW - 32f));
            float height = Mathf.Min(preferredH, Mathf.Max(180f, viewH - 32f));
            float x = Mathf.Clamp((viewW - width) * 0.5f, 8f, Mathf.Max(8f, viewW - width - 8f));
            float y = Mathf.Clamp(viewH * 0.2f, 8f, Mathf.Max(8f, viewH - height - 8f));
            if (x + width > viewW) width = Mathf.Max(32f, viewW - x);
            if (y + height > viewH) height = Mathf.Max(32f, viewH - y);
            return new Rect(x, y, width, height);
        }

        /// <summary>Screen pixels and the applied UI Scale (1 = 100%).</summary>
        public static Rect PanelRectForScreen(int screenW, int screenH, float hudScale)
        {
            float s = hudScale < 0.05f ? 1f : hudScale;
            return PanelRect(screenW / s, screenH / s);
        }

        /// <summary>
        /// Conquered body raises <see cref="CampaignProgress.MaxKey"/> through the existing unlock.
        /// Does not invent a new preference name.
        /// </summary>
        public static void RecordProgress(CelestialBodyId conquered)
        {
            CampaignProgress.UnlockNextFrom(conquered);
        }

        /// <summary>First time every gate is met. A loss or a second call does not re-fire.</summary>
        public static bool ShouldLatch(bool alreadyWon, bool alreadyLost, bool allGatesMet, bool alreadyLatched)
        {
            if (alreadyWon || alreadyLost || alreadyLatched) return false;
            return allGatesMet;
        }

        private static string HeadlineFor(CelestialBodyId id)
        {
            string name = NameOf(id);
            return string.IsNullOrEmpty(name) ? "CLAIM SECURED" : name.ToUpperInvariant() + " SECURED";
        }

        private static string NameOf(CelestialBodyId id)
        {
            var profile = CelestialBodyCatalog.Get(id);
            if (profile == null || profile.Id != id) return "";
            return profile.DisplayName ?? "";
        }
    }
}
