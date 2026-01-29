Shader "Custom/ToonShading"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Steps ("Light Steps", Range(1,4)) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows

        sampler2D _MainTex;
        float _Steps;

        struct Input
        {
            float2 uv_MainTex;
        };

        half4 LightingToon (SurfaceOutput s, half3 lightDir, half atten)
        {
            float NdotL = dot(s.Normal, lightDir);
            NdotL = max(0, NdotL);

            // STEPPED LIGHTING
            float stepped = floor(NdotL * _Steps) / _Steps;

            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * stepped * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            half4 tex = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = tex.rgb;
            o.Alpha = tex.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
