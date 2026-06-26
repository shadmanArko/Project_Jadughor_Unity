Shader "MineSystem/MineInteriorLightMask"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.06, 0.02, 0.13, 0.78)
        _Softness ("Softness", Range(0.01, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define MAX_MINE_LIGHTS 32

            fixed4 _TintColor;
            float _Softness;
            int _LightCount;
            float4 _LightData[MAX_MINE_LIGHTS];

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float reveal = 0;

                [loop]
                for (int index = 0; index < _LightCount; index++)
                {
                    float4 lightData = _LightData[index];
                    float radius = max(lightData.z, 0.001);
                    float intensity = saturate(lightData.w);
                    float distanceToLight =
                        distance(i.worldPos.xy, lightData.xy);
                    float innerRadius = radius * saturate(1 - _Softness);
                    float lightMask =
                        1 - smoothstep(innerRadius, radius, distanceToLight);
                    reveal = max(reveal, lightMask * intensity);
                }

                fixed4 color = _TintColor;
                color.a *= 1 - saturate(reveal);
                return color;
            }
            ENDCG
        }
    }
}
