Shader "Lantern/Amber Cast Glass"
{
    Properties
    {
        [NoScaleOffset] _TintMap ("Baked amber tint", 2D) = "white" {}
        [Normal] [NoScaleOffset] _NormalMap ("Cast glass normal", 2D) = "bump" {}
        [NoScaleOffset] _SmoothnessMap ("Baked smoothness (alpha)", 2D) = "white" {}
        _GlassTint ("Amber color", Color) = (0.74, 0.57, 0.33, 1)
        _EdgeTint ("Amber edge color", Color) = (0.63, 0.43, 0.20, 1)
        _FaceOpacity ("Face opacity", Range(0, 1)) = 0.33
        _EdgeOpacity ("Edge opacity", Range(0, 1)) = 0.72
        _FresnelPower ("Edge falloff", Range(1, 9)) = 3
        _Gloss ("Reflection strength", Range(0, 1)) = 0.76
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "AMBER_GLASS"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _TintMap, _NormalMap, _SmoothnessMap;
            float4 _GlassTint, _EdgeTint;
            float _FaceOpacity, _EdgeOpacity, _FresnelPower, _Gloss;

            struct MeshVertex
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct GlassFragment
            {
                float4 clipPosition : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float4 worldTangent : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            GlassFragment vert(MeshVertex vertex)
            {
                GlassFragment output;
                output.clipPosition = UnityObjectToClipPos(vertex.position);
                output.worldPosition = mul(unity_ObjectToWorld, vertex.position).xyz;
                output.worldNormal = UnityObjectToWorldNormal(vertex.normal);
                output.worldTangent = float4(UnityObjectToWorldDir(vertex.tangent.xyz),
                                             vertex.tangent.w * unity_WorldTransformParams.w);
                output.uv = vertex.uv;
                return output;
            }

            float4 frag(GlassFragment input) : SV_Target
            {
                float3 surfaceNormal = normalize(input.worldNormal);
                float3 tangent = normalize(input.worldTangent.xyz);
                float3 bitangent = cross(surfaceNormal, tangent) * input.worldTangent.w;
                float3 normalMap = UnpackNormal(tex2D(_NormalMap, input.uv));
                float3 normal = normalize(tangent * normalMap.x + bitangent * normalMap.y + surfaceNormal * normalMap.z);
                float3 toCamera = normalize(UnityWorldSpaceViewDir(input.worldPosition));
                float edge = pow(1 - saturate(dot(normal, toCamera)), _FresnelPower);

                float exponent = lerp(24, 68, tex2D(_SmoothnessMap, input.uv).a);
                float3 reflection = reflect(-toCamera, normal);
                float3 viewReflection = normalize(mul((float3x3)UNITY_MATRIX_V, reflection));
                float key = saturate(dot(viewReflection, normalize(float3(-0.60, 0.50, 0.62))));
                float fill = saturate(dot(viewReflection, normalize(float3(0.71, 0.30, 0.64))));
                float highlight = _Gloss * (0.32 * pow(key, exponent) + 0.22 * pow(fill, exponent + 18));

                float3 amber = tex2D(_TintMap, input.uv).rgb * _GlassTint.rgb;
                float3 tint = lerp(amber, _EdgeTint.rgb, edge * 0.65);
                float opacity = saturate(lerp(_FaceOpacity, _EdgeOpacity, edge) + highlight * 0.32);
                return float4(tint * opacity + highlight * float3(1.0, 0.90, 0.74), opacity);
            }
            ENDCG
        }
    }
    Fallback Off
}
