using NUnit.Framework;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Every DemoAudio event resolves to a rendered clip, so none of them falls through to the
    /// procedural chord in a shipped build. Loads through the same Resources path DemoAudio uses.
    /// </summary>
    public class SfxClipCoverageTests
    {
        /// <summary>Keep in step with the SfxEvent keys in Runtime/DemoAudio.cs.</summary>
        private static readonly string[] EventKeys =
        {
            "ui_hover", "ui_click", "retry", "flag_post", "claim", "bite", "robot_hit", "stalker_growl",
            "stalker_death", "build_place", "build_complete", "construction_tick", "extract", "research",
            "level_up", "credits", "robot_down", "robot_spawn", "party_form", "repair", "heal", "equip",
            "launch_ready", "launch", "alert_warning", "alert_critical", "fail", "victory"
        };

        [Test]
        public void EveryEvent_HasARenderedClip()
        {
            foreach (string key in EventKeys)
            {
                var clip = Resources.Load<AudioClip>("Audio/Sfx/sfx_" + key + "_1");
                if (clip == null) clip = Resources.Load<AudioClip>("Audio/sfx_" + key);
                Assert.IsNotNull(clip, $"sfx_{key} has no clip and would play the procedural chord");
                Assert.Greater(clip.length, 0.02f, $"sfx_{key} is empty");
            }
        }

        /// <summary>The three the release checklist called out as missing.</summary>
        [Test]
        public void Extract_BuildComplete_Research_HaveAllThreeTakes()
        {
            foreach (string key in new[] { "extract", "build_complete", "research" })
            {
                for (int take = 1; take <= 3; take++)
                    Assert.IsNotNull(Resources.Load<AudioClip>("Audio/Sfx/sfx_" + key + "_" + take), $"sfx_{key}_{take}");
            }
        }
    }
}
