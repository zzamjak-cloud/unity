using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 여러 캐릭터를 등록하고 애니메이션을 테스트할 수 있는 매니저
/// </summary>
public class CharacterViewManager : MonoBehaviour
{
    [Header("Character Settings")]
    [SerializeField] private bool autoFindChildCharacters = true; // 자식 캐릭터 자동 찾기
    
    [Header("Animation Settings")]
    [SerializeField] private bool enableAnimationControl = true; // 애니메이션 제어 활성화
    [SerializeField] private float animationChangeDelay = 0.1f; // 애니메이션 변경 지연 시간
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true; // 디버그 정보 표시
    
    // 자식 캐릭터들
    private List<CharacterBase> childCharacters = new List<CharacterBase>();
    
    // 애니메이션 상태 관리
    private CharacterAnimationState[] animationStates;
    private int currentAnimationIndex = 0;
    private bool isInitialized = false;
    
    // 입력 처리
    private bool leftArrowPressed = false;
    private bool rightArrowPressed = false;
    
    private void Awake()
    {
        // 애니메이션 상태 배열 초기화
        animationStates = System.Enum.GetValues(typeof(CharacterAnimationState))
            .Cast<CharacterAnimationState>()
            .ToArray();

#if UNITY_EDITOR
        // 플레이모드 상태 변경 이벤트 구독
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }
    
    private void Start()
    {
        FindChildCharacters();
    }
    
    // OnApplicationPause와 OnApplicationFocus 대신 더 안전한 방법 사용
    private void OnValidate()
    {
        // Inspector에서 값이 변경될 때만 호출
        if (Application.isEditor && !Application.isPlaying)
        {
            // 에디터에서만 실행되는 검증 로직
            FindChildCharacters();
        }
    }
    
#if UNITY_EDITOR
    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        switch (state)
        {
            case UnityEditor.PlayModeStateChange.ExitingEditMode:
                // 플레이모드 진입 직전 정리
                SafeCleanupForPlayModeTransition();
                break;
            case UnityEditor.PlayModeStateChange.ExitingPlayMode:
                // 플레이모드 종료 직전 정리
                SafeCleanupForPlayModeTransition();
                break;
        }
    }
#endif

    /// <summary>
    /// 플레이모드 전환 시 안전한 정리
    /// </summary>
    private void SafeCleanupForPlayModeTransition()
    {
        if (Application.isEditor)
        {
#if UNITY_EDITOR
            // 지연된 콜백들 정리
            UnityEditor.EditorApplication.delayCall -= OnDelayedRefresh;
            
            // Inspector 선택 해제
            UnityEditor.Selection.activeGameObject = null;
            UnityEditor.Selection.objects = new UnityEngine.Object[0];
#endif
        }
    }
    
    private void Update()
    {
        if (!isInitialized) return;
        
        // null인 캐릭터들을 정리
        CleanupNullCharacters();
        
        HandleInput();
    }
    
    /// <summary>
    /// null인 캐릭터들을 리스트에서 제거
    /// </summary>
    private void CleanupNullCharacters()
    {
        for (int i = childCharacters.Count - 1; i >= 0; i--)
        {
            if (childCharacters[i] == null || childCharacters[i].gameObject == null)
            {
                childCharacters.RemoveAt(i);
            }
        }
    }
    
    /// <summary>
    /// 자식 캐릭터들 찾기
    /// </summary>
    private void FindChildCharacters()
    {
        // 기존 캐릭터 리스트 정리
        childCharacters.Clear();
        
        // 자식 오브젝트들에서 CharacterBase 컴포넌트 찾기
        CharacterBase[] foundCharacters = GetComponentsInChildren<CharacterBase>();
        
        foreach (var character in foundCharacters)
        {
            if (character != null && character.gameObject != null)
            {
                childCharacters.Add(character);
            }
        }
        
        // 모든 캐릭터를 Idle 상태로 초기화
        SetAllCharactersToIdle();
        
        isInitialized = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"CharacterViewManager: {childCharacters.Count}개의 자식 캐릭터를 찾았습니다.");
        }
    }
    
    
    /// <summary>
    /// 모든 캐릭터를 Idle 상태로 설정
    /// </summary>
    private void SetAllCharactersToIdle()
    {
        foreach (var character in childCharacters)
        {
            if (character != null)
            {
                SetCharacterAnimation(character, CharacterAnimationState.Idle);
            }
        }
        
        currentAnimationIndex = 0; // Idle 상태로 초기화
        
        if (showDebugInfo)
        {
            Debug.Log("CharacterViewManager: 모든 자식 캐릭터가 Idle 상태로 설정되었습니다.");
        }
    }
    
    /// <summary>
    /// 입력 처리
    /// </summary>
    private void HandleInput()
    {
        if (!enableAnimationControl) return;
        
        // 왼쪽 방향키 (,) 입력 처리 - 이전 애니메이션 상태로
        if (Input.GetKeyDown(KeyCode.Comma))
        {
            if (!leftArrowPressed)
            {
                leftArrowPressed = true;
                ChangeAllCharactersAnimation(false); // false = 이전 상태로
            }
        }
        else if (Input.GetKeyUp(KeyCode.Comma))
        {
            leftArrowPressed = false;
        }
        
        // 오른쪽 방향키 (.) 입력 처리 - 다음 애니메이션 상태로
        if (Input.GetKeyDown(KeyCode.Period))
        {
            if (!rightArrowPressed)
            {
                rightArrowPressed = true;
                ChangeAllCharactersAnimation(true); // true = 다음 상태로
            }
        }
        else if (Input.GetKeyUp(KeyCode.Period))
        {
            rightArrowPressed = false;
        }
    }
    
    /// <summary>
    /// 모든 캐릭터의 애니메이션을 이전/다음 상태로 변경
    /// </summary>
    /// <param name="forward">true면 다음 상태, false면 이전 상태</param>
    private void ChangeAllCharactersAnimation(bool forward)
    {
        if (childCharacters.Count == 0) return;
        
        // 애니메이션 상태 인덱스 변경
        if (forward)
        {
            // 다음 상태로
            currentAnimationIndex = (currentAnimationIndex + 1) % animationStates.Length;
        }
        else
        {
            // 이전 상태로
            currentAnimationIndex = (currentAnimationIndex - 1 + animationStates.Length) % animationStates.Length;
        }
        
        CharacterAnimationState newState = animationStates[currentAnimationIndex];
        
        // 모든 캐릭터에 동일한 애니메이션 적용 (안전한 접근)
        for (int i = childCharacters.Count - 1; i >= 0; i--)
        {
            if (childCharacters[i] != null && childCharacters[i].gameObject != null)
            {
                SetCharacterAnimation(childCharacters[i], newState);
            }
            else
            {
                // null인 캐릭터는 리스트에서 제거
                childCharacters.RemoveAt(i);
            }
        }
        
        if (showDebugInfo)
        {
            string direction = forward ? "다음" : "이전";
            Debug.Log($"CharacterViewManager: 모든 자식 캐릭터의 애니메이션이 {direction} 상태인 {newState}로 변경되었습니다.");
        }
    }
    
    /// <summary>
    /// 특정 캐릭터의 애니메이션을 설정
    /// </summary>
    /// <param name="character">대상 캐릭터</param>
    /// <param name="animationState">설정할 애니메이션 상태</param>
    private void SetCharacterAnimation(CharacterBase character, CharacterAnimationState animationState)
    {
        if (character == null || character.gameObject == null) return;
        
        try
        {
            // 이동 애니메이션 (Idle, Walk, Run) 처리
            if (animationState == CharacterAnimationState.Idle)
            {
                // Idle 상태: 모든 이동 애니메이션 비활성화
                Animator animator = character.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetBool(GameConstants.ANIM_IS_WALKING, false);
                    animator.SetBool(GameConstants.ANIM_IS_RUNNING, false);
                    animator.SetBool(GameConstants.ANIM_IS_ATTACKING, false);
                }
            }
            else if (animationState == CharacterAnimationState.Walk)
            {
                // Walk 상태
                Animator animator = character.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetBool(GameConstants.ANIM_IS_WALKING, true);
                    animator.SetBool(GameConstants.ANIM_IS_RUNNING, false);
                    animator.SetBool(GameConstants.ANIM_IS_ATTACKING, false);
                }
            }
            else if (animationState == CharacterAnimationState.Run)
            {
                // Run 상태
                Animator animator = character.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetBool(GameConstants.ANIM_IS_WALKING, false);
                    animator.SetBool(GameConstants.ANIM_IS_RUNNING, true);
                    animator.SetBool(GameConstants.ANIM_IS_ATTACKING, false);
                }
            }
            else
            {
                // 특수 애니메이션 (Attack, Ceremony, Blank, Death)
                character.TriggerSpecialAnimation(animationState);
            }
        }
        catch (System.Exception e)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"CharacterViewManager: 캐릭터 애니메이션 설정 중 오류 발생: {e.Message}");
            }
        }
    }
    
    
    /// <summary>
    /// 특정 애니메이션 상태로 모든 캐릭터 설정
    /// </summary>
    /// <param name="animationState">설정할 애니메이션 상태</param>
    public void SetAllCharactersToAnimation(CharacterAnimationState animationState)
    {
        if (!isInitialized) return;
        
        // null인 캐릭터들 정리
        CleanupNullCharacters();
        
        // 안전한 접근으로 모든 캐릭터에 애니메이션 적용
        for (int i = childCharacters.Count - 1; i >= 0; i--)
        {
            if (childCharacters[i] != null && childCharacters[i].gameObject != null)
            {
                SetCharacterAnimation(childCharacters[i], animationState);
            }
            else
            {
                childCharacters.RemoveAt(i);
            }
        }
        
        // 현재 애니메이션 인덱스 업데이트
        for (int i = 0; i < animationStates.Length; i++)
        {
            if (animationStates[i] == animationState)
            {
                currentAnimationIndex = i;
                break;
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"CharacterViewManager: 모든 자식 캐릭터가 {animationState} 상태로 설정되었습니다.");
        }
    }
    
    /// <summary>
    /// 자식 캐릭터들 다시 찾기
    /// </summary>
    public void RefreshChildCharacters()
    {
        FindChildCharacters();
    }
    
    /// <summary>
    /// 현재 자식 캐릭터 수 반환
    /// </summary>
    /// <returns>자식 캐릭터 수</returns>
    public int GetChildCharacterCount()
    {
        return childCharacters.Count;
    }
    
    /// <summary>
    /// 현재 애니메이션 상태 반환
    /// </summary>
    /// <returns>현재 애니메이션 상태</returns>
    public CharacterAnimationState GetCurrentAnimationState()
    {
        if (animationStates != null && currentAnimationIndex < animationStates.Length)
        {
            return animationStates[currentAnimationIndex];
        }
        return CharacterAnimationState.Idle;
    }
    
    /// <summary>
    /// 뷰어 재초기화
    /// </summary>
    public void Reinitialize()
    {
        isInitialized = false;
        FindChildCharacters();
    }
    
    
    private void OnDestroy()
    {
#if UNITY_EDITOR
        // 이벤트 구독 해제
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        UnityEditor.EditorApplication.delayCall -= OnDelayedRefresh;
#endif
        
        // 안전한 정리
        if (childCharacters != null)
        {
            childCharacters.Clear();
        }
    }
    
    /// <summary>
    /// 지연된 새로고침 콜백 (안전한 접근)
    /// </summary>
    private void OnDelayedRefresh()
    {
        if (this != null)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneView.RepaintAll();
#endif
        }
    }
    
}
