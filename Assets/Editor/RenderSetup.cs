using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LangGio.EditorTools
{
    /// <summary>
    /// Tự chuyển project sang URP khi mở Unity: tạo asset URP + renderer (có SSAO), gán cho Graphics và mọi mức Quality,
    /// thêm shader của game vào "Always Included". Menu "Làng Gió / Thiết lập lại URP" để tạo lại từ đầu.
    /// </summary>
    [InitializeOnLoad]
    public static class RenderSetup
    {
        const string Dir = "Assets/Settings";
        const string AssetPath = Dir + "/LangGio_URP.asset";
        const string RendererPath = Dir + "/LangGio_Renderer.asset";
        const string PostDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        static RenderSetup()
        {
            EditorApplication.delayCall += () => Setup(false);
        }

        [MenuItem("Làng Gió/Thiết lập lại URP")]
        static void ForceSetup() => Setup(true);

        internal static void Setup(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (asset == null || force)
            {
                if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Settings");
                if (force)
                {
                    AssetDatabase.DeleteAsset(AssetPath);
                    AssetDatabase.DeleteAsset(RendererPath);
                }

                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostDataPath);
                AssetDatabase.CreateAsset(data, RendererPath);
                AddSsao(data);

                asset = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(asset, AssetPath);
                Configure(asset);
                AssetDatabase.SaveAssets();
                Debug.Log("[Làng Gió] Đã tạo URP asset: " + AssetPath);
            }

            if (GraphicsSettings.defaultRenderPipeline != asset) GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);

            IncludeShaders("LangGio/Stylized", "LangGio/SkyGradient", "LangGio/Water");
            FixIconImport();
        }

        /// <summary>Ảnh bộ biểu tượng UI: giữ nền trong suốt, không nén, kẹp mép.</summary>
        static void FixIconImport()
        {
            const string path = "Assets/Resources/UI/icons.png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            bool changed = ti.textureType != TextureImporterType.Default || !ti.alphaIsTransparency || ti.textureCompression != TextureImporterCompression.Uncompressed
                           || ti.wrapMode != TextureWrapMode.Clamp || ti.npotScale != TextureImporterNPOTScale.None;
            if (!changed) return;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Trilinear;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.SaveAndReimport();
        }

        static void Configure(UniversalRenderPipelineAsset asset)
        {
            asset.supportsHDR = true;
            asset.msaaSampleCount = 2;
            asset.shadowDistance = 130f;
            asset.shadowCascadeCount = 4;
            asset.mainLightShadowmapResolution = 2048;
            asset.supportsCameraDepthTexture = true;
            asset.maxAdditionalLightsCount = 8;

            var so = new SerializedObject(asset);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_SoftShadowQuality", 2);          // Medium
            SetInt(so, "m_ColorGradingMode", 1);           // HDR
            SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void AddSsao(UniversalRendererData data)
        {
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";
            AssetDatabase.AddObjectToAsset(ssao, data);
            data.rendererFeatures.Add(ssao);
            AssetDatabase.SaveAssets();

            var so = new SerializedObject(data);
            var map = so.FindProperty("m_RendererFeatureMap");
            if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId))
            {
                map.arraySize = data.rendererFeatures.Count;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // Bóng tối ở khe, góc tường, chân nhà: bán kính lớn hơn mặc định cho cảnh làng (đơn vị mét)
            var sso = new SerializedObject(ssao);
            var st = sso.FindProperty("m_Settings");
            if (st != null)
            {
                SetFloat(st, "Intensity", 1.0f);
                SetFloat(st, "Radius", 0.45f);
                SetFloat(st, "DirectLightingStrength", 0.25f);
                SetFloat(st, "Falloff", 90f);
            }
            sso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ssao);
            EditorUtility.SetDirty(data);
        }

        static void IncludeShaders(params string[] names)
        {
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            bool changed = false;
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) continue;
                bool has = false;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { has = true; break; }
                if (has) continue;
                arr.arraySize++;
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                changed = true;
            }
            // giữ biến thể sương (fog) khi build: scene không bật fog nên chế độ "tự động" sẽ cắt mất
            var fs = so.FindProperty("m_FogStripping");
            if (fs != null && fs.intValue != 1) { fs.intValue = 1; changed = true; }
            foreach (var k in new[] { "m_FogKeepLinear", "m_FogKeepExp", "m_FogKeepExp2" })
            {
                var p = so.FindProperty(k);
                if (p != null && !p.boolValue) { p.boolValue = true; changed = true; }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(SerializedObject so, string name, bool v) { var p = so.FindProperty(name); if (p != null) p.boolValue = v; }
        static void SetInt(SerializedObject so, string name, int v) { var p = so.FindProperty(name); if (p != null) p.intValue = v; }
        static void SetFloat(SerializedProperty parent, string name, float v) { var p = parent.FindPropertyRelative(name); if (p != null) p.floatValue = v; }
    }
}
