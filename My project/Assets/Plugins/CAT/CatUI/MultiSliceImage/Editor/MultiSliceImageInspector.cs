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
    SerializedProperty m_PixelsPerUnitMultiplier;
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
        m_PixelsPerUnitMultiplier = serializedObject.FindProperty("m_PixelsPerUnitMultiplier");
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
            new GUIContent("Image Type", "Sliced | Tiled | Mixed(축별 혼합) | TiledFilled | TiledFilledMask"),
            targetImg.imageType
        );
        bool imageTypeChanged = EditorGUI.EndChangeCheck();

        if (imageTypeChanged)
        {
            Undo.RecordObject(targetImg, "Change Image Type");
            targetImg.imageType = newImageType;
            EditorUtility.SetDirty(targetImg);
        }

        if (targetImg.imageType == ImageType.Mixed)
        {
            EditorGUI.BeginChangeCheck();
            MixedAxisMode newMixedAxis = (MixedAxisMode)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Mixed Axis",
                    "가로=열(column)·세로=행(row). 한 축만 Tiled로 두고 나머지는 스트레치합니다."),
                targetImg.mixedAxisMode
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetImg, "Change Mixed Axis Mode");
                targetImg.mixedAxisMode = newMixedAxis;
                EditorUtility.SetDirty(targetImg);
                UpdateImageImmediately(targetImg);
            }
        }

        // GraphicEditor의 기본 기능들 표시 (Color, Material, Raycast Target 등)
        base.OnInspectorGUI();

        EditorGUILayout.Space(3);

        // Preserve Aspect 표시
        EditorGUILayout.PropertyField(m_PreserveAspect, new GUIContent("Preserve Aspect", "종횡비 유지"));

        // Pixels Per Unit Multiplier — 변경 시 RectTransform width/height을 multiplier 변경 비율만큼 함께 스케일.
        // 이렇게 하면 타일/슬라이스 영역이 rect에 대해 같은 비율로 유지되어, 정점 폭발이나 메쉬 변형이 발생하지 않습니다.
        // 또한 사용자가 커스텀한 width/height 비율(가로/세로 늘림 등)도 그대로 보존됩니다.
        float oldPpuMultiplier = m_PixelsPerUnitMultiplier.floatValue;

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            m_PixelsPerUnitMultiplier,
            new GUIContent("Pixels Per Unit Multiplier", "Unity Image와 동일한 PPU 배율. 1보다 작으면 더 크게 렌더링됩니다. 변경 시 width/height이 비율에 맞춰 자동 스케일됩니다.")
        );
        bool ppuMultiplierChanged = EditorGUI.EndChangeCheck();
        if (ppuMultiplierChanged)
        {
            // 음수/0 보정 (런타임 안전)
            float newPpuMultiplier = Mathf.Max(0.01f, m_PixelsPerUnitMultiplier.floatValue);
            m_PixelsPerUnitMultiplier.floatValue = newPpuMultiplier;

            // 변경된 값을 즉시 객체에 반영
            serializedObject.ApplyModifiedProperties();

            // multiplier가 작아지면 자연 크기가 커지므로(이미지 확대), rect도 같은 비율로 확대.
            // 비율 = oldMultiplier / newMultiplier (예: 1 → 0.5 이면 비율 2 → rect 2배)
            float sizeScale = (oldPpuMultiplier > 0.0001f) ? (oldPpuMultiplier / newPpuMultiplier) : 1f;
            bool shouldScale = Mathf.Abs(sizeScale - 1f) > 0.0001f;

            foreach (Object t in targets)
            {
                MultiSliceImage img = t as MultiSliceImage;
                if (img == null) continue;

                if (shouldScale)
                {
                    RectTransform rt = img.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        Undo.RecordObject(rt, "Pixels Per Unit Multiplier (Scale Size)");
                        rt.sizeDelta = rt.sizeDelta * sizeScale;
                    }
                }
                EditorUtility.SetDirty(img);
                UpdateImageImmediately(img);
            }
        }

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