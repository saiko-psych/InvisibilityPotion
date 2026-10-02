Shader "JVLmock_Custom/Vegetation"
{
    // Dummy stump (assumed vanilla name, verify with ip_bundle; C# falls back to Custom/Creature): Jötunn replaces this shader with the vanilla "Custom/Vegetation" at runtime
    // (fixReference: true). Only the name line matters; properties set on the material are kept.
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _BumpMap ("Normal", 2D) = "bump" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input { float2 uv_MainTex; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
