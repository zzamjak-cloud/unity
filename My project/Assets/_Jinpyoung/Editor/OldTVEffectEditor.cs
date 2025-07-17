using UnityEngine;
using UnityEditor;

namespace CAT.Effects
{
    [CustomEditor(typeof(OldTVEffect))]
    public class OldTVEffectEditor : Editor
    {
        // 프로퍼티 캐싱
        private SerializedProperty _noiseTexture;
        private SerializedProperty _noiseIntensity;
        private SerializedProperty _noiseScale;
        private SerializedProperty _scanLineIntensity;
        private SerializedProperty _scanLineCount;
        private SerializedProperty _scanLineThickness;
        private SerializedProperty _verticalJitter;
        private SerializedProperty _horizontalJitter;
        private SerializedProperty _colorBleed;
        private SerializedProperty _colorBleedOffset;
        private SerializedProperty _rollSpeed;

        private bool _showNoiseSettings = true;
        private bool _showScanLineSettings = true;
        private bool _showJitterSettings = true;
        private bool _showColorBleedSettings = true;

        private void OnEnable()
        {
            // 프로퍼티 찾기
            _noiseTexture = serializedObject.FindProperty("noiseTexture");
            _noiseIntensity = serializedObject.FindProperty("noiseIntensity");
            _noiseScale = serializedObject.FindProperty("noiseScale");
            _scanLineIntensity = serializedObject.FindProperty("scanLineIntensity");
            _scanLineCount = serializedObject.FindProperty("scanLineCount");
            _scanLineThickness = serializedObject.FindProperty("scanLineThickness");
            _verticalJitter = serializedObject.FindProperty("verticalJitter");
            _horizontalJitter = serializedObject.FindProperty("horizontalJitter");
            _colorBleed = serializedObject.FindProperty("colorBleed");
            _colorBleedOffset = serializedObject.FindProperty("colorBleedOffset");
            _rollSpeed = serializedObject.FindProperty("rollSpeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_noiseTexture);
            EditorGUILayout.HelpBox("노이즈 텍스처를 지정하지 않으면 자동 생성된 노이즈가 사용됩니다.", MessageType.Info);

            EditorGUILayout.Space();

            // 노이즈 설정
            _showNoiseSettings = EditorGUILayout.Foldout(_showNoiseSettings, "노이즈 설정", true, EditorStyles.foldoutHeader);
            if (_showNoiseSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_noiseIntensity, new GUIContent("노이즈 강도"));
                EditorGUILayout.PropertyField(_noiseScale, new GUIContent("노이즈 스케일"));
                EditorGUILayout.PropertyField(_rollSpeed, new GUIContent("롤링 속도"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 주사선 설정
            _showScanLineSettings = EditorGUILayout.Foldout(_showScanLineSettings, "주사선 설정", true, EditorStyles.foldoutHeader);
            if (_showScanLineSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_scanLineIntensity, new GUIContent("주사선 강도"));
                EditorGUILayout.PropertyField(_scanLineCount, new GUIContent("주사선 개수"));
                EditorGUILayout.PropertyField(_scanLineThickness, new GUIContent("주사선 두께"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 지터 설정
            _showJitterSettings = EditorGUILayout.Foldout(_showJitterSettings, "화면 떨림 설정", true, EditorStyles.foldoutHeader);
            if (_showJitterSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_verticalJitter, new GUIContent("수직 떨림"));
                EditorGUILayout.PropertyField(_horizontalJitter, new GUIContent("수평 떨림"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 색상 번짐 설정
            _showColorBleedSettings = EditorGUILayout.Foldout(_showColorBleedSettings, "색상 번짐 설정", true, EditorStyles.foldoutHeader);
            if (_showColorBleedSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_colorBleed, new GUIContent("색상 번짐 강도"));
                EditorGUILayout.PropertyField(_colorBleedOffset, new GUIContent("색상 번짐 간격"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            // 프리셋 버튼
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("프리셋", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("강한 레트로 TV"))
            {
                ApplyStrongRetroPreset();
            }

            if (GUILayout.Button("미묘한 레트로 TV"))
            {
                ApplySubtleRetroPreset();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("깨진 TV"))
            {
                ApplyBrokenTVPreset();
            }

            if (GUILayout.Button("흑백 TV"))
            {
                ApplyBlackAndWhiteTVPreset();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyStrongRetroPreset()
        {
            Undo.RecordObject(target, "Apply Strong Retro Preset");

            OldTVEffect effect = (OldTVEffect)target;
            effect.noiseIntensity = 0.6f;
            effect.noiseScale = 4f;
            effect.scanLineIntensity = 0.7f;
            effect.scanLineCount = 100f;
            effect.scanLineThickness = 1.2f;
            effect.verticalJitter = 0.008f;
            effect.horizontalJitter = 0.005f;
            effect.colorBleed = 0.25f;
            effect.colorBleedOffset = 0.03f;
            effect.rollSpeed = 1.5f;

            EditorUtility.SetDirty(target);
        }

        private void ApplySubtleRetroPreset()
        {
            Undo.RecordObject(target, "Apply Subtle Retro Preset");

            OldTVEffect effect = (OldTVEffect)target;
            effect.noiseIntensity = 0.2f;
            effect.noiseScale = 3f;
            effect.scanLineIntensity = 0.3f;
            effect.scanLineCount = 120f;
            effect.scanLineThickness = 0.8f;
            effect.verticalJitter = 0.003f;
            effect.horizontalJitter = 0.002f;
            effect.colorBleed = 0.1f;
            effect.colorBleedOffset = 0.01f;
            effect.rollSpeed = 0.8f;

            EditorUtility.SetDirty(target);
        }

        private void ApplyBrokenTVPreset()
        {
            Undo.RecordObject(target, "Apply Broken TV Preset");

            OldTVEffect effect = (OldTVEffect)target;
            effect.noiseIntensity = 0.8f;
            effect.noiseScale = 2f;
            effect.scanLineIntensity = 0.5f;
            effect.scanLineCount = 80f;
            effect.scanLineThickness = 1.5f;
            effect.verticalJitter = 0.02f;
            effect.horizontalJitter = 0.015f;
            effect.colorBleed = 0.4f;
            effect.colorBleedOffset = 0.05f;
            effect.rollSpeed = 3f;

            EditorUtility.SetDirty(target);
        }

        private void ApplyBlackAndWhiteTVPreset()
        {
            Undo.RecordObject(target, "Apply Black and White TV Preset");

            OldTVEffect effect = (OldTVEffect)target;
            effect.noiseIntensity = 0.5f;
            effect.noiseScale = 3.5f;
            effect.scanLineIntensity = 0.6f;
            effect.scanLineCount = 110f;
            effect.scanLineThickness = 1.0f;
            effect.verticalJitter = 0.006f;
            effect.horizontalJitter = 0.003f;
            effect.colorBleed = 0.0f; // 흑백 TV에는 색상 번짐이 없음
            effect.colorBleedOffset = 0.0f;
            effect.rollSpeed = 1.2f;

            EditorUtility.SetDirty(target);

            // 흑백 TV 효과는 셰이더에서 직접 처리하진 않지만 사용자에게 힌트 제공
            EditorUtility.DisplayDialog("흑백 TV 프리셋",
                "흑백 TV 효과를 위해 색상 번짐을 제거했습니다.\n\n완전한 흑백 효과를 위해 대상 이미지/스프라이트의 채도를 0으로 조정하거나 그레이스케일 포스트 프로세싱을 적용하세요.", "확인");
        }
    }
}