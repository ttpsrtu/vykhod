// Триплaнарное наложение в мировых координатах с нормал-картой и подмешиванием второй текстуры по шуму (без видимого тайлинга).
Shader "Vykhod/Triplanar"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = 1
        _Tex2 ("Albedo 2 (blend)", 2D) = "white" {}
        _Blend ("Blend amount", Range(0,1)) = 0
        _Scale ("Metres per tile", Float) = 2.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.12
        _Sharp ("Blend sharpness", Range(1,8)) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0
        sampler2D _MainTex; sampler2D _BumpMap; sampler2D _Tex2;
        fixed4 _Color; float _Scale; half _Glossiness; float _Sharp; float _BumpScale; float _Blend;
        struct Input { float3 worldPos; float3 worldNormal; INTERNAL_DATA };

        // дешёвый шум для разбиения тайлинга
        float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float noise2(float2 p)
        {
            float2 i = floor(p); float2 f = frac(p); f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(hash2(i), hash2(i + float2(1, 0)), f.x), lerp(hash2(i + float2(0, 1)), hash2(i + float2(1, 1)), f.x), f.y);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 wn = WorldNormalVector(IN, float3(0, 0, 1));
            float3 n = abs(wn);
            n = pow(n, _Sharp);
            n /= (n.x + n.y + n.z + 1e-5);
            float3 p = IN.worldPos / max(_Scale, 0.01);
            float2 uvx = p.zy, uvy = p.xz, uvz = p.xy;
            fixed4 cx = tex2D(_MainTex, uvx), cy = tex2D(_MainTex, uvy), cz = tex2D(_MainTex, uvz);
            fixed4 c = cx * n.x + cy * n.y + cz * n.z;
            if (_Blend > 0.001)
            {
                float k = saturate((noise2(IN.worldPos.xz * 0.05) * 1.6 - 0.3)) * _Blend;
                fixed4 c2 = tex2D(_Tex2, uvx) * n.x + tex2D(_Tex2, uvy) * n.y + tex2D(_Tex2, uvz) * n.z;
                c = lerp(c, c2, k);
            }
            // нормали: тангенс-пространство каждой проекции → мировое → тангенс-пространство поверхности
            float3 tx = UnpackScaleNormal(tex2D(_BumpMap, uvx), _BumpScale);
            float3 ty = UnpackScaleNormal(tex2D(_BumpMap, uvy), _BumpScale);
            float3 tz = UnpackScaleNormal(tex2D(_BumpMap, uvz), _BumpScale);
            float3 sgn = sign(wn);
            float3 nx = float3(0, tx.y, tx.x * sgn.x) * 1.0;
            float3 ny = float3(ty.x, 0, ty.y * sgn.y) * 1.0;
            float3 nz = float3(tz.x, tz.y * sgn.z, 0) * 1.0;
            float3 worldN = normalize(wn + nx * n.x + ny * n.y + nz * n.z);
            // мировая нормаль → тангенс-пространство поверхности через базис, который даёт сам surface shader
            float3 wT = WorldNormalVector(IN, float3(1, 0, 0));
            float3 wB = WorldNormalVector(IN, float3(0, 1, 0));
            o.Normal = normalize(float3(dot(wT, worldN), dot(wB, worldN), dot(wn, worldN)));
            o.Albedo = (c * _Color).rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
