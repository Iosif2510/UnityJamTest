Shader "UI/Prescription Effects"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [Toggle(PRESCRIPTION_GLOW)] _Glow ("Selection Glow", Float) = 0
        _GlowColor ("Glow Color", Color) = (1, 0.59, 0.16, 1)
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ PRESCRIPTION_GLOW
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;
            half4 _GlowColor;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.localPosition = input.vertex.xy;
                output.color = input.color;
                return output;
            }

            half SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            half RingAlpha(float2 uv, float2 stepUV)
            {
                float2 x = float2(stepUV.x, 0);
                float2 y = float2(0, stepUV.y);
                float2 diagonal = stepUV * 0.707107;
                float2 otherDiagonal = float2(diagonal.x, -diagonal.y);
                return (SampleAlpha(uv + x) + SampleAlpha(uv - x) +
                    SampleAlpha(uv + y) + SampleAlpha(uv - y) +
                    SampleAlpha(uv + diagonal) + SampleAlpha(uv - diagonal) +
                    SampleAlpha(uv + otherDiagonal) + SampleAlpha(uv - otherDiagonal)) * 0.125;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                #ifdef PRESCRIPTION_GLOW
                    float2 texel = _MainTex_TexelSize.xy;
                    // Match the 16 source pixels of padding on each edge of the glow quad.
                    float2 uv = input.uv + (input.uv * 2.0 - 1.0) * texel * 16.0;
                    half glow = RingAlpha(uv, texel * 5.0) * 1.15 +
                        RingAlpha(uv, texel * 10.0) * 0.50 + RingAlpha(uv, texel * 15.0) * 0.20;
                    half4 color = half4(_GlowColor.rgb, saturate(glow) * (1.0 - SampleAlpha(uv)) * _GlowColor.a);
                #else
                    half4 color = tex2D(_MainTex, input.uv);
                    half gray = dot(color.rgb, half3(0.2126, 0.7152, 0.0722));
                    color.rgb = gray;
                #endif
                color *= input.color;
                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(input.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
