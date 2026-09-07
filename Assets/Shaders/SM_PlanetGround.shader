// Procedural planetary ground + optional authored detail tiles.
//
// Blends a low-lying dust colour with an exposed rock colour by slope and height, then adds a
// speckle and a broad mottle so the surface does not read as one flat tint at isometric distance.
// World-space UVs keep the grade seamless. When PlanetaryMapDressing binds
// SM_Ground_* albedo/normal, those tiles add pebble / ripple grit without replacing the
// body color lock (white hulls must stay white against Mars dirt).
Shader "SolarMajesty/PlanetGround"
{
    Properties
    {
        [MainColor] _BaseColor("Dust Color", Color) = (0.82, 0.42, 0.18, 1)
        _DarkColor("Low Color", Color) = (0.46, 0.18, 0.08, 1)
        _RockColor("Rock Color", Color) = (0.50, 0.24, 0.12, 1)

        _MacroScale("Macro Blotch Scale (m)", Range(4, 120)) = 42
        _MacroStrength("Macro Blotch Strength", Range(0,1)) = 0.35
        _DetailScale("Speckle Scale (m)", Range(0.2, 12)) = 2.2
        _DetailStrength("Speckle Strength", Range(0,1)) = 0.22

        [Header(Authored Detail Tiles)]
        [NoScaleOffset] _DetailAlbedo("Detail Albedo", 2D) = "white" {}
        [NoScaleOffset] _DetailNormal("Detail Normal", 2D) = "bump" {}
        _DetailTexScale("Detail Tex Scale (m)", Range(0.5, 32)) = 8
        _DetailTexAmount("Detail Tex Amount", Range(0,1)) = 0

        [Header(Pebbles)]
        _PebbleColor("Pebble Color", Color) = (0.30, 0.16, 0.10, 1)
        _PebbleCell("Pebble Cell (m)", Range(0.08, 2)) = 0.34
        _PebbleDensity("Pebble Density", Range(0,1)) = 0
        _PebbleSize("Pebble Size", Range(0.05, 0.6)) = 0.22

        _SlopeStart("Rock Slope Start", Range(0,1)) = 0.55
        _SlopeEnd("Rock Slope End", Range(0,1)) = 0.88

        _Smoothness("Smoothness", Range(0,1)) = 0.06
        _Metallic("Metallic", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 250

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _DarkColor;
            float4 _RockColor;
            float  _MacroScale;
            float  _MacroStrength;
            float  _DetailScale;
            float  _DetailStrength;
            float4 _DetailAlbedo_ST;
            float4 _DetailNormal_ST;
            float  _DetailTexScale;
            float  _DetailTexAmount;
            float4 _PebbleColor;
            float  _PebbleCell;
            float  _PebbleDensity;
            float  _PebbleSize;
            float  _SlopeStart;
            float  _SlopeEnd;
            float  _Smoothness;
            float  _Metallic;
        CBUFFER_END

        TEXTURE2D(_DetailAlbedo);
        SAMPLER(sampler_DetailAlbedo);
        TEXTURE2D(_DetailNormal);
        SAMPLER(sampler_DetailNormal);

        float SM_GHash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
        }

        float SM_GNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = SM_GHash(i);
            float b = SM_GHash(i + float2(1, 0));
            float c = SM_GHash(i + float2(0, 1));
            float d = SM_GHash(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float SM_Fbm(float2 p)
        {
            float v = 0.0;
            float a = 0.5;
            for (int i = 0; i < 4; i++)
            {
                v += a * SM_GNoise(p);
                p *= 2.03;
                a *= 0.5;
            }
            return v;
        }

        // Scattered pebbles: one jittered disc per grid cell, kept only where the cell's hash
        // clears the density threshold. Returns 0..1 coverage with a soft lit rim on the sun side
        // so each stone reads as a bump, not a paint fleck. Cheap enough to run per pixel.
        float SM_Pebbles(float2 p, out float rim)
        {
            rim = 0.0;
            if (_PebbleDensity <= 0.001) return 0.0;
            float2 cellP = p / max(_PebbleCell, 0.02);
            float2 cell = floor(cellP);
            float2 f = frac(cellP);
            float coverage = 0.0;
            // Check the 3x3 neighbourhood so discs can straddle cell edges.
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                float2 c = cell + float2(ox, oy);
                float h = SM_GHash(c);
                if (h > _PebbleDensity) continue;
                float2 jitter = float2(SM_GHash(c + 17.3), SM_GHash(c + 41.7));
                float2 centre = float2(ox, oy) + 0.5 + (jitter - 0.5) * 0.7;
                float radius = _PebbleSize * (0.55 + 0.9 * SM_GHash(c + 3.1));
                float2 d = f - centre;
                // Slight ellipse so stones are not all perfect discs.
                d.x *= 1.0 + 0.5 * (SM_GHash(c + 7.7) - 0.5);
                float dist = length(d);
                float disc = 1.0 - smoothstep(radius * 0.72, radius, dist);
                // Crescent faces the Mars key (sun yaw 148 → light travels +X/-Z on the ground).
                float lit = saturate(dot(normalize(d + 1e-4), float2(-0.53, 0.85)));
                rim = max(rim, disc * lit * (1.0 - smoothstep(radius * 0.25, radius * 0.8, dist)) * 0.5);
                coverage = max(coverage, disc);
            }
            return coverage;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GroundVertex
            #pragma fragment GroundFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
                float  fogCoord   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings GroundVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);

                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = nrm.normalWS;
                output.color = input.color;
                output.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 GroundFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float2 p = input.positionWS.xz;

                // Broad blotches stop the open ground reading as one flat colour.
                float macro = SM_Fbm(p / max(_MacroScale, 1.0));
                float speckle = SM_GNoise(p / max(_DetailScale, 0.05));

                // Vertex red channel is terrain exposure, written by TerrainMeshBuilder.
                float exposure = saturate(input.color.r);

                float3 albedo = lerp(_DarkColor.rgb, _BaseColor.rgb, saturate(exposure * 0.7 + macro * 0.6));
                albedo = lerp(albedo, albedo * 0.82, (1.0 - macro) * _MacroStrength);
                albedo *= 1.0 + (speckle - 0.5) * _DetailStrength;

                // Authored tile: grit / ripples. Multiplies the grade — does not replace body tint.
                float amount = saturate(_DetailTexAmount);
                if (amount > 0.001)
                {
                    float2 detailUV = p / max(_DetailTexScale, 0.25);
                    half3 detailAlb = SAMPLE_TEXTURE2D(_DetailAlbedo, sampler_DetailAlbedo, detailUV).rgb;
                    albedo *= lerp(1.0, detailAlb, amount);
                    half4 nS = SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, detailUV);
                    float3 nTS = UnpackNormal(nS);
                    normalWS = normalize(normalWS + float3(nTS.x, 0.0, nTS.y) * (amount * 0.65));
                }

                // Steep faces lose their dust cover and show rock.
                float slope = 1.0 - saturate(dot(normalWS, float3(0, 1, 0)));
                float rock = smoothstep(1.0 - _SlopeEnd, 1.0 - _SlopeStart, slope);
                albedo = lerp(albedo, _RockColor.rgb, rock);

                // Pebble field: dark stones with a lit crescent. Thinned out on the smooth low
                // ground (macro < 0.5) so packed dust stays readable next to gravelly patches.
                float pebbleRim;
                float pebble = SM_Pebbles(p, pebbleRim);
                pebble *= lerp(0.45, 1.0, saturate(macro * 1.6));
                albedo = lerp(albedo, _PebbleColor.rgb, pebble * 0.85);
                albedo += pebbleRim * pebble * 0.30 * _BaseColor.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogCoord;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness + rock * 0.06 + pebble * 0.05;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
