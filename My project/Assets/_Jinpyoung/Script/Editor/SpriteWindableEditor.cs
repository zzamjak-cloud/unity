using UnityEditor;
using UnityEngine;

namespace CAT.Utility
{
    [CustomEditor(typeof(SpriteWindable))]
    public class SpriteWindableEditor : Editor
    {
        private SpriteWindable _targetScript;
        private bool _isPlaying = false;
        private double _startTime;
        private const float _duration = 10.0f; // 테스트 재생 시간

        private void OnEnable()
        {
            _targetScript = (SpriteWindable)target;
            // 인스펙터가 비활성화될 때 테스트가 멈추도록 보장
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
            _isPlaying = false;
        }

        public override void OnInspectorGUI()
        {
            // 기본 인스펙터 필드들을 그립니다 (Wind Speed, Strength 등).
            DrawDefaultInspector();

            // SpriteWindable 스크립트의 변경사항을 감지합니다.
            if (GUI.changed)
            {
                // 값이 바뀌면 즉시 머티리얼에 반영합니다 (시간은 0으로).
                EditorApplication.delayCall += () => _targetScript.UpdateMaterialProperties();
            }

            EditorGUILayout.Space();

            // 재생 테스트 버튼
            if (GUILayout.Button($"Play Test ({_duration}s)"))
            {
                if (!_isPlaying)
                {
                    _startTime = EditorApplication.timeSinceStartup;
                    EditorApplication.update += EditorUpdate;
                    _isPlaying = true;
                }
            }
        }

        // 에디터의 업데이트 루프에 등록될 함수
        private void EditorUpdate()
        {
            if (_targetScript == null)
            {
                EditorApplication.update -= EditorUpdate;
                _isPlaying = false;
                return;
            }

            double elapsedTime = EditorApplication.timeSinceStartup - _startTime;

            if (elapsedTime > _duration)
            {
                // 테스트 시간이 끝나면 업데이트 루프에서 제거하고 종료
                EditorApplication.update -= EditorUpdate;
                _isPlaying = false;
                // 효과를 초기 상태(시간 0)로 리셋
                _targetScript.UpdateMaterialProperties(0);
                return;
            }

            // 경과 시간을 쉐이더의 _CustomTime으로 전달하여 애니메이션 효과를 냅니다.
            _targetScript.UpdateMaterialProperties((float)elapsedTime);
        }
    }
}