using UnityEngine;
using UnityEditor;

namespace CAT.UI
{
#if UNITY_EDITOR
    [CustomEditor(typeof(UICornerRound))]
    public class UICornerRoundEditor : Editor
    {
        // SerializedProperty 변수들
        private SerializedProperty _topLeftRadius;
        private SerializedProperty _topRightRadius;
        private SerializedProperty _bottomLeftRadius;
        private SerializedProperty _bottomRightRadius;
        private SerializedProperty _size;
        private SerializedProperty _cornerRoundShader;
        private SerializedProperty _useSharedMaterial;

        // 추가: 균일한 radius 사용 여부를 저장할 변수
        private bool _useUniformRadius = false;
        private float _uniformRadius = 0f;

        private void OnEnable()
        {
            // 모든 프로퍼티 null 체크하며 찾기
            _topLeftRadius = serializedObject.FindProperty("topLeftRadius");
            _topRightRadius = serializedObject.FindProperty("topRightRadius");
            _bottomLeftRadius = serializedObject.FindProperty("bottomLeftRadius");
            _bottomRightRadius = serializedObject.FindProperty("bottomRightRadius");
            _size = serializedObject.FindProperty("size");
            _cornerRoundShader = serializedObject.FindProperty("cornerRoundShader");
            _useSharedMaterial = serializedObject.FindProperty("useSharedMaterial");

            // 디버그 로그 추가
            if (_topLeftRadius == null) Debug.LogError("topLeftRadius 프로퍼티를 찾을 수 없습니다");
            if (_topRightRadius == null) Debug.LogError("topRightRadius 프로퍼티를 찾을 수 없습니다");
            if (_bottomLeftRadius == null) Debug.LogError("bottomLeftRadius 프로퍼티를 찾을 수 없습니다");
            if (_bottomRightRadius == null) Debug.LogError("bottomRightRadius 프로퍼티를 찾을 수 없습니다");
            if (_size == null) Debug.LogError("size 프로퍼티를 찾을 수 없습니다");
            if (_cornerRoundShader == null) Debug.LogError("cornerRoundShader 프로퍼티를 찾을 수 없습니다");
            if (_useSharedMaterial == null) Debug.LogError("useSharedMaterial 프로퍼티를 찾을 수 없습니다");

            // 초기 상태 확인 - 모든 반경이 동일하면 균일한 반경 모드로 설정
            CheckUniformRadiusState();
        }

        // 추가: 균일한 반경 상태 확인 메서드
        private void CheckUniformRadiusState()
        {
            if (_topLeftRadius != null && _topRightRadius != null &&
                _bottomLeftRadius != null && _bottomRightRadius != null)
            {
                float tl = _topLeftRadius.floatValue;
                _useUniformRadius = Mathf.Approximately(tl, _topRightRadius.floatValue) &&
                                   Mathf.Approximately(tl, _bottomLeftRadius.floatValue) &&
                                   Mathf.Approximately(tl, _bottomRightRadius.floatValue);

                if (_useUniformRadius)
                {
                    _uniformRadius = tl;
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            UICornerRound component = (UICornerRound)target;

            // 필요한 프로퍼티가 없는 경우 경고 표시
            if (_topLeftRadius == null || _topRightRadius == null || _bottomLeftRadius == null ||
                _bottomRightRadius == null || _size == null)
            {
                EditorGUILayout.HelpBox("필요한 프로퍼티를 찾을 수 없습니다!", MessageType.Error);
                return;
            }

            // 최적화 옵션 표시
            EditorGUILayout.Space();
            //EditorGUILayout.LabelField("Optimization Settings", EditorStyles.boldLabel);

            if (_useSharedMaterial != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(_useSharedMaterial, new GUIContent("Use Shared Material", "Enable to share materials between similar corners (reduces draw calls)"));

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    component.SetUseSharedMaterial(_useSharedMaterial.boolValue);
                }

                if (_useSharedMaterial.boolValue)
                {
                    EditorGUILayout.HelpBox("Materials will be shared between UI elements with identical corner settings. This reduces draw calls but limits dynamic changes.", MessageType.Info);
                }
            }

            // Shader reference field
            EditorGUILayout.Space();
            //EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            if (_cornerRoundShader != null)
            {
                EditorGUILayout.PropertyField(_cornerRoundShader);
            }

            // Size section
            EditorGUILayout.Space();
            //EditorGUILayout.LabelField("Size Settings", EditorStyles.boldLabel);
            if (_size != null)
            {
                EditorGUILayout.PropertyField(_size);
            }

            if (GUILayout.Button("Auto-Set Size from RectTransform"))
            {
                Undo.RecordObject(component, "Auto-Set Size");
                component.AutoSetSize();
            }

            // Radius controls
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Corner Radius Settings", EditorStyles.boldLabel);

            if (_topLeftRadius == null || _topRightRadius == null ||
                _bottomLeftRadius == null || _bottomRightRadius == null)
            {
                EditorGUILayout.HelpBox("반경 프로퍼티를 찾을 수 없습니다!", MessageType.Error);
            }
            else
            {
                // 수정된 부분: Use Uniform Radius 토글 처리
                EditorGUI.BeginChangeCheck();
                _useUniformRadius = EditorGUILayout.Toggle("Use Uniform Radius", _useUniformRadius);

                if (EditorGUI.EndChangeCheck())
                {
                    // 토글 상태가 변경되면 현재 상단 왼쪽 반경을 기준으로 설정
                    if (_useUniformRadius)
                    {
                        _uniformRadius = _topLeftRadius.floatValue;
                        Undo.RecordObject(component, "Switch to Uniform Radius");
                        component.SetUniformRadius(_uniformRadius);
                    }
                }

                float maxRadius = _size != null ? _size.floatValue / 2f : 50f;

                if (_useUniformRadius)
                {
                    // 균일한 반경 모드: 하나의 슬라이더로 모든 반경 조절
                    EditorGUI.BeginChangeCheck();
                    _uniformRadius = EditorGUILayout.Slider("Uniform Radius", _uniformRadius, 0f, maxRadius);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(component, "Change Uniform Radius");
                        component.SetUniformRadius(_uniformRadius);
                    }
                }
                else
                {
                    // 개별 반경 모드: 각 모서리별 슬라이더
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.PropertyField(_topLeftRadius, new GUIContent("Top-Left Radius"));
                    EditorGUILayout.PropertyField(_topRightRadius, new GUIContent("Top-Right Radius"));
                    EditorGUILayout.PropertyField(_bottomLeftRadius, new GUIContent("Bottom-Left Radius"));
                    EditorGUILayout.PropertyField(_bottomRightRadius, new GUIContent("Bottom-Right Radius"));

                    if (EditorGUI.EndChangeCheck())
                    {
                        // 변경된 값들이 최대 반경을 초과하지 않도록 제한
                        _topLeftRadius.floatValue = Mathf.Clamp(_topLeftRadius.floatValue, 0f, maxRadius);
                        _topRightRadius.floatValue = Mathf.Clamp(_topRightRadius.floatValue, 0f, maxRadius);
                        _bottomLeftRadius.floatValue = Mathf.Clamp(_bottomLeftRadius.floatValue, 0f, maxRadius);
                        _bottomRightRadius.floatValue = Mathf.Clamp(_bottomRightRadius.floatValue, 0f, maxRadius);

                        // 각 모서리의 반경이 변경될 때마다 균일한 반경 상태 확인
                        CheckUniformRadiusState();
                    }
                }
            }

            // Quick corner presets
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Square (No Radius)"))
            {
                Undo.RecordObject(component, "Set Square Corners");
                component.SetUniformRadius(0f);
                _useUniformRadius = true;
                _uniformRadius = 0f;
            }

            if (GUILayout.Button("Circle/Oval"))
            {
                Undo.RecordObject(component, "Set Circle Corners");
                float radius = _size != null ? _size.floatValue / 2f : 50f;
                component.SetUniformRadius(radius);
                _useUniformRadius = true;
                _uniformRadius = radius;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rounded (25%)"))
            {
                Undo.RecordObject(component, "Set 25% Rounded Corners");
                float radius = _size != null ? _size.floatValue / 4f : 25f;
                component.SetUniformRadius(radius);
                _useUniformRadius = true;
                _uniformRadius = radius;
            }

            if (GUILayout.Button("Capsule (Left/Right)"))
            {
                Undo.RecordObject(component, "Set Capsule Shape");
                float halfHeight = _size != null ? _size.floatValue / 2f : 50f;
                component.SetCornerRadii(halfHeight, 0f, halfHeight, 0f);
                _useUniformRadius = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Top Rounded"))
            {
                Undo.RecordObject(component, "Set Top Rounded Corners");
                float radius = _size != null ? _size.floatValue / 4f : 25f;
                component.SetCornerRadii(radius, radius, 0f, 0f);
                _useUniformRadius = false;
            }

            if (GUILayout.Button("Bottom Rounded"))
            {
                Undo.RecordObject(component, "Set Bottom Rounded Corners");
                float radius = _size != null ? _size.floatValue / 4f : 25f;
                component.SetCornerRadii(0f, 0f, radius, radius);
                _useUniformRadius = false;
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();

            // Update the shader if any properties changed
            if (GUI.changed)
            {
                component.UpdateShaderProperties();
            }
        }
    }
#endif
}