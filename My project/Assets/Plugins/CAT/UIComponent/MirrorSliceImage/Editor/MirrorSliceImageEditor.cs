#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.UI;
using CAT.UI;
[CustomEditor(typeof(MirrorSliceImage))]
public class MirrorSliceImageEditor : GraphicEditor
{
    /// <summary>
    /// 선택된 오브젝트의 자식으로 MirrorSliceImage를 가진 GameObject를 생성합니다.
    /// 단축키: Windows (Ctrl+Alt+Shift+I), Mac (Cmd+Opt+Shift+I)
    /// </summary>
    [MenuItem("GameObject/UI/Mirror Slice Image %&#_i", false, 10)]
    public static void CreateMirrorSliceImage()
    {
        // 선택된 오브젝트 확인
        GameObject selectedObject = Selection.activeGameObject;
        
        if (selectedObject == null)
        {
            Debug.LogWarning("MirrorSliceImage를 생성하려면 먼저 부모가 될 GameObject를 선택해주세요.");
            return;
        }

        // 새 GameObject 생성
        GameObject newObject = new GameObject("MirroImage");
        
        // 선택된 오브젝트의 자식으로 설정
        newObject.transform.SetParent(selectedObject.transform, false);
        
        // RectTransform 추가 (UI 컴포넌트이므로 필요)
        RectTransform rectTransform = newObject.AddComponent<RectTransform>();
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(100, 100);
        
        // MirrorSliceImage 컴포넌트 추가
        newObject.AddComponent<MirrorSliceImage>();
        
        // 생성된 오브젝트 선택
        Selection.activeGameObject = newObject;
        
        // Undo 시스템에 등록
        Undo.RegisterCreatedObjectUndo(newObject, "Create Mirror Slice Image");
        
        Debug.Log($"MirrorSliceImage가 '{selectedObject.name}'의 자식으로 생성되었습니다.");
    }
    
    /// <summary>
    /// 메뉴 항목 활성화 여부 확인
    /// </summary>
    [MenuItem("GameObject/UI/Mirror Slice Image %&#_i", true)]
    public static bool ValidateCreateMirrorSliceImage()
    {
        // GameObject가 선택되어 있을 때만 활성화
        return Selection.activeGameObject != null;
    }
    SerializedProperty m_Sprite;
    SerializedProperty m_MirrorMode;
    SerializedProperty m_PixelsPerUnitMultiplier;
    SerializedProperty m_IsGradient;
    SerializedProperty m_GradientDirection;
    SerializedProperty m_GradientColorA;
    SerializedProperty m_GradientColorB;
    SerializedProperty m_GradientLerp;

    protected override void OnEnable()
    {
        base.OnEnable();
        m_Sprite = serializedObject.FindProperty("m_Sprite");
        m_MirrorMode = serializedObject.FindProperty("mirrorMode");
        m_PixelsPerUnitMultiplier = serializedObject.FindProperty("m_PixelsPerUnitMultiplier");
        m_IsGradient = serializedObject.FindProperty("m_IsGradient");
        m_GradientDirection = serializedObject.FindProperty("m_GradientDirection");
        m_GradientColorA = serializedObject.FindProperty("m_GradientColorA");
        m_GradientColorB = serializedObject.FindProperty("m_GradientColorB");
        m_GradientLerp = serializedObject.FindProperty("m_GradientLerp");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Sprite 필드를 먼저 표시 (Unity 기본 Image처럼)
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(m_Sprite);
        bool spriteChanged = EditorGUI.EndChangeCheck();
        
        // Sprite 변경 시 즉시 적용
        if (spriteChanged)
        {
            serializedObject.ApplyModifiedProperties();
            MirrorSliceImage img = (MirrorSliceImage)target;
            if (img != null)
            {
                // sprite setter가 cacheDirty를 설정하므로 직접 호출
                img.sprite = img.sprite; // setter를 통해 cacheDirty 설정
                UpdateImageImmediately(img);
            }
        }
        
        // GraphicEditor의 기본 기능들 표시 (Color, Material, Raycast Target 등)
        base.OnInspectorGUI();

        EditorGUILayout.Space(3);

        // Pixels Per Unit Multiplier — 변경 시 RectTransform width/height을 multiplier 변경 비율만큼 함께 스케일.
        // 이렇게 하면 9-Slice 영역이 rect에 대해 같은 비율로 유지되어 메쉬 변형 없이 자연스럽게 확대/축소됩니다.
        // 사용자가 커스텀한 width/height 비율(가로/세로 늘림 등)도 그대로 보존됩니다.
        float oldPpuMultiplier = m_PixelsPerUnitMultiplier.floatValue;

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            m_PixelsPerUnitMultiplier,
            new GUIContent("Pixels Per Unit Multiplier", "Unity Image와 동일한 PPU 배율. 1보다 작으면 더 크게 렌더링됩니다. 변경 시 width/height이 비율에 맞춰 자동 스케일됩니다.")
        );
        bool ppuMultiplierChanged = EditorGUI.EndChangeCheck();
        if (ppuMultiplierChanged)
        {
            // 최소값 보정
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
                MirrorSliceImage img = t as MirrorSliceImage;
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

        EditorGUILayout.Space(3);

        // ===== Gradient 설정 =====
        EditorGUILayout.LabelField("Gradient Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(m_IsGradient, new GUIContent("Is Gradient", "Gradient 색상 적용"));
        bool gradientChanged = EditorGUI.EndChangeCheck();

        if (m_IsGradient.boolValue)
        {
            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_GradientDirection, new GUIContent("Direction", "Gradient 방향"));
            EditorGUILayout.PropertyField(m_GradientColorA, new GUIContent("Color A", "시작 색상"));
            EditorGUILayout.PropertyField(m_GradientColorB, new GUIContent("Color B", "끝 색상"));
            EditorGUILayout.Slider(m_GradientLerp, 0f, 1f, new GUIContent("Lerp", "색상 분포 비율 (0: A 많음, 1: B 많음)"));
            bool gradientParamsChanged = EditorGUI.EndChangeCheck();

            EditorGUI.indentLevel--;

            gradientChanged |= gradientParamsChanged;
        }

        EditorGUILayout.Space(3);

        // Mirror Mode 라디오 버튼
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("Mirror Mode", EditorStyles.boldLabel);
        int selected = (int)m_MirrorMode.enumValueIndex;
        selected = GUILayout.SelectionGrid(
            selected,
            new string[] { "None", "Vertical", "Horizontal" },
            3,
            EditorStyles.radioButton
        );
        m_MirrorMode.enumValueIndex = selected;
        bool mirrorModeChanged = EditorGUI.EndChangeCheck();

        bool propertyChanged = spriteChanged || mirrorModeChanged || gradientChanged || ppuMultiplierChanged;

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

        // 메시 정점/쿼드 수 표시 (메시 빌드 후 갱신됨)
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

