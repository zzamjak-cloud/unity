using UnityEngine;
using UnityEditor;

namespace CAT.UI
{
    [CustomEditor(typeof(TMPCharacterAnimation))]
    public class TMPCharacterAnimationEditor : Editor
    {
        private TMPCharacterAnimation _target;

        private void OnEnable()
        {
            _target = (TMPCharacterAnimation)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ─────────────────────────────────────────────
            // Second Face 지원 안내
            // ─────────────────────────────────────────────

            var outlineEffect = _target.GetComponent<TMPOutlineEffect>();
            if (outlineEffect != null && outlineEffect.EnableSecondFace)
            {
                EditorGUILayout.HelpBox(
                    "✓ TMPOutlineEffect의 Second Face가 감지되었습니다.\n" +
                    "애니메이션이 Second Face에도 자동으로 적용됩니다.",
                    MessageType.Info
                );
                EditorGUILayout.Space();
            }

            // ─────────────────────────────────────────────
            // Preset
            // ─────────────────────────────────────────────

            EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);

            var presetProp = serializedObject.FindProperty("_preset");
            EditorGUILayout.PropertyField(presetProp, new GUIContent("Preset"));

            if (presetProp.objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox("프리셋을 적용하려면 '프리셋 적용' 버튼을 누르세요.", MessageType.Info);

                if (GUILayout.Button("프리셋 적용"))
                {
                    _target.ApplyPreset(presetProp.objectReferenceValue as TMPCharacterAnimationPreset);
                    EditorUtility.SetDirty(_target);
                }

                EditorGUILayout.Space();
            }

            EditorGUILayout.Space();

            // ─────────────────────────────────────────────
            // Playback Controls
            // ─────────────────────────────────────────────

            EditorGUILayout.LabelField("Playback Controls", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("재생 컨트롤은 플레이 모드에서만 작동합니다.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();

                GUI.enabled = !_target.IsPlaying;
                if (GUILayout.Button("▶ Play", GUILayout.Height(30)))
                {
                    _target.Play();
                }

                GUI.enabled = _target.IsPlaying;
                if (GUILayout.Button("⏸ Pause", GUILayout.Height(30)))
                {
                    _target.Pause();
                }

                if (GUILayout.Button("⏹ Stop", GUILayout.Height(30)))
                {
                    _target.Stop();
                }

                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                // 재생 상태 표시
                EditorGUILayout.LabelField("Status", _target.IsPlaying ? "Playing" : "Stopped", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();

            // ─────────────────────────────────────────────
            // Settings
            // ─────────────────────────────────────────────

            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
