#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CAT.UI
{
    [CustomEditor(typeof(UICornerRoundMask))]
    public class UICornerRoundMaskEditor : Editor
    {
        private SerializedProperty uniformCornersProperty;
        private SerializedProperty cornerRadiusProperty;
        private SerializedProperty topLeftRadiusProperty;
        private SerializedProperty topRightRadiusProperty;
        private SerializedProperty bottomLeftRadiusProperty;
        private SerializedProperty bottomRightRadiusProperty;

        private void OnEnable()
        {
            uniformCornersProperty = serializedObject.FindProperty("uniformCorners");
            cornerRadiusProperty = serializedObject.FindProperty("cornerRadius");
            topLeftRadiusProperty = serializedObject.FindProperty("topLeftRadius");
            topRightRadiusProperty = serializedObject.FindProperty("topRightRadius");
            bottomLeftRadiusProperty = serializedObject.FindProperty("bottomLeftRadius");
            bottomRightRadiusProperty = serializedObject.FindProperty("bottomRightRadius");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // EditorGUILayout.Space();
            // EditorGUILayout.LabelField("라운드 코너 마스크 설정", EditorStyles.boldLabel);

            // Uniform 모드 토글
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(uniformCornersProperty, new GUIContent("Apply uniform"));
            bool uniformChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.Space();

            if (uniformCornersProperty.boolValue)
            {
                // Uniform 모드: 하나의 슬라이더로 모든 코너 조절
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(cornerRadiusProperty, new GUIContent("Radius"));
                if (EditorGUI.EndChangeCheck() || uniformChanged)
                {
                    float value = cornerRadiusProperty.floatValue;
                    topLeftRadiusProperty.floatValue = value;
                    topRightRadiusProperty.floatValue = value;
                    bottomLeftRadiusProperty.floatValue = value;
                    bottomRightRadiusProperty.floatValue = value;
                }
            }
            else
            {
                // 개별 모드: 각 코너별 슬라이더
                //EditorGUILayout.LabelField("개별 코너 설정", EditorStyles.boldLabel);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Top-Left", GUILayout.Width(80));
                EditorGUILayout.PropertyField(topLeftRadiusProperty, GUIContent.none);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Top-Right", GUILayout.Width(80));
                EditorGUILayout.PropertyField(topRightRadiusProperty, GUIContent.none);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Bottom-Left", GUILayout.Width(80));
                EditorGUILayout.PropertyField(bottomLeftRadiusProperty, GUIContent.none);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Bottom-Right", GUILayout.Width(80));
                EditorGUILayout.PropertyField(bottomRightRadiusProperty, GUIContent.none);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                // 모든 코너 동일하게 설정하는 버튼
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("모든 코너 설정"))
                {
                    // 팝업 메뉴로 값 선택
                    GenericMenu menu = new GenericMenu();
                    for (int i = 0; i <= 50; i += 5)
                    {
                        int value = i;
                        menu.AddItem(new GUIContent(i.ToString()), false, () => {
                            topLeftRadiusProperty.floatValue = value;
                            topRightRadiusProperty.floatValue = value;
                            bottomLeftRadiusProperty.floatValue = value;
                            bottomRightRadiusProperty.floatValue = value;
                            cornerRadiusProperty.floatValue = value;
                            serializedObject.ApplyModifiedProperties();

                            // 바로 적용
                            UICornerRoundMask maskComponent = (UICornerRoundMask)target;
                            maskComponent.TopLeftRadius = value;
                            maskComponent.TopRightRadius = value;
                            maskComponent.BottomLeftRadius = value;
                            maskComponent.BottomRightRadius = value;

                            EditorUtility.SetDirty(target);
                        });
                    }
                    menu.ShowAsContext();
                }

                if (GUILayout.Button("값 복사"))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Top-Left 값으로 모두 설정"), false, () => {
                        float value = topLeftRadiusProperty.floatValue;
                        topRightRadiusProperty.floatValue = value;
                        bottomLeftRadiusProperty.floatValue = value;
                        bottomRightRadiusProperty.floatValue = value;
                        cornerRadiusProperty.floatValue = value;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    });
                    menu.AddItem(new GUIContent("Top-Right 값으로 모두 설정"), false, () => {
                        float value = topRightRadiusProperty.floatValue;
                        topLeftRadiusProperty.floatValue = value;
                        bottomLeftRadiusProperty.floatValue = value;
                        bottomRightRadiusProperty.floatValue = value;
                        cornerRadiusProperty.floatValue = value;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    });
                    menu.AddItem(new GUIContent("Bottom-Left 값으로 모두 설정"), false, () => {
                        float value = bottomLeftRadiusProperty.floatValue;
                        topLeftRadiusProperty.floatValue = value;
                        topRightRadiusProperty.floatValue = value;
                        bottomRightRadiusProperty.floatValue = value;
                        cornerRadiusProperty.floatValue = value;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    });
                    menu.AddItem(new GUIContent("Bottom-Right 값으로 모두 설정"), false, () => {
                        float value = bottomRightRadiusProperty.floatValue;
                        topLeftRadiusProperty.floatValue = value;
                        topRightRadiusProperty.floatValue = value;
                        bottomLeftRadiusProperty.floatValue = value;
                        cornerRadiusProperty.floatValue = value;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    });
                    menu.ShowAsContext();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("이 컴포넌트는 Image와 Mask 컴포넌트를 필요로 합니다.\n라운드 코너를 가진 마스크로 하위 UI 요소들을 클리핑합니다.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();

            // 값이 변경되면 실시간으로 화면 갱신
            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
                SceneView.RepaintAll();
            }
        }
    }
}
#endif