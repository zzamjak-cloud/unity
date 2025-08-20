using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
#if UNITY_EDITOR
    [CustomEditor(typeof(SpriteDissolve))]
    public class SpriteDissolveEditor : Editor
    {
        private SpriteDissolve dissolve;
        private SerializedProperty dissolveTex;
        private SerializedProperty dissolveScale;
        private SerializedProperty threshold;

        private void OnEnable()
        {
            dissolve = (SpriteDissolve)target;
            dissolveTex = serializedObject.FindProperty("_dissolveTex");
            dissolveScale = serializedObject.FindProperty("_dissolveScale");
            threshold = serializedObject.FindProperty("_threshold");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(dissolveTex);
            EditorGUILayout.PropertyField(dissolveScale);
            EditorGUILayout.PropertyField(threshold);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.Space(10);

            // Preview Animation Button
            if (GUILayout.Button("Preview Dissolve Animation"))
            {
                PreviewDissolveAnimation();
            }

            // Reset Button
            if (GUILayout.Button("Reset Dissolve"))
            {
                dissolve.Threshold = 0f;
                EditorUtility.SetDirty(target);
            }
        }

        private void PreviewDissolveAnimation()
        {
            dissolve.Threshold = 0f;
            EditorCoroutine.Start(PreviewDissolveCoroutine());
        }

        private System.Collections.IEnumerator PreviewDissolveCoroutine()
        {
            float elapsed = 0f;
            float duration = 1f;

            while (elapsed < duration)
            {
                elapsed += 0.016f; // Approximately 60 FPS
                float t = elapsed / duration;
                dissolve.Threshold = Mathf.Lerp(0f, 1f, t);
                EditorUtility.SetDirty(target);
                yield return null;
            }

            dissolve.Threshold = 1f;
            EditorUtility.SetDirty(target);
        }
    }

    // EditorCoroutine.cs
    public class EditorCoroutine
    {
        public static EditorCoroutine Start(System.Collections.IEnumerator routine)
        {
            EditorCoroutine coroutine = new EditorCoroutine(routine);
            coroutine.Start();
            return coroutine;
        }

        private readonly System.Collections.IEnumerator routine;
        private bool isRunning;

        EditorCoroutine(System.Collections.IEnumerator routine)
        {
            this.routine = routine;
        }

        private void Start()
        {
            isRunning = true;
            EditorApplication.update += Update;
        }

        private void Stop()
        {
            isRunning = false;
            EditorApplication.update -= Update;
        }

        private void Update()
        {
            if (!isRunning) return;

            if (!routine.MoveNext())
            {
                Stop();
            }
        }
    }
#endif
}