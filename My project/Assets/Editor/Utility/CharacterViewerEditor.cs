using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// CharacterViewManager를 위한 에디터 윈도우
/// </summary>
public class CharacterViewerEditor : EditorWindow
{
    private CharacterViewManager characterViewManager;
    private Vector2 scrollPosition;
    private bool showDebugInfo = true;
    private bool enableAnimationControl = true;
    
    // 애니메이션 상태 선택
    private CharacterAnimationState selectedAnimationState = CharacterAnimationState.Idle;
    private string[] animationStateNames;
    private int selectedAnimationIndex = 0;
    
    [MenuItem("CAT/Utility/Character Viewer")]
    public static void ShowWindow()
    {
        CharacterViewerEditor window = GetWindow<CharacterViewerEditor>("Character Viewer");
        window.minSize = new Vector2(400, 600);
        window.Show();
    }
    
    private void OnEnable()
    {
        // 애니메이션 상태 이름 배열 초기화
        animationStateNames = System.Enum.GetNames(typeof(CharacterAnimationState));
        
        // 씬에서 CharacterViewManager 찾기
        FindCharacterViewManager();
    }
    
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.LabelField("Character Viewer", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // CharacterViewManager 찾기/생성 섹션
        DrawManagerSection();
        
        EditorGUILayout.Space();
        
        // 자식 캐릭터 정보 섹션
        DrawChildCharactersSection();
        
        EditorGUILayout.Space();
        
        // 설정 섹션
        DrawSettingsSection();
        
        EditorGUILayout.Space();
        
        // 애니메이션 제어 섹션
        DrawAnimationControlSection();
        
        EditorGUILayout.Space();
        
        // 액션 버튼 섹션
        DrawActionButtonsSection();
        
        EditorGUILayout.EndScrollView();
    }
    
    /// <summary>
    /// CharacterViewManager 찾기/생성 섹션
    /// </summary>
    private void DrawManagerSection()
    {
        EditorGUILayout.LabelField("Manager", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        if (characterViewManager == null)
        {
            EditorGUILayout.HelpBox("CharacterViewManager를 찾을 수 없습니다.", MessageType.Warning);
            
            if (GUILayout.Button("씬에서 찾기", GUILayout.Width(100)))
            {
                FindCharacterViewManager();
            }
            
            if (GUILayout.Button("새로 생성", GUILayout.Width(100)))
            {
                CreateCharacterViewManager();
            }
        }
        else
        {
            EditorGUILayout.ObjectField("Manager", characterViewManager, typeof(CharacterViewManager), true);
            
            if (GUILayout.Button("선택", GUILayout.Width(60)))
            {
                Selection.activeGameObject = characterViewManager.gameObject;
            }
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    /// <summary>
    /// 자식 캐릭터 정보 섹션
    /// </summary>
    private void DrawChildCharactersSection()
    {
        EditorGUILayout.LabelField("Child Characters", EditorStyles.boldLabel);
        
        if (characterViewManager != null)
        {
            int childCount = characterViewManager.GetChildCharacterCount();
            EditorGUILayout.LabelField($"자식 캐릭터 수: {childCount}");
            
            if (childCount == 0)
            {
                EditorGUILayout.HelpBox("CharacterViewManager의 자식으로 CharacterBase 컴포넌트가 있는 GameObject를 배치하세요.", MessageType.Info);
            }
            
            if (GUILayout.Button("자식 캐릭터 새로고침", GUILayout.Height(25)))
            {
                characterViewManager.RefreshChildCharacters();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("CharacterViewManager를 찾을 수 없습니다.", MessageType.Warning);
        }
    }
    
    /// <summary>
    /// 설정 섹션
    /// </summary>
    private void DrawSettingsSection()
    {
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        
        showDebugInfo = EditorGUILayout.Toggle("Show Debug Info", showDebugInfo);
        enableAnimationControl = EditorGUILayout.Toggle("Enable Animation Control", enableAnimationControl);
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("CharacterViewManager의 자식으로 CharacterBase 컴포넌트가 있는 GameObject를 배치하면 자동으로 감지됩니다.", MessageType.Info);
    }
    
    /// <summary>
    /// 애니메이션 제어 섹션
    /// </summary>
    private void DrawAnimationControlSection()
    {
        EditorGUILayout.LabelField("Animation Control", EditorStyles.boldLabel);
        
        // 애니메이션 상태 선택
        selectedAnimationIndex = EditorGUILayout.Popup("Animation State", selectedAnimationIndex, animationStateNames);
        selectedAnimationState = (CharacterAnimationState)selectedAnimationIndex;
        
        EditorGUILayout.HelpBox("키보드 입력:\n- , (쉼표): 이전 애니메이션 상태로 변경\n- . (마침표): 다음 애니메이션 상태로 변경", MessageType.Info);
        
        if (characterViewManager != null)
        {
            CharacterAnimationState currentState = characterViewManager.GetCurrentAnimationState();
            EditorGUILayout.LabelField($"Current State: {currentState}");
            
            int childCount = characterViewManager.GetChildCharacterCount();
            EditorGUILayout.LabelField($"Child Characters: {childCount}");
        }
    }
    
    /// <summary>
    /// 액션 버튼 섹션
    /// </summary>
    private void DrawActionButtonsSection()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = characterViewManager != null;
        
        if (GUILayout.Button("Refresh Child Characters"))
        {
            RefreshChildCharacters();
        }
        
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = characterViewManager != null;
        
        if (GUILayout.Button("Set All to Idle"))
        {
            SetAllToIdle();
        }
        
        if (GUILayout.Button("Set Selected Animation"))
        {
            SetSelectedAnimation();
        }
        
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = characterViewManager != null;
        
        if (GUILayout.Button("Reinitialize"))
        {
            ReinitializeManager();
        }
        
        if (GUILayout.Button("Update Settings"))
        {
            UpdateManagerSettings();
        }
        
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();
    }
    
    /// <summary>
    /// 씬에서 CharacterViewManager 찾기
    /// </summary>
    private void FindCharacterViewManager()
    {
        characterViewManager = FindObjectOfType<CharacterViewManager>();
        
        if (characterViewManager != null)
        {
            // SerializedObject를 사용하여 설정들 가져오기
            SerializedObject serializedManager = new SerializedObject(characterViewManager);
            showDebugInfo = serializedManager.FindProperty("showDebugInfo").boolValue;
            enableAnimationControl = serializedManager.FindProperty("enableAnimationControl").boolValue;
        }
    }
    
    /// <summary>
    /// 새로운 CharacterViewManager 생성
    /// </summary>
    private void CreateCharacterViewManager()
    {
        GameObject managerObject = new GameObject("CharacterViewManager");
        characterViewManager = managerObject.AddComponent<CharacterViewManager>();
        
        // 기본 설정 적용
        UpdateManagerSettings();
        
        Debug.Log("CharacterViewManager가 생성되었습니다.");
    }
    
    /// <summary>
    /// 자식 캐릭터들 새로고침
    /// </summary>
    private void RefreshChildCharacters()
    {
        if (characterViewManager == null)
        {
            Debug.LogError("CharacterViewManager가 없습니다.");
            return;
        }

        // Undo 등록
        Undo.RegisterCompleteObjectUndo(characterViewManager, "Refresh Child Characters");
        
        try
        {
            // 설정 업데이트
            UpdateManagerSettings();
            
            // 자식 캐릭터들 새로고침
            characterViewManager.RefreshChildCharacters();
            
            // 에디터 상태 정리
            EditorUtility.SetDirty(characterViewManager);
            SceneView.RepaintAll();
            
            Debug.Log("자식 캐릭터들이 새로고침되었습니다.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"자식 캐릭터 새로고침 중 오류 발생: {e.Message}");
        }
    }
    
    
    /// <summary>
    /// 모든 캐릭터를 Idle 상태로 설정
    /// </summary>
    private void SetAllToIdle()
    {
        if (characterViewManager == null)
        {
            Debug.LogError("CharacterViewManager가 없습니다.");
            return;
        }
        
        characterViewManager.SetAllCharactersToAnimation(CharacterAnimationState.Idle);
        Debug.Log("모든 자식 캐릭터가 Idle 상태로 설정되었습니다.");
    }
    
    /// <summary>
    /// 선택된 애니메이션 상태로 모든 캐릭터 설정
    /// </summary>
    private void SetSelectedAnimation()
    {
        if (characterViewManager == null)
        {
            Debug.LogError("CharacterViewManager가 없습니다.");
            return;
        }
        
        characterViewManager.SetAllCharactersToAnimation(selectedAnimationState);
        Debug.Log($"모든 자식 캐릭터가 {selectedAnimationState} 상태로 설정되었습니다.");
    }
    
    /// <summary>
    /// 매니저 재초기화
    /// </summary>
    private void ReinitializeManager()
    {
        if (characterViewManager == null)
        {
            Debug.LogError("CharacterViewManager가 없습니다.");
            return;
        }
        
        characterViewManager.Reinitialize();
        Debug.Log("CharacterViewManager가 재초기화되었습니다.");
    }
    
    /// <summary>
    /// 매니저 설정 업데이트
    /// </summary>
    private void UpdateManagerSettings()
    {
        if (characterViewManager == null) return;
        
        // SerializedObject를 사용하여 설정 업데이트
        SerializedObject serializedManager = new SerializedObject(characterViewManager);
        
        // 설정 업데이트
        serializedManager.FindProperty("showDebugInfo").boolValue = showDebugInfo;
        serializedManager.FindProperty("enableAnimationControl").boolValue = enableAnimationControl;
        
        serializedManager.ApplyModifiedProperties();
        
        Debug.Log("CharacterViewManager 설정이 업데이트되었습니다.");
    }
}
