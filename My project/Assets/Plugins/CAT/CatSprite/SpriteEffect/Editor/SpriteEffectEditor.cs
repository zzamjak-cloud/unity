using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
    [CustomEditor(typeof(SpriteEffect))]
    [CanEditMultipleObjects]
    public class SpriteEffectEditor : Editor
    {
        private SpriteEffect effect;
        private SerializedProperty targetColor;
        private SerializedProperty lerpValue;
        private SerializedProperty dissolveTex;
        private SerializedProperty dissolveScale;
        private SerializedProperty threshold;
        private SerializedProperty matchSpriteAspect;

        private SpriteEffectPreview preview;

        private void OnEnable()
        {
            effect = (SpriteEffect)target;
            targetColor = serializedObject.FindProperty("targetColor");
            lerpValue = serializedObject.FindProperty("lerpValue");
            dissolveTex = serializedObject.FindProperty("dissolveTex");
            dissolveScale = serializedObject.FindProperty("dissolveScale");
            threshold = serializedObject.FindProperty("threshold");
            matchSpriteAspect = serializedObject.FindProperty("matchSpriteAspect");

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
            EditorGUILayout.PropertyField(matchSpriteAspect);
            bool changed = EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();

            if (changed)
            {
                foreach (Object t in targets)
                    (t as SpriteEffect)?.Apply();
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
                        preview.StartFlash(effect, v => effect.LerpValue = v, lerpValue.floatValue);

                    if (GUILayout.Button("디졸브 (0→1)"))
                        preview.StartRamp(effect, v => effect.Threshold = v, threshold.floatValue);
                }

                if (GUILayout.Button("초기화"))
                {
                    preview.Stop();
                    lerpValue.floatValue = 0f;
                    threshold.floatValue = 0f;
                    serializedObject.ApplyModifiedProperties();

                    foreach (Object t in targets)
                        (t as SpriteEffect)?.RestoreOriginalMaterial();
                }
            }
        }

        private void DrawWarnings()
        {
            EditorGUILayout.Space(4);

            SpriteRenderer renderer = effect.Renderer;
            if (renderer != null && renderer.sprite == null)
            {
                EditorGUILayout.HelpBox(
                    "Sprite Renderer에 Sprite가 없습니다. 아틀라스 UV 보정이 동작하지 않습니다.",
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
                Vector2 scale = dissolveScale.vector2Value;
                if (scale.x > 1f || scale.y > 1f || matchSpriteAspect.boolValue)
                {
                    EditorGUILayout.HelpBox(
                        "Dissolve Texture의 Wrap Mode가 Clamp입니다. 스케일/종횡비 보정으로 UV가 1을 넘으면 " +
                        "가장자리 픽셀이 늘어납니다. Repeat으로 변경하세요.",
                        MessageType.Warning);
                }
            }

            if (effect.GetComponent<SpriteGroupEffect>() != null)
            {
                EditorGUILayout.HelpBox(
                    "같은 오브젝트에 SpriteGroupEffect가 있습니다. 두 컴포넌트가 머티리얼을 서로 덮어쓰므로 " +
                    "동시에 활성화하지 마세요.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "효과가 켜져 있는 동안에만 머티리얼이 공유 이펙트 머티리얼로 교체되고, 값이 0으로 돌아가면 원본으로 복구됩니다.\n" +
                "여러 파츠로 나뉜 캐릭터라면 루트에 SpriteGroupEffect를 쓰세요.",
                MessageType.Info);

            SpriteEffectShaderRegistration.DrawFixupUI();
        }
    }
}
