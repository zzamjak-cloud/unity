using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
    [CustomEditor(typeof(UIShining))]
    public class UIShiningEditor : Editor
    {
        private UIShining _target;
        private bool _isPlaying;
        private double _startTime;
        private double _prevTime;
        private const double DURATION = 60.0;

        private void OnEnable()
        {
            _target = (UIShining)target;
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_isPlaying)
                StopTest();
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

                if (_isPlaying)
                {
                    double remaining = System.Math.Max(0.0, DURATION - (EditorApplication.timeSinceStartup - _startTime));
                    EditorGUILayout.HelpBox($"에디터 테스트 재생 중... (남은 시간: {remaining:F1}초)", MessageType.Info);

                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("재생 중지", GUILayout.Height(24f)))
                        StopTest();
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("에디터 재생 (60초)", GUILayout.Height(24f)))
                        StartTest();
                    GUI.backgroundColor = Color.white;
                }
            }

            if (serializedObject.ApplyModifiedProperties() && !Application.isPlaying)
                SceneView.RepaintAll();
        }

        private void StartTest()
        {
            if (_isPlaying) return;

            _startTime = EditorApplication.timeSinceStartup;
            _prevTime = _startTime;
            _isPlaying = true;
            _target.ResetProgressToStart();
            EditorApplication.update += EditorUpdate;
        }

        private void StopTest()
        {
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
            if (_target != null)
                _target.ResetProgressToStart();
            Repaint();
        }

        /// <summary>
        /// EditorApplication.update 콜백: timeSinceStartup 기반으로 deltaTime을 계산하여
        /// UIShining.EditorAdvance()를 직접 호출한다. [ExecuteAlways] Update()에 의존하지 않는다.
        /// </summary>
        private void EditorUpdate()
        {
            if (_target == null)
            {
                StopTest();
                return;
            }

            double now = EditorApplication.timeSinceStartup;

            if (now - _startTime >= DURATION)
            {
                StopTest();
                return;
            }

            float dt = (float)(now - _prevTime);
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            _prevTime = now;

            _target.EditorAdvance(dt);

            Repaint();
            SceneView.RepaintAll();
        }
    }
}
