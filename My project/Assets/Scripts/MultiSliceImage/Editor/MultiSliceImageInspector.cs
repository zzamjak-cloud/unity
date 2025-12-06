#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.UI;
using CAT.UI;

/// <summary>
/// MultiSliceImage 컴포넌트의 에디터
/// </summary>
[CustomEditor(typeof(MultiSliceImage))]
public class MultiSliceImageInspector : GraphicEditor
{
    SerializedProperty m_Sprite;
    SerializedProperty m_VerticalCuts;
    SerializedProperty m_HorizontalCuts;

    protected override void OnEnable()
    {
        base.OnEnable();
        m_Sprite = serializedObject.FindProperty("m_Sprite");
        m_VerticalCuts = serializedObject.FindProperty("verticalCuts");
        m_HorizontalCuts = serializedObject.FindProperty("horizontalCuts");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(m_Sprite);
        
        // Vertical, Horizontal Cuts는 Inspector에서 숨김 (Editor Window에서만 수정 가능)
        // EditorGUILayout.PropertyField(m_VerticalCuts, new GUIContent("Vertical Cuts", "최대 4개까지 가능"), true);
        // EditorGUILayout.PropertyField(m_HorizontalCuts, new GUIContent("Horizontal Cuts", "최대 4개까지 가능"), true);
        
        bool propertyChanged = EditorGUI.EndChangeCheck();

        EditorGUILayout.Space(5);

        // Set Native Size 버튼
        MultiSliceImage targetImage = (MultiSliceImage)target;
        EditorGUI.BeginDisabledGroup(targetImage.sprite == null);
        if (GUILayout.Button("Set Native Size", GUILayout.Height(25)))
        {
            Undo.RecordObject(targetImage.GetComponent<RectTransform>(), "Set Native Size");
            targetImage.SetNativeSize();
            EditorUtility.SetDirty(targetImage);
        }
        EditorGUI.EndDisabledGroup();

        if (targetImage.sprite == null)
        {
            EditorGUILayout.HelpBox("스프라이트를 할당해야 Set Native Size를 사용할 수 있습니다.", MessageType.Info);
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Open Multi-Slice Editor", GUILayout.Height(30)))
        {
            MultiSliceEditorWindow.Open((MultiSliceImage)target);
        }

        serializedObject.ApplyModifiedProperties();
        
        // 스프라이트나 타입이 변경되면 열려있는 Editor 창 갱신
        if (propertyChanged)
        {
            MultiSliceEditorWindow window = EditorWindow.GetWindow<MultiSliceEditorWindow>(false, null, false);
            if (window != null)
            {
                // targetImage가 null이거나 다른 오브젝트를 가리키는 경우 다시 설정
                if (window.TargetImage == null || window.TargetImage != target)
                {
                    window.SetTarget((MultiSliceImage)target);
                }
                window.Repaint();
            }
        }
    }
}
#endif