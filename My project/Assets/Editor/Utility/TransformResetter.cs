using UnityEngine;
using UnityEditor;

namespace CAT.Utility
{
    [CustomEditor(typeof(Transform))]
    public class TransformResetter : Editor
    {
        private Transform _transform;

        private void OnEnable()
        {
            _transform = (Transform)target;
        }

        public override void OnInspectorGUI()
        {
            // 변경 사항 기록을 위해 직렬화된 객체를 사용합니다.
            // 이렇게 하면 각 필드에 대한 변경이 더 부드럽게 적용됩니다.
            serializedObject.Update();

            // Position 필드와 "R" 버튼
            DrawFieldWithResetButton("Local Position", () => _transform.localPosition = Vector3.zero);

            // Rotation 필드와 "R" 버튼
            DrawFieldWithResetButton("Local Rotation", () => _transform.localRotation = Quaternion.identity);

            // Scale 필드와 "R" 버튼
            DrawFieldWithResetButton("Local Scale", () => _transform.localScale = Vector3.one);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFieldWithResetButton(string label, System.Action resetAction)
        {
            EditorGUILayout.BeginHorizontal();

            // 필드 레이아웃과 컨트롤
            if (label == "Local Position")
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LocalPosition"), new GUIContent("Position"));
            }
            else if (label == "Local Rotation")
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LocalRotation"), new GUIContent("Rotation"));
            }
            else if (label == "Local Scale")
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LocalScale"), new GUIContent("Scale"));
            }

            // "R" 버튼
            if (GUILayout.Button("R", GUILayout.Width(30)))
            {
                Undo.RecordObject(_transform, $"{label} Reset");
                resetAction.Invoke();
                EditorUtility.SetDirty(_transform);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}