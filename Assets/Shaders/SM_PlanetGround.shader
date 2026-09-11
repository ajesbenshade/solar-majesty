// Procedural planetary ground + optional authored detail tiles.
//
// Blends a low-lying dust colour with an exposed rock colour by slope and height, then adds a
// speckle and a broad mottle so the surface does not read as one flat tint at isometric distance.
// World-space UVs keep the grade seamless. Terrain Data Baker Sand/Grass/Snow tiles
// weight the splat mix (Mars remaps Grass → rock grit; never a green lawn). Authored
// SM_Ground_* tiles add extra grit without replacing the body color lock (white hulls
// must stay white against Mars dirt).
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

        _SlopeStart("Rock Slope Start", Range(0,1)) = 0.55
        _SlopeEnd("Rock Slope End", Range(0,1)) = 0.88

        [Header(Terrain Data Baker Maps)]
        [NoScaleOffset] _HeightMap("Height Map", 2D) = "gray" {}
        [NoScaleOffset] _NormalMap("World Normal (TDB)", 2D) = "bump" {}
        [NoScaleOffset] _SplatMap("Splat Map", 2D) = "red" {}
        [NoScaleOffset] _MaskMap("Mask Map", 2D) = "white" {}
        _BakeSize("Bake Size (m)", Vector) = (384, 384, 0, 0)
        _BakeNormalAmount("Bake Normal Amount", Range(0,1)) = 0
        _SplatAmount("Splat Amount", Range(0,1)) = 0
        _GrassColor("Grass / Dune Color", Color) = (0.22, 0.42, 0.16, 1)
        _WetColor("Wet / Floor Color", Color) = (0.18, 0.22, 0.16, 1)

        [Header(Splat Albedo Tiles)]
        [NoScaleOffset] _SplatAlbedo0("Splat Albedo 0 (Sand / Dust)", 2D) = "white" {}
        [NoScaleOffset] _SplatAlbedo1("Splat Albedo 1 (Grass / Rock)", 2D) = "white" {}
        [NoScaleOffset] _SplatAlbedo2("Splat Albedo 2 (Snow / High)", 2D) = "white" {}
        [NoScaleOffset] _SplatAlbedo3("Splat Albedo 3 (Wet / Floor)", 2D) = "white" {}
        _SplatTexScale("Splat Tex Scale (m)", Range(1, 48)) = 8
        _SplatAlbedoAmount("Splat Albedo Amount", Range(0,1)) = 0
        _SplatDesat("Splat Desat (Sand/Grass/Snow/Wet)", Vector) = (0, 0, 0, 0)

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
            float  _SlopeStart;
            float  _SlopeEnd;
            float4 _BakeSize;
            float  _BakeNormalAmount;
            float  _SplatAmount;
            float4 _GrassColor;
            float4 _WetColor;
            float4 _SplatAlbedo0_ST;
            float4 _SplatAlbedo1_ST;
            float4 _SplatAlbedo2_ST;
            float4 _SplatAlbedo3_ST;
            float  _SplatTexScale;
            float  _SplatAlbedoAmount;
            float4 _SplatDesat;
            float  _Smoothness;
            float  _Metallic;
        CBUFFER_END

        TEXTURE2D(_DetailAlbedo);
        SAMPLER(sampler_DetailAlbedo);
        TEXTURE2D(_DetailNormal);
        SAMPLER(sampler_DetailNormal);
        TEXTURE2D(_HeightMap);
        SAMPLER(sampler_HeightMap);
        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);
        TEXTURE2D(_SplatMap);
        SAMPLER(sampler_SplatMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);
        TEXTURE2D(_SplatAlbedo0);
        SAMPLER(sampler_SplatAlbedo0);
        TEXTURE2D(_SplatAlbedo1);
        TEXTURE2D(_SplatAlbedo2);
        TEXTURE2D(_SplatAlbedo3);

        // Earth keeps authored chroma (desat 0). Mars desaturates then colorizes so
        // TDB Grass never reads as a lawn and Sand tufts do not stay olive.
        half3 SM_SplatLayer(half3 texRgb, half3 tint, float desat)
        {
            float d = saturate(desat);
            half luma = dot(texRgb, half3(0.299, 0.587, 0.114));
            half3 dehued = lerp(texRgb, luma.xxx, d);
            // Keep luma variation; do not lift mean above tint (r15 TDB sand went peach).
            half3 colorized = tint * (0.22 + luma * 0.78);
            return lerp(dehued, colorized, d);
        }

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

            // URP's ComputeFogFactor(positionCS.z) remaps clip-z assuming a perspective projection
            // and collapses to ~0 under orthographic cameras (the ortho-10 Game tab), so
            // RenderSettings fog never reached the far ground in play stills. Linear view depth
            // gives the same haze read in perspective editor stills and the ortho Game tab.
            float SM_FogCoord(float3 positionWS)
            {
                float viewZ = -TransformWorldToView(positionWS).z;
                return ComputeFogFactorZ0ToFar(viewZ);
            }

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
                return output;
            }

            half4 GroundFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float2 p = input.positionWS.xz;
                float2 bakeUV = p / max(_BakeSize.xy, float2(1, 1));
                bakeUV = saturate(bakeUV);

                float splatAmt = saturate(_SplatAmount);
                if (splatAmt > 0.001)
                {
                    // TDB world-space packing: R = nx*0.5+0.5, G = 1, B = nz*0.5+0.5.
                    half3 packedN = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, bakeUV).rgb;
                    float3 bakeN;
                    bakeN.x = packedN.r * 2.0 - 1.0;
                    bakeN.z = packedN.b * 2.0 - 1.0;
                    bakeN.y = sqrt(saturate(1.0 - bakeN.x * bakeN.x - bakeN.z * bakeN.z));
                    normalWS = normalize(lerp(normalWS, bakeN, saturate(_BakeNormalAmount)));
                }

                // Broad blotches stop the open ground reading as one flat colour.
                float macro = SM_Fbm(p / max(_MacroScale, 1.0));
                float speckle = SM_GNoise(p / max(_DetailScale, 0.05));

                // Vertex red channel is terrain exposure, written by TerrainMeshBuilder.
                float exposure = saturate(input.color.r);

                float3 albedo = lerp(_DarkColor.rgb, _BaseColor.rgb, saturate(exposure * 0.7 + macro * 0.6));
                albedo = lerp(albedo, albedo * 0.82, (1.0 - macro) * _MacroStrength);
                albedo *= 1.0 + (speckle - 0.5) * _DetailStrength;

                if (splatAmt > 0.001)
                {
                    float4 splat = SAMPLE_TEXTURE2D(_SplatMap, sampler_SplatMap, bakeUV);
                    float3 splatSolid =
                        _BaseColor.rgb * splat.r +
                        _RockColor.rgb * splat.g +
                        _GrassColor.rgb * splat.b +
                        _WetColor.rgb * splat.a;

                    float texAmt = saturate(_SplatAlbedoAmount);
                    float3 splatAlb = splatSolid;
                    if (texAmt > 0.001)
                    {
                        float2 splatUV = p / max(_SplatTexScale, 0.25);
                        half3 l0 = SM_SplatLayer(
                            SAMPLE_TEXTURE2D(_SplatAlbedo0, sampler_SplatAlbedo0, splatUV).rgb,
                            _BaseColor.rgb, _SplatDesat.x);
                        half3 l1 = SM_SplatLayer(
                            SAMPLE_TEXTURE2D(_SplatAlbedo1, sampler_SplatAlbedo0, splatUV).rgb,
                            _RockColor.rgb, _SplatDesat.y);
                        half3 l2 = SM_SplatLayer(
                            SAMPLE_TEXTURE2D(_SplatAlbedo2, sampler_SplatAlbedo0, splatUV).rgb,
                            _GrassColor.rgb, _SplatDesat.z);
                        half3 l3 = SM_SplatLayer(
                            SAMPLE_TEXTURE2D(_SplatAlbedo3, sampler_SplatAlbedo0, splatUV).rgb,
                            _WetColor.rgb, _SplatDesat.w);
                        float3 splatTex = l0 * splat.r + l1 * splat.g + l2 * splat.b + l3 * splat.a;
                        splatAlb = lerp(splatSolid, splatTex, texAmt);
                    }

                    albedo = lerp(albedo, splatAlb, splatAmt);
                    half ao = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, bakeUV).r;
                    albedo *= lerp(1.0, ao, 0.45);
                }

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
                albedo = lerp(albedo, _RockColor.rgb, rock * (1.0 - splatAmt * 0.7));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = SM_FogCoord(input.positionWS);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness + rock * 0.06;
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
