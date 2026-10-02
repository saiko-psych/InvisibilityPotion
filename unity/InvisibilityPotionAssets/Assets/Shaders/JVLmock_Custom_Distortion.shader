Shader "JVLmock_Custom/Distortion"
{
    // Dummy stump: Jötunn replaces this shader with the vanilla "Custom/Distortion" at runtime
    // (fixReference: true). Only the name line matters; properties set on the material are kept.
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _RefractionIntensity ("Refraction", Float) = 0.02
        _Glossiness ("Smoothness", Range(0,1)) = 0.9
        _Metallic ("Metallic", Range(0,1)) = 0
        _NormalTex ("Normal", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Lambert
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input { float2 uv_MainTex; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
