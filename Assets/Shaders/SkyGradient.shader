// Làng Gió · bầu trời chuyển màu: đỉnh → chân trời → dưới đất, đĩa mặt trời + quầng, sương chân trời,
// hai lớp mây (mây tích có khối sáng/tối theo hướng nắng + mây ti mỏng), sao ban đêm.
// Không dùng ánh sáng nên chạy được cả URP lẫn Built-in.
Shader "LangGio/SkyGradient"
{
    Properties
    {
        _Top ("Top", Color) = (0.55, 0.75, 0.88, 1)
        _Horizon ("Horizon", Color) = (0.93, 0.92, 0.86, 1)
        _Bottom ("Bottom", Color) = (0.80, 0.84, 0.72, 1)
        _SunColor ("Sun Glow", Color) = (1, 0.85, 0.6, 1)
        _SunDir ("Sun Direction", Vector) = (0.3, 0.5, 0.3, 0)
        _SunSize ("Sun Glow Size", Float) = 8
        _CloudColor ("Cloud", Color) = (1, 1, 1, 1)
        _Clouds ("Cloud Amount", Float) = 0.5
        _Night ("Night", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top, _Horizon, _Bottom, _SunColor, _CloudColor;
            float4 _SunDir;
            float _SunSize, _Clouds, _Night;
            float _LG_Rainbow;   // toàn cục, đặt từ Seasons

            fixed3 Hue(float h)
            {
                return saturate(abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
            }

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += noise(p) * a; p = p * 2.03 + float2(1.7, 9.2); a *= 0.5; }
                return s;
            }

            // mật độ mây tích tại uv
            float cumulus(float2 uv, float amount)
            {
                float n = fbm(uv);
                float base = 1.0 - amount * 0.6;
                return saturate((n - base * 0.6) * 4.0);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 sunD = normalize(_SunDir.xyz);
                float sd = saturate(dot(d, sunD));

                fixed3 col = y > 0
                    ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(y), 0.3))
                    : lerp(_Horizon.rgb, _Bottom.rgb, saturate(-y * 4));

                // dải sương sáng ngay chân trời, ấm hơn về phía mặt trời
                float haze = exp(-abs(y) * 14.0);
                col = lerp(col, _Horizon.rgb * 1.06 + _SunColor.rgb * 0.18 * pow(sd, 3), haze * 0.55);

                // quầng nắng (Mie) + đĩa mặt trời
                col += _SunColor.rgb * (pow(sd, _SunSize) * 0.45 + pow(sd, _SunSize * 6) * 0.35);
                float disc = smoothstep(0.9990, 0.9996, sd);
                col = lerp(col, _SunColor.rgb * 1.6 + 0.4, disc * saturate(y * 20 + 0.5));

                // cầu vồng: vòng cung 40–42° quanh điểm đối nhật
                if (_LG_Rainbow > 0.001 && y > 0)
                {
                    float ang = degrees(acos(clamp(dot(d, -sunD), -1, 1)));
                    float t = (ang - 39.5) / 3.5;                        // 0 tím (trong) → 1 đỏ (ngoài)
                    if (t > 0 && t < 1)
                    {
                        float edge = smoothstep(0, 0.15, t) * smoothstep(1, 0.85, t);
                        float vfade = smoothstep(0.0, 0.12, y) * (1 - smoothstep(0.35, 0.6, y) * 0.5);
                        col = lerp(col, col * 0.85 + Hue(0.78 - t * 0.78) * 0.6, edge * vfade * _LG_Rainbow * 0.8);
                    }
                }

                // sao (chỉ ban đêm)
                if (_Night > 0.01 && y > 0)
                {
                    float2 g = d.xz / (y + 0.3) * 140.0;
                    float s = hash(floor(g));
                    float tw = 0.6 + 0.4 * sin(_Time.y * (2 + s * 5) + s * 40);
                    float star = step(0.9965, s) * tw * smoothstep(0.05, 0.3, y);
                    col += star * _Night * 0.9;
                }

                if (y > 0.0)
                {
                    float invY = 1.0 / (y + 0.12);
                    float fade = smoothstep(0.0, 0.18, y);

                    // mây ti cao, mảnh, kéo dài
                    float2 uvc = d.xz * invY * 0.5 + _Time.y * float2(0.006, 0.001);
                    float ci = fbm(float2(uvc.x * 1.0, uvc.y * 4.0) * 1.7);
                    ci = smoothstep(0.55, 0.85, ci) * 0.35 * fade;
                    col = lerp(col, _CloudColor.rgb * 1.02 + _SunColor.rgb * 0.15, ci);

                    // mây tích: có khối, sáng phía mặt trời, đáy hơi tối
                    float2 uv = d.xz * invY * 1.1 + _Time.y * float2(0.012, 0.005);
                    float c = cumulus(uv * 1.2, _Clouds);
                    if (c > 0.001)
                    {
                        float2 toSun = normalize(sunD.xz + 1e-4) * 0.09;
                        float cs = cumulus(uv * 1.2 + toSun, _Clouds);
                        float lit = saturate(1.0 - (cs - c) * 2.2);               // mặt hướng nắng sáng hơn
                        fixed3 shade = lerp(_Top.rgb * 0.35 + _Horizon.rgb * 0.6, _CloudColor.rgb * 1.05, lit);
                        shade += _SunColor.rgb * pow(sd, 6) * (1 - c) * 0.6;       // viền rực khi ngược sáng
                        float alpha = smoothstep(0.0, 0.55, c) * fade * 0.95;
                        col = lerp(col, shade, alpha);
                    }
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
