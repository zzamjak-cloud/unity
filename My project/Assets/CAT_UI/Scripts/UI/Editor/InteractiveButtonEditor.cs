using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// InteractiveButton의 Custom Editor입니다.
/// Inspector에서 자동 상태 생성을 처리합니다.
/// </summary>
[CustomEditor(typeof(InteractiveButton))]
public class InteractiveButtonEditor : Editor
{
    private InteractiveButton interactiveButton;
    
    // Foldout 상태 관리
    private bool showImageColors = true;
    private bool showTextColors = true;
    private bool showIconGameObjects = true;
    
    private void OnEnable()
    {
        interactiveButton = (InteractiveButton)target;
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // 자동 상태 생성 체크
        CheckAndGenerateStates();
        
        // 버튼 상태 섹션
        DrawButtonStateSection();
        
        // 스케일 설정 섹션
        DrawScaleSettingsSection();
        
        // 애니메이션 설정 섹션
        DrawAnimationSettingsSection();
        
        // 이미지 컬러 설정 섹션
        DrawImageColorSection();
        
        // 텍스트 컬러 설정 섹션
        DrawTextColorSection();
        
        // 아이콘 GameObject 설정 섹션
        DrawIconGameObjectSection();
        
        serializedObject.ApplyModifiedProperties();
    }
    
    /// <summary>
    /// Target이 변경되었는지 확인하고 자동으로 상태를 생성합니다.
    /// </summary>
    private void CheckAndGenerateStates()
    {
        bool needsUpdate = false;
        
        // 이미지 상태 확인
        var imageColorInfos = serializedObject.FindProperty("imageColorInfos");
        for (int i = 0; i < imageColorInfos.arraySize; i++)
        {
            var imageInfo = imageColorInfos.GetArrayElementAtIndex(i);
            var targetImage = imageInfo.FindPropertyRelative("targetImage");
            var stateColors = imageInfo.FindPropertyRelative("stateColors");
            
            if (targetImage.objectReferenceValue != null && stateColors.arraySize == 0)
            {
                needsUpdate = true;
                break;
            }
        }
        
        // 텍스트 상태 확인
        if (!needsUpdate)
        {
            var textColorInfos = serializedObject.FindProperty("textColorInfos");
            for (int i = 0; i < textColorInfos.arraySize; i++)
            {
                var textInfo = textColorInfos.GetArrayElementAtIndex(i);
                var targetText = textInfo.FindPropertyRelative("targetText");
                var stateColors = textInfo.FindPropertyRelative("stateColors");
                
                if (targetText.objectReferenceValue != null && stateColors.arraySize == 0)
                {
                    needsUpdate = true;
                    break;
                }
            }
        }
        
        // 아이콘 상태 확인
        if (!needsUpdate)
        {
            var iconGameObjectInfos = serializedObject.FindProperty("iconGameObjectInfos");
            for (int i = 0; i < iconGameObjectInfos.arraySize; i++)
            {
                var iconInfo = iconGameObjectInfos.GetArrayElementAtIndex(i);
                var stateGameObjects = iconInfo.FindPropertyRelative("stateGameObjects");
                
                if (stateGameObjects != null && stateGameObjects.arraySize == 0)
                {
                    needsUpdate = true;
                    break;
                }
            }
        }
        
        // 자동 생성 실행
        if (needsUpdate)
        {
            GenerateAllStates();
        }
    }
    
    /// <summary>
    /// 버튼 상태 섹션을 그립니다.
    /// </summary>
    private void DrawButtonStateSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Button State", EditorStyles.boldLabel);
        
        EditorGUI.indentLevel++;
        var currentState = serializedObject.FindProperty("currentState");
        var isClickable = serializedObject.FindProperty("isClickable");
        
        // 상태 변경 감지를 위한 백업
        var oldState = (ButtonState)currentState.enumValueIndex;
        
        EditorGUILayout.PropertyField(currentState);
        EditorGUILayout.PropertyField(isClickable);
        
        // 상태가 변경되었으면 상태 적용
        var newState = (ButtonState)currentState.enumValueIndex;
        if (oldState != newState)
        {
            if (Application.isPlaying)
            {
                // 플레이모드: SetState 호출 (런타임 로직 포함)
                interactiveButton.SetState(newState);
            }
            else
            {
                // 에디터 모드: SetStateForEditor 호출 (시각적 변경만)
                interactiveButton.SetStateForEditor(newState);
            }
        }
        
        EditorGUI.indentLevel--;
    }
    
    /// <summary>
    /// 스케일 설정 섹션을 그립니다.
    /// </summary>
    private void DrawScaleSettingsSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scale Settings", EditorStyles.boldLabel);
        
        EditorGUI.indentLevel++;
        var targetScale = serializedObject.FindProperty("targetScale");
        EditorGUILayout.PropertyField(targetScale);
        EditorGUI.indentLevel--;
    }
    
    /// <summary>
    /// 애니메이션 설정 섹션을 그립니다.
    /// </summary>
    private void DrawAnimationSettingsSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation Settings", EditorStyles.boldLabel);
        
        var pressDuration = serializedObject.FindProperty("pressDuration");
        var pressCurve = serializedObject.FindProperty("pressCurve");
        var releaseDuration = serializedObject.FindProperty("releaseDuration");
        var releaseCurve = serializedObject.FindProperty("releaseCurve");
        
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(pressDuration);
        EditorGUILayout.PropertyField(pressCurve);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(releaseDuration);
        EditorGUILayout.PropertyField(releaseCurve);
        EditorGUI.indentLevel--;
    }
    
    /// <summary>
    /// 이미지 컬러 설정 섹션을 그립니다.
    /// </summary>
    private void DrawImageColorSection()
    {
        EditorGUILayout.Space();
        var imageColorInfos = serializedObject.FindProperty("imageColorInfos");
        
        // Foldout 헤더와 + 버튼
        EditorGUILayout.BeginHorizontal();
        showImageColors = EditorGUILayout.Foldout(showImageColors, "Image Color Settings", true, EditorStyles.foldoutHeader);
        if (GUILayout.Button("+", GUILayout.Width(20)))
        {
            imageColorInfos.InsertArrayElementAtIndex(imageColorInfos.arraySize);
        }
        EditorGUILayout.EndHorizontal();
        
        if (showImageColors)
        {
            EditorGUI.indentLevel++;
            
            // 각 이미지 요소 표시
            for (int i = 0; i < imageColorInfos.arraySize; i++)
            {
                DrawImageColorElement(imageColorInfos, i);
            }
            EditorGUI.indentLevel--;
        }
    }
    
    /// <summary>
    /// 개별 이미지 컬러 요소를 그립니다.
    /// </summary>
    private void DrawImageColorElement(SerializedProperty imageColorInfos, int index)
    {
        var imageInfo = imageColorInfos.GetArrayElementAtIndex(index);
        var targetImage = imageInfo.FindPropertyRelative("targetImage");
        var stateColors = imageInfo.FindPropertyRelative("stateColors");
        
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.Space(5);
        
        // Target Image 등록
        EditorGUILayout.PropertyField(targetImage, new GUIContent("Image"));
        
        EditorGUI.indentLevel++;
        
        // 이미지가 등록되어 있으면 상태 컬러 필드들 표시
        if (targetImage.objectReferenceValue != null)
        {
            // 상태 컬러가 없으면 자동 생성
            if (stateColors.arraySize == 0)
            {
                GenerateImageStatesForElement(imageInfo);
            }

            // 상태 컬러 필드들 표시
            EditorGUILayout.Space(5);

            for (int j = 0; j < stateColors.arraySize; j++)
            {
                var stateColor = stateColors.GetArrayElementAtIndex(j);
                var state = stateColor.FindPropertyRelative("state");
                var color = stateColor.FindPropertyRelative("color");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(state.enumDisplayNames[state.enumValueIndex], GUILayout.Width(100));
                EditorGUILayout.PropertyField(color, GUIContent.none, GUILayout.Width(160));
                EditorGUILayout.EndHorizontal();
            }
        }
        
        EditorGUI.indentLevel--;
        
        // 삭제 버튼을 하단 우측에 배치
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        // 빨간색 삭제 버튼
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(20)))
        {
            imageColorInfos.DeleteArrayElementAtIndex(index);
            GUI.backgroundColor = originalColor;
            return;
        }
        GUI.backgroundColor = originalColor;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }
    
    /// <summary>
    /// 텍스트 컬러 설정 섹션을 그립니다.
    /// </summary>
    private void DrawTextColorSection()
    {
        EditorGUILayout.Space();
        var textColorInfos = serializedObject.FindProperty("textColorInfos");
        
        // Foldout 헤더와 + 버튼
        EditorGUILayout.BeginHorizontal();
        showTextColors = EditorGUILayout.Foldout(showTextColors, "Text Color Settings", true, EditorStyles.foldoutHeader);
        if (GUILayout.Button("+", GUILayout.Width(20)))
        {
            textColorInfos.InsertArrayElementAtIndex(textColorInfos.arraySize);
        }
        EditorGUILayout.EndHorizontal();
        
        if (showTextColors)
        {
            EditorGUI.indentLevel++;
            
            // 각 텍스트 요소 표시
            for (int i = 0; i < textColorInfos.arraySize; i++)
            {
                DrawTextColorElement(textColorInfos, i);
            }
            EditorGUI.indentLevel--;
        }
    }
    
    /// <summary>
    /// 개별 텍스트 컬러 요소를 그립니다.
    /// </summary>
    private void DrawTextColorElement(SerializedProperty textColorInfos, int index)
    {
        var textInfo = textColorInfos.GetArrayElementAtIndex(index);
        var targetText = textInfo.FindPropertyRelative("targetText");
        var stateColors = textInfo.FindPropertyRelative("stateColors");
        
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.Space(5);
        
        EditorGUI.indentLevel++;
        
        EditorGUILayout.PropertyField(targetText, new GUIContent("Text"));
        
        // 텍스트가 등록되어 있으면 상태 컬러 필드들 표시
        if (targetText.objectReferenceValue != null)
        {
            // 상태 컬러가 없으면 자동 생성
            if (stateColors.arraySize == 0)
            {
                GenerateTextStatesForElement(textInfo);
            }
            
            // 상태 컬러 필드들 표시
            EditorGUILayout.Space(5);
            
            EditorGUI.indentLevel++;
            for (int j = 0; j < stateColors.arraySize; j++)
            {
                var stateColor = stateColors.GetArrayElementAtIndex(j);
                var state = stateColor.FindPropertyRelative("state");
                var color = stateColor.FindPropertyRelative("color");
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(state.enumDisplayNames[state.enumValueIndex], GUILayout.Width(100));
                EditorGUILayout.PropertyField(color, GUIContent.none, GUILayout.Width(160));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }
        
        EditorGUI.indentLevel--;
        
        // 삭제 버튼을 하단 우측에 배치
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        // 빨간색 삭제 버튼
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(20)))
        {
            textColorInfos.DeleteArrayElementAtIndex(index);
            GUI.backgroundColor = originalColor;
            return;
        }
        GUI.backgroundColor = originalColor;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }
    
    /// <summary>
    /// 아이콘 GameObject 설정 섹션을 그립니다.
    /// </summary>
    private void DrawIconGameObjectSection()
    {
        EditorGUILayout.Space();
        var iconGameObjectInfos = serializedObject.FindProperty("iconGameObjectInfos");
        
        // Foldout 헤더와 + 버튼
        EditorGUILayout.BeginHorizontal();
        showIconGameObjects = EditorGUILayout.Foldout(showIconGameObjects, "Icon GameObject Settings", true, EditorStyles.foldoutHeader);
        if (GUILayout.Button("+", GUILayout.Width(20)))
        {
            interactiveButton.AddIconGameObjectInfo();
        }
        EditorGUILayout.EndHorizontal();
        
        if (showIconGameObjects)
        {
            EditorGUI.indentLevel++;
            
            // 각 아이콘 요소 표시
            for (int i = 0; i < iconGameObjectInfos.arraySize; i++)
            {
                DrawIconGameObjectElement(iconGameObjectInfos, i);
            }
            EditorGUI.indentLevel--;
        }
    }
    
    /// <summary>
    /// 개별 아이콘 GameObject 요소를 그립니다.
    /// </summary>
    private void DrawIconGameObjectElement(SerializedProperty iconGameObjectInfos, int index)
    {
        var iconInfo = iconGameObjectInfos.GetArrayElementAtIndex(index);
        var stateGameObjects = iconInfo.FindPropertyRelative("stateGameObjects");
        
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.Space(5);
        
        // 상태별 GameObject 등록 필드들 표시
        EditorGUILayout.LabelField("State GameObjects", EditorStyles.miniBoldLabel);
        
        EditorGUI.indentLevel++;
        
        // 상태별 GameObject가 없으면 자동 생성
        if (stateGameObjects.arraySize == 0)
        {
            GenerateIconStatesForElement(iconInfo);
        }
        
        // 상태별 GameObject 등록 필드들 표시
        EditorGUILayout.Space(5);
        
        for (int j = 0; j < stateGameObjects.arraySize; j++)
        {
            var stateGameObject = stateGameObjects.GetArrayElementAtIndex(j);
            var state = stateGameObject.FindPropertyRelative("state");
            var gameObject = stateGameObject.FindPropertyRelative("gameObject");
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(state.enumDisplayNames[state.enumValueIndex], GUILayout.Width(100));
            EditorGUILayout.PropertyField(gameObject, GUIContent.none, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUI.indentLevel--;
        
        // 삭제 버튼을 하단 우측에 배치
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        // 빨간색 삭제 버튼
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(20)))
        {
            iconGameObjectInfos.DeleteArrayElementAtIndex(index);
            GUI.backgroundColor = originalColor;
            return;
        }
        GUI.backgroundColor = originalColor;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }
    
    /// <summary>
    /// 모든 상태를 자동 생성합니다.
    /// </summary>
    private void GenerateAllStates()
    {
        Undo.RecordObject(interactiveButton, "Generate All States");
        
        GenerateImageStates();
        GenerateTextStates();
        GenerateIconStates();
        
        EditorUtility.SetDirty(interactiveButton);
        Debug.Log("All states generated successfully!");
    }
    
    /// <summary>
    /// 개별 이미지 요소에 대한 상태를 생성합니다.
    /// </summary>
    private void GenerateImageStatesForElement(SerializedProperty imageInfo)
    {
        var targetImage = imageInfo.FindPropertyRelative("targetImage");
        var stateColors = imageInfo.FindPropertyRelative("stateColors");
        
        if (targetImage.objectReferenceValue != null)
        {
            Image image = targetImage.objectReferenceValue as Image;
            if (image != null)
            {
                // 기존 상태들 제거
                stateColors.ClearArray();
                
                // Normal 상태 추가
                AddStateColor(stateColors, ButtonState.Normal, image.color);
                
                // Active 상태 추가 (밝게)
                Color activeColor = new Color(
                    Mathf.Min(1f, image.color.r * 1.2f),
                    Mathf.Min(1f, image.color.g * 1.2f),
                    Mathf.Min(1f, image.color.b * 1.2f),
                    image.color.a
                );
                AddStateColor(stateColors, ButtonState.Active, activeColor);
                
                // Disabled 상태 추가 (어둡게)
                Color disabledColor = new Color(
                    image.color.r * 0.5f,
                    image.color.g * 0.5f,
                    image.color.b * 0.5f,
                    image.color.a * 0.7f
                );
                AddStateColor(stateColors, ButtonState.Disabled, disabledColor);
            }
        }
    }
    
    /// <summary>
    /// 개별 텍스트 요소에 대한 상태를 생성합니다.
    /// </summary>
    private void GenerateTextStatesForElement(SerializedProperty textInfo)
    {
        var targetText = textInfo.FindPropertyRelative("targetText");
        var stateColors = textInfo.FindPropertyRelative("stateColors");
        
        if (targetText.objectReferenceValue != null)
        {
            TextMeshProUGUI text = targetText.objectReferenceValue as TextMeshProUGUI;
            if (text != null)
            {
                // 기존 상태들 제거
                stateColors.ClearArray();
                
                // Normal 상태 추가
                AddStateColor(stateColors, ButtonState.Normal, text.color);
                
                // Active 상태 추가 (밝게)
                Color activeColor = new Color(
                    Mathf.Min(1f, text.color.r * 1.2f),
                    Mathf.Min(1f, text.color.g * 1.2f),
                    Mathf.Min(1f, text.color.b * 1.2f),
                    text.color.a
                );
                AddStateColor(stateColors, ButtonState.Active, activeColor);
                
                // Disabled 상태 추가 (어둡게)
                Color disabledColor = new Color(
                    text.color.r * 0.5f,
                    text.color.g * 0.5f,
                    text.color.b * 0.5f,
                    text.color.a * 0.7f
                );
                AddStateColor(stateColors, ButtonState.Disabled, disabledColor);
            }
        }
    }
    
    /// <summary>
    /// 개별 아이콘 요소에 대한 상태를 생성합니다.
    /// </summary>
    private void GenerateIconStatesForElement(SerializedProperty iconInfo)
    {
        var stateGameObjects = iconInfo.FindPropertyRelative("stateGameObjects");
        
        // 기존 상태들 제거
        stateGameObjects.ClearArray();
        
        // Normal 상태 추가
        AddStateGameObject(stateGameObjects, ButtonState.Normal, null);
        
        // Active 상태 추가
        AddStateGameObject(stateGameObjects, ButtonState.Active, null);
        
        // Disabled 상태 추가
        AddStateGameObject(stateGameObjects, ButtonState.Disabled, null);
    }
    
    /// <summary>
    /// 이미지 상태들을 자동 생성합니다.
    /// </summary>
    private void GenerateImageStates()
    {
        var imageColorInfos = serializedObject.FindProperty("imageColorInfos");
        
        for (int i = 0; i < imageColorInfos.arraySize; i++)
        {
            var imageInfo = imageColorInfos.GetArrayElementAtIndex(i);
            var targetImage = imageInfo.FindPropertyRelative("targetImage");
            var stateColors = imageInfo.FindPropertyRelative("stateColors");
            
            if (targetImage.objectReferenceValue != null)
            {
                Image image = targetImage.objectReferenceValue as Image;
                if (image != null)
                {
                    // 기존 상태들 제거
                    stateColors.ClearArray();
                    
                    // Normal 상태 추가
                    AddStateColor(stateColors, ButtonState.Normal, image.color);
                    
                    // Active 상태 추가 (밝게)
                    Color activeColor = new Color(
                        Mathf.Min(1f, image.color.r * 1.2f),
                        Mathf.Min(1f, image.color.g * 1.2f),
                        Mathf.Min(1f, image.color.b * 1.2f),
                        image.color.a
                    );
                    AddStateColor(stateColors, ButtonState.Active, activeColor);
                    
                    // Disabled 상태 추가 (어둡게)
                    Color disabledColor = new Color(
                        image.color.r * 0.5f,
                        image.color.g * 0.5f,
                        image.color.b * 0.5f,
                        image.color.a * 0.7f
                    );
                    AddStateColor(stateColors, ButtonState.Disabled, disabledColor);
                }
            }
        }
    }
    
    /// <summary>
    /// 텍스트 상태들을 자동 생성합니다.
    /// </summary>
    private void GenerateTextStates()
    {
        var textColorInfos = serializedObject.FindProperty("textColorInfos");
        
        for (int i = 0; i < textColorInfos.arraySize; i++)
        {
            var textInfo = textColorInfos.GetArrayElementAtIndex(i);
            var targetText = textInfo.FindPropertyRelative("targetText");
            var stateColors = textInfo.FindPropertyRelative("stateColors");
            
            if (targetText.objectReferenceValue != null)
            {
                TextMeshProUGUI text = targetText.objectReferenceValue as TextMeshProUGUI;
                if (text != null)
                {
                    // 기존 상태들 제거
                    stateColors.ClearArray();
                    
                    // Normal 상태 추가
                    AddStateColor(stateColors, ButtonState.Normal, text.color);
                    
                    // Active 상태 추가 (밝게)
                    Color activeColor = new Color(
                        Mathf.Min(1f, text.color.r * 1.2f),
                        Mathf.Min(1f, text.color.g * 1.2f),
                        Mathf.Min(1f, text.color.b * 1.2f),
                        text.color.a
                    );
                    AddStateColor(stateColors, ButtonState.Active, activeColor);
                    
                    // Disabled 상태 추가 (어둡게)
                    Color disabledColor = new Color(
                        text.color.r * 0.5f,
                        text.color.g * 0.5f,
                        text.color.b * 0.5f,
                        text.color.a * 0.7f
                    );
                    AddStateColor(stateColors, ButtonState.Disabled, disabledColor);
                }
            }
        }
    }
    
    /// <summary>
    /// 아이콘 상태들을 자동 생성합니다.
    /// </summary>
    private void GenerateIconStates()
    {
        var iconGameObjectInfos = serializedObject.FindProperty("iconGameObjectInfos");
        
        for (int i = 0; i < iconGameObjectInfos.arraySize; i++)
        {
            var iconInfo = iconGameObjectInfos.GetArrayElementAtIndex(i);
            GenerateIconStatesForElement(iconInfo);
        }
    }
    
    /// <summary>
    /// 상태별 컬러를 추가합니다.
    /// </summary>
    private void AddStateColor(SerializedProperty stateColors, ButtonState state, Color color)
    {
        int index = stateColors.arraySize;
        stateColors.InsertArrayElementAtIndex(index);
        
        var newStateColor = stateColors.GetArrayElementAtIndex(index);
        newStateColor.FindPropertyRelative("state").enumValueIndex = (int)state;
        newStateColor.FindPropertyRelative("color").colorValue = color;
    }
    
    /// <summary>
    /// 상태별 활성화 설정을 추가합니다.
    /// </summary>
    private void AddStateActivation(SerializedProperty stateActivations, ButtonState state, bool isActive)
    {
        int index = stateActivations.arraySize;
        stateActivations.InsertArrayElementAtIndex(index);
        
        var newStateActivation = stateActivations.GetArrayElementAtIndex(index);
        newStateActivation.FindPropertyRelative("state").enumValueIndex = (int)state;
        newStateActivation.FindPropertyRelative("isActive").boolValue = isActive;
    }
    
    /// <summary>
    /// 상태별 GameObject 정보를 추가합니다.
    /// </summary>
    private void AddStateGameObject(SerializedProperty stateGameObjects, ButtonState state, GameObject gameObject)
    {
        int index = stateGameObjects.arraySize;
        stateGameObjects.InsertArrayElementAtIndex(index);
        
        var newStateGameObject = stateGameObjects.GetArrayElementAtIndex(index);
        newStateGameObject.FindPropertyRelative("state").enumValueIndex = (int)state;
        newStateGameObject.FindPropertyRelative("gameObject").objectReferenceValue = gameObject;
    }
}
