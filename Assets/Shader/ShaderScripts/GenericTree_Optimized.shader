Shader "GenericTree_Optimized"
{
    Properties
    {
        _BaseMap("Base Map (RGB=Color, A=Alpha Cutout)", 2D) = "white" {}
        _BaseColor("Base Color Tint", Color) = (1,1,1,1)

        // Toon ramp
        _RampThreshold("Ramp Threshold", Range(0,1)) = 0.6
        _RampSmoothness("Ramp Smoothness", Range(0.001,0.5)) = 0.2
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1.0

        _LightTint("Light Tint", Color) = (1,1,1,1)
        _ShadowTint("Shadow Tint", Color) = (0.90,0.86,1.00,1)

        // AO
        _AOStrength("AO Strength", Range(0,1)) = 0.3
        [Toggle]_AOInShadowsOnly("AO Mostly In Shadows", Float) = 1

        // Alpha Cutout
        [Toggle]_AlphaClip("Use Alpha Cutout", Float) = 1
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.45

        // Foliage
        [Toggle]_DoubleSided("Double Sided (Foliage)", Float) = 1

        // Outline
        [Toggle]_Outline("Enable Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width (Object)", Range(0,0.08)) = 0.006
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "IgnoreProjector"="True"
        }

        LOD 200

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="Always" }

            Cull Front
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vertOL
            #pragma fragment fragOL
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;

            float4 _BaseColor;
            float _AlphaClip;
            float _Cutoff;

            float _Outline;
            float4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float GetCutoutMask(fixed4 tex)
            {
                return tex.a * _BaseColor.a;
            }

            float GetCutoutThreshold()
            {
                return _Cutoff;
            }

            v2f vertOL(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;

                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float w = (_Outline > 0.5) ? _OutlineWidth : 0.0;
                float3 posOS = v.vertex.xyz + v.normal * w;

                o.pos = UnityObjectToClipPos(float4(posOS, 1.0));
                o.uv  = TRANSFORM_TEX(v.uv, _BaseMap);

                return o;
            }

            fixed4 fragOL(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                fixed4 tex = tex2D(_BaseMap, i.uv);

                if (_AlphaClip > 0.5)
                    clip(GetCutoutMask(tex) - GetCutoutThreshold());

                return fixed4(_OutlineColor.rgb, 1);
            }

            ENDCG
        }

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
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;

            float4 _BaseColor;

            float _RampThreshold;
            float _RampSmoothness;
            float _ShadowStrength;

            float4 _LightTint;
            float4 _ShadowTint;

            float _AOStrength;
            float _AOInShadowsOnly;

            float _AlphaClip;
            float _Cutoff;

            float _DoubleSided;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float3 normalWS  : TEXCOORD1;
                float3 posWS     : TEXCOORD2;

                UNITY_FOG_COORDS(3)
                SHADOW_COORDS(4)

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float GetCutoutMask(fixed4 tex)
            {
                return tex.a * _BaseColor.a;
            }

            float GetCutoutThreshold()
            {
                return _Cutoff;
            }

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;

                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _BaseMap);

                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWS = UnityObjectToWorldNormal(v.normal);

                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_SHADOW(o);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                fixed4 tex = tex2D(_BaseMap, i.uv);

                if (_AlphaClip > 0.5)
                    clip(GetCutoutMask(tex) - GetCutoutThreshold());

                half3 albedo = tex.rgb * _BaseColor.rgb;

                half3 N = normalize(i.normalWS);
                half3 V = normalize(_WorldSpaceCameraPos.xyz - i.posWS);

                if (_DoubleSided > 0.5)
                    N = (dot(N, V) < 0) ? -N : N;

                half3 L = normalize(_WorldSpaceLightPos0.xyz);
                half ndotl = saturate(dot(N, L));

                half shadowAtten = SHADOW_ATTENUATION(i);
                shadowAtten = lerp(1.0h, shadowAtten, saturate((half)_ShadowStrength));

                half litInput = ndotl * shadowAtten;

                half t  = (half)_RampThreshold;
                half sm = (half)_RampSmoothness;

                half ramp = smoothstep(t - sm, t + sm, litInput);

                // AO defaults to green channel
                half ao = tex.g;
                half aoMul = lerp(1.0h, ao, saturate((half)_AOStrength));

                if (_AOInShadowsOnly > 0.5)
                {
                    half aoShadow = lerp(1.0h, aoMul, 1.0h - ramp);
                    albedo *= aoShadow;
                }
                else
                {
                    albedo *= aoMul;
                }

                half3 lightCol  = albedo * _LightColor0.rgb * _LightTint.rgb;
                half3 shadowCol = albedo * _ShadowTint.rgb;
                half3 color     = lerp(shadowCol, lightCol, ramp);

                // Ambient from spherical harmonics
                half3 ambient = albedo * ShadeSH9(half4(N, 1.0));
                color += ambient;

                fixed4 outCol = fixed4(color, tex.a * _BaseColor.a);

                UNITY_APPLY_FOG(i.fogCoord, outCol);

                return outCol;
            }

            ENDCG
        }

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
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;

            float4 _BaseColor;
            float _AlphaClip;
            float _Cutoff;

            float GetCutoutMask(fixed4 tex)
            {
                return tex.a * _BaseColor.a;
            }

            float GetCutoutThreshold()
            {
                return _Cutoff;
            }

            struct appdataSC
            {
                float4 vertex   : POSITION;
                float3 normal   : NORMAL;
                float2 texcoord : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vertSC(appdataSC v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;

                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.uv = TRANSFORM_TEX(v.texcoord, _BaseMap);

                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

                return o;
            }

            float4 fragSC(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                if (_AlphaClip > 0.5)
                {
                    fixed4 tex = tex2D(_BaseMap, i.uv);
                    clip(GetCutoutMask(tex) - GetCutoutThreshold());
                }

                SHADOW_CASTER_FRAGMENT(i)
            }

            ENDCG
        }
    }

    FallBack Off
}