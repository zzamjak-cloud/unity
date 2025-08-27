using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
#if UNITY_EDITOR
    [CustomEditor(typeof(ColorReplace))]
    public class ColorReplaceEditor : Editor
    {
        private ColorReplace colorReplace;
        private SerializedProperty color;
        private SerializedProperty hsvRangeMin;
        private SerializedProperty hsvRangeMax;
        private SerializedProperty hsvAdjust;

        private void OnEnable()
        {
            colorReplace = (ColorReplace)target;
            color = serializedObject.FindProperty("_color");
            hsvRangeMin = serializedObject.FindProperty("_hsvRangeMin");
            hsvRangeMax = serializedObject.FindProperty("_hsvRangeMax");
            hsvAdjust = serializedObject.FindProperty("_hsvAdjust");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            // Display renderer type info
            Component renderer = ((ColorReplace)target).GetComponent<SpriteRenderer>();
            bool isUIComponent = false;
            if (renderer == null)
            {
                renderer = ((ColorReplace)target).GetComponent<UnityEngine.UI.Graphic>();
                isUIComponent = renderer != null;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Renderer Type");
            EditorGUILayout.LabelField(isUIComponent ? "UI Graphic" : "Sprite Renderer", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 메인 컬러 필드 (의미있게 활용)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUILayout.ColorField(new GUIContent("Replace Color", "선택한 색상을 기준으로 HSV 범위를 자동 설정합니다"), color.colorValue);
            if (EditorGUI.EndChangeCheck())
            {
                color.colorValue = newColor;
                // 색상이 변경되면 HSV 범위를 자동으로 설정
                SetHSVRangeFromColor(newColor);
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("정확한 색상만", GUILayout.Width(100)))
            {
                SetHSVRangeFromColor(color.colorValue, 0.02f); // 매우 좁은 범위
            }
            if (GUILayout.Button("유사한 색상", GUILayout.Width(100)))
            {
                SetHSVRangeFromColor(color.colorValue, 0.1f); // 중간 범위
            }
            if (GUILayout.Button("넓은 범위", GUILayout.Width(100)))
            {
                SetHSVRangeFromColor(color.colorValue, 0.2f); // 넓은 범위
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("HSV Range", EditorStyles.boldLabel);

            // 툴팁 추가
            EditorGUILayout.HelpBox("HSV Range defines which hue values will be affected. Set Min and Max to target specific color ranges.", MessageType.Info);

            // HSV 색상 다이어그램 (개선된 버전 - 어두운 표시)
            DrawImprovedHSVDiagram(hsvRangeMin.floatValue, hsvRangeMax.floatValue);

            // HSV Range Min/Max 슬라이더
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            float newMinValue = EditorGUILayout.Slider(new GUIContent("Min Range", "Minimum HSV range (0.0 ~ 1.0)"), hsvRangeMin.floatValue, 0f, 1f);
            float newMaxValue = EditorGUILayout.Slider(new GUIContent("Max Range", "Maximum HSV range (0.0 ~ 1.0)"), hsvRangeMax.floatValue, 0f, 1f);
            EditorGUILayout.EndVertical();

            // 슬라이더 값이 변경되었으면 SerializedProperty 업데이트
            if (Mathf.Abs(newMinValue - hsvRangeMin.floatValue) > 0.001f)
            {
                hsvRangeMin.floatValue = newMinValue;
            }
            if (Mathf.Abs(newMaxValue - hsvRangeMax.floatValue) > 0.001f)
            {
                hsvRangeMax.floatValue = newMaxValue;
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("HSV Adjust", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            Vector4 currentAdjust = hsvAdjust.vector4Value;
            currentAdjust.x = EditorGUILayout.Slider(new GUIContent("Hue", "Adjusts color hue"), currentAdjust.x, -1f, 1f);
            currentAdjust.y = EditorGUILayout.Slider(new GUIContent("Saturation", "Adjusts color saturation"), currentAdjust.y, -1f, 1f);
            currentAdjust.z = EditorGUILayout.Slider(new GUIContent("Value (Brightness)", "Adjusts brightness"), currentAdjust.z, -1f, 1f);
            currentAdjust.w = EditorGUILayout.Slider(new GUIContent("Alpha", "Adjusts transparency"), currentAdjust.w, -1f, 1f);

            EditorGUI.indentLevel--;
            hsvAdjust.vector4Value = currentAdjust;

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                
                // This will trigger the OnValidate method in the component for live preview
                if (PrefabUtility.IsPartOfAnyPrefab(target))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
            }

            EditorGUILayout.Space(10);

            // Reset Button (Quick Presets 기능 제거, Reset All Values만 남김)
            if (GUILayout.Button("Reset All Values"))
            {
                Undo.RecordObject(target, "Reset ColorReplace Values");

                color.colorValue = Color.black;
                hsvRangeMin.floatValue = 0f;
                hsvRangeMax.floatValue = 1f;
                hsvAdjust.vector4Value = Vector4.zero;

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }

            // Clear cache button (only in play mode)
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Clear Material Cache"))
                {
                    ColorReplace.ClearMaterialCache();
                    EditorUtility.DisplayDialog("Cache Cleared", "Material cache has been cleared.", "OK");
                }
            }

            // Show info about material sharing for optimization
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Objects with identical ColorReplace settings share materials to optimize draw calls. Changes made in play mode affect all objects using the same settings.", MessageType.Info);
        }

        /// <summary>
        /// 개선된 HSV 다이어그램을 그립니다 (어두운 색상으로 범위 표시)
        /// </summary>
        private void DrawImprovedHSVDiagram(float minRange, float maxRange)
        {
            Rect rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            rect.height = 20;
            rect.x += 2;
            rect.width -= 4;

            // 배경 그리기 (HSV 색상환)
            DrawHSVBackground(rect);
            
            // 선택된 범위 표시 (어두운 오버레이)
            DrawRangeOverlay(rect, minRange, maxRange);
            
            // 범위 핸들 그리기
            DrawRangeHandles(rect, minRange, maxRange);
        }

        /// <summary>
        /// HSV 색상환 배경을 그립니다
        /// </summary>
        private void DrawHSVBackground(Rect rect)
        {
            int segments = 360;
            float segmentWidth = rect.width / segments;

            for (int i = 0; i < segments; i++)
            {
                float hue = (float)i / segments;
                Color color = Color.HSVToRGB(hue, 1f, 1f);
                
                Rect segmentRect = new Rect(
                    rect.x + i * segmentWidth, 
                    rect.y, 
                    segmentWidth + 1, // 작은 겹침으로 틈새 방지
                    rect.height
                );
                
                EditorGUI.DrawRect(segmentRect, color);
            }
        }

        /// <summary>
        /// 선택되지 않은 범위에 어두운 오버레이를 그립니다
        /// </summary>
        private void DrawRangeOverlay(Rect rect, float minRange, float maxRange)
        {
            Color overlayColor = new Color(0.1f, 0.1f, 0.1f, 0.7f); // 어두운 반투명 색상

            if (maxRange < minRange) // 범위가 0을 넘나드는 경우
            {
                // 0 ~ maxRange 구간에 오버레이
                if (maxRange > 0)
                {
                    Rect leftOverlay = new Rect(
                        rect.x,
                        rect.y,
                        rect.width * maxRange,
                        rect.height
                    );
                    EditorGUI.DrawRect(leftOverlay, overlayColor);
                }

                // minRange ~ 1 구간에 오버레이  
                if (minRange < 1)
                {
                    Rect rightOverlay = new Rect(
                        rect.x + rect.width * minRange,
                        rect.y,
                        rect.width * (1f - minRange),
                        rect.height
                    );
                    EditorGUI.DrawRect(rightOverlay, overlayColor);
                }
            }
            else // 일반적인 경우
            {
                // 0 ~ minRange 구간에 오버레이
                if (minRange > 0)
                {
                    Rect leftOverlay = new Rect(
                        rect.x,
                        rect.y,
                        rect.width * minRange,
                        rect.height
                    );
                    EditorGUI.DrawRect(leftOverlay, overlayColor);
                }

                // maxRange ~ 1 구간에 오버레이
                if (maxRange < 1)
                {
                    Rect rightOverlay = new Rect(
                        rect.x + rect.width * maxRange,
                        rect.y,
                        rect.width * (1f - maxRange),
                        rect.height
                    );
                    EditorGUI.DrawRect(rightOverlay, overlayColor);
                }
            }
        }

        /// <summary>
        /// 범위 조절 핸들을 그립니다
        /// </summary>
        private void DrawRangeHandles(Rect rect, float minRange, float maxRange)
        {
            Color handleColor = Color.white;
            Color borderColor = Color.black;
            float handleWidth = 3f;

            // Min 핸들
            float minX = rect.x + rect.width * minRange;
            Rect minHandle = new Rect(minX - handleWidth / 2, rect.y - 1, handleWidth, rect.height + 2);
            EditorGUI.DrawRect(minHandle, borderColor); // 테두리
            EditorGUI.DrawRect(new Rect(minX - handleWidth / 2 + 1, rect.y, handleWidth - 2, rect.height), handleColor);

            // Max 핸들
            float maxX = rect.x + rect.width * maxRange;
            Rect maxHandle = new Rect(maxX - handleWidth / 2, rect.y - 1, handleWidth, rect.height + 2);
            EditorGUI.DrawRect(maxHandle, borderColor); // 테두리
            EditorGUI.DrawRect(new Rect(maxX - handleWidth / 2 + 1, rect.y, handleWidth - 2, rect.height), handleColor);
        }

        /// <summary>
        /// 선택한 색상을 기준으로 HSV 범위를 자동으로 설정합니다
        /// </summary>
        private void SetHSVRangeFromColor(Color selectedColor, float tolerance = 0.05f)
        {
            // RGB를 HSV로 변환
            Color.RGBToHSV(selectedColor, out float hue, out float saturation, out float value);
            
            // 허용 오차를 적용한 범위 계산
            float minHue = hue - tolerance;
            float maxHue = hue + tolerance;
            
            // Hue는 0~1 범위를 순환하므로 경계 처리
            if (minHue < 0f)
            {
                minHue += 1f;
            }
            if (maxHue > 1f)
            {
                maxHue -= 1f;
            }
            
            // SerializedProperty 업데이트
            Undo.RecordObject(target, "Auto Set HSV Range");
            hsvRangeMin.floatValue = minHue;
            hsvRangeMax.floatValue = maxHue;
            
            // 변경사항 적용
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
#endif
}