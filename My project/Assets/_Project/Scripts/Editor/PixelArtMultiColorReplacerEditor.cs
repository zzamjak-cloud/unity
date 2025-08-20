using UnityEngine;
using UnityEditor;

namespace CAT.Effects
{
    // 커스텀 셰이더 에디터 클래스
    public class PixelArtMultiColorReplacerEditor : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            // 기본 속성 설정
            Material targetMat = materialEditor.target as Material;

            // 메인 텍스처와 오차범위 속성 표시
            MaterialProperty mainTexProp = FindProperty("_MainTex", properties);
            MaterialProperty toleranceProp = FindProperty("_Tolerance", properties);

            materialEditor.TexturePropertySingleLine(
                new GUIContent("Main Texture", "The texture to apply color replacement to"),
                mainTexProp
            );

            materialEditor.ShaderProperty(toleranceProp, new GUIContent("Color Tolerance", "How closely colors need to match to be replaced (higher = more inclusive)"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Color Replacement Pairs", EditorStyles.boldLabel);

            // 각 색상 페어에 대한 UI 생성
            for (int i = 1; i <= 10; i++)
            {
                string enabledName = "_ColorEnabled" + i;
                string sourceName = "_SourceColor" + i;
                string targetName = "_TargetColor" + i;

                MaterialProperty enabledProp = FindProperty(enabledName, properties);
                MaterialProperty sourceProp = FindProperty(sourceName, properties);
                MaterialProperty targetProp = FindProperty(targetName, properties);

                // 폴더블 섹션 생성
                bool enabled = targetMat.GetFloat(enabledName) > 0.5f;
                string label = "Color Pair " + i + (enabled ? " (Active)" : " (Inactive)");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 활성화 토글 및 제목
                EditorGUILayout.BeginHorizontal();
                bool newEnabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(20));
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                targetMat.SetFloat(enabledName, newEnabled ? 1.0f : 0.0f);

                // 색상 페어가 활성화된 경우에만 표시
                if (newEnabled)
                {
                    // 원본 색상과 대상 색상 필드
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Source Color");
                    Color sourceColor = EditorGUILayout.ColorField(sourceProp.colorValue);
                    targetMat.SetColor(sourceName, sourceColor);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Target Color");
                    Color targetColor = EditorGUILayout.ColorField(targetProp.colorValue);
                    targetMat.SetColor(targetName, targetColor);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }
    }
}