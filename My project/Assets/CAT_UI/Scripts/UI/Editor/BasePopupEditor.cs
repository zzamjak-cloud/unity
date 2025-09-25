using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BasePopup), true)]
public class BasePopupEditor : Editor
{
    private bool showAnimationSettings = false;
    
    // SerializedProperty 참조
    private SerializedProperty canvasGroupProp;
    private SerializedProperty popupContentObjectProp;
    private SerializedProperty showDurationProp;
    private SerializedProperty hideDurationProp;
    private SerializedProperty showCurveProp;
    private SerializedProperty hideCurveProp;
    private SerializedProperty animationTypeProp;
    private SerializedProperty useFadeAnimationProp;
    private SerializedProperty startScaleProp;
    private SerializedProperty startOffsetProp;
    private SerializedProperty buttonContainerProp;
    
    private void OnEnable()
    {
        // SerializedProperty 초기화
        canvasGroupProp = serializedObject.FindProperty("canvasGroup");
        popupContentObjectProp = serializedObject.FindProperty("popupContentObject");
        showDurationProp = serializedObject.FindProperty("showDuration");
        hideDurationProp = serializedObject.FindProperty("hideDuration");
        showCurveProp = serializedObject.FindProperty("showCurve");
        hideCurveProp = serializedObject.FindProperty("hideCurve");
        animationTypeProp = serializedObject.FindProperty("animationType");
        useFadeAnimationProp = serializedObject.FindProperty("useFadeAnimation");
        startScaleProp = serializedObject.FindProperty("startScale");
        startOffsetProp = serializedObject.FindProperty("startOffset");
        buttonContainerProp = serializedObject.FindProperty("buttonContainer");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.Space(5);
        // Popup Core 섹션
        EditorGUILayout.PropertyField(canvasGroupProp, new GUIContent("Canvas Group"));
        
        EditorGUILayout.Space();
        
        // Animation Settings foldout
        showAnimationSettings = EditorGUILayout.Foldout(showAnimationSettings, "Animation Settings", true);
        if (showAnimationSettings)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(popupContentObjectProp, new GUIContent("Popup Content Object"));
            EditorGUILayout.PropertyField(showDurationProp, new GUIContent("Show Duration"));
            EditorGUILayout.PropertyField(hideDurationProp, new GUIContent("Hide Duration"));
            EditorGUILayout.PropertyField(showCurveProp, new GUIContent("Show Curve"));
            EditorGUILayout.PropertyField(hideCurveProp, new GUIContent("Hide Curve"));
            EditorGUILayout.PropertyField(animationTypeProp, new GUIContent("Animation Type"));
            EditorGUILayout.PropertyField(useFadeAnimationProp, new GUIContent("Use Fade Animation"));
            EditorGUILayout.PropertyField(startScaleProp, new GUIContent("Start Scale"));
            EditorGUILayout.PropertyField(startOffsetProp, new GUIContent("Start Offset"));
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space();
        
        // Content 섹션
        EditorGUILayout.PropertyField(buttonContainerProp, new GUIContent("Button Container"));
        
        EditorGUILayout.Space();
        
        // 하위 클래스의 추가 필드들만 표시 (BasePopup 필드 제외)
        DrawPropertiesExcluding(serializedObject, 
            "canvasGroup", "popupContentObject", "showDuration", "hideDuration", 
            "showCurve", "hideCurve", "animationType", "useFadeAnimation", 
            "startScale", "startOffset", "buttonContainer");
        
        // 변경사항 적용
        serializedObject.ApplyModifiedProperties();
    }
}
