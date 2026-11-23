// Stylized cartoon flame for a single upright quad (Unity's built-in Quad mesh).
// Follows the "few readable shapes" approach of stylized fire (RiME-style, Sam Hedges' single-quad fire):
// one teardrop silhouette per card, eroded by scrolling noise, cut into three flat colour bands,
// animated on a quantised clock so it moves like a hand-drawn flipbook.
// The card turns around Y to face the camera (place it with its centre at half the flame height).
Shader "PotionShop/StylizedFlame"
{
    Properties
    {
        [HDR] _CoreColor ("Core", Color) = (2.2, 2.0, 1.4, 1)
        [HDR] _MidColor ("Middle", Color) = (2.0, 1.05, 0.35, 1)
        [HDR] _EdgeColor ("Edge", Color) = (1.6, 0.42, 0.18, 1)
        [HDR] _TipColor ("Tip", Color) = (1.3, 0.3, 0.45, 1)
        _Fps ("Flipbook frames per second", Float) = 12
        _Speed ("Rise speed", Float) = 2.2
        _Sway ("Sway", Range(0, 0.6)) = 0.28
        _Erosion ("Edge erosion", Range(0, 1)) = 0.45
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "StylizedFlame"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _MidColor, _EdgeColor, _TipColor;
                float _Fps, _Speed, _Sway, _Erosion, _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 center = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), 1);
                float3 toCamera = _WorldSpaceCameraPos - center;
                toCamera.y = 0;
                float3 right = normalize(cross(float3(0, 1, 0), toCamera + float3(1e-4, 0, 0)));
                float3 world = center + right * v.positionOS.x * scale.x + float3(0, 1, 0) * v.positionOS.y * scale.y;
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = floor(_Time.y * _Fps) / _Fps + _Seed * 17.0;
                float h = i.uv.y;

                // Sway the whole flame more towards the tip, then erode its edge with rising noise.
                float sway = (Noise(float2(t * 1.3, _Seed * 5.0)) - 0.5) * _Sway * h * h;
                float x = i.uv.x - 0.5 - sway;
                float rising = Noise(float2(i.uv.x * 3.1 + _Seed, h * 2.4 - t * _Speed));
                // Plump rounded body that tapers late into a licking tip; a second noise breaks the tip into tongues.
                float halfWidth = 0.47 * pow(saturate(1.0 - h), 0.55) * smoothstep(0.0, 0.3, h);
                float tongues = Noise(float2(i.uv.x * 6.0 - _Seed, h * 3.0 - t * _Speed * 1.4));
                float edge = abs(x) / max(halfWidth, 1e-3) + (rising - 0.5) * _Erosion * (0.35 + h) +
                             (tongues - 0.5) * _Erosion * h * h * 1.6;

                float alpha = 1.0 - smoothstep(0.9, 1.0, edge);
                clip(alpha - 0.01);

                // Three flat bands from the core outwards, warming to a pinkish tip near the top.
                float band = edge * 0.75 + h * 0.55 + (rising - 0.5) * 0.25;
                half3 color = band < 0.38 ? _CoreColor.rgb : band < 0.72 ? _MidColor.rgb : _EdgeColor.rgb;
                color = lerp(color, _TipColor.rgb, step(0.78, h + (rising - 0.5) * 0.2) * 0.6);
                color = MixFog(color, i.fog);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
