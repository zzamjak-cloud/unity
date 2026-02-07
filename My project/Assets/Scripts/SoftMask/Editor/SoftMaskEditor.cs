using UnityEditor;
using UnityEngine;

namespace CAT.UI
{
#if UNITY_EDITOR
    [CustomEditor(typeof(SoftMask))]
    public class SoftMaskEditor : Editor
    {
        private SerializedProperty showMaskGraphicProp;
        private SerializedProperty softnessProp;
        private SerializedProperty invertMaskProp;

        private void OnEnable()
        {
            showMaskGraphicProp = serializedObject.FindProperty("_showMaskGraphic");
            softnessProp = serializedObject.FindProperty("_softness");
            invertMaskProp = serializedObject.FindProperty("_invertMask");

            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            if (target == null) return;
            SoftMask mask = (SoftMask)target;
            mask.CheckForChildChanges();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SoftMask softMask = (SoftMask)target;

            // UI Graphic 확인
            var graphic = softMask.GetComponent<UnityEngine.UI.Graphic>();
            if (graphic == null)
            {
                EditorGUILayout.HelpBox("UI.Graphic 컴포넌트가 필요합니다. (Image, RawImage 등)", MessageType.Warning);
                return;
            }

            // 컴포넌트 정보 박스
            string graphicType = graphic.GetType().Name;
            EditorGUILayout.HelpBox($"마스크 소스: {graphicType} (자신의 알파 채널)", MessageType.None);

            // 중첩 마스크 정보
            if (softMask.ParentSoftMask != null)
            {
                EditorGUILayout.HelpBox(
                    $"중첩 마스크: 부모 [{softMask.ParentSoftMask.gameObject.name}]의 마스크도 함께 적용됩니다.",
                    MessageType.Info
                );
            }

            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();

            // Mask Settings
            EditorGUILayout.LabelField("Mask Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(
                showMaskGraphicProp,
                new GUIContent("Show Mask Graphic", "마스크 이미지를 렌더링할지 여부 (체크 해제 시 투명하게 숨김, 마스킹은 유지)")
            );

            EditorGUILayout.Space(3);

            EditorGUILayout.Slider(
                softnessProp,
                0f, 1f,
                new GUIContent("Softness", "마스크 엣지의 부드러운 정도 (0 = 하드 엣지, 1 = 매우 부드러움)")
            );

            EditorGUILayout.PropertyField(
                invertMaskProp,
                new GUIContent("Invert Mask", "마스크 반전 (밝은 영역과 어두운 영역 교환)")
            );

            EditorGUI.indentLevel--;

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);

                if (PrefabUtility.IsPartOfAnyPrefab(target))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
            }

            EditorGUILayout.Space(10);

            // 상태 정보
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Masked Children", softMask.MaskedChildCount);
            EditorGUILayout.TextField("Shared Material", softMask.MaskedChildCount > 0 ? "1 (공유)" : "없음");
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);

            // 버튼
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset"))
            {
                Undo.RecordObject(target, "Reset SoftMask Values");

                showMaskGraphicProp.boolValue = true;
                softnessProp.floatValue = 0.1f;
                invertMaskProp.boolValue = false;

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);

                softMask.RestoreChildrenMaterials();
                softMask.ApplyMaskToChildren();
            }

            if (GUILayout.Button("Reapply"))
            {
                softMask.RestoreChildrenMaterials();
                softMask.ApplyMaskToChildren();
            }

            EditorGUILayout.EndHorizontal();

            // 도움말
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "알파 채널 기반 SoftMask (1-Pass 렌더링)\n\n" +
                "- 부모: Mask 역할 (자신의 UI Graphic 알파가 마스킹 영역)\n" +
                "- 자식: 부모 마스크 내에서만 렌더링 (자동 적용)\n" +
                "- 이동/회전/스케일 변경 시 동적 갱신\n" +
                "- 중첩 SoftMask 지원 (최대 2단계)\n" +
                "- SoftMask당 1개 공유 Material (배칭 최적화)\n" +
                "- Atlas 스프라이트 UV 자동 보정",
                MessageType.Info
            );
        }

        private void OnSceneGUI()
        {
            SoftMask mask = (SoftMask)target;

            if (Event.current.type == EventType.Layout)
            {
                mask.CheckForChildChanges();
            }
        }
    }
#endif
}
