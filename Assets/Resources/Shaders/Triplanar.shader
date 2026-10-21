// Триплaнарное наложение текстуры в мировых координатах: процедурная геометрия без UV выглядит как настоящая.
Shader "Vykhod/Triplanar"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Scale ("Metres per tile", Float) = 2.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.12
        _Sharp ("Blend sharpness", Range(1,8)) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        float _Scale;
        half _Glossiness;
        float _Sharp;
        struct Input { float3 worldPos; float3 worldNormal; };
        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = abs(normalize(IN.worldNormal));
            n = pow(n, _Sharp);
            n /= (n.x + n.y + n.z + 1e-5);
            float3 p = IN.worldPos / max(_Scale, 0.01);
            fixed4 cx = tex2D(_MainTex, p.zy);
            fixed4 cy = tex2D(_MainTex, p.xz);
            fixed4 cz = tex2D(_MainTex, p.xy);
            fixed4 c = (cx * n.x + cy * n.y + cz * n.z) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
