Shader "BASE_ToonComicShader"
{
    Properties
    {
        _BaseMap("Base Map (RGB=Color, A=Cutout)", 2D) = "white" {}
        _BaseColor("Base Color Tint", Color) = (1,1,1,1)

        // Toon ramp
        _RampThreshold("Ramp Threshold", Range(0,1)) = 0.6
        _RampSmoothness("Ramp Smoothness", Range(0.001,0.5)) = 0.2
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1.0

        // Spider-verse ink banding
        _ShadowBands("Shadow Bands", Range(1,6)) = 6
        _BandStrength("Band Strength", Range(0,1)) = 1

        _LightTint("Light Tint", Color) = (1,1,1,1)
        _ShadowTint("Shadow Tint", Color) = (0.90,0.86,1.00,1)

        // Rim
        _RimColor("Rim Color", Color) = (0.90,0.86,1.00,1)
        _RimPower("Rim Power", Range(0.5,12)) = 12
        _RimStrength("Rim Strength", Range(0,2)) = 0

        // Halftone Layer A (Dots)
        _HalftoneScale("Halftone Scale (Dots)", Range(10,400)) = 150
        _HalftoneStrength("Halftone Strength (Dots)", Range(0,2)) = 0.45
        _HalftoneThreshold("Halftone Threshold (Dots)", Range(0,1)) = 0
        _HalftoneSoftness("Halftone Softness (Dots)", Range(0.001,0.5)) = 0.001
        _HalftoneColor("Halftone Ink Color", Color) = (0,0,0,1)
        [Toggle]_ScreenSpaceHalftone("Screen-space Halftone", Float) = 1

        // Halftone Layer B (Hatching / Crosshatch)
        [Toggle]_HatchEnabled("Enable Hatch Halftone", Float) = 1
        _HatchScale("Hatch Scale", Range(20,600)) = 200
        _HatchStrength("Hatch Strength", Range(0,2)) = 0.3
        _HatchThreshold("Hatch Threshold", Range(0,1)) = 0
        _HatchSoftness("Hatch Softness", Range(0.001,0.5)) = 0.5
        _HatchLineWidth("Hatch Line Width", Range(0.01,0.5)) = 0.3
        _HatchAngle("Hatch Angle (Deg)", Range(0,180)) = 35
        [Toggle]_CrossHatch("Cross Hatch (2nd Angle)", Float) = 1

        // Halftone distance fade
        _HalftoneFadeStart("Halftone Fade Start (World)", Range(0,200)) = 20
        _HalftoneFadeEnd("Halftone Fade End (World)", Range(0,400)) = 60

        // Posterize
        _PosterizeSteps("Posterize Steps", Range(2,12)) = 6
        _PosterizeStrength("Posterize Strength", Range(0,1)) = 0.3

        // Ambient
        _AmbientBoost("Ambient Boost", Range(0,1)) = 0.7

        // AO
        _AOChannel("AO Channel (0=R,1=G,2=B)", Range(0,2)) = 1
        _AOStrength("AO Strength", Range(0,1)) = 0.3
        [Toggle]_AOInShadowsOnly("AO Mostly In Shadows", Float) = 1

        // Cutout
        [Toggle]_AlphaClip("Use Cutout", Float) = 1
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.45

        [Toggle]_UseBlackCutout("Use Black Cutout Instead of Alpha", Float) = 1
        _BlackCutoutThreshold("Black Cutout Threshold", Range(0.001,1)) = 0.08

        // Foliage
        [Toggle]_DoubleSided("Double Sided (Foliage)", Float) = 1

        // Outline
        [Toggle]_Outline("Enable Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width (Object)", Range(0,0.08)) = 0.006
        _OutlineDepthOffset("Outline Depth Offset", Range(0,0.02)) = 0.002
        _OutlineEdgeSoftness("Outline Edge Softness", Range(0.05,0.5)) = 0.18
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
            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;

            float4 _BaseColor;
            float _AlphaClip;
            float _Cutoff;
            float _UseBlackCutout;
            float _BlackCutoutThreshold;

            float _Outline;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineDepthOffset;
            float _OutlineEdgeSoftness;

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

            float GetCutoutMask(fixed4 tex)
            {
                if (_UseBlackCutout > 0.5)
                {
                    return length(tex.rgb);
                }
                else
                {
                    return tex.a * _BaseColor.a;
                }
            }

            float GetCutoutThreshold()
            {
                return (_UseBlackCutout > 0.5) ? _BlackCutoutThreshold : _Cutoff;
            }

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
                half mask = GetCutoutMask(tex);
                half cutoff = GetCutoutThreshold();

                if (_AlphaClip > 0.5)
                    clip(mask - cutoff);

                half softness = max(0.001h, (half)_OutlineEdgeSoftness);
                half edge = saturate((mask - cutoff) / softness);
                half edgeMask = 1.0h - edge;

                return fixed4(_OutlineColor.rgb, 1) * edgeMask;
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

            float4 _RimColor;
            float _RimPower;
            float _RimStrength;

            float _HalftoneScale;
            float _HalftoneStrength;
            float _HalftoneThreshold;
            float _HalftoneSoftness;
            float4 _HalftoneColor;
            float _ScreenSpaceHalftone;

            float _HatchEnabled;
            float _HatchScale;
            float _HatchStrength;
            float _HatchThreshold;
            float _HatchSoftness;
            float _HatchLineWidth;
            float _HatchAngle;
            float _CrossHatch;

            float _HalftoneFadeStart;
            float _HalftoneFadeEnd;

            float _PosterizeSteps;
            float _PosterizeStrength;

            float _AmbientBoost;

            float _AOChannel;
            float _AOStrength;
            float _AOInShadowsOnly;

            float _AlphaClip;
            float _Cutoff;
            float _UseBlackCutout;
            float _BlackCutoutThreshold;

            float _DoubleSided;

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

            float GetCutoutMask(fixed4 tex)
            {
                if (_UseBlackCutout > 0.5)
                {
                    return length(tex.rgb);
                }
                else
                {
                    return tex.a * _BaseColor.a;
                }
            }

            float GetCutoutThreshold()
            {
                return (_UseBlackCutout > 0.5) ? _BlackCutoutThreshold : _Cutoff;
            }

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

            float2 Rotate2D(float2 p, float radians)
            {
                float s = sin(radians);
                float c = cos(radians);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            half HatchLines(float2 uv, half scale, half lineWidth, half angleDeg)
            {
                float ang = radians(angleDeg);
                float2 r = Rotate2D(uv, ang);

                float v = r.x * scale;
                float wave = abs(frac(v) - 0.5) * 2.0;
                half ink = 1.0h - smoothstep(0.0h, lineWidth, (half)wave);
                return ink;
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

                float2 uvHT;
                if (_ScreenSpaceHalftone > 0.5)
                {
                    float2 screenUV = (i.screenPos.xy / max(i.screenPos.w, 1e-5));
                    uvHT = screenUV;
                }
                else
                {
                    uvHT = i.posWS.xz * 0.2;
                }

                half shadowiness = 1.0h - ramp;

                half camDist = distance(_WorldSpaceCameraPos.xyz, i.posWS);
                half denom = max(0.001h, (half)(_HalftoneFadeEnd - _HalftoneFadeStart));
                half fade = 1.0h - saturate((camDist - (half)_HalftoneFadeStart) / denom);

                half dotMask = HalftoneDots(uvHT, (half)_HalftoneScale);

                half dotDrive = smoothstep((half)_HalftoneThreshold - (half)_HalftoneSoftness,
                                           (half)_HalftoneThreshold + (half)_HalftoneSoftness,
                                           shadowiness);

                half dotsInk = dotMask * dotDrive * (half)_HalftoneStrength * shadowiness * fade;
                baseCol = lerp(baseCol, _HalftoneColor.rgb, saturate(dotsInk));

                if (_HatchEnabled > 0.5)
                {
                    half h1 = HatchLines(uvHT, (half)_HatchScale, (half)_HatchLineWidth, (half)_HatchAngle);
                    half h = h1;

                    if (_CrossHatch > 0.5)
                    {
                        half h2 = HatchLines(uvHT, (half)_HatchScale, (half)_HatchLineWidth, (half)(_HatchAngle + 90.0));
                        h = max(h1, h2);
                    }

                    half hatchDrive = smoothstep((half)_HatchThreshold - (half)_HatchSoftness,
                                                 (half)_HatchThreshold + (half)_HatchSoftness,
                                                 shadowiness);

                    half hatchInk = h * hatchDrive * (half)_HatchStrength * shadowiness * fade;
                    baseCol = lerp(baseCol, _HalftoneColor.rgb, saturate(hatchInk));
                }

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
            float _UseBlackCutout;
            float _BlackCutoutThreshold;

            float GetCutoutMask(fixed4 tex)
            {
                if (_UseBlackCutout > 0.5)
                {
                    return length(tex.rgb);
                }
                else
                {
                    return tex.a * _BaseColor.a;
                }
            }

            float GetCutoutThreshold()
            {
                return (_UseBlackCutout > 0.5) ? _BlackCutoutThreshold : _Cutoff;
            }

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
                    clip(GetCutoutMask(tex) - GetCutoutThreshold());
                }
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    FallBack Off
}