using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
    [CustomEditor(typeof(UVPatternFlow))]
    public class UVPatternFlowEditor : Editor
    {
        private SerializedProperty _scrollSpeed;
        private SerializedProperty _playOnEnable;

        private bool _editorPreviewRunning;
        private double _editorPreviewLastTime;

        private void OnEnable()
        {
            _scrollSpeed  = serializedObject.FindProperty("_scrollSpeed");
            _playOnEnable = serializedObject.FindProperty("_playOnEnable");
        }

        private void OnDisable()
        {
            StopEditorPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_scrollSpeed,  new GUIContent("스크롤 속도 (X/Y)"));
            EditorGUILayout.PropertyField(_playOnEnable, new GUIContent("활성화 시 자동 재생"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);

            var flow = (UVPatternFlow)target;

            if (Application.isPlaying)
                DrawPlayModeButtons(flow);
            else
                DrawEditModePreview(flow);
        }

        private void DrawPlayModeButtons(UVPatternFlow flow)
        {
            EditorGUILayout.LabelField("플레이 모드 제어", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !flow.IsPlaying;
                if (GUILayout.Button("▶ Play"))
                    flow.Play();

                GUI.enabled = flow.IsPlaying;
                if (GUILayout.Button("⏸ Pause"))
                    flow.Pause();

                GUI.enabled = true;
                if (GUILayout.Button("■ Stop"))
                    flow.Stop();
            }
        }

        private void DrawEditModePreview(UVPatternFlow flow)
        {
            EditorGUILayout.LabelField("에디터 미리보기", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!_editorPreviewRunning)
                {
                    if (GUILayout.Button("▶ 미리보기 시작"))
                        StartEditorPreview();
                }
                else
                {
                    GUI.color = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("■ 미리보기 중지"))
                        StopEditorPreview();
                    GUI.color = Color.white;
                }

                if (GUILayout.Button("오프셋 초기화", GUILayout.Width(90)))
                {
                    flow.ResetOffset();
                    SceneView.RepaintAll();
                }
            }
        }

        private void StartEditorPreview()
        {
            if (_editorPreviewRunning) return;
            _editorPreviewRunning = true;
            _editorPreviewLastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorPreviewUpdate;
        }

        private void StopEditorPreview()
        {
            if (!_editorPreviewRunning) return;
            _editorPreviewRunning = false;
            EditorApplication.update -= EditorPreviewUpdate;
            (target as UVPatternFlow)?.ResetOffset();
            SceneView.RepaintAll();
        }

        private void EditorPreviewUpdate()
        {
            if (!_editorPreviewRunning) return;

            var flow = target as UVPatternFlow;
            if (flow == null) { StopEditorPreview(); return; }

            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _editorPreviewLastTime);
            _editorPreviewLastTime = now;

            flow.EditorAdvance(dt);
            SceneView.RepaintAll();
            Repaint();
        }
    }
}
