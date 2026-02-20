using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
#if UNITY_EDITOR
    [CustomEditor(typeof(ColorReplace))]
    public class ColorReplaceEditor : Editor
    {
        // SerializedProperty
        private SerializedProperty hsvRangeMin;
        private SerializedProperty hsvRangeMax;
        private SerializedProperty hsvAdjust;

        // 에디터 전용 컬러 피커 (HSV 범위 자동 설정용)
        private Color pickerColor = Color.red;

        private void OnEnable()
        {
            hsvRangeMin = serializedObject.FindProperty("_hsvRangeMin");
            hsvRangeMax = serializedObject.FindProperty("_hsvRangeMax");
            hsvAdjust = serializedObject.FindProperty("_hsvAdjust");

            // 현재 HSV Range에서 대표 색상 추정
            if (hsvRangeMin != null && hsvRangeMax != null)
            {
                float midHue = (hsvRangeMin.floatValue + hsvRangeMax.floatValue) * 0.5f;
                if (hsvRangeMin.floatValue > hsvRangeMax.floatValue)
                {
                    // wrap-around 케이스
                    midHue = (hsvRangeMin.floatValue + hsvRangeMax.floatValue + 1f) * 0.5f;
                    if (midHue > 1f) midHue -= 1f;
                }
                pickerColor = Color.HSVToRGB(midHue, 1f, 1f);
            }

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

            // 렌더러 타입 표시
            Component renderer = ((ColorReplace)target).GetComponent<SpriteRenderer>();
            bool isUIComponent = renderer == null;
            if (isUIComponent)
            {
                renderer = ((ColorReplace)target).GetComponent<UnityEngine.UI.Graphic>();
            }

            string rendererType = isUIComponent ? "UI Graphic" : "SpriteRenderer";
            EditorGUILayout.HelpBox($"Mode: {rendererType}", MessageType.None);

            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();

            // 컬러 피커 (HSV 범위 자동 설정용)
            EditorGUILayout.LabelField("Color Picker", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            pickerColor = EditorGUILayout.ColorField(
                new GUIContent("Target Color", "이 색상을 기준으로 HSV 범위를 자동 설정합니다"),
                pickerColor
            );
            if (EditorGUI.EndChangeCheck())
            {
                SetHSVRangeFromColor(pickerColor);
            }
            EditorGUI.indentLevel--;

            // 범위 프리셋 버튼
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Compact", GUILayout.Width(80)))
            {
                SetHSVRangeFromColor(pickerColor, 0.02f);
            }
            if (GUILayout.Button("Similar", GUILayout.Width(80)))
            {
                SetHSVRangeFromColor(pickerColor, 0.1f);
            }
            if (GUILayout.Button("Wide", GUILayout.Width(80)))
            {
                SetHSVRangeFromColor(pickerColor, 0.2f);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // HSV Range
            EditorGUILayout.LabelField("HSV Range", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            DrawImprovedHSVDiagram(hsvRangeMin.floatValue, hsvRangeMax.floatValue);

            EditorGUILayout.Space(3);
            EditorGUI.indentLevel++;

            float newMinValue = EditorGUILayout.Slider(
                new GUIContent("Min", "HSV 범위 최소값 (0.0 ~ 1.0)"),
                hsvRangeMin.floatValue, 0f, 1f
            );
            float newMaxValue = EditorGUILayout.Slider(
                new GUIContent("Max", "HSV 범위 최대값 (0.0 ~ 1.0)"),
                hsvRangeMax.floatValue, 0f, 1f
            );

            EditorGUI.indentLevel--;

            if (Mathf.Abs(newMinValue - hsvRangeMin.floatValue) > 0.001f)
            {
                hsvRangeMin.floatValue = newMinValue;
            }
            if (Mathf.Abs(newMaxValue - hsvRangeMax.floatValue) > 0.001f)
            {
                hsvRangeMax.floatValue = newMaxValue;
            }

            EditorGUILayout.Space(10);

            // HSV Adjust
            EditorGUILayout.LabelField("HSV Adjust", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            Vector4 currentAdjust = hsvAdjust.vector4Value;
            currentAdjust.x = EditorGUILayout.Slider(new GUIContent("H", "색조(Hue) 조정"), currentAdjust.x, -1f, 1f);
            currentAdjust.y = EditorGUILayout.Slider(new GUIContent("S", "채도(Saturation) 조정"), currentAdjust.y, -1f, 1f);
            currentAdjust.z = EditorGUILayout.Slider(new GUIContent("V", "명도(Value) 조정"), currentAdjust.z, -1f, 1f);
            currentAdjust.w = EditorGUILayout.Slider(new GUIContent("A", "알파(Alpha) 조정"), currentAdjust.w, -1f, 1f);

            EditorGUI.indentLevel--;
            hsvAdjust.vector4Value = currentAdjust;

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);

                if (PrefabUtility.IsPartOfAnyPrefab(target))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
            }

            EditorGUILayout.Space(10);

            // Reset 및 캐시 클리어 버튼
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset"))
            {
                Undo.RecordObject(target, "Reset ColorReplace Values");

                hsvRangeMin.floatValue = 0f;
                hsvRangeMax.floatValue = 1f;
                hsvAdjust.vector4Value = Vector4.zero;
                pickerColor = Color.red;

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }

            // 캐시 클리어 버튼 (플레이 모드에서만)
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Clear Cache"))
                {
                    ColorReplace.ClearMaterialCache();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawImprovedHSVDiagram(float minRange, float maxRange)
        {
            Rect rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            rect.height = 15;
            rect.x += 14;
            rect.width -= 8;

            DrawHSVBackground(rect);
            DrawRangeOverlay(rect, minRange, maxRange);
            DrawRangeHandles(rect, minRange, maxRange);
        }

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
                    segmentWidth + 1,
                    rect.height
                );

                EditorGUI.DrawRect(segmentRect, color);
            }
        }

        private void DrawRangeOverlay(Rect rect, float minRange, float maxRange)
        {
            Color overlayColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

            if (maxRange < minRange) // wrap-around
            {
                Rect middleOverlay = new Rect(
                    rect.x + rect.width * maxRange,
                    rect.y,
                    rect.width * (minRange - maxRange),
                    rect.height
                );
                EditorGUI.DrawRect(middleOverlay, overlayColor);
            }
            else // 일반 케이스
            {
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

        private void DrawRangeHandles(Rect rect, float minRange, float maxRange)
        {
            Color handleColor = Color.white;
            Color borderColor = Color.black;
            float handleWidth = 3f;

            float minX = rect.x + rect.width * minRange;
            Rect minHandle = new Rect(minX - handleWidth / 2, rect.y - 1, handleWidth, rect.height + 2);
            EditorGUI.DrawRect(minHandle, borderColor);
            EditorGUI.DrawRect(new Rect(minX - handleWidth / 2 + 1, rect.y, handleWidth - 2, rect.height), handleColor);

            float maxX = rect.x + rect.width * maxRange;
            Rect maxHandle = new Rect(maxX - handleWidth / 2, rect.y - 1, handleWidth, rect.height + 2);
            EditorGUI.DrawRect(maxHandle, borderColor);
            EditorGUI.DrawRect(new Rect(maxX - handleWidth / 2 + 1, rect.y, handleWidth - 2, rect.height), handleColor);
        }

        private void SetHSVRangeFromColor(Color selectedColor, float tolerance = 0.05f)
        {
            Color.RGBToHSV(selectedColor, out float hue, out _, out _);

            float minHue = hue - tolerance;
            float maxHue = hue + tolerance;

            if (minHue < 0f) minHue += 1f;
            if (maxHue > 1f) maxHue -= 1f;

            Undo.RecordObject(target, "Auto Set HSV Range");
            hsvRangeMin.floatValue = minHue;
            hsvRangeMax.floatValue = maxHue;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
#endif
}
