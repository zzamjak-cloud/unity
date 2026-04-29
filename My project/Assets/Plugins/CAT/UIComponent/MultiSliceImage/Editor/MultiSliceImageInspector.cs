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
    SerializedProperty m_PreserveAspect;
    SerializedProperty m_ImageType;
    SerializedProperty m_VerticalCuts;
    SerializedProperty m_HorizontalCuts;
    SerializedProperty m_FillAmount;
    SerializedProperty m_FillOrigin;

    protected override void OnEnable()
    {
        base.OnEnable();
        m_Sprite = serializedObject.FindProperty("m_Sprite");
        m_PreserveAspect = serializedObject.FindProperty("m_PreserveAspect");
        m_ImageType = serializedObject.FindProperty("m_ImageType");
        m_VerticalCuts = serializedObject.FindProperty("verticalCuts");
        m_HorizontalCuts = serializedObject.FindProperty("horizontalCuts");
        m_FillAmount = serializedObject.FindProperty("m_FillAmount");
        m_FillOrigin = serializedObject.FindProperty("m_FillOrigin");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Sprite 필드 표시
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(m_Sprite);
        bool spriteChanged = EditorGUI.EndChangeCheck();

        // Sprite 변경 시 즉시 적용
        if (spriteChanged)
        {
            serializedObject.ApplyModifiedProperties();
            MultiSliceImage img = (MultiSliceImage)target;
            if (img != null)
            {
                // sprite setter가 stopsDirty를 설정하므로 직접 호출
                img.sprite = img.sprite; // setter를 통해 stopsDirty 설정
                UpdateImageImmediately(img);
            }
        }

        // Image Type 선택
        EditorGUI.BeginChangeCheck();
        MultiSliceImage targetImg = (MultiSliceImage)target;
        ImageType newImageType = (ImageType)EditorGUILayout.EnumPopup(
            new GUIContent("Image Type", "Sliced | Tiled | TiledFilled(스텝 와이즈) | TiledFilledMask(마스크 클리핑)"),
            targetImg.imageType
        );
        bool imageTypeChanged = EditorGUI.EndChangeCheck();

        if (imageTypeChanged)
        {
            Undo.RecordObject(targetImg, "Change Image Type");
            targetImg.imageType = newImageType;
            EditorUtility.SetDirty(targetImg);
        }

        // GraphicEditor의 기본 기능들 표시 (Color, Material, Raycast Target 등)
        base.OnInspectorGUI();

        EditorGUILayout.Space(3);

        // Preserve Aspect 표시
        EditorGUILayout.PropertyField(m_PreserveAspect, new GUIContent("Preserve Aspect", "종횡비 유지"));

        MultiSliceImage targetImage = (MultiSliceImage)target;

        bool showFillSettings = targetImage.imageType == ImageType.TiledFilled
            || targetImage.imageType == ImageType.TiledFilledMask;

        // TiledFilled / Filled Mask: 진행값 (Unity Filled의 fillAmount 느낌)
        if (showFillSettings)
        {
            EditorGUI.BeginChangeCheck();

            float newFill = EditorGUILayout.Slider("Fill Amount", targetImage.fillAmount, 0f, 1f);
            FillOrigin newOrigin = (FillOrigin)EditorGUILayout.EnumPopup("Fill Origin", targetImage.fillOrigin);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetImage, "Change TiledFilled Fill");

                targetImage.fillAmount = newFill;
                targetImage.fillOrigin = newOrigin;

                // setter에서 SetVerticesDirty()를 호출하지만, Undo/씬 반영용으로 한 번 더 처리
                EditorUtility.SetDirty(targetImage);
                UpdateImageImmediately(targetImage);
            }
        }

        // Vertical, Horizontal Cuts는 Inspector에서 숨김 (Editor Window에서만 수정 가능)
        // EditorGUILayout.PropertyField(m_VerticalCuts, new GUIContent("Vertical Cuts", "최대 4개까지 가능"), true);
        // EditorGUILayout.PropertyField(m_HorizontalCuts, new GUIContent("Horizontal Cuts", "최대 4개까지 가능"), true);

        EditorGUILayout.Space(5);

        // Set Native Size 버튼
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

        // Slice 타입 사용 시 정점 수 표시 (메시 빌드 후 갱신됨)
        EditorGUILayout.Space(3);
        int vertCount = targetImage.lastPopulateVertexCount;
        if (vertCount > 0)
        {
            int quadCount = vertCount / 4;
            EditorGUILayout.LabelField("Mesh (현재)", $"{vertCount} 정점 (쿼드 {quadCount}개)");
        }
        else if (targetImage.sprite != null)
        {
            EditorGUILayout.LabelField("Mesh (현재)", "메시 미빌드 (캔버스 갱신 후 표시)");
            if (GUILayout.Button("메시 갱신하여 정점 수 확인", GUILayout.Height(18)))
            {
                targetImage.SetVerticesDirty();
                targetImage.SetMaterialDirty();
                Canvas.ForceUpdateCanvases();
                Repaint();
            }
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Open Multi-Slice Editor", GUILayout.Height(30)))
        {
            MultiSliceEditorWindow.Open((MultiSliceImage)target);
        }

        // Sprite 변경 시 이미 ApplyModifiedProperties를 호출했으므로 조건부 호출
        if (!spriteChanged)
        {
            serializedObject.ApplyModifiedProperties();
        }

        // 열려있는 Editor 창 갱신
        if (spriteChanged)
        {
            MultiSliceEditorWindow window = EditorWindow.GetWindow<MultiSliceEditorWindow>(false, null, false);
            if (window != null)
            {
                // targetImage가 null이거나 다른 오브젝트를 가리키는 경우 다시 설정
                if (window.TargetImage == null || window.TargetImage != target)
                {
                    window.SetTarget(targetImage);
                }
                window.Repaint();
            }
        }
    }

    private void UpdateImageImmediately(MultiSliceImage targetImage)
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