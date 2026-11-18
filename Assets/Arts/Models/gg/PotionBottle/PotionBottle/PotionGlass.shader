Shader "Potion Bottle/Transparent Violet Glass"
{
    Properties
    {
        _GlassTint ("Clear glass tint", Color) = (0.93, 0.91, 1.0, 1)
        _EdgeTint ("Violet rim color", Color) = (0.77, 0.75, 0.93, 1)
        _FaceOpacity ("Face opacity", Range(0, 1)) = 0.075
        _EdgeOpacity ("Rim opacity", Range(0, 1)) = 0.52
        _FresnelPower ("Edge falloff", Range(1, 9)) = 3.45
        _Gloss ("Highlight strength", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "TRANSPARENT_GLASS"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _GlassTint, _EdgeTint;
            float _FaceOpacity, _EdgeOpacity, _FresnelPower, _Gloss;

            struct MeshInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct ScreenInput
            {
                float4 clipPosition : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            ScreenInput vert(MeshInput vertex)
            {
                ScreenInput output;
                output.clipPosition = UnityObjectToClipPos(vertex.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, vertex.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(vertex.normal);
                return output;
            }

            float4 frag(ScreenInput input) : SV_Target
            {
                float3 normal = normalize(input.worldNormal);
                float3 toCamera = normalize(_WorldSpaceCameraPos - input.worldPosition);
                float facing = saturate(dot(normal, toCamera));
                float edge = pow(1 - facing, _FresnelPower);

                float3 reflection = reflect(-toCamera, normal);
                float3 viewReflection = normalize(mul((float3x3)UNITY_MATRIX_V, reflection));
                float key = saturate(dot(viewReflection, normalize(float3(-0.59, 0.53, 0.6))));
                float fill = saturate(dot(viewReflection, normalize(float3(0.72, 0.37, 0.58))));
                float reflectedLight = _Gloss * (0.34 * pow(key, 36) + 0.30 * pow(fill, 70));
                float opacity = saturate(lerp(_FaceOpacity, _EdgeOpacity, edge) + reflectedLight);
                float3 glassColor = lerp(_GlassTint.rgb, _EdgeTint.rgb, edge);
                float3 surface = glassColor * opacity + reflectedLight * float3(0.84, 0.85, 1.0) * 0.32;
                return float4(surface, opacity);
            }
            ENDCG
        }
    }
    Fallback Off
}

