using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
    [CustomEditor(typeof(SpriteGroupEffect))]
    [CanEditMultipleObjects]
    public class SpriteGroupEffectEditor : Editor
    {
        private SpriteGroupEffect group;
        private SerializedProperty targetColor;
        private SerializedProperty lerpValue;
        private SerializedProperty dissolveTex;
        private SerializedProperty dissolveScale;
        private SerializedProperty threshold;
        private SerializedProperty includeInactive;

        private SpriteEffectPreview preview;

        private void OnEnable()
        {
            group = (SpriteGroupEffect)target;
            targetColor = serializedObject.FindProperty("targetColor");
            lerpValue = serializedObject.FindProperty("lerpValue");
            dissolveTex = serializedObject.FindProperty("dissolveTex");
            dissolveScale = serializedObject.FindProperty("dissolveScale");
            threshold = serializedObject.FindProperty("threshold");
            includeInactive = serializedObject.FindProperty("includeInactive");

            preview = new SpriteEffectPreview(Repaint);
        }

        private void OnDisable()
        {
            preview.Stop();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Color Lerp", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetColor);
            EditorGUILayout.PropertyField(lerpValue, new GUIContent("Lerp Value"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Dissolve", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(dissolveTex);
            EditorGUILayout.PropertyField(dissolveScale);
            EditorGUILayout.PropertyField(threshold);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Group", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(includeInactive);
            bool changed = EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();

            if (changed)
            {
                foreach (Object t in targets)
                    (t as SpriteGroupEffect)?.Apply();
            }

            EditorGUILayout.LabelField("대상 렌더러", $"{group.RendererCount} 개");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("렌더러 다시 수집"))
                {
                    foreach (Object t in targets)
                        (t as SpriteGroupEffect)?.RefreshRenderers();
                }

                if (GUILayout.Button("패턴 범위 재계산"))
                {
                    foreach (Object t in targets)
                        (t as SpriteGroupEffect)?.RecalculateBounds();
                }
            }

            EditorGUILayout.Space(8);
            DrawPreviewSection();
            DrawWarnings();
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (preview.Running)
                {
                    if (GUILayout.Button("프리뷰 중지"))
                        preview.Stop();
                }
                else
                {
                    if (GUILayout.Button("번쩍임 (0→1→0)"))
                        preview.StartFlash(group, v => group.LerpValue = v, lerpValue.floatValue);

                    if (GUILayout.Button("디졸브 (0→1)"))
                        preview.StartRamp(group, v => group.Threshold = v, threshold.floatValue);
                }

                if (GUILayout.Button("초기화"))
                {
                    preview.Stop();
                    lerpValue.floatValue = 0f;
                    threshold.floatValue = 0f;
                    serializedObject.ApplyModifiedProperties();

                    foreach (Object t in targets)
                        (t as SpriteGroupEffect)?.RestoreOriginalMaterials();
                }
            }
        }

        private void DrawWarnings()
        {
            EditorGUILayout.Space(4);

            if (group.RendererCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "자식에 SpriteRenderer가 없습니다. 캐릭터 루트에 붙였는지 확인하세요.",
                    MessageType.Warning);
            }

            if (threshold.floatValue > 0f && dissolveTex.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Dissolve Texture가 비어 있습니다. 셰이더 기본값(회색)이 쓰여 Threshold 0.5 부근에서 한 번에 사라집니다.",
                    MessageType.Warning);
            }
            else if (dissolveTex.objectReferenceValue is Texture2D tex && tex.wrapMode == TextureWrapMode.Clamp)
            {
                EditorGUILayout.HelpBox(
                    "Dissolve Texture의 Wrap Mode가 Clamp입니다. 그룹 바운즈 밖이나 Scale 1 초과 구간에서 " +
                    "가장자리 픽셀이 늘어납니다. Repeat 권장.",
                    MessageType.Warning);
            }

            if (group.GetComponent<SpriteEffect>() != null)
            {
                EditorGUILayout.HelpBox(
                    "같은 오브젝트에 SpriteEffect가 있습니다. 두 컴포넌트가 머티리얼을 서로 덮어쓰므로 " +
                    "동시에 활성화하지 마세요.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "효과가 켜져 있는 동안 자식의 머티리얼이 그룹 공유 머티리얼로 교체됩니다. " +
                "자식에 다른 커스텀 셰이더를 쓰고 있다면 그동안 무시됩니다.\n" +
                "디졸브 패턴은 그룹 로컬 공간 기준이라 파츠마다 따로 녹지 않고 캐릭터 전체에 하나로 이어집니다.",
                MessageType.Info);

            SpriteEffectShaderRegistration.DrawFixupUI();
        }
    }
}
