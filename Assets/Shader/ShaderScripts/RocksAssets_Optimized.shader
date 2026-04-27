Shader "RocksAssets_Optimized"
{
    Properties
    {
        _BaseMap("Base Map (RGB=Color)", 2D) = "white" {}
        _BaseColor("Base Color Tint", Color) = (1,1,1,1)

        // Toon ramp
        _RampThreshold("Ramp Threshold", Range(0,1)) = 0.6
        _RampSmoothness("Ramp Smoothness", Range(0.001,0.5)) = 0.2
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1.0

        // Shadow bands
        _ShadowBands("Shadow Bands", Range(1,6)) = 4
        _BandStrength("Band Strength", Range(0,1)) = 1

        _LightTint("Light Tint", Color) = (1,1,1,1)
        _ShadowTint("Shadow Tint", Color) = (0.90,0.86,1.00,1)

        // AO
        _AOStrength("AO Strength", Range(0,1)) = 0.3
        [Toggle]_AOInShadowsOnly("AO Mostly In Shadows", Float) = 1

        // Halftone dots
        _HalftoneColor("Halftone Ink Color", Color) = (0,0,0,1)
        _HalftoneScale("Halftone Scale", Range(10,400)) = 150
        _HalftoneStrength("Halftone Strength", Range(0,2)) = 0.35
        _HalftoneThreshold("Halftone Threshold", Range(0,1)) = 0.25

        // Outline
        [Toggle]_Outline("Enable Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width (Object)", Range(0,0.08)) = 0.006
    }

    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
            "RenderType"="Opaque"
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

            float _Outline;
            float4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vertOL(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;

                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float w = (_Outline > 0.5) ? _OutlineWidth : 0.0;
                float3 posOS = v.vertex.xyz + v.normal * w;

                o.pos = UnityObjectToClipPos(float4(posOS, 1.0));

                return o;
            }

            fixed4 fragOL(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

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
            Cull Back

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

            float _ShadowBands;
            float _BandStrength;

            float4 _LightTint;
            float4 _ShadowTint;

            float _AOStrength;
            float _AOInShadowsOnly;

            float4 _HalftoneColor;
            float _HalftoneScale;
            float _HalftoneStrength;
            float _HalftoneThreshold;

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
                float4 screenPos : TEXCOORD3;

                UNITY_FOG_COORDS(4)
                SHADOW_COORDS(5)

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            half Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half HalftoneDots(float2 coord, float scale)
            {
                float2 grid = coord * scale;
                float2 cell = floor(grid);
                float2 f = frac(grid) - 0.5;

                half j = Hash21(cell) - 0.5h;
                f += j * 0.18;

                float r = length(f);
                half dotMask = saturate(1.0h - (half)(r / 0.55));

                return dotMask;
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

                o.screenPos = ComputeScreenPos(o.pos);

                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_SHADOW(o);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                fixed4 tex = tex2D(_BaseMap, i.uv);

                half3 albedo = tex.rgb * _BaseColor.rgb;

                half3 N = normalize(i.normalWS);
                half3 L = normalize(_WorldSpaceLightPos0.xyz);

                half ndotl = saturate(dot(N, L));

                half shadowAtten = SHADOW_ATTENUATION(i);
                shadowAtten = lerp(1.0h, shadowAtten, saturate((half)_ShadowStrength));

                half litInput = ndotl * shadowAtten;

                half t  = (half)_RampThreshold;
                half sm = (half)_RampSmoothness;

                half rampSoft = smoothstep(t - sm, t + sm, litInput);

                // Shadow bands
                half bands = max(1.0h, (half)_ShadowBands);
                half rampBanded = floor(rampSoft * bands) / bands;
                half ramp = lerp(rampSoft, rampBanded, saturate((half)_BandStrength));

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
                half3 baseCol   = lerp(shadowCol, lightCol, ramp);

                // Screen-space halftone
                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 1e-5);

                half shadowiness = 1.0h - ramp;

                half halftoneSoftness = 0.08h;
                half dotMask = HalftoneDots(screenUV, (half)_HalftoneScale);

                half dotDrive = smoothstep(
                    (half)_HalftoneThreshold - halftoneSoftness,
                    (half)_HalftoneThreshold + halftoneSoftness,
                    shadowiness
                );

                half dotsInk =
                    dotMask *
                    dotDrive *
                    (half)_HalftoneStrength *
                    shadowiness;

                baseCol = lerp(baseCol, _HalftoneColor.rgb, saturate(dotsInk));

                // Ambient from spherical harmonics
                half3 ambient = albedo * ShadeSH9(half4(N, 1.0));
                half3 color = baseCol + ambient;

                fixed4 outCol = fixed4(color, 1);

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
            Cull Back

            CGPROGRAM
            #pragma vertex vertSC
            #pragma fragment fragSC
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdataSC
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vertSC(appdataSC v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;

                UNITY_TRANSFER_INSTANCE_ID(v, o);

                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

                return o;
            }

            float4 fragSC(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                SHADOW_CASTER_FRAGMENT(i)
            }

            ENDCG
        }
    }

    FallBack Off
}