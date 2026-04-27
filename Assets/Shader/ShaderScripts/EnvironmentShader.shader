Shader "FG/ToonEnvironment"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)

        _ShadowTint("Shadow Tint", Color) = (0.75,0.75,0.8,1)
        _LightTint("Light Tint", Color) = (1,1,1,1)

        _RampThreshold("Light Threshold", Range(0,1)) = 0.5
        _RampSmoothness("Edge Softness", Range(0.001,0.3)) = 0.05

        [Toggle]_AlphaClip("Use Alpha Cutout", Float) = 1
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5

        [Toggle]_DoubleSided("Double Sided (Foliage)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
        }

        LOD 100

        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode"="ForwardBase" }

            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            float4 _BaseColor;

            float4 _ShadowTint;
            float4 _LightTint;

            float _RampThreshold;
            float _RampSmoothness;

            float _AlphaClip;
            float _Cutoff;

            float _DoubleSided;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 posWS    : TEXCOORD2;

                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _BaseMap);

                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWS = UnityObjectToWorldNormal(v.normal);

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_BaseMap, i.uv);

                // Alpha Cutout
                if (_AlphaClip > 0.5)
                    clip(tex.a * _BaseColor.a - _Cutoff);

                half3 albedo = tex.rgb * _BaseColor.rgb;

                half3 N = normalize(i.normalWS);
                half3 V = normalize(_WorldSpaceCameraPos.xyz - i.posWS);

                // Double-sided fix for foliage
                if (_DoubleSided > 0.5)
                    N = (dot(N, V) < 0) ? -N : N;

                half3 L = normalize(_WorldSpaceLightPos0.xyz);

                half ndotl = saturate(dot(N, L));

                // Simple toon ramp (2-band)
                half ramp = smoothstep(
                    _RampThreshold - _RampSmoothness,
                    _RampThreshold + _RampSmoothness,
                    ndotl
                );

                half3 lightCol  = albedo * _LightColor0.rgb * _LightTint.rgb;
                half3 shadowCol = albedo * _ShadowTint.rgb;

                half3 color = lerp(shadowCol, lightCol, ramp);

                fixed4 outCol = fixed4(color, 1);

                UNITY_APPLY_FOG(i.fogCoord, outCol);
                return outCol;
            }
            ENDCG
        }

        // Shadow pass (kept for proper shadows)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            CGPROGRAM
            #pragma vertex vertSC
            #pragma fragment fragSC
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            float4 _BaseColor;

            float _AlphaClip;
            float _Cutoff;

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2f vertSC(appdata_base v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 fragSC(v2f i) : SV_Target
            {
                if (_AlphaClip > 0.5)
                {
                    fixed4 tex = tex2D(_BaseMap, i.uv);
                    clip(tex.a * _BaseColor.a - _Cutoff);
                }

                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}