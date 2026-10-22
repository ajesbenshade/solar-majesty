using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Shaders reached only through Shader.Find are stripped from player builds unless an included
    /// asset references them. The keeper materials in Resources/ShaderKeep are that reference.
    /// </summary>
    public class ShaderKeepTests
    {
        private const string SkyKeeper = "Assets/Resources/ShaderKeep/SM_Keep_SkyPanorama.mat";

        [Test]
        public void SkyPanoramaShaderIsKeptForPlayerBuilds()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyKeeper);
            Assert.IsNotNull(mat, $"{SkyKeeper} is missing: run Solar Majesty → Build → Keep Runtime Shader Variants");
            Assert.IsNotNull(mat.shader);
            Assert.AreEqual(SkyPanorama.ShaderName, mat.shader.name);
        }
    }
}
