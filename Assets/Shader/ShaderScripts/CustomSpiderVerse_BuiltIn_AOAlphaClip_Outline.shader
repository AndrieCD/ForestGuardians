Shader "Custom/SpiderVerse_BuiltIn_AOAlphaClip_Outline"
{
    Properties
    {
        _BaseMap("Base Map (RGB=Color, A=Cutout)", 2D) = "white" {}
        _BaseColor("Base Color Tint", Color) = (1,1,1,1)

        // Toon ramp (FROM YOUR SCREENSHOT)
        _RampThreshold("Ramp Threshold", Range(0,1)) = 0.07
        _RampSmoothness("Ramp Smoothness", Range(0.001,0.5)) = 0.372
        _ShadowStrength("Shadow Strength", Range(0,1)) = 0.11

        // Ink banding (FROM YOUR SCREENSHOT)
        _ShadowBands("Shadow Bands", Range(1,6)) = 3.07
        _BandStrength("Band Strength", Range(0,1)) = 1

        // Tints (FROM YOUR SCREENSHOT)
        _LightTint("Light Tint", Color) = (1,1,1,1)
        _ShadowTint("Shadow Tint", Color) = (0.80,0.78,0.95,1)

        // Rim (FROM YOUR SCREENSHOT)
        _RimColor("Rim Color", Color) = (0.80,0.78,0.95,1)
        _RimPower("Rim Power", Range(0.5,12)) = 12
        _RimStrength("Rim Strength", Range(0,2)) = 0.2

        // Halftone (FROM YOUR SCREENSHOT)
        _HalftoneScale("Halftone Scale", Range(10,400)) = 200
        _HalftoneStrength("Halftone Strength", Range(0,2)) = 0.9
        _HalftoneThreshold("Halftone Threshold", Range(0,1)) = 0.56
        _HalftoneSoftness("Halftone Softness", Range(0.001,0.5)) = 0.001
        _HalftoneColor("Halftone Color", Color) = (0,0,0,1)
        [Toggle]_ScreenSpaceHalftone("Screen-space Halftone", Float) = 0

        // Posterize (FROM YOUR SCREENSHOT)
        _PosterizeSteps("Posterize Steps", Range(2,12)) = 2
        _PosterizeStrength("Posterize Strength", Range(0,1)) = 0

        // Ambient (FROM YOUR SCREENSHOT)
        _AmbientBoost("Ambient Boost", Range(0,1)) = 1

        // AO (FROM YOUR SCREENSHOT)
        _AOChannel("AO Channel (0=R,1=G,2=B)", Range(0,2)) = 0.22
        _AOStrength("AO Strength", Range(0,1)) = 1
        [Toggle]_AOInShadowsOnly("AO Mostly In Shadows", Float) = 1

        // Cutout (FROM YOUR SCREENSHOT)
        [Toggle]_AlphaClip("Alpha Clip (Cutout)", Float) = 1
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.9

        // Foliage
        [Toggle]_DoubleSided("Double Sided (Foliage)", Float) = 1

        // Outline (FROM YOUR SCREENSHOT)
        [Toggle]_Outline("Enable Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width (Object)", Range(0,0.08)) = 0.012
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

        // ---------------- OUTLINE PASS (Inverted Hull + Edge-only Alpha Fade) ----------------
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

        // ---------------- FORWARD BASE (Toon + Shadows + Halftone) ----------------
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

            float  _HalftoneScale;
            float  _HalftoneStrength;
            float  _HalftoneThreshold;
            float  _HalftoneSoftness;
            float4 _HalftoneColor;
            float  _ScreenSpaceHalftone;

            float  _PosterizeSteps;
            float  _PosterizeStrength;

            float  _AmbientBoost;

            float  _AOChannel;
            float  _AOStrength;
            float  _AOInShadowsOnly;

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
                float4 screenPos : TEXCOORD3;

                UNITY_FOG_COORDS(4)
                SHADOW_COORDS(5)
            };

            half SelectAOChannel(half3 rgb, half channel)
            {
                half ao = rgb.r;
                ao = lerp(ao, rgb.g, step(0.5h, channel));
                ao = lerp(ao, rgb.b, step(1.5h, channel));
                return ao;
            }

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

                o.screenPos = ComputeScreenPos(o.pos);

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

                half ao = SelectAOChannel(tex.rgb, (half)_AOChannel);
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

                float2 uvDots;
                if (_ScreenSpaceHalftone > 0.5)
                {
                    float2 screenUV = (i.screenPos.xy / max(i.screenPos.w, 1e-5));
                    uvDots = screenUV;
                }
                else
                {
                    uvDots = i.posWS.xz * 0.2;
                }

                half dotMask = HalftoneDots(uvDots, (half)_HalftoneScale);

                half shadowiness = 1.0h - ramp;
                half dotDrive = smoothstep((half)_HalftoneThreshold - (half)_HalftoneSoftness,
                                           (half)_HalftoneThreshold + (half)_HalftoneSoftness,
                                           shadowiness);

                half dots = dotMask * dotDrive * (half)_HalftoneStrength * shadowiness;
                baseCol = lerp(baseCol, _HalftoneColor.rgb, saturate(dots));

                half rim = pow(1.0h - saturate(dot(N, V)), (half)_RimPower) * (half)_RimStrength;
                rim *= lerp(0.6h, 1.0h, 1.0h - ramp);
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

        // ---------------- SHADOW CASTER (Alpha Cutout Shadows) ----------------
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