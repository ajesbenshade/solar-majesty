using UnityEngine;
using UnityEngine.Rendering;

namespace SolarMajesty
{
    /// <summary>
    /// Applies Uber Stylized Water to runtime lake/river/pond meshes.
    /// Falls back to URP Lit tint if the Resources material is missing.
    /// </summary>
    public static class StylizedWaterVisual
    {
        private const string ResourcePath = "Environment/SM_Water";
        private static Material _template;

        public static void Apply(GameObject go, Color deep, Color shallow)
        {
            if (go == null) return;

            var rendList = go.GetComponentsInChildren<Renderer>(true);
            if (rendList == null || rendList.Length == 0) return;

            Material mat = CreateInstance(deep, shallow);
            if (mat == null)
            {
                PlanetaryWorldGen.Tint(go, Color.Lerp(deep, shallow, 0.4f), 0.72f);
                return;
            }

            foreach (var rend in rendList)
            {
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static Material CreateInstance(Color deep, Color shallow)
        {
            if (_template == null)
                _template = Resources.Load<Material>(ResourcePath);
            if (_template == null) return null;

            var mat = Object.Instantiate(_template);
            mat.name = "SM_Water_Runtime";

            // Pond meshes are thin discs — keep waves off and skip planar reflections
            // (no reflection camera in the planetary map).
            mat.DisableKeyword("_ENABLEWAVE");
            mat.DisableKeyword("_ENABLEPLANERREFLECTION");
            if (mat.HasProperty("_ENABLEWAVE")) mat.SetFloat("_ENABLEWAVE", 0f);
            if (mat.HasProperty("_ENABLEPLANERREFLECTION")) mat.SetFloat("_ENABLEPLANERREFLECTION", 0f);

            Color deepA = deep;
            deepA.a = 0.72f;
            Color shallowA = shallow;
            shallowA.a = 0.28f;

            if (mat.HasProperty("_Color_Deep")) mat.SetColor("_Color_Deep", deepA);
            if (mat.HasProperty("_Color_Shallow")) mat.SetColor("_Color_Shallow", shallowA);
            if (mat.HasProperty("_Deep_Color")) mat.SetColor("_Deep_Color", deepA);
            if (mat.HasProperty("_ShallowColor")) mat.SetColor("_ShallowColor", shallowA);

            Color under = Color.Lerp(deep, shallow, 0.35f);
            under.a = 0.35f;
            if (mat.HasProperty("_UnderwaterLayerColor")) mat.SetColor("_UnderwaterLayerColor", under);
            if (mat.HasProperty("_Underwater_Color")) mat.SetColor("_Underwater_Color", under);

            return mat;
        }
    }
}
