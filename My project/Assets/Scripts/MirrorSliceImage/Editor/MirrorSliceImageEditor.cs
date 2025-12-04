#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(MirrorSliceImage))]
public class MirrorSliceImageEditor : GraphicEditor
{
    SerializedProperty m_Sprite;
    SerializedProperty m_MirrorMode;

    protected override void OnEnable()
    {
        base.OnEnable();
        m_Sprite = serializedObject.FindProperty("m_Sprite");
        m_MirrorMode = serializedObject.FindProperty("mirrorMode");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // GraphicEditor의 기본 기능들 표시 (Color, Material, Raycast Target 등)
        base.OnInspectorGUI();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Mirror Slice Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(m_Sprite);
        
        EditorGUILayout.Space(3);
        
        // Mirror Mode 라디오 버튼
        EditorGUILayout.LabelField("Mirror Mode", EditorStyles.boldLabel);
        int selected = (int)m_MirrorMode.enumValueIndex;
        selected = GUILayout.SelectionGrid(
            selected,
            new string[] { "None", "Vertical", "Horizontal" },
            3,
            EditorStyles.radioButton
        );
        m_MirrorMode.enumValueIndex = selected;
        
        bool propertyChanged = EditorGUI.EndChangeCheck();

        MirrorSliceImage targetImage = (MirrorSliceImage)target;

        EditorGUILayout.Space(5);

        // Set Native Size 버튼
        EditorGUI.BeginDisabledGroup(targetImage.sprite == null);
        if (GUILayout.Button("Set Native Size", GUILayout.Height(25)))
        {
            Undo.RecordObject(targetImage.GetComponent<RectTransform>(), "Set Native Size");
            targetImage.SetNativeSize();
            EditorUtility.SetDirty(targetImage);
            UpdateImageImmediately(targetImage);
        }
        EditorGUI.EndDisabledGroup();

        if (targetImage.sprite == null)
        {
            EditorGUILayout.HelpBox("스프라이트를 할당해야 합니다.", MessageType.Info);
        }
        else
        {
            Sprite sprite = targetImage.sprite;
            Vector4 border = sprite.border;
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Sprite Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Size: {sprite.rect.width} x {sprite.rect.height}");
            EditorGUILayout.LabelField($"Border - L:{border.x} B:{border.y} R:{border.z} T:{border.w}");
            
            EditorGUILayout.Space(5);
            MirrorSliceImage.MirrorMode mode = (MirrorSliceImage.MirrorMode)m_MirrorMode.enumValueIndex;
            string modeDescription = mode == MirrorSliceImage.MirrorMode.None 
                ? "반전 없음: 원본 스프라이트를 그대로 표시합니다."
                : mode == MirrorSliceImage.MirrorMode.Vertical
                    ? "Vertical: 좌측 절반을 우측으로 복제합니다."
                    : "Horizontal: 위쪽 절반을 아래로 복제합니다.";
            
            EditorGUILayout.HelpBox(
                "이 컴포넌트는 절반 스프라이트를 사용하여 나머지 절반을 자동으로 반전시켜 생성합니다.\n" +
                "9-Slice border 정보를 활용하여 중앙 부분이 확장됩니다.\n" +
                modeDescription,
                MessageType.Info
            );
        }

        serializedObject.ApplyModifiedProperties();

        if (propertyChanged)
        {
            UpdateImageImmediately(targetImage);
        }
    }

    private void UpdateImageImmediately(MirrorSliceImage targetImage)
    {
        if (targetImage == null) return;

        // 메시와 머티리얼 갱신
        targetImage.SetVerticesDirty();
        targetImage.SetMaterialDirty();
        
        // Editor에서 변경사항을 즉시 반영
        EditorUtility.SetDirty(targetImage);
        
        // 씬 뷰와 게임 뷰를 즉시 리페인트
        SceneView.RepaintAll();
        
        // Inspector도 리페인트
        Repaint();
    }
}
#endif

