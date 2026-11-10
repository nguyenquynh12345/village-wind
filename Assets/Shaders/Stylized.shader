// Làng Gió · shader "vẽ tay" dùng cho mọi vật.
// - Màu = _BaseColor (× màu đỉnh nếu _UseVertexColor = 1)
// - Vân nhiễu theo vị trí thế giới (mặt tường / mái không phẳng lì)
// - Sáng tối chuyển mềm (wrap lighting), viền sáng nhẹ phía ngược nắng
// - Nhận bóng mềm, SSAO, đèn phụ (đèn lồng, cửa sổ), sương
// SubShader 1: URP · SubShader 2: Built-in (dự phòng nếu chưa chuyển sang URP)
Shader "LangGio/Stylized"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Color ("Color (legacy)", Color) = (1, 1, 1, 1)
        _UseVertexColor ("Use Vertex Color", Float) = 0
        _Noise ("Noise Strength", Float) = 1
        _Wrap ("Light Wrap", Float) = 0.35
        _Rim ("Rim", Float) = 0.22
        _Pattern ("Pattern (0 none, 1 plaster, 2 wood, 3 stone, 4 brick)", Float) = 0
        _Wind ("Wind (uv0.x = sway weight)", Float) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
    }

    // ======================================================================
    //  URP
    // ======================================================================
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _Color;
            half _UseVertexColor;
            half _Noise;
            half _Wrap;
            half _Rim;
            half _Pattern;
            half4 _EmissionColor;
            half _Wind;
        CBUFFER_END

        half _LG_CloudShadow;   // toàn cục, đặt từ DayCycle
        half _LG_Spring, _LG_Autumn, _LG_Snow;   // toàn cục, đặt từ Seasons
        half _LG_Wet;                            // mưa: bề mặt sẫm + loáng nước
        half4 _LG_SkyColor;



        float LG_Hash(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float LG_Noise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = LG_Hash(i);
            float b = LG_Hash(i + float3(1, 0, 0));
            float c = LG_Hash(i + float3(0, 1, 0));
            float d = LG_Hash(i + float3(1, 1, 0));
            float e = LG_Hash(i + float3(0, 0, 1));
            float g = LG_Hash(i + float3(1, 0, 1));
            float h = LG_Hash(i + float3(0, 1, 1));
            float k = LG_Hash(i + float3(1, 1, 1));
            return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y),
                        lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
        }

        float LG_Hash2(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        // Hoa văn khối xếp (đá / gạch): ô so le, mạch vữa, viên lệch màu, mép trên sáng / mép dưới tối như nổi lên.
        half3 LG_Masonry(half3 albedo, float2 uv, float cellW, float cellH, float mortarW, half3 mortarCol, float jitter, float px)
        {
            float row = floor(uv.y / cellH);
            float off = frac(row * 0.5) * cellW + LG_Hash2(float2(row, 7.3)) * cellW * 0.35;
            float2 g = float2((uv.x + off) / cellW, uv.y / cellH);
            float2 id = floor(g);
            float2 f = frac(g);
            float dx = min(f.x, 1.0 - f.x) * cellW;
            float dy = min(f.y, 1.0 - f.y) * cellH;
            float aa = max(px, 0.002);
            float mortar = 1.0 - smoothstep(mortarW - aa, mortarW + aa, min(dx, dy));
            float h = LG_Hash2(id + row * 13.1);
            half3 block = albedo * (1.0 + (h - 0.5) * 2.0 * jitter);
            block *= lerp(0.93, 1.06, smoothstep(0.55, 0.95, f.y));       // mép trên sáng
            block *= lerp(0.9, 1.0, smoothstep(0.0, 0.18, f.y));           // mép dưới tối
            half3 c = lerp(block, mortarCol, mortar);
            float fade = saturate((px - cellH * 0.18) / (cellH * 0.25));   // xa quá → bỏ hoa văn, tránh nhoè
            return lerp(c, albedo * 0.97, fade);
        }

        half3 LG_Pattern(half3 albedo, float3 pos, half3 N, half pattern)
        {
            if (pattern < 0.5) return albedo;
            float2 uv = abs(N.y) > 0.7 ? pos.xz : float2(abs(N.x) > abs(N.z) ? pos.z : pos.x, pos.y);
            float px = length(fwidth(uv));
            if (pattern < 1.5)
            {
                // vữa: loang lổ nhẹ, ố dần xuống dưới
                float b = LG_Noise(pos * 0.8) * 0.6 + LG_Noise(pos * 3.3) * 0.4;
                return albedo * lerp(0.88, 1.05, b);
            }
            if (pattern < 2.5)
            {
                // ván gỗ dọc: khe ván + vân gỗ
                float w = 0.24;
                float id = floor(uv.x / w);
                float f = frac(uv.x / w);
                float aa = max(px / w, 0.01);
                float seam = 1.0 - smoothstep(0.03 - aa, 0.03 + aa, min(f, 1.0 - f));
                float grain = LG_Noise(float3(id * 3.7, uv.y * 0.7, f * 6.0));
                half3 c = albedo * (0.9 + LG_Hash2(float2(id, 3.1)) * 0.18) * lerp(0.92, 1.05, grain);
                c = lerp(c, albedo * 0.55, seam);
                float fade = saturate((px - 0.06) / 0.08);
                return lerp(c, albedo * 0.96, fade);
            }
            if (pattern < 3.5)
                return LG_Masonry(albedo, uv, 0.62, 0.34, 0.03, albedo * 0.62, 0.11, px);   // đá xếp
            if (pattern < 4.5)
                return LG_Masonry(albedo, uv, 0.34, 0.15, 0.018, half3(0.6, 0.55, 0.48), 0.13, px); // gạch
            if (pattern > 5.5 && pattern < 6.5)
            {
                // đất: loang + sỏi nhỏ rải rác
                float dn = LG_Noise(pos * 2.2) * 0.6 + LG_Noise(pos * 7.0) * 0.4;
                half3 c = albedo * lerp(0.8, 1.1, dn);
                float2 g = uv / 0.45;
                float2 ig = floor(g);
                float2 c0 = float2(LG_Hash2(ig), LG_Hash2(ig + 5.3)) * 0.7 + 0.15;
                float dd = length(frac(g) - c0);
                float isP = step(LG_Hash2(ig + 9.1), 0.35);
                float peb = isP * (1.0 - smoothstep(0.09, 0.09 + max(px / 0.45, 0.02) * 2.0, dd));
                c = lerp(c, half3(0.3, 0.28, 0.25) * (0.85 + LG_Hash2(ig + 2.2) * 0.3), peb);
                float fade = saturate((px - 0.06) / 0.1);
                return lerp(c, albedo * 0.95, fade);
            }
            if (pattern > 6.5)
            {
                // đá cuội: ô Voronoi, viên tròn trịa, khe tối
                float2 g = uv / 0.32;
                float2 ig = floor(g), fg = frac(g);
                float d1 = 8.0, d2 = 8.0;
                float2 id1 = ig;
                for (int yy = -1; yy <= 1; yy++)
                    for (int xx = -1; xx <= 1; xx++)
                    {
                        float2 o = float2(xx, yy);
                        float2 rr = o + float2(LG_Hash2(ig + o), LG_Hash2(ig + o + 17.1)) * 0.8 + 0.1 - fg;
                        float d = dot(rr, rr);
                        if (d < d1) { d2 = d1; d1 = d; id1 = ig + o; }
                        else if (d < d2) d2 = d;
                    }
                float edge = sqrt(d2) - sqrt(d1);
                float aa = max(px / 0.32, 0.02);
                float stone = smoothstep(0.04, 0.1 + aa * 2.0, edge);
                half3 sc = albedo * (0.82 + LG_Hash2(id1) * 0.32);
                sc *= lerp(0.9, 1.08, saturate(1.0 - sqrt(d1) * 1.4));   // viên hơi gồ lên
                half3 c = lerp(albedo * 0.5, sc, stone);
                float fade = saturate((px - 0.08) / 0.1);
                return lerp(c, albedo * 0.9, fade);
            }
            // cỏ: lốm đốm nhiều cỡ (đám cỏ đậm / nhạt, đốm nhỏ), mờ dần khi xa
            float g1 = LG_Noise(pos * 0.35);
            float g2 = LG_Noise(pos * 1.7 + 5.0);
            float g3 = LG_Noise(pos * 6.0 + 11.0);
            float nearK = 1.0 - saturate((px - 0.04) / 0.12);
            half k = lerp(0.86, 1.1, g1) * lerp(0.9, 1.07, g2) * lerp(1.0, lerp(0.88, 1.08, g3), nearK);
            half3 grass = albedo * k;
            grass = lerp(grass, grass * half3(1.06, 1.04, 0.86), saturate((g2 - 0.62) * 3.0));   // vạt cỏ hơi vàng
            return grass;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half4  color      : TEXCOORD2;
                half   fog        : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posOS = input.positionOS.xyz;
                if (_Wind > 0.0)
                {
                    // gió: cỏ / hoa lay theo sóng chạy ngang thế giới
                    float3 wp = TransformObjectToWorld(posOS);
                    float w = input.uv.x * input.uv.x * _Wind;
                    float t = _Time.y;
                    float gust = sin(wp.x * 0.35 + wp.z * 0.2 + t * 1.6) * 0.6 + sin(wp.x * 1.3 - wp.z * 0.9 + t * 3.1) * 0.25;
                    wp.x += (gust + 0.35) * w * 0.12;
                    wp.z += sin(wp.z * 0.5 + t * 2.3) * w * 0.05;
                    posOS = TransformWorldToObject(wp);
                }
                VertexPositionInputs p = GetVertexPositionInputs(posOS);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = nrm.normalWS;
                o.color = input.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Đổi màu theo mùa: chỉ tác động phần "xanh lá" (cỏ, tán cây, dây leo) + tuyết trên mặt hướng lên
            half3 LG_Season(half3 albedo, float3 posWS, half3 N, float n1, float n2, half ground)
            {
                half green = saturate((albedo.g - max(albedo.r, albedo.b)) * 9.0);
                half lum = dot(albedo, half3(0.3, 0.59, 0.11));
                if (_LG_Spring > 0.001)
                {
                    albedo = lerp(albedo, albedo * half3(0.97, 1.03, 0.9), green * _LG_Spring * (1.0 - ground * 0.6));
                    // tán lá (không phải mặt đất) lấm tấm hoa trắng / hồng, nhiều hơn ở mặt hướng lên
                    float bn = LG_Noise(posWS * 7.5 + 11.0) * 0.7 + LG_Noise(posWS * 17.0) * 0.3;
                    half bloom = smoothstep(0.66, 0.72, bn + N.y * 0.06) * green * (1.0 - ground);
                    half3 petal = lerp(half3(0.95, 0.72, 0.8), half3(0.97, 0.94, 0.9), LG_Noise(posWS * 2.0 + 5.0));
                    albedo = lerp(albedo, petal * 0.85, bloom * _LG_Spring);
                }
                if (_LG_Autumn > 0.001)
                {
                    float v = LG_Noise(posWS * 0.45 + 31.0);
                    half3 fall = lerp(half3(1.0, 0.58, 0.14), half3(0.9, 0.26, 0.08), saturate(v * 1.4 - 0.2));
                    fall = lerp(fall, half3(0.95, 0.78, 0.25), saturate(n1 * 1.6 - 0.9));
                    // mặt đất cỏ chỉ ngả vàng úa (ô liu → rơm), tán lá mới đổi màu rực
                    half3 dry = lerp(half3(0.62, 0.66, 0.26), half3(0.85, 0.7, 0.32), saturate(v * 1.6 - 0.4));
                    half3 target = ground > 0.5 ? dry : fall;
                    half amt = ground > 0.5 ? 0.6 : 1.0;
                    albedo = lerp(albedo, target * lum * 2.0, green * _LG_Autumn * amt);
                }
                if (_LG_Snow > 0.001)
                {
                    half3 grey = lum.xxx;
                    albedo = lerp(albedo, lerp(albedo, grey * half3(0.95, 1.0, 1.05), 0.55), green * _LG_Snow);
                    half up = smoothstep(0.25, 0.55, N.y + (n2 - 0.5) * 0.2 + (n1 - 0.5) * 0.1);
                    albedo = lerp(albedo, half3(0.52, 0.57, 0.68) * (0.92 + n2 * 0.14), up * _LG_Snow);
                }
                return albedo;
            }

            half3 ShadeLight(Light light, half3 N, half wrap)
            {
                half ndl = dot(N, light.direction);
                half w = saturate((ndl + wrap) / (1.0 + wrap));
                w = w * w * (3.0 - 2.0 * w);
                return light.color * w * light.distanceAttenuation * light.shadowAttenuation;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 N = normalize(i.normalWS);

                half3 vcol = i.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    vcol = SRGBToLinear(vcol);   // màu đỉnh được ghi theo sRGB
                #endif
                half3 albedo = _BaseColor.rgb * lerp(half3(1, 1, 1), vcol, _UseVertexColor);
                half3 patterned = LG_Pattern(albedo, i.positionWS, N, _Pattern);
                albedo = lerp(albedo, patterned, _UseVertexColor > 0.5 ? i.color.a : 1.0);   // alpha đỉnh = mức phủ hoa văn (viền đường = 0)
                float n1 = LG_Noise(i.positionWS * 1.6);
                float n2 = LG_Noise(i.positionWS * 5.5 + 17.0);
                albedo *= 1.0 + _Noise * ((n1 - 0.5) * 0.16 + (n2 - 0.5) * 0.07);
                albedo = LG_Season(albedo, i.positionWS, N, n1, n2, abs(_Pattern - 5.0) < 0.5 ? 1.0 : 0.0);
                albedo *= 1.0 - _LG_Wet * 0.22;   // mưa: màu sẫm, đậm hơn
                albedo = lerp(dot(albedo, half3(0.3, 0.59, 0.11)).xxx, albedo, 1.0 + _LG_Wet * 0.12);

                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                half indirectAO = 1.0;
                half directAO = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aof = GetScreenSpaceAmbientOcclusion(suv);
                    indirectAO = aof.indirectAmbientOcclusion;
                    directAO = aof.directAmbientOcclusion;
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                if (_LG_CloudShadow > 0.0)
                {
                    // bóng mây trôi chậm trên mặt đất (chiếu theo hướng nắng)
                    float2 cp = (i.positionWS.xz - mainLight.direction.xz / max(mainLight.direction.y, 0.2) * i.positionWS.y) * 0.035
                              + _Time.y * float2(0.012, 0.006);
                    float cn = LG_Noise(float3(cp, 0.0)) * 0.65 + LG_Noise(float3(cp * 2.3, 3.7)) * 0.35;
                    mainLight.shadowAttenuation *= 1.0 - smoothstep(0.52, 0.7, cn) * _LG_CloudShadow;
                }
                half3 direct = ShadeLight(mainLight, N, _Wrap) * directAO;
                half3 ambient = SampleSH(N) * indirectAO;

                // viền sáng nhẹ (sáng hơn ở phía có nắng chiếu ngang)
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(1.0 - saturate(dot(N, V)), 4.0) * _Rim * saturate(dot(N, mainLight.direction) + 0.6);

                half3 color = albedo * (direct + ambient) + mainLight.color * rim * mainLight.shadowAttenuation;
                if (_LG_Wet > 0.001)
                {
                    // ướt: mặt hướng lên loáng phản chiếu trời, vũng nước loang lổ
                    half up = saturate(N.y);
                    half puddle = smoothstep(0.68, 0.8, LG_Noise(i.positionWS * 0.45 + 7.0)) * smoothstep(0.9, 0.99, N.y);
                    half fres = pow(1.0 - saturate(dot(N, V)), 3.0);
                    half3 skyC = _LG_SkyColor.a > 0.0 ? _LG_SkyColor.rgb : half3(0.6, 0.65, 0.7);
                    color = lerp(color, skyC * (0.35 + ambient * 0.5), _LG_Wet * saturate(fres * up * 0.45 + puddle * 0.3));
                }

                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalWS = N;
                    inputData.normalizedScreenSpaceUV = suv;
                    uint pixelLightCount = (uint)GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint dl = 0; dl < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dl++)
                    {
                        Light d = GetAdditionalLight(dl, i.positionWS, half4(1, 1, 1, 1));
                        color += albedo * ShadeLight(d, N, 0.5);
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                        color += albedo * ShadeLight(light, N, 0.5);
                    LIGHT_LOOP_END
                #endif

                color += _EmissionColor.rgb;
                color = MixFog(color, i.fog);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct AttributesDN
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsDN
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDN vertDN(AttributesDN input)
            {
                VaryingsDN o = (VaryingsDN)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 fragDN(VaryingsDN input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    // ======================================================================
    //  Built-in (dự phòng)
    // ======================================================================
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf WrapLambert fullforwardshadows addshadow vertex:vert
        #pragma target 3.0

        fixed4 _BaseColor;
        half _UseVertexColor;
        half _Noise;
        half _Wrap;
        half4 _EmissionColor;

        struct Input
        {
            float4 vcol;
            float3 worldPos;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcol = v.color;
        }

        float LG_Hash(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        half4 LightingWrapLambert(SurfaceOutput s, half3 lightDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
            half w = saturate((ndl + _Wrap) / (1.0 + _Wrap));
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * w * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            half3 albedo = _BaseColor.rgb * lerp(half3(1, 1, 1), IN.vcol.rgb, _UseVertexColor);
            float n = LG_Hash(floor(IN.worldPos * 1.6));
            albedo *= 1.0 + _Noise * (n - 0.5) * 0.1;
            o.Albedo = albedo;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
