Shader "Shader_DiffOpacVal"
{
    Properties
    {
        _BaseMap("Base Map (RGB=Color, A=Transparency)", 2D) = "white" {}
        _BaseColor("Base Color Tint", Color) = (1,1,1,1)

        // ============================================================
        // TRANSPARENCY
        // ============================================================
        [Enum(Alpha Cutout,0,Dither Transparency,1,Real Transparency,2)]
        _TransparencyMode("Transparency Mode", Float) = 1

        _AlphaClipThreshold("Alpha Clip Threshold", Range(0,1)) = 0.35
        _DitherPatternScale("Dither Pattern Scale", Range(1,8)) = 1

        _OutlineAlphaFade("Outline Fade With Alpha", Range(0,1)) = 1
        _ShadowAlphaFade("Shadow Fade With Alpha", Range(0,1)) = 1
        _LineAlphaFade("Comic Lines Fade With Alpha", Range(0,1)) = 0

        // Render state controls
        // Cutout / Dither:
        // Src Blend = One, Dst Blend = Zero, ZWrite = On
        //
        // Real Transparency:
        // Src Blend = SrcAlpha, Dst Blend = OneMinusSrcAlpha, ZWrite = Off
        [Enum(UnityEngine.Rendering.BlendMode)]
        _SrcBlend("Src Blend", Float) = 1

        [Enum(UnityEngine.Rendering.BlendMode)]
        _DstBlend("Dst Blend", Float) = 0

        [Enum(Off,0,On,1)]
        _ZWrite("ZWrite", Float) = 1

        // Toon ramp
        _RampThreshold("Ramp Threshold", Range(0,1)) = 0.58
        _RampSmoothness("Ramp Smoothness", Range(0.001,0.5)) = 0.04
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1.0

        // 3-band cartoon shadows
        _BandStrength("Band Strength", Range(0,1)) = 1.0

        _LightTint("Light Tint", Color) = (1,1,1,1)
        _ShadowTint("Shadow Tint", Color) = (0.48,0.45,0.65,1)

        // Ambient control
        _AmbientStrength("Ambient Strength", Range(0,1)) = 0.20

        // AO
        _AOStrength("AO Strength", Range(0,1)) = 0.0
        [Toggle]_AOInShadowsOnly("AO Mostly In Shadows", Float) = 1

        // Comic diagonal lines
        _LineColor("Line Ink Color", Color) = (0,0,0,1)
        _LineScale("Dark Shadow Line Density", Range(10,400)) = 130
        _LineStrength("Overall Line Strength", Range(0,2)) = 0.75
        _LineThickness("Line Thickness", Range(0.01,0.49)) = 0.08
        _LineSoftness("Line Edge Softness", Range(0.001,0.2)) = 0.02

        // Direction control
        _LineDirection("Line Direction XY", Vector) = (1,1,0,0)

        // Medium shadow lines
        _MediumLineDensity("Medium Shadow Line Density", Range(0.1,1)) = 0.55
        _MediumLineStrength("Medium Shadow Line Strength", Range(0,1)) = 0.35

        // Darkest shadow lines
        _DarkLineStrength("Dark Shadow Line Strength", Range(0,2)) = 1.0

        // Outline
        [Toggle]_Outline("Enable Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width Object Space", Range(0,0.08)) = 0.006
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

        CGINCLUDE

        #include "UnityCG.cginc"

        sampler2D _BaseMap;
        float4 _BaseMap_ST;

        float4 _BaseColor;

        float _TransparencyMode;
        float _AlphaClipThreshold;
        float _DitherPatternScale;

        float _OutlineAlphaFade;
        float _ShadowAlphaFade;
        float _LineAlphaFade;

        half Bayer4x4(float2 pixelPos)
        {
            float scale = max(_DitherPatternScale, 1.0);
            float2 p = floor(fmod(pixelPos / scale, 4.0));

            half b = 0.0h;

            if (p.y < 0.5)
            {
                if (p.x < 0.5) b = 0.0h;
                else if (p.x < 1.5) b = 8.0h;
                else if (p.x < 2.5) b = 2.0h;
                else b = 10.0h;
            }
            else if (p.y < 1.5)
            {
                if (p.x < 0.5) b = 12.0h;
                else if (p.x < 1.5) b = 4.0h;
                else if (p.x < 2.5) b = 14.0h;
                else b = 6.0h;
            }
            else if (p.y < 2.5)
            {
                if (p.x < 0.5) b = 3.0h;
                else if (p.x < 1.5) b = 11.0h;
                else if (p.x < 2.5) b = 1.0h;
                else b = 9.0h;
            }
            else
            {
                if (p.x < 0.5) b = 15.0h;
                else if (p.x < 1.5) b = 7.0h;
                else if (p.x < 2.5) b = 13.0h;
                else b = 5.0h;
            }

            // Offset by 0.5 so alpha 0 fully disappears and alpha 1 fully appears.
            return (b + 0.5h) / 16.0h;
        }

        void ApplyAlphaForMainPass(half alpha, float2 pixelPos)
        {
            // Mode 0: classic binary alpha cutout
            if (_TransparencyMode < 0.5)
            {
                clip(alpha - _AlphaClipThreshold);
            }
            // Mode 1: dithered alpha coverage
            else if (_TransparencyMode < 1.5)
            {
                half ditherThreshold = Bayer4x4(pixelPos);
                clip(alpha - ditherThreshold);
            }
            // Mode 2: real transparency
            // No clip in main pass. Blending handles alpha.
        }

        void ApplyAlphaForShadowPass(half alpha, float2 pixelPos)
        {
            // Real transparency cannot write partial alpha into Unity shadow maps.
            // So Cutout uses threshold, while Dither and Real both use dithered shadow coverage.
            if (_TransparencyMode < 0.5)
            {
                clip(alpha - _AlphaClipThreshold);
            }
            else
            {
                half ditherThreshold = Bayer4x4(pixelPos);
                clip(alpha - ditherThreshold);
            }
        }

        half GetOutputAlpha(half alpha)
        {
            // Cutout and Dither should output opaque pixels after clipping.
            // Real Transparency outputs actual grayscale alpha.
            return (_TransparencyMode > 1.5) ? alpha : 1.0h;
        }

        ENDCG

        // ============================================================
        // OUTLINE PASS
        // ============================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="Always" }

            Cull Front
            ZWrite [_ZWrite]
            ZTest LEqual
            Blend [_SrcBlend] [_DstBlend]

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vertOL
            #pragma fragment fragOL
            #pragma multi_compile_instancing

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

            v2f vertOL(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;

                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float outlineEnabled = step(0.5, _Outline);
                float w = _OutlineWidth * outlineEnabled;

                float3 posOS = v.vertex.xyz + v.normal * w;

                o.pos = UnityObjectToClipPos(float4(posOS, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

                return o;
            }

            fixed4 fragOL(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Fully disable outline when toggle is off.
                clip(_Outline - 0.5);

                fixed4 tex = tex2D(_BaseMap, i.uv);
                half alpha = tex.a * _BaseColor.a;

                half outlineAlpha = lerp(1.0h, alpha, saturate((half)_OutlineAlphaFade));

                ApplyAlphaForMainPass(outlineAlpha, i.pos.xy);

                half finalAlpha = GetOutputAlpha(outlineAlpha) * _OutlineColor.a;

                return fixed4(_OutlineColor.rgb, finalAlpha);
            }

            ENDCG
        }

        // ============================================================
        // MAIN TOON PASS
        // ============================================================
        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode"="ForwardBase" }

            ZWrite [_ZWrite]
            ZTest LEqual
            Blend [_SrcBlend] [_DstBlend]

            // Important for flat leaf planes
            Cull Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            float _RampThreshold;
            float _RampSmoothness;
            float _ShadowStrength;

            float _BandStrength;

            float4 _LightTint;
            float4 _ShadowTint;

            float _AmbientStrength;

            float _AOStrength;
            float _AOInShadowsOnly;

            float4 _LineColor;
            float _LineScale;
            float _LineStrength;
            float _LineThickness;
            float _LineSoftness;
            float4 _LineDirection;

            float _MediumLineDensity;
            float _MediumLineStrength;
            float _DarkLineStrength;

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
                float4 screenPos : TEXCOORD2;

                UNITY_FOG_COORDS(3)
                SHADOW_COORDS(4)

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            half DiagonalLines(float2 screenUV, float scale, float thickness, float softness)
            {
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 p = screenUV;
                p.x *= aspect;

                float2 rawDir = _LineDirection.xy;
                float dirLen = max(dot(rawDir, rawDir), 0.0001);
                float2 dir = rawDir * rsqrt(dirLen);

                float lineValue = dot(p, dir) * scale;
                float distToLine = abs(frac(lineValue) - 0.5);

                half lineMask = 1.0h - smoothstep(
                    (half)thickness,
                    (half)(thickness + softness),
                    (half)distToLine
                );

                return lineMask;
            }

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;

                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _BaseMap);

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

                // Read grayscale opacity from BaseMap alpha channel.
                half alpha = tex.a * _BaseColor.a;

                // Cutout / Dither / Real transparency handling.
                ApplyAlphaForMainPass(alpha, i.pos.xy);

                half3 albedo = tex.rgb * _BaseColor.rgb;

                half3 N = normalize(i.normalWS);
                half3 L = normalize(_WorldSpaceLightPos0.xyz);

                half ndotl = saturate(dot(N, L));

                half shadowAtten = SHADOW_ATTENUATION(i);
                shadowAtten = lerp(1.0h, shadowAtten, saturate((half)_ShadowStrength));

                half litInput = ndotl * shadowAtten;

                half threshold = (half)_RampThreshold;
                half smoothness = (half)_RampSmoothness;

                half rampSoft = smoothstep(
                    threshold - smoothness,
                    threshold + smoothness,
                    litInput
                );

                // 3-band toon lighting
                half bandRaw = floor(saturate(rampSoft) * 3.0h);
                half rampBanded = min(bandRaw, 2.0h) * 0.5h;

                half ramp = lerp(
                    rampSoft,
                    rampBanded,
                    saturate((half)_BandStrength)
                );

                // AO defaults to green channel, preserving your current setup.
                half ao = tex.g;
                half aoMul = lerp(1.0h, ao, saturate((half)_AOStrength));

                half aoShadow = lerp(1.0h, aoMul, 1.0h - ramp);
                half aoMode = step(0.5h, (half)_AOInShadowsOnly);
                half aoFinal = lerp(aoMul, aoShadow, aoMode);

                albedo *= aoFinal;

                half3 lightCol  = albedo * _LightColor0.rgb * _LightTint.rgb;
                half3 shadowCol = albedo * _ShadowTint.rgb;

                half3 baseCol = lerp(shadowCol, lightCol, ramp);

                half3 ambient = albedo * ShadeSH9(half4(N, 1.0)) * (half)_AmbientStrength;
                ambient *= lerp(0.25h, 1.0h, ramp);

                half3 shadedCol = baseCol + ambient;

                // Comic diagonal lines
                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 1e-5);

                half lineRamp = rampBanded;

                half darkBand = 1.0h - step(0.25h, lineRamp);
                half mediumBand = step(0.25h, lineRamp) * (1.0h - step(0.75h, lineRamp));

                half mediumLines = DiagonalLines(
                    screenUV,
                    _LineScale * _MediumLineDensity,
                    _LineThickness * 0.9,
                    _LineSoftness
                );

                half darkLines = DiagonalLines(
                    screenUV,
                    _LineScale,
                    _LineThickness * 1.15,
                    _LineSoftness
                );

                half alphaLineFade = lerp(1.0h, alpha, saturate((half)_LineAlphaFade));

                half mediumInk =
                    mediumBand *
                    mediumLines *
                    (half)_LineStrength *
                    (half)_MediumLineStrength *
                    alphaLineFade;

                half darkInk =
                    darkBand *
                    darkLines *
                    (half)_LineStrength *
                    (half)_DarkLineStrength *
                    alphaLineFade;

                half lineInk = saturate(mediumInk + darkInk);

                half3 color = lerp(shadedCol, _LineColor.rgb, lineInk);

                fixed4 outCol = fixed4(saturate(color), GetOutputAlpha(alpha));

                UNITY_APPLY_FOG(i.fogCoord, outCol);

                return outCol;
            }

            ENDCG
        }

        // ============================================================
        // SHADOW CASTER PASS
        // ============================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            // Important for double-sided leaf planes
            Cull Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vertSC
            #pragma fragment fragSC
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing

            struct appdataSC
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;

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

                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)

                return o;
            }

            float4 fragSC(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                fixed4 tex = tex2D(_BaseMap, i.uv);
                half alpha = tex.a * _BaseColor.a;

                half shadowAlpha = lerp(1.0h, alpha, saturate((half)_ShadowAlphaFade));

                ApplyAlphaForShadowPass(shadowAlpha, i.pos.xy);

                SHADOW_CASTER_FRAGMENT(i)
            }

            ENDCG
        }
    }

    FallBack Off
}