// Instanced ground cover (grass tufts, flowers, pebbles, bushes, crystals, ice shards).
//
// Meshes come from GroundCoverMeshes and carry everything per vertex:
//   COLOR.rgb  base colour (grey ramps for tinted parts, real colours for fixed parts)
//   COLOR.a    self-illumination (crystal glow), 0 for everything else
//   UV0.x      wind weight (0 at the root, 1 at a blade tip)
//   UV0.y      tint mask (1 = multiply by the per-instance _Tint, 0 = keep the vertex colour)
// Per instance: _Tint (palette colour picked at planting time).
// Lighting is a soft wrapped Lambert with the main light's shadows plus SH ambient, so cover
// sits in the same light as the ground shader. Fog uses linear view depth, which works under the
// orthographic game camera (URP's clip-z fog collapses there).
Shader "SolarMajesty/GroundCover"
{
    Properties
    {
        _WindStrength("Wind Strength (m)", Range(0, 0.6)) = 0.10
        _WindSpeed("Wind Speed", Range(0, 5)) = 1.4
        _WrapLight("Wrap Lighting", Range(0, 1)) = 0.5
        _AmbientBoost("Ambient Boost", Range(0, 2)) = 1.0
        _GlowBoost("Glow Boost", Range(0, 8)) = 2.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _WindStrength;
            float _WindSpeed;
            float _WrapLight;
            float _AmbientBoost;
            float _GlowBoost;
            float _Cull;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(CoverProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
        UNITY_INSTANCING_BUFFER_END(CoverProps)

        // World-space sway: a slow travelling wave with gusts. Weight is 0 at roots.
        float3 CoverSway(float3 positionWS, float weight)
        {
            if (weight <= 0.0) return float3(0, 0, 0);
            float t = _Time.y * _WindSpeed;
            float phase = dot(positionWS.xz, float2(0.23, 0.17));
            float gust = sin(t * 0.31 + positionWS.x * 0.027 + positionWS.z * 0.019) * 0.5 + 0.5;
            float2 dir = float2(sin(t + phase), cos(t * 0.83 + phase * 1.3) * 0.55);
            return float3(dir.x, 0.0, dir.y) * weight * _WindStrength * (0.45 + 0.55 * gust);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CoverVertex
            #pragma fragment CoverFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 albedoGlow : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CoverVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += CoverSway(positionWS, input.uv.x);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 tint = UNITY_ACCESS_INSTANCED_PROP(CoverProps, _Tint);
                float3 albedo = input.color.rgb * lerp(float3(1, 1, 1), tint.rgb, saturate(input.uv.y));
                output.albedoGlow = float4(albedo, input.color.a);
                return output;
            }

            half4 CoverFragment(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                // Thin blades are lit the same from both sides.
                n = frontFace ? n : float3(n.x, abs(n.y), n.z);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndl = dot(n, mainLight.direction);
                float wrap = saturate((ndl + _WrapLight) / (1.0 + _WrapLight));
                float3 direct = mainLight.color * wrap * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float3 ambient = SampleSH(n) * _AmbientBoost;

                float3 albedo = input.albedoGlow.rgb;
                float3 color = albedo * (direct + ambient) + albedo * input.albedoGlow.a * _GlowBoost;

                float viewZ = -TransformWorldToView(input.positionWS).z;
                color = MixFog(color, ComputeFogFactorZ0ToFar(viewZ));
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CoverShadowVertex
            #pragma fragment CoverShadowFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 CoverShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += CoverSway(positionWS, input.uv.x);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 CoverShadowFragment() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CoverDepthVertex
            #pragma fragment CoverDepthFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 CoverDepthVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += CoverSway(positionWS, input.uv.x);
                return TransformWorldToHClip(positionWS);
            }

            half CoverDepthFragment() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CoverDepthNormalsVertex
            #pragma fragment CoverDepthNormalsFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings CoverDepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += CoverSway(positionWS, input.uv.x);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 CoverDepthNormalsFragment(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
