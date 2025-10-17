using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimatorGroupController : MonoBehaviour
{
    // 자식 오브젝트들의 애니메이터를 저장할 리스트
    private List<Animator> childAnimators = new List<Animator>();
    
    // 애니메이션 파라미터 이름들
    private const string CEREMONY_TRIGGER = "Ceremony";
    private const string DEATH_TRIGGER = "Death";
    private const string BLANK_TRIGGER = "Blank";
    private const string WALK_TRIGGER = "Walk";
    private const string RUN_TRIGGER = "Run";
    private const string IS_WALKING_BOOL = "IsWalking";
    private const string IS_RUNNING_BOOL = "IsRunning";
    private const string IS_ATTACKING_BOOL = "IsAttacking";
    
    // 공격 애니메이션 지속 시간 (필요에 따라 조정)
    [SerializeField] private float attackDuration = 1.0f;
    private Coroutine attackCoroutine;
    
    void Start()
    {
        // 시작할 때 모든 자식 오브젝트에서 Animator 컴포넌트를 찾아서 리스트에 추가
        GetChildAnimators();
    }
    
    void Update()
    {
        HandleInputs();
    }
    
    // 자식 오브젝트들에서 애니메이터 컴포넌트 찾기
    void GetChildAnimators()
    {
        childAnimators.Clear();
        
        // GetComponentsInChildren은 자식 오브젝트들의 컴포넌트를 재귀적으로 찾음
        Animator[] animators = GetComponentsInChildren<Animator>();
        
        foreach (Animator animator in animators)
        {
            // 자기 자신의 애니메이터는 제외 (만약 있다면)
            if (animator.gameObject != this.gameObject)
            {
                childAnimators.Add(animator);
            }
        }
        
        Debug.Log($"총 {childAnimators.Count}개의 애니메이터를 찾았습니다.");
    }
    
    // 입력 처리
    void HandleInputs()
    {
        // Ceremony 트리거 (C키)
        if (Input.GetKeyDown(KeyCode.C))
        {
            TriggerAnimation(CEREMONY_TRIGGER);
        }
        
        // Death 트리거 (K키)
        if (Input.GetKeyDown(KeyCode.K))
        {
            TriggerAnimation(DEATH_TRIGGER);
        }
        
        // Blank 트리거 (B키)
        if (Input.GetKeyDown(KeyCode.B))
        {
            TriggerAnimation(BLANK_TRIGGER);
        }
        
        // Bool 파라미터 방식 테스트
        // UpArrow - 걷기 (IsWalking = true, IsRunning = false)
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            SetBoolAnimation(IS_WALKING_BOOL, true);
            SetBoolAnimation(IS_RUNNING_BOOL, false);
            Debug.Log("걷기 모드 설정 - IsWalking=true, IsRunning=false");
            PrintCurrentAnimatorStates();
        }
        
        // DownArrow - 달리기 (IsWalking = false, IsRunning = true)
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            SetBoolAnimation(IS_WALKING_BOOL, false);
            SetBoolAnimation(IS_RUNNING_BOOL, true);
            Debug.Log("달리기 모드 설정 - IsWalking=false, IsRunning=true");
            PrintCurrentAnimatorStates();
        }
        
        // LeftArrow - 정지 (모든 이동 파라미터 false)
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SetBoolAnimation(IS_WALKING_BOOL, false);
            SetBoolAnimation(IS_RUNNING_BOOL, false);
            Debug.Log("정지 모드 설정 - IsWalking=false, IsRunning=false");
            PrintCurrentAnimatorStates();
        }
        
        // Attack (SpaceBar)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 이전 공격 코루틴이 있으면 중지
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
            }
            attackCoroutine = StartCoroutine(HandleAttack());
        }
    }
    
    // 트리거 애니메이션 실행
    void TriggerAnimation(string triggerName)
    {
        int triggerCount = 0;
        foreach (Animator animator in childAnimators)
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                // 트리거 파라미터가 존재하는지 확인
                bool hasTrigger = false;
                foreach (AnimatorControllerParameter param in animator.parameters)
                {
                    if (param.name == triggerName && param.type == AnimatorControllerParameterType.Trigger)
                    {
                        hasTrigger = true;
                        break;
                    }
                }
                
                if (hasTrigger)
                {
                    animator.SetTrigger(triggerName);
                    triggerCount++;
                    Debug.Log($"애니메이터 {animator.name}에 {triggerName} 트리거 실행됨");
                }
                else
                {
                    Debug.LogWarning($"애니메이터 {animator.name}에 {triggerName} 트리거 파라미터가 없습니다!");
                }
            }
        }
        
        if (triggerCount == 0)
        {
            Debug.LogWarning($"활성화된 애니메이터가 없거나 {triggerName} 트리거가 실행되지 않았습니다.");
        }
    }
    
    // Bool 애니메이션 파라미터 설정
    void SetBoolAnimation(string boolName, bool value)
    {
        int setCount = 0;
        foreach (Animator animator in childAnimators)
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                // 파라미터가 존재하는지 확인
                bool hasParameter = false;
                foreach (AnimatorControllerParameter param in animator.parameters)
                {
                    if (param.name == boolName && param.type == AnimatorControllerParameterType.Bool)
                    {
                        hasParameter = true;
                        break;
                    }
                }
                
                if (hasParameter)
                {
                    animator.SetBool(boolName, value);
                    setCount++;
                    Debug.Log($"✅ 애니메이터 {animator.name}에 {boolName} = {value} 설정됨");
                }
                else
                {
                    Debug.LogWarning($"애니메이터 {animator.name}에 {boolName} 파라미터가 없습니다!");
                }
            }
        }
        
        if (setCount == 0)
        {
            Debug.LogWarning($"활성화된 애니메이터가 없거나 {boolName} 파라미터가 설정되지 않았습니다.");
        }
    }
    
    // 공격 애니메이션 처리 (일정 시간 후 자동으로 false로 변경)
    IEnumerator HandleAttack()
    {
        SetBoolAnimation(IS_ATTACKING_BOOL, true);
        yield return new WaitForSeconds(attackDuration);
        SetBoolAnimation(IS_ATTACKING_BOOL, false);
    }
    
    // 에디터에서 자식 애니메이터 다시 찾기 (Inspector에서 버튼으로 사용 가능)
    [ContextMenu("Refresh Child Animators")]
    public void RefreshChildAnimators()
    {
        GetChildAnimators();
    }
    
    // Animator Controller 정보 출력 (디버깅용)
    [ContextMenu("Print Animator Info")]
    public void PrintAnimatorInfo()
    {
        foreach (Animator animator in childAnimators)
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                Debug.Log($"=== 애니메이터 {animator.name} 정보 ===");
                Debug.Log($"Controller: {animator.runtimeAnimatorController?.name}");
                
                // 파라미터 정보
                Debug.Log("파라미터 목록:");
                foreach (AnimatorControllerParameter param in animator.parameters)
                {
                    Debug.Log($"- {param.name} ({param.type})");
                }
                
                // 현재 상태 정보
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"현재 상태 길이: {stateInfo.length}");
                Debug.Log($"정규화된 시간: {stateInfo.normalizedTime}");
                Debug.Log($"애니메이션 속도: {animator.speed}");
                Debug.Log($"활성화됨: {animator.enabled}");
                Debug.Log("========================");
            }
        }
    }
    
    // 현재 애니메이터 상태 출력 (디버깅용)
    void PrintCurrentAnimatorStates()
    {
        foreach (Animator animator in childAnimators)
        {
            if (animator != null && animator.gameObject.activeInHierarchy)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"🎬 {animator.name} 현재 상태: 길이={stateInfo.length:F2}, 시간={stateInfo.normalizedTime:F2}, 속도={animator.speed}");
                
                // 현재 파라미터 값들 출력
                bool isWalking = animator.GetBool(IS_WALKING_BOOL);
                bool isRunning = animator.GetBool(IS_RUNNING_BOOL);
                bool isAttacking = animator.GetBool(IS_ATTACKING_BOOL);
                Debug.Log($"📊 파라미터 값 - IsWalking:{isWalking}, IsRunning:{isRunning}, IsAttacking:{isAttacking}");
                
                // 상태 전환 가능성 체크
                if (isWalking && !isRunning)
                {
                    Debug.Log($"⚠️ IsWalking=true인데 Walk 상태로 전환되지 않음! Transition 설정을 확인하세요.");
                }
                if (isRunning && !isWalking)
                {
                    Debug.Log($"⚠️ IsRunning=true인데 Run 상태로 전환되지 않음! Transition 설정을 확인하세요.");
                }
            }
        }
    }
}