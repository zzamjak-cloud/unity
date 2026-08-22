using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
    [CustomEditor(typeof(SpriteGroupTint))]
    [CanEditMultipleObjects]
    public class SpriteGroupTintEditor : Editor
    {
        private SerializedProperty tintColor;
        private SerializedProperty includeMeshRenderers;

        private void OnEnable()
        {
            tintColor = serializedObject.FindProperty("tintColor");
            includeMeshRenderers = serializedObject.FindProperty("includeMeshRenderers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 색을 바꾸기 전에 자식 렌더러까지 Undo 에 기록해야 Ctrl+Z 로 원래 색이 복원된다.
            EditorGUI.BeginChangeCheck();
            Color newTint = EditorGUILayout.ColorField(
                new GUIContent("Tint Color", "자식의 원본 색에 곱해지는 색"), tintColor.colorValue);

            if (EditorGUI.EndChangeCheck())
            {
                RecordTargetsAndChildren("Change Sprite Group Tint");
                ApplyToAll(t => t.SetTintColor(newTint));
                return;
            }

            EditorGUILayout.PropertyField(includeMeshRenderers);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent("자식 렌더러 목록 갱신",
                "런타임/에디터에서 자식을 추가·삭제한 뒤 목록을 다시 수집한다.")))
            {
                RecordTargetsAndChildren("Refresh Sprite Group Tint Renderers");
                ApplyToAll(t => t.RefreshRenderers());
            }

            if (GUILayout.Button(new GUIContent("원본 색으로 복원",
                "틴트를 흰색으로 되돌리고 자식을 저장된 원본 색으로 되돌린다.")))
            {
                RecordTargetsAndChildren("Restore Sprite Group Tint Base Colors");
                ApplyToAll(t => t.RestoreBaseColors());
            }

            if (GUILayout.Button(new GUIContent("현재 자식 색을 원본으로 캡처",
                "지금 보이는 자식 색을 새 원본으로 확정하고 틴트를 흰색으로 초기화한다.")))
            {
                RecordTargetsAndChildren("Capture Sprite Group Tint Base Colors");
                ApplyToAll(t => t.CaptureCurrentAsBaseColors());
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "자식 색 = 원본 색 x Tint Color.\n" +
                "Animator 가 tintColor 를 애니메이션하는 경우 스크립트로 설정한 값은 Animator 값에 덮어써진다.",
                MessageType.Info);
        }

        private void RecordTargetsAndChildren(string undoName)
        {
            foreach (Object obj in targets)
            {
                var groupTint = obj as SpriteGroupTint;
                if (groupTint == null) continue;

                Undo.RecordObjects(groupTint.CollectUndoTargets(), undoName);
            }
        }

        private void ApplyToAll(System.Action<SpriteGroupTint> action)
        {
            foreach (Object obj in targets)
            {
                var groupTint = obj as SpriteGroupTint;
                if (groupTint == null) continue;

                action(groupTint);
                EditorUtility.SetDirty(groupTint);
            }
        }
    }
}
