// Additive round glow with no texture: a soft radial falloff tinted by the material and vertex colour.
// Used for the warm pool of light on the floor under the cauldron and for rising embers (particle vertex colour).
Shader "PotionShop/SoftGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.6, 0.7, 0.25, 1)
        _Softness ("Softness", Range(0.5, 4)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SoftGlow"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = saturate(1.0 - length(i.uv - 0.5) * 2.0);
                float a = pow(d, _Softness) * _Color.a * i.color.a;
                return half4(_Color.rgb * i.color.rgb, a);
            }
            ENDHLSL
        }
    }
}
