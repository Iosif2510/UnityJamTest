Shader "UI/Hue Shift"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _HueShift ("Hue Shift (Degrees)", Range(-180, 180)) = 0

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
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

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
            Name "Default"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                #ifdef UNITY_UI_CLIP_RECT
                    float4 mask : TEXCOORD1;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _HueShift;
            CBUFFER_END

            float4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                    if (_UIVertexColorAlwaysGammaSpace)
                        output.color.rgb = SRGBToLinear(output.color.rgb);
                #endif

                #ifdef UNITY_UI_CLIP_RECT
                    float2 pixelSize = output.positionCS.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                    float4 rect = clamp(_ClipRect, -2e10, 2e10);
                    float2 softness = float2(_UIMaskSoftnessX, _UIMaskSoftnessY);
                    output.mask = float4(input.positionOS.xy * 2.0 - rect.xy - rect.zw,
                        0.25 / (0.25 * softness + abs(pixelSize)));
                #endif

                return output;
            }

            float3 ShiftHue(float3 rgb)
            {
                float hueOffset = frac(_HueShift / 360.0);
                if (hueOffset == 0.0)
                    return rgb;

                // Use display-space HSV for consistent hue angles in Gamma and Linear projects.
                #ifndef UNITY_COLORSPACE_GAMMA
                    rgb = LinearToSRGB(rgb);
                #endif

                float3 hsv = RgbToHsv(rgb);
                hsv.x = frac(hsv.x + hueOffset);
                rgb = HsvToRgb(hsv);

                #ifndef UNITY_COLORSPACE_GAMMA
                    rgb = SRGBToLinear(rgb);
                #endif
                return rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) + _TextureSampleAdd;
                color.rgb = ShiftHue(color.rgb);
                input.color.a = round(input.color.a * 255.0) / 255.0;
                color *= input.color;

                #ifdef UNITY_UI_CLIP_RECT
                    float2 softness = float2(_UIMaskSoftnessX, _UIMaskSoftnessY);
                    float2 halfPixelBias = 0.5 - 0.5 * softness * input.mask.zw;
                    float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw + halfPixelBias);
                    float2 hardThreshold = 0.5 + 1e-3 * step(0.0, input.mask.xy);
                    mask = lerp(mask, step(hardThreshold, mask), step(softness, 0.0));
                    color.a *= mask.x * mask.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif

                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
}
