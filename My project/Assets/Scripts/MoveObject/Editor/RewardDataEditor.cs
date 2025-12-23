using UnityEngine;
using UnityEditor;
using DG.Tweening;

namespace MoveObject
{
    [CustomEditor(typeof(RewardData))]
    public class RewardDataEditor : Editor
    {
        private SerializedProperty _rewardID;
        private SerializedProperty _itemPrefab;
        private SerializedProperty _duration;
        private SerializedProperty _moveEase;
        private SerializedProperty _moveCustomCurve;
        private SerializedProperty _spawnStartScale;
        private SerializedProperty _moveStartScale;
        private SerializedProperty _moveEndScale;
        private SerializedProperty _useSpawnAnimation;
        private SerializedProperty _spawnDuration;
        private SerializedProperty _spawnEase;
        private SerializedProperty _spawnCustomCurve;
        private SerializedProperty _arrivalEffect;
        private SerializedProperty _arrivalSound;
        private SerializedProperty _maxSpawnCount;
        private SerializedProperty _minSpawnInterval;
        private SerializedProperty _maxSpawnInterval;
        private SerializedProperty _useBezierPath;
        private SerializedProperty _bezierControlPointOffset;
        private SerializedProperty _spawnRandomRadius;

        private void OnEnable()
        {
            _rewardID = serializedObject.FindProperty("RewardID");
            _itemPrefab = serializedObject.FindProperty("ItemPrefab");
            _duration = serializedObject.FindProperty("Duration");
            _moveEase = serializedObject.FindProperty("MoveEase");
            _moveCustomCurve = serializedObject.FindProperty("MoveCustomCurve");
            _spawnStartScale = serializedObject.FindProperty("SpawnStartScale");
            _moveStartScale = serializedObject.FindProperty("MoveStartScale");
            _moveEndScale = serializedObject.FindProperty("MoveEndScale");
            _useSpawnAnimation = serializedObject.FindProperty("UseSpawnAnimation");
            _spawnDuration = serializedObject.FindProperty("SpawnDuration");
            _spawnEase = serializedObject.FindProperty("SpawnEase");
            _spawnCustomCurve = serializedObject.FindProperty("SpawnCustomCurve");
            _arrivalEffect = serializedObject.FindProperty("ArrivalEffect");
            _arrivalSound = serializedObject.FindProperty("ArrivalSound");
            _maxSpawnCount = serializedObject.FindProperty("MaxSpawnCount");
            _minSpawnInterval = serializedObject.FindProperty("MinSpawnInterval");
            _maxSpawnInterval = serializedObject.FindProperty("MaxSpawnInterval");
            _useBezierPath = serializedObject.FindProperty("UseBezierPath");
            _bezierControlPointOffset = serializedObject.FindProperty("BezierControlPointOffset");
            _spawnRandomRadius = serializedObject.FindProperty("SpawnRandomRadius");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(5);
            DrawHeader("기본 정보");
            EditorGUILayout.PropertyField(_rewardID, new GUIContent("보상 ID"));
            EditorGUILayout.PropertyField(_itemPrefab, new GUIContent("아이템 프리팹"));

            EditorGUILayout.Space(10);
            DrawHeader("이동 설정");
            EditorGUILayout.PropertyField(_duration, new GUIContent("이동 시간"));

            // Ease 드롭다운
            EditorGUILayout.PropertyField(_moveEase, new GUIContent("이동 이징"));

            // INTERNAL_Custom 선택 시 커스텀 곡선 표시
            bool isCustomMove = _moveEase.enumValueIndex == (int)Ease.INTERNAL_Custom;

            if (isCustomMove)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_moveCustomCurve, new GUIContent("커스텀 곡선"));
                EditorGUI.indentLevel--;
            }
            else if (_moveEase.enumValueIndex >= 0)
            {
                // 일반 Ease 프리뷰 표시
                Ease ease = (Ease)_moveEase.enumValueIndex;
                DrawEasePreview(ease, "이동 곡선");
            }

            EditorGUILayout.Space(10);
            DrawHeader("스케일 설정");
            EditorGUILayout.PropertyField(_spawnStartScale, new GUIContent("등장 시작"));
            EditorGUILayout.PropertyField(_moveStartScale, new GUIContent("이동 시작"));
            EditorGUILayout.PropertyField(_moveEndScale, new GUIContent("이동 도착"));

            EditorGUILayout.Space(10);
            DrawHeader("등장 연출");
            EditorGUILayout.PropertyField(_useSpawnAnimation, new GUIContent("등장 애니메이션 사용"));

            if (_useSpawnAnimation.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_spawnDuration, new GUIContent("등장 시간"));

                // Ease 드롭다운
                EditorGUILayout.PropertyField(_spawnEase, new GUIContent("등장 이징"));

                // INTERNAL_Custom 선택 시 커스텀 곡선 표시
                bool isCustomSpawn = _spawnEase.enumValueIndex == (int)Ease.INTERNAL_Custom;

                if (isCustomSpawn)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_spawnCustomCurve, new GUIContent("커스텀 곡선"));
                    EditorGUI.indentLevel--;
                }
                else if (_spawnEase.enumValueIndex >= 0)
                {
                    // 일반 Ease 프리뷰 표시
                    Ease ease = (Ease)_spawnEase.enumValueIndex;
                    DrawEasePreview(ease, "등장 곡선");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);
            DrawHeader("도착 연출");
            EditorGUILayout.PropertyField(_arrivalEffect, new GUIContent("도착 이펙트"));
            EditorGUILayout.PropertyField(_arrivalSound, new GUIContent("도착 사운드"));

            EditorGUILayout.Space(10);
            DrawHeader("최적화 설정");
            EditorGUILayout.PropertyField(_maxSpawnCount, new GUIContent("최대 생성 개수"));
            EditorGUILayout.PropertyField(_minSpawnInterval, new GUIContent("최소 생성 간격"));
            EditorGUILayout.PropertyField(_maxSpawnInterval, new GUIContent("최대 생성 간격"));

            EditorGUILayout.Space(10);
            DrawHeader("고급 설정");
            EditorGUILayout.PropertyField(_spawnRandomRadius, new GUIContent("생성 위치 랜덤 반경"));
            EditorGUILayout.PropertyField(_useBezierPath, new GUIContent("베지어 곡선 사용"));

            if (_useBezierPath.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_bezierControlPointOffset, new GUIContent("제어점 오프셋"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader(string title)
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            EditorGUILayout.LabelField(title, headerStyle);
            Rect rect = GUILayoutUtility.GetLastRect();
            rect.y += rect.height - 1;
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorGUILayout.Space(3);
        }

        private void DrawEasePreview(Ease ease, string label)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);

            Rect previewRect = GUILayoutUtility.GetRect(100, 50);
            previewRect = EditorGUI.IndentedRect(previewRect);

            DrawEaseCurve(previewRect, ease);
            EditorGUILayout.EndVertical();
        }

        private void DrawEaseCurve(Rect rect, Ease ease)
        {
            // 배경
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));

            // 곡선 그리기
            int segments = 50;
            Vector3[] points = new Vector3[segments];

            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / (segments - 1);
                float value = DOVirtual.EasedValue(0, 1, t, ease);

                float x = rect.x + rect.width * t;
                float y = rect.y + rect.height * (1 - value);

                points[i] = new Vector3(x, y, 0);
            }

            Handles.color = new Color(0.3f, 0.8f, 1f);
            Handles.DrawAAPolyLine(2f, points);

            // 그리드
            Handles.color = new Color(1f, 1f, 1f, 0.1f);
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f;
                float x = rect.x + rect.width * t;
                float y = rect.y + rect.height * t;

                Handles.DrawLine(new Vector3(x, rect.y, 0), new Vector3(x, rect.y + rect.height, 0));
                Handles.DrawLine(new Vector3(rect.x, y, 0), new Vector3(rect.x + rect.width, y, 0));
            }
        }
    }
}
