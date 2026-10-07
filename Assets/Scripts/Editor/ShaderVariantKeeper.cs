#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Runtime code builds glass, shield and VFX materials from URP shaders and switches them to
    /// transparent / emissive with keywords. URP strips any variant no saved material uses, so in a
    /// player build those materials fall back to opaque (the solid blue "bubble"). These keeper
    /// materials live in Resources, which forces the variants into every build.
    /// Menu: Solar Majesty → Build → Keep Runtime Shader Variants. Also run before each build.
    /// </summary>
    public static class ShaderVariantKeeper
    {
        private const string Folder = "Assets/Resources/ShaderKeep";

        [MenuItem("Solar Majesty/Build/Keep Runtime Shader Variants", priority = 199)]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "ShaderKeep");

            Keep("Universal Render Pipeline/Lit", "SM_Keep_LitTransparent", transparent: true, emission: false);
            Keep("Universal Render Pipeline/Lit", "SM_Keep_LitTransparentEmissive", transparent: true, emission: true);
            Keep("Universal Render Pipeline/Lit", "SM_Keep_LitEmissive", transparent: false, emission: true);
            Keep("Universal Render Pipeline/Simple Lit", "SM_Keep_SimpleLitTransparent", transparent: true, emission: false);
            Keep("Universal Render Pipeline/Unlit", "SM_Keep_UnlitTransparent", transparent: true, emission: false);
            Keep("Universal Render Pipeline/Unlit", "SM_Keep_Unlit", transparent: false, emission: false);
            // Vendor leaf and grass cards are remapped onto alpha-clipped Lit at runtime.
            Keep("Universal Render Pipeline/Lit", "SM_Keep_LitCutout", transparent: false, emission: false, cutout: true);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShaderVariantKeeper] Keeper materials written to " + Folder);
        }

        private static void Keep(string shaderName, string assetName, bool transparent, bool emission, bool cutout = false)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[ShaderVariantKeeper] Missing shader " + shaderName);
                return;
            }

            string path = $"{Folder}/{assetName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader) { name = assetName };
            else mat.shader = shader;

            if (transparent)
            {
                // Same switches as ColonyVisualUtility.ApplyTransparent.
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            if (cutout)
            {
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (emission && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white);
            }

            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
        }
    }
}
#endif
