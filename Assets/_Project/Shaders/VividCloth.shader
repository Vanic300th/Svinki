Shader "Svinki/Vivid Cloth"
{
    Properties
    {
        [MainTexture] _BaseMap("Fabric texture", 2D) = "white" {}
        [MainColor] _BaseColor("Fabric color", Color) = (1,1,1,1)
        _EmissionColor("Target highlight", Color) = (0,0,0,0)
        _VividGain("Brightness", Range(1,2)) = 1.5
        _Saturation("Saturation", Range(1,2)) = 1.3
        _Contrast("Contrast", Range(1,1.5)) = 1.1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "VividFabric"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor, _EmissionColor;
                half _VividGain, _Saturation, _Contrast;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half fog : TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Varyings output; VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS; output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS); output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(pos.positionCS.z); return output;
            }
            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 fabric = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half grey = dot(fabric, half3(.2126,.7152,.0722));
                fabric = max(0, (lerp(grey.xxx, fabric, _Saturation) - .18h) * _Contrast + .18h) * _VividGain;
                half3 normal = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                // A modest ambient floor preserves color in the intentionally dim store.
                half3 light = max(SampleSH(normal), half3(.24,.24,.24));
                Light main = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                light += main.color * saturate(dot(normal, main.direction)) * main.distanceAttenuation * main.shadowAttenuation;
                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0; i < count; i++)
                {
                    Light extra = GetAdditionalLight(i, input.positionWS);
                    light += extra.color * saturate(dot(normal, extra.direction)) * extra.distanceAttenuation * extra.shadowAttenuation;
                }
                #endif
                return half4(MixFog(fabric * light + _EmissionColor.rgb, input.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
