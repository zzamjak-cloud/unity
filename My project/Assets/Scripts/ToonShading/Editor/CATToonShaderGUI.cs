using UnityEditor;
using UnityEngine;

namespace CAT.Toon.Editor
{
    /// <summary>
    /// CAT/Toon/ToonLit 머티리얼 인스펙터. 기능별로 접이식 그룹을 나눠 표시합니다.
    /// </summary>
    public class CATToonShaderGUI : ShaderGUI
    {
        private const string PrefsPrefix = "CAT.Toon.ShaderGUI.";

        private MaterialProperty[] m_Properties;
        private MaterialEditor     m_Editor;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            m_Editor     = materialEditor;
            m_Properties = properties;

            var material = materialEditor.target as Material;
            if (material == null)
                return;

            EditorGUIUtility.labelWidth = 0f;

            if (Group("Base", "기본"))
            {
                TextureProp("_BaseMap", "_BaseColor", showTiling: true);
                Prop("_AlphaClip");
                using (new EditorGUI.DisabledScope(!IsOn(material, "_ALPHATEST_ON")))
                    Prop("_Cutoff");
                TextureProp("_BumpMap");
                Prop("_NormalScale");
                Prop("_Cull");
            }

            if (Group("Tone", "2톤 셰이딩"))
            {
                Prop("_ShadeColor");
                Prop("_ShadeThreshold");
                Prop("_ShadeSmooth");
                Prop("_ShadeIntensity");
                EditorGUILayout.Space(2f);
                Prop("_HalfLambert");
                Prop("_ReceiveShadowStrength");
                Prop("_OcclusionStrength");
                Prop("_AmbientStrength");

                EditorGUILayout.Space(4f);
                Prop("_MidToneEnabled");
                using (new EditorGUI.DisabledScope(!IsOn(material, "_MIDTONE_ON")))
                {
                    Prop("_MidColor");
                    Prop("_MidThreshold");
                    Prop("_MidSmooth");
                }
            }

            if (Group("Specular", "툰 스페큘러"))
            {
                Prop("_SpecularEnabled");
                using (new EditorGUI.DisabledScope(!IsOn(material, "_TOONSPECULAR_ON")))
                {
                    Prop("_SpecularColor");
                    Prop("_SpecularSize");
                    Prop("_SpecularSmooth");
                }
            }

            if (Group("Rim", "림 라이트 (뷰 기준)"))
            {
                Prop("_RimEnabled");
                using (new EditorGUI.DisabledScope(!IsOn(material, "_RIM_ON")))
                {
                    Prop("_RimColor");
                    Prop("_RimWidth");
                    Prop("_RimSmooth");
                    Prop("_RimLightAlign");
                }
            }

            if (Group("Sketch", "스케치 해칭"))
            {
                Prop("_SketchEnabled");
                using (new EditorGUI.DisabledScope(!IsOn(material, "_SKETCH_ON")))
                {
                    Prop("_SketchColor");
                    Prop("_SketchStrength");
                    Prop("_SketchScale");
                    Prop("_SketchAngle");
                    Prop("_SketchWidth");
                    Prop("_SketchUseTexture");
                    TextureProp("_SketchMap");
                }
            }

            if (Group("Emission", "이미시브"))
            {
                Prop("_EmissionEnabled");
                using (new EditorGUI.DisabledScope(!IsOn(material, "_EMISSION")))
                {
                    TextureProp("_EmissionMap", "_EmissionColor");
                }
            }

            if (Group("Advanced", "고급"))
            {
                materialEditor.EnableInstancingField();
                materialEditor.RenderQueueField();
                materialEditor.DoubleSidedGIField();
            }
        }

        // -------------------------------------------------------------------
        private bool Group(string key, string label)
        {
            string prefsKey = PrefsPrefix + key;
            bool   expanded = EditorPrefs.GetBool(prefsKey, true);

            EditorGUILayout.Space(4f);
            bool newExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, label);
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (newExpanded != expanded)
                EditorPrefs.SetBool(prefsKey, newExpanded);

            return newExpanded;
        }

        private void Prop(string name)
        {
            var p = FindProperty(name, m_Properties, false);
            if (p != null)
                m_Editor.ShaderProperty(p, p.displayName);
        }

        /// <summary>텍스처 슬롯을 (선택적으로) 컬러와 한 줄로 묶어 그린다.</summary>
        private void TextureProp(string texName, string colorName = null, bool showTiling = false)
        {
            var tex = FindProperty(texName, m_Properties, false);
            if (tex == null)
                return;

            var color = colorName != null ? FindProperty(colorName, m_Properties, false) : null;
            if (color != null)
                m_Editor.TexturePropertySingleLine(new GUIContent(tex.displayName), tex, color);
            else
                m_Editor.TexturePropertySingleLine(new GUIContent(tex.displayName), tex);

            if (showTiling)
                m_Editor.TextureScaleOffsetProperty(tex);
        }

        private static bool IsOn(Material material, string keyword) => material.IsKeywordEnabled(keyword);

        /// <summary>
        /// 노멀맵 슬롯은 토글이 아니라 텍스처 유무로 키워드를 결정한다.
        /// 머티리얼이 변경될 때마다 Unity 가 호출한다.
        /// </summary>
        public override void ValidateMaterial(Material material)
        {
            bool hasNormalMap = material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null;
            if (hasNormalMap)
                material.EnableKeyword("_NORMALMAP");
            else
                material.DisableKeyword("_NORMALMAP");

            bool alphaClip = material.IsKeywordEnabled("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
            material.renderQueue = alphaClip
                ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest
                : (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }
    }
}
