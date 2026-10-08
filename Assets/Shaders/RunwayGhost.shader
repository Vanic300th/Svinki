Shader "Svinki/Runway Ghost"
{
    Properties
    {
        [MainColor] _BaseColor("Skin", Color) = (.81,.365,.32,1)
        _PatternColor("Markings", Color) = (.085,.037,.024,1)
        _Pattern("Pattern", Float) = 0
        _Tattoo("Tattoo", Float) = 0
        _Smoothness("Smoothness", Range(0,1)) = .25
        _GhostTint("Ghost glow", Color) = (.48,.86,1,1)
        _GhostOpacity("Ghost opacity", Range(0,1)) = .48
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        HLSLINCLUDE
        #define _SURFACE_TYPE_TRANSPARENT 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _PatternColor;
            half _Pattern;
            half _Tattoo;
            half _Smoothness;
            half4 _GhostTint;
            half _GhostOpacity;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            // Bind-pose coordinates survive skinning, so markings never slide on an animated pig.
            float3 restPosition : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float3 restPosition : TEXCOORD2;
            half fog : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings PigVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.restPosition = input.restPosition;
            output.fog = ComputeFogFactor(position.positionCS.z);
            return output;
        }
        half Markings(float3 p)
        {
            if (_Pattern < .5) return 0;
            if (_Pattern < 1.5)
            {
                float3 q = p * 8;
                float3 cell = floor(q);
                float3 center = .5 + .14 * sin(dot(cell, float3(12.3, 7.1, 19.7)) + float3(0, 2, 4));
                float distance = length(frac(q) - center);
                float edge = max(fwidth(distance), .018);
                return 1 - smoothstep(.29 - edge, .29 + edge, distance);
            }
            if (_Pattern < 2.5)
            {
                float stripe = sin(p.y * 32 + p.x * 9 + p.z * 5);
                float edge = max(fwidth(stripe), .04);
                return smoothstep(.15 - edge, .15 + edge, stripe);
            }
            if (_Pattern < 3.5)
            {
                float saddle = length(float2(p.x / .46, (p.y - .91) / .35));
                return (1 - smoothstep(.9, 1.03, saddle)) * (1 - smoothstep(-.02, .11, p.z));
            }
            return 1 - smoothstep(.38, .44, p.y);
        }
        float TriangleDistance(float2 q, float2 a, float2 b, float2 c)
        {
            float2 ab=b-a, bc=c-b, ca=a-c;
            float2 pa=q-a, pb=q-b, pc=q-c;
            float2 ea=pa-ab*saturate(dot(pa,ab)/dot(ab,ab));
            float2 eb=pb-bc*saturate(dot(pb,bc)/dot(bc,bc));
            float2 ec=pc-ca*saturate(dot(pc,ca)/dot(ca,ca));
            float s1=ab.x*pa.y-ab.y*pa.x, s2=bc.x*pb.y-bc.y*pb.x, s3=ca.x*pc.y-ca.y*pc.x;
            float inside=(min(s1,min(s2,s3))>=0 || max(s1,max(s2,s3))<=0) ? -1 : 1;
            return sqrt(min(dot(ea,ea),min(dot(eb,eb),dot(ec,ec))))*inside;
        }
        half Tattoo(float3 p)
        {
            if (_Tattoo < .5) return 0;
            float2 q = float2(p.x, p.y - .88) / .16;
            float d=1;
            if (_Tattoo < 1.5)
            {
                float lobes=min(length(q-float2(-.32,.26)),length(q-float2(.32,.26)))-.44;
                float tip=TriangleDistance(q,float2(-.72,.25),float2(.72,.25),float2(0,-.78));
                d=min(lobes,tip);
            }
            else if (_Tattoo < 2.5)
            {
                float sector=1.256637;
                float angle=abs(frac(atan2(q.x,q.y)/sector+.5)-.5)*sector;
                float2 folded=float2(sin(angle),cos(angle))*length(q);
                float2 normal=normalize(float2(.9-.36*cos(.6283185),.36*sin(.6283185)));
                d=dot(folded,normal)-.9*normal.y;
            }
            else if (_Tattoo < 3.5)
            {
                float upper=TriangleDistance(q,float2(-.15,.95),float2(.45,.95),float2(-.33,-.24));
                float lower=TriangleDistance(q,float2(-.33,-.12),float2(.33,.14),float2(-.42,-.95));
                d=min(upper,lower);
            }
            else
            {
                float bands=abs(sin((p.y-.93)*85));
                return smoothstep(.355,.38,abs(p.x)) * (1-smoothstep(.58,.61,abs(p.x))) *
                    smoothstep(.90,.915,p.y) * (1-smoothstep(1.035,1.05,p.y)) * smoothstep(.52,.65,bands);
            }
            float edge=max(fwidth(d),.012);
            return (1-smoothstep(-edge,edge,d)) * smoothstep(.10,.18,p.z);
        }
        half4 PigFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            InputData lighting = (InputData)0;
            lighting.positionWS = input.positionWS;
            lighting.positionCS = input.positionCS;
            lighting.normalWS = NormalizeNormalPerPixel(input.normalWS);
            lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            lighting.bakedGI = SampleSH(lighting.normalWS);
            lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            lighting.shadowMask = half4(1,1,1,1);
            lighting.vertexLighting = VertexLighting(input.positionWS, lighting.normalWS);
            SurfaceData surface = (SurfaceData)0;
            surface.albedo = lerp(_BaseColor.rgb, _PatternColor.rgb, Markings(input.restPosition));
            surface.albedo = lerp(surface.albedo, half3(.015,.024,.032), Tattoo(input.restPosition));
            half rim = pow(1 - saturate(dot(lighting.normalWS, lighting.viewDirectionWS)), 2);
            surface.albedo = lerp(surface.albedo, _GhostTint.rgb, .38);
            surface.emission = _GhostTint.rgb * (.10 + rim * .65);
            surface.alpha = saturate(_GhostOpacity + rim * .23);
            surface.smoothness = _Smoothness;
            surface.normalTS = half3(0,0,1);
            surface.occlusion = 1;
            half4 color = UniversalFragmentPBR(lighting, surface);
            color.rgb = MixFog(color.rgb, input.fog);
            color.a = surface.alpha;
            return color;
        }
        half4 DepthFragment(Varyings input) : SV_Target { return 0; }
        half4 NormalsFragment(Varyings input) : SV_Target { return half4(NormalizeNormalPerPixel(input.normalWS), 0); }
        float3 _LightDirection;
        float3 _LightPosition;
        Varyings ShadowVertex(Attributes input)
        {
            Varyings output = PigVertex(input);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction = normalize(_LightPosition - output.positionWS);
            #else
                float3 direction = _LightDirection;
            #endif
            output.positionCS = ApplyShadowClamping(TransformWorldToHClip(
                ApplyShadowBias(output.positionWS, output.normalWS, direction)));
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PigVertex
            #pragma fragment PigFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            ENDHLSL
        }
    }
}
