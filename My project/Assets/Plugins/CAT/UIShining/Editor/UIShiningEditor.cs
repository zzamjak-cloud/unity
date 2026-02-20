using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
    [CustomEditor(typeof(UIShining))]
    public class UIShiningEditor : Editor
    {
        private SerializedProperty _editorTestRunningProp;

        private void OnEnable()
        {
            _editorTestRunningProp = serializedObject.FindProperty("_editorTestRunning");
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            if (!Application.isPlaying)
            {
                EditorGUILayout.Space(4f);
                bool running = _editorTestRunningProp != null && _editorTestRunningProp.boolValue;
                string label = running ? "재생 중지" : "에디터 재생 (60초)";
                if (GUILayout.Button(label, GUILayout.Height(24f)))
                {
                    if (!running)
                    {
                        Undo.RecordObject(target, "Start UIShining Editor Test");
                        _editorTestRunningProp.boolValue = true;
                        var startTimeProp = serializedObject.FindProperty("_editorTestStartTime");
                        if (startTimeProp != null)
                            startTimeProp.doubleValue = EditorApplication.timeSinceStartup;
                        serializedObject.ApplyModifiedProperties();
                    }
                    else
                    {
                        Undo.RecordObject(target, "Stop UIShining Editor Test");
                        var uishining = (UIShining)target;
                        uishining.ResetProgressToStart();
                        _editorTestRunningProp.boolValue = false;
                        serializedObject.ApplyModifiedProperties();
                    }
                    SceneView.RepaintAll();
                }
                if (running)
                {
                    var startTimeProp = serializedObject.FindProperty("_editorTestStartTime");
                    if (startTimeProp != null)
                    {
                        double elapsed = EditorApplication.timeSinceStartup - startTimeProp.doubleValue;
                        double remaining = System.Math.Max(0.0, 60.0 - elapsed);
                        EditorGUILayout.HelpBox($"에디터 테스트 재생 중... (남은 시간: {remaining:F1}초)", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("에디터 테스트 재생 중... (60초 후 자동 중지)", MessageType.Info);
                    }
                }
            }

            if (serializedObject.ApplyModifiedProperties() && !Application.isPlaying)
                SceneView.RepaintAll();
        }
    }
}
