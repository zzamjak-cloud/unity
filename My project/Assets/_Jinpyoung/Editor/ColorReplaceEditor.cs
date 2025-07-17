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
        private bool showPresets = false;

        // 컬러 프리셋
        private readonly Color[] presetColors = new Color[]
        {
            Color.red,
            Color.green,
            Color.blue,
            Color.yellow,
            Color.cyan,
            Color.magenta,
            new Color(1f, 0.5f, 0f), // Orange
            new Color(0.5f, 0f, 1f)  // Purple
        };

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

            // 메인 컬러 필드
            EditorGUILayout.PropertyField(color, new GUIContent("Replace Color"));

            // 컬러 프리셋
            showPresets = EditorGUILayout.Foldout(showPresets, "Color Presets", true);
            if (showPresets)
            {
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i < presetColors.Length; i++)
                {
                    if (i > 0 && i % 4 == 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                    }

                    GUI.backgroundColor = presetColors[i];
                    if (GUILayout.Button("", GUILayout.Width(30), GUILayout.Height(20)))
                    {
                        color.colorValue = presetColors[i];
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("HSV Range", EditorStyles.boldLabel);

            // 툴팁 추가
            EditorGUILayout.HelpBox("HSV Range defines which hue values will be affected. Set Min and Max to target specific color ranges.", MessageType.Info);

            EditorGUILayout.PropertyField(hsvRangeMin, new GUIContent("Range Min (Hue)"));
            EditorGUILayout.PropertyField(hsvRangeMax, new GUIContent("Range Max (Hue)"));

            // HSV 범위 시각화
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.DrawRect(rect, Color.black);
            
            // Hue 스펙트럼 그리기
            for (int i = 0; i < rect.width; i++)
            {
                float hue = i / rect.width;
                Color hueColor = Color.HSVToRGB(hue, 1, 1);
                Rect colorRect = new Rect(rect.x + i, rect.y, 1, rect.height);
                EditorGUI.DrawRect(colorRect, hueColor);
            }
            
            // 선택된 범위 그리기
            float minX = rect.x + hsvRangeMin.floatValue * rect.width;
            float maxX = rect.x + hsvRangeMax.floatValue * rect.width;
            Rect selectionRect = new Rect(minX, rect.y, maxX - minX, rect.height);
            EditorGUI.DrawRect(selectionRect, new Color(1, 1, 1, 0.3f));

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("HSV Adjustment", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            Vector4 currentAdjust = hsvAdjust.vector4Value;
            currentAdjust.x = EditorGUILayout.Slider(new GUIContent("Hue", "Shifts the hue of affected colors"), currentAdjust.x, -1f, 1f);
            currentAdjust.y = EditorGUILayout.Slider(new GUIContent("Saturation", "Adjusts color intensity"), currentAdjust.y, -1f, 1f);
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

            // Quick presets for common effects
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Grayscale"))
            {
                Undo.RecordObject(target, "Set Grayscale Preset");
                hsvAdjust.vector4Value = new Vector4(0, -1f, 0, 0);
                hsvRangeMin.floatValue = 0f;
                hsvRangeMax.floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("Sepia"))
            {
                Undo.RecordObject(target, "Set Sepia Preset");
                hsvAdjust.vector4Value = new Vector4(0.05f, 0.4f, 0, 0);
                hsvRangeMin.floatValue = 0f;
                hsvRangeMax.floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }

            if (GUILayout.Button("Invert"))
            {
                Undo.RecordObject(target, "Set Invert Preset");
                hsvAdjust.vector4Value = new Vector4(0.5f, 0, 0, 0);
                hsvRangeMin.floatValue = 0f;
                hsvRangeMax.floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Brighten"))
            {
                Undo.RecordObject(target, "Set Brighten Preset");
                hsvAdjust.vector4Value = new Vector4(0, 0, 0.2f, 0);
                hsvRangeMin.floatValue = 0f;
                hsvRangeMax.floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("Darken"))
            {
                Undo.RecordObject(target, "Set Darken Preset");
                hsvAdjust.vector4Value = new Vector4(0, 0, -0.2f, 0);
                hsvRangeMin.floatValue = 0f;
                hsvRangeMax.floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("Fade"))
            {
                Undo.RecordObject(target, "Set Fade Preset");
                hsvAdjust.vector4Value = new Vector4(0, 0, 0, -0.3f);
                hsvRangeMin.floatValue = 0f;
                hsvRangeMax.floatValue = 1f;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Reset Button
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
    }
#endif
}