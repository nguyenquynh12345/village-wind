// Làng Gió · mặt nước ao: đậm dần theo độ sâu, viền bọt ở bờ, gợn sóng, phản chiếu màu trời, nắng lấp lánh.
// Cần "Depth Texture" bật trong URP asset (RenderSetup đã bật).
Shader "LangGio/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.42, 0.74, 0.74, 1)
        _Deep ("Deep", Color) = (0.15, 0.36, 0.47, 1)
        _Foam ("Foam", Color) = (1, 1, 1, 1)
        _DepthRange ("Depth Range", Float) = 0.7
        _FlowSpeed ("Flow Speed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow;
                half4 _Deep;
                half4 _Foam;
                half _DepthRange;
                half _FlowSpeed;
            CBUFFER_END

            half4 _LG_SkyColor;   // đặt toàn cục từ DayCycle
            half _LG_Snow;        // mùa đông: mặt nước đóng băng
            half _LG_Wet;         // mưa: vòng gợn hạt mưa

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fog : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.uv = input.uv;
                return o;
            }

            float W_Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float W_Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(W_Hash(i), W_Hash(i + float2(1, 0)), f.x), lerp(W_Hash(i + float2(0, 1)), W_Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Waves(float2 uv, float t)
            {
                return W_Noise(uv * 1.3 + t * float2(0.12, 0.08)) + 0.5 * W_Noise(uv * 2.9 - t * float2(0.1, 0.15));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float surfEye = -TransformWorldToView(i.positionWS).z;
                float depth = max(0.0, sceneEye - surfEye);

                float t = _Time.y;
                float2 uv = _FlowSpeed > 0.001 ? (i.uv - float2(0.0, t * _FlowSpeed * 2.2)) : i.positionWS.xz;
                const float e = 0.08;
                float h0 = Waves(uv, t);
                float hx = Waves(uv + float2(e, 0), t);
                float hz = Waves(uv + float2(0, e), t);
                half3 N = normalize(half3(-(hx - h0) / e * 0.18, 1.0, -(hz - h0) / e * 0.18));

                half k = saturate(depth / _DepthRange);
                half3 col = lerp(_Shallow.rgb, _Deep.rgb, k);

                // vân sáng (caustics) dưới đáy chỗ nước nông
                float2 cu = uv * 2.2 + N.xz * 0.6;
                float c1 = W_Noise(cu + t * float2(0.21, 0.13));
                float c2 = W_Noise(cu * 1.37 - t * float2(0.17, 0.23));
                half caus = pow(saturate(1.0 - abs(c1 - c2) * 3.0), 6.0);
                col += caus * (1.0 - k) * 0.22 * _Shallow.rgb;

                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 lit = L.color * (0.55 * L.shadowAttenuation) + ambient;
                col *= lit;

                // phản chiếu màu trời theo góc nhìn
                half fres = pow(1.0 - saturate(dot(N, V)), 4.0);
                half3 sky = _LG_SkyColor.a > 0.0 ? _LG_SkyColor.rgb : half3(0.7, 0.8, 0.9);
                sky = lerp(sky * 1.08, sky * 0.85, saturate(V.y));
                col = lerp(col, sky, saturate(fres * 0.7 + 0.08));

                // nắng lấp lánh
                half3 H = normalize(L.direction + V);
                half spec = pow(saturate(dot(N, H)), 140.0) * 2.2 * L.shadowAttenuation;
                col += L.color * spec;

                // mưa: các vòng tròn lan ra từ chỗ hạt mưa rơi (lưới ô, mỗi ô 1 giọt lệch pha)
                if (_LG_Wet > 0.001)
                {
                    half rings = 0;
                    for (int L = 0; L < 2; L++)
                    {
                        float2 g = uv * (2.2 + L * 1.3) + L * 7.1;
                        float2 cell = floor(g);
                        float2 f = frac(g);
                        float h = W_Hash(cell + L * 13.0);
                        float2 c = 0.2 + 0.6 * float2(h, W_Hash(cell.yx + 3.0));
                        float ph = frac(t * 0.9 + h);
                        float d = length(f - c);
                        float ring = 1.0 - smoothstep(0.0, 0.035, abs(d - ph * 0.4));
                        rings += ring * (1.0 - ph);
                    }
                    col += rings * _LG_Wet * 0.35 * lit;
                }

                // viền bọt ở bờ
                float fn = W_Noise(uv * 4.0 + t * 0.25);
                half foam = 1.0 - smoothstep(0.015, 0.09, depth + (fn - 0.5) * 0.05);
                col = lerp(col, _Foam.rgb * lit, foam * 0.8);

                half alpha = saturate(0.6 + k * 0.38 + fres * 0.15);
                alpha = max(alpha, foam * 0.9);
                alpha *= saturate(depth / 0.05 + foam);

                if (_LG_Snow > 0.001)
                {
                    // băng: trắng xanh, vết nứt mờ, gần như đục
                    float cr = W_Noise(uv * 3.0);
                    half3 ice = lerp(half3(0.78, 0.88, 0.94), half3(0.9, 0.95, 0.98), cr) * lit;
                    col = lerp(col, ice, _LG_Snow * 0.85);
                    alpha = lerp(alpha, 0.95, _LG_Snow);
                }

                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    // Built-in (dự phòng): nước trong suốt đơn giản
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Shallow;
            float4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            fixed4 frag() : SV_Target { return fixed4(_Shallow.rgb, 0.8); }
            ENDCG
        }
    }
    FallBack Off
}
