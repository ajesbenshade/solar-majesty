using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>The shipped player carries the Solar Majesty icon, not Unity's default.</summary>
    public class AppIconTests
    {
        // Mirrors ReleaseStamp.IconFolder / IconPrefix (the Editor assembly is not referenced here).
        private const string Prefix = "Assets/Art/Icons/SM_AppIcon_";
        private const string Master = Prefix + "1024.png";
        private const string WindowsIco = "Assets/Art/Icons/SM_AppIcon.ico";

        [Test]
        public void MasterIconAndWindowsIcoExist()
        {
            Assert.IsTrue(File.Exists(Master), Master);
            Assert.IsTrue(File.Exists(WindowsIco), WindowsIco);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Master);
            Assert.IsNotNull(tex);
            Assert.AreEqual(1024, tex.width);
        }

        [Test]
        public void DefaultIconIsTheGameIcon()
        {
            Texture2D[] icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            Assert.IsNotEmpty(icons);
            Assert.IsNotNull(icons[0], "Default Icon is empty");
            Assert.AreEqual(Master, AssetDatabase.GetAssetPath(icons[0]));
        }

        [Test]
        public void EveryStandaloneIconSlotIsFilled()
        {
            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            Texture2D[] icons = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Any);
            Assert.AreEqual(sizes.Length, icons.Length);
            for (int i = 0; i < icons.Length; i++)
            {
                Assert.IsNotNull(icons[i], $"Standalone icon slot {sizes[i]} px is empty");
                StringAssert.StartsWith(Prefix, AssetDatabase.GetAssetPath(icons[i]));
                Assert.GreaterOrEqual(icons[i].width, sizes[i], $"slot {sizes[i]} px is upscaled");
            }
        }
    }
}
