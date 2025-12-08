using UnityEngine;
using UnityEditor;
using CAT.UI;

[CustomEditor(typeof(UIMaskRadialStencil))]
public class UIMaskRadialStencilEditor : Editor
{
    private UIMaskRadialStencil maskComponent;
    private SerializedProperty innerRadiusProp;
    private SerializedProperty outerRadiusProp;
    private SerializedProperty autoApplyToChildrenProp;
    private SerializedProperty showMaskGraphicProp;
    private SerializedProperty maskShaderProp;
    private SerializedProperty maskTargetShaderProp;

    private void OnEnable()
    {
        maskComponent = (UIMaskRadialStencil)target;
        innerRadiusProp = serializedObject.FindProperty("innerRadius");
        outerRadiusProp = serializedObject.FindProperty("outerRadius");
        autoApplyToChildrenProp = serializedObject.FindProperty("autoApplyToChildren");
        showMaskGraphicProp = serializedObject.FindProperty("showMaskGraphic");
        maskShaderProp = serializedObject.FindProperty("maskShader");
        maskTargetShaderProp = serializedObject.FindProperty("maskTargetShader");
        
        // Hierarchy 변경 감지 등록
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }
    
    private void OnDisable()
    {
        // Hierarchy 변경 감지 해제
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
    }
    
    private void OnHierarchyChanged()
    {
        // Hierarchy가 변경되었을 때 자식 오브젝트 변경 체크
        if (!Application.isPlaying && maskComponent != null && maskComponent.autoApplyToChildren)
        {
            EditorApplication.delayCall += () =>
            {
                if (maskComponent != null)
                {
                    maskComponent.CheckForChildChanges();
                }
            };
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();

        // 기본 프로퍼티들
        EditorGUILayout.PropertyField(innerRadiusProp);
        EditorGUILayout.PropertyField(outerRadiusProp);
        
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(autoApplyToChildrenProp);
        EditorGUILayout.PropertyField(showMaskGraphicProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shader References (Optional)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(maskShaderProp);
        EditorGUILayout.PropertyField(maskTargetShaderProp);

        // 변경사항이 있으면 적용
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            
            // Editor 환경에서 즉시 마스킹 적용
            if (!Application.isPlaying && maskComponent.autoApplyToChildren)
            {
                // 약간의 지연을 두고 적용 (Editor 업데이트 사이클 고려)
                EditorApplication.delayCall += () =>
                {
                    if (maskComponent != null)
                    {
                        maskComponent.ApplyMaskToChildren();
                    }
                };
            }
        }

        // 수동 적용 버튼
        EditorGUILayout.Space();
        if (GUILayout.Button("Apply Mask to Children"))
        {
            maskComponent.ApplyMaskToChildren();
        }

        if (GUILayout.Button("Restore Children Materials"))
        {
            maskComponent.RestoreChildrenMaterials();
        }
    }

    private void OnSceneGUI()
    {
        // Scene View에서도 실시간 업데이트를 위한 체크
        if (!Application.isPlaying && maskComponent.autoApplyToChildren)
        {
            // 자식 오브젝트 변경 감지를 위한 간단한 체크
            if (Event.current.type == EventType.Layout)
            {
                maskComponent.CheckForChildChanges();
            }
        }
    }
}
