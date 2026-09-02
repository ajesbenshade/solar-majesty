using UnityEditor;
using UnityEngine;

namespace SolarMajesty.EditorTools
{
    /// <summary>
    /// Fails loudly on shader compile errors. Shader problems do not break a C# build, so without
    /// this a broken hull shader would ship as magenta and only surface in a screenshot.
    /// </summary>
    public static class ShaderValidation
    {
        private static readonly string[] Shaders =
        {
            "Assets/Shaders/SM_Hull.shader",
            "Assets/Shaders/SM_PlanetGround.shader"
        };

        [MenuItem("Solar Majesty/Render/Validate Shaders", priority = 101)]
        public static void Validate()
        {
            bool ok = true;

            for (int i = 0; i < Shaders.Length; i++)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(Shaders[i]);
                if (shader == null)
                {
                    Debug.LogError($"[ShaderCheck] Missing: {Shaders[i]}");
                    ok = false;
                    continue;
                }

                int count = ShaderUtil.GetShaderMessageCount(shader);
                if (count == 0)
                {
                    Debug.Log($"[ShaderCheck] OK: {shader.name}");
                    continue;
                }

                var messages = ShaderUtil.GetShaderMessages(shader);
                for (int m = 0; m < messages.Length; m++)
                {
                    var msg = messages[m];
                    string line = $"[ShaderCheck] {shader.name} ({msg.platform}) line {msg.line}: {msg.message}";
                    if (msg.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    {
                        Debug.LogError(line);
                        ok = false;
                    }
                    else
                    {
                        Debug.LogWarning(line);
                    }
                }
            }

            Debug.Log(ok ? "[ShaderCheck] All shaders compiled." : "[ShaderCheck] FAILED.");

            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
