Shader "Custom/SpiderVerse_BuiltIn_Simple_AlphaClip_Outline"
{
    Properties
    {
        _BaseMap("Base Map (RGB=Color, A=Cutout)", 2D) = "white" {}
        _BaseColor("Base Color Tint", Color) = (1,1,1,1)

        // Toon ramp (YOUR DEFAULTS)
        _RampThreshold("Ramp Threshold", Range(0,1)) = 0
        _RampSmoothness("Ramp Smoothness", Range(0.001,0.5)) = 0.102
        _ShadowStrength("Shadow Strength", Range(0,1)) = 0.56

        // Banding (YOUR DEFAULTS)
        _ShadowBands("Shadow Bands", Range(1,6)) = 1
        _BandStrength("Band Strength", Range(0,1)) = 0.96

        // Tints (YOUR DEFAULTS)
        _LightTint("Light Tint", Color) = (1,0.85,0.85,1)
        _ShadowTint("Shadow Tint", Color) = (0.85,0.72,0.60,1)

        // Rim (YOUR DEFAULTS)
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimPower("Rim Power", Range(0.5,12)) = 12
        _RimStrength("Rim Strength", Range(0,2)) = 0.2

        // Posterize (YOUR DEFAULTS)
        _PosterizeSteps("Posterize Steps", Range(2,12)) = 2
        _PosterizeStrength("Posterize Strength", Range(0,1)) = 0

        // Ambient (YOUR DEFAULTS)
        _AmbientBoost("Ambient Boost", Range(0,2)) = 2

        // Cutout (YOUR DEFAULTS)
        [Toggle]_AlphaClip("Alpha Clip (Cutout)", Float) = 1
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.71

        // Foliage (YOUR DEFAULTS)
        [Toggle]_DoubleSided("Double Sided (Foliage)", Float) = 1

        // Outline (YOUR DEFAULTS)
        [Toggle]_Outline("Enable Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width (Object)", Range(0,0.08)) = 0.015
        _OutlineDepthOffset("Outline Depth Offset", Range(0,0.02)) = 0
        _OutlineEdgeSoftness("Outline Edge Softness", Range(0.05,0.5)) = 0.05
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

        // Outline pass
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
            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;

            float4 _BaseColor;
            float  _AlphaClip;
            float  _Cutoff;

            float  _Outline;
            float4 _OutlineColor;
            float  _OutlineWidth;
            float  _OutlineDepthOffset;
            float  _OutlineEdgeSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vertOL(appdata v)
            {
                v2f o;
                float w = (_Outline > 0.5) ? _OutlineWidth : 0.0;

                float3 posOS = v.vertex.xyz + v.normal * w;
                float4 clipPos = UnityObjectToClipPos(float4(posOS, 1.0));
                clipPos.z -= _OutlineDepthOffset * clipPos.w;

                o.pos = clipPos;
                o.uv  = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            fixed4 fragOL(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_BaseMap, i.uv);
                half a = tex.a * _BaseColor.a;

                if (_AlphaClip > 0.5)
                    clip(a - _Cutoff);

                half softness = max(0.001h, (half)_OutlineEdgeSoftness);
                half edge = saturate((a - _Cutoff) / softness);
                half edgeMask = 1.0h - edge;

                return fixed4(_OutlineColor.rgb, 1) * edgeMask;
            }
            ENDCG
        }

        // Forward lighting pass (unchanged logic)
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
            #include "AutoLight.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;

            float4 _BaseColor;

            float  _RampThreshold;
            float  _RampSmoothness;
            float  _ShadowStrength;
            float  _ShadowBands;
            float  _BandStrength;

            float4 _LightTint;
            float4 _ShadowTint;

            float4 _RimColor;
            float  _RimPower;
            float  _RimStrength;

            float  _PosterizeSteps;
            float  _PosterizeStrength;

            float  _AmbientBoost;

            float  _AlphaClip;
            float  _Cutoff;

            float  _DoubleSided;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float3 normalWS  : TEXCOORD1;
                float3 posWS     : TEXCOORD2;

                UNITY_FOG_COORDS(3)
                SHADOW_COORDS(4)
            };

            half3 Posterize(half3 c, half steps)
            {
                steps = max(2.0h, steps);
                return floor(c * steps) / steps;
            }

            v2f vert(appdata v)
            {
                v2f o;
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
                fixed4 tex = tex2D(_BaseMap, i.uv);

                if (_AlphaClip > 0.5)
                    clip(tex.a * _BaseColor.a - _Cutoff);

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
                half rampSoft = smoothstep(t - sm, t + sm, litInput);

                half bands = max(1.0h, (half)_ShadowBands);
                half rampBanded = floor(rampSoft * bands) / bands;
                half ramp = lerp(rampSoft, rampBanded, saturate((half)_BandStrength));

                half3 lightCol  = albedo * _LightColor0.rgb * _LightTint.rgb;
                half3 shadowCol = albedo * _ShadowTint.rgb;
                half3 baseCol   = lerp(shadowCol, lightCol, ramp);

                half rim = pow(1.0h - saturate(dot(N, V)), (half)_RimPower) * (half)_RimStrength;
                half3 rimCol = rim * _RimColor.rgb;

                half3 sh = ShadeSH9(half4(N, 1.0));
                half3 ambient = albedo * sh * (half)_AmbientBoost;

                half3 color = baseCol + ambient + rimCol;

                if (_PosterizeStrength > 0.001)
                {
                    half3 post = Posterize(color, (half)_PosterizeSteps);
                    color = lerp(color, post, saturate((half)_PosterizeStrength));
                }

                fixed4 outCol = fixed4(color, tex.a * _BaseColor.a);
                UNITY_APPLY_FOG(i.fogCoord, outCol);
                return outCol;
            }
            ENDCG
        }

        // Shadow caster pass
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
            float  _AlphaClip;
            float  _Cutoff;

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

    FallBack Off
}