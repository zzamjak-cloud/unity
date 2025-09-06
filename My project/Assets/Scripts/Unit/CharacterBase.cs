using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// 캐릭터의 기본 기능을 제공하는 추상 클래스
/// 플레이어와 적 캐릭터 모두 이 클래스를 상속받아 공통 기능을 사용합니다.
/// </summary>
public abstract class CharacterBase : MonoBehaviour, ICharacterController, ICharacterAttackEffect, ICharacterMoveEffect, ICharacterBlankEffect, ICharacterDamageEffect, ICollisionHandler
{
    [Header("Movement Settings")]
    [SerializeField] protected float moveSpeed = 5f;  // 이동 속도
    [SerializeField] protected float runSpeedMultiplier = 1.5f;  // 달리기 속도 배수
    
    [Header("Sorting System")]
    [SerializeField] protected bool enableSortingOrderAdjustment = true; // Y축 기반 정렬 순서 조절 활성화 여부
    [SerializeField] protected int baseSortingOrder = 0; // 기본 정렬 순서
    [SerializeField] protected float sortingOrderMultiplier = -10f; // Y축에 곱할 배수 (음수: 위쪽이 앞, 양수: 아래쪽이 앞) - 더 세밀한 정렬을 위해 10배 증가

    [Header("Effects")]
    [SerializeField] protected EffectManager effectManager;  // 이펙트 관리자
    
    [Header("Collision System")]
    [SerializeField] protected CharacterCollisionManager collisionManager;  // 콜리전 관리자
    [SerializeField] protected bool enableCollisionSystem = true;  // 콜리전 시스템 활성화 여부
    
    [Header("Attack Collision Objects")]
    [SerializeField] protected GameObject attackCollisionObject;  // Attack 콜리전이 적용된 GameObject
    [SerializeField] protected float attackCollisionDuration = 0.5f;  // Attack 콜리전 지속 시간
    
    [Header("Health System")]
    [SerializeField] protected int maxHealth = 100;  // 최대 체력
    [SerializeField] protected int currentHealth;  // 현재 체력
    [SerializeField] protected int attackPower = 20;  // 공격력
    [SerializeField] protected bool isDead = false;  // 사망 상태
    [SerializeField] protected float invincibilityDuration = 0.5f;  // 무적 시간 (초)
    [SerializeField] protected bool isInvincible = false;  // 무적 상태
    
    [Header("Status UI")]
    [SerializeField] protected bool enableStatusBar = true;  // 상태바 표시 여부
    

    [Header("Animation")]
    [SerializeField] protected Animator anim;  // 애니메이터 컴포넌트 (Inspector에서 직접 연결)
    
    protected Rigidbody2D rb;
    protected CharacterAnimationState currentAnimationState = CharacterAnimationState.Idle;
    protected SortingGroup sortingGroup; // 정렬 순서 조절을 위한 SortingGroup 컴포넌트
    protected int lastSortingOrder = 0; // 마지막 정렬 순서 (중복 업데이트 방지)
    
    // 메모리 할당 최적화를 위한 캐시된 변수들
    protected Vector3 cachedPosition;
    protected Vector3 cachedVelocity;
    protected float cachedSortingOrder;
    protected int cachedHealthValue;
    

    // 현재 이동 상태
    protected Vector2 currentMovement = Vector2.zero;
    protected bool isCurrentlyRunning = false;
    
    // 체력 시스템 관련 변수들
    protected float invincibilityTimer = 0f;  // 무적 타이머
    protected System.Action<int, int> onHealthChanged;  // 체력 변경 이벤트 (현재체력, 최대체력)
    protected System.Action onDeath;  // 사망 이벤트
    
    // 이벤트 리스너 관리 (메모리 할당 최적화)
    protected List<System.Action<int, int>> healthChangedListeners;
    protected List<System.Action> deathListeners;
    
    // 체력 UI 관련 변수들
    protected StatusBarUI statusBarUI;  // 상태바 UI 컴포넌트

    protected virtual void Awake()
    {
        // EffectManager 자동 설정
        if (effectManager == null)
        {
            effectManager = GetComponent<EffectManager>();
            if (effectManager == null)
            {
                effectManager = gameObject.AddComponent<EffectManager>();
                Debug.Log($"{gameObject.name}: EffectManager 컴포넌트를 자동으로 추가했습니다.");
            }
        }
        
        // 이벤트 리스너 리스트 초기화
        healthChangedListeners = new List<System.Action<int, int>>();
        deathListeners = new List<System.Action>();
    }

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Animator 컴포넌트가 Inspector에 연결되지 않았을 경우, 자식 오브젝트에서 찾기
        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        if (rb == null)
        {
            Debug.LogError("Rigidbody2D 컴포넌트가 필요합니다.");
        }
        if (anim == null)
        {
            Debug.LogError("Animator 컴포넌트가 필요합니다. Inspector에 연결하거나 자식 오브젝트에 추가해주세요.");
        }
        
        // SortingGroup 컴포넌트 찾기 또는 추가
        sortingGroup = GetComponent<SortingGroup>();
        if (sortingGroup == null)
        {
            sortingGroup = gameObject.AddComponent<SortingGroup>();
            Debug.Log($"{gameObject.name}: SortingGroup 컴포넌트를 자동으로 추가했습니다.");
        }
        
        // Attack 콜리전 GameObject 자동 찾기
        if (attackCollisionObject == null)
        {
            attackCollisionObject = transform.Find("AttackCollision")?.gameObject;
            if (attackCollisionObject == null)
            {
                Debug.LogWarning($"{gameObject.name}: Attack 콜리전 GameObject를 찾을 수 없습니다. 'AttackCollision'이라는 이름의 자식 오브젝트를 생성하거나 Inspector에서 직접 할당해주세요.");
            }
        }
        
        // CharacterCollisionManager 설정
        if (collisionManager == null)
        {
            collisionManager = GetComponent<CharacterCollisionManager>();
            if (collisionManager == null)
            {
                collisionManager = gameObject.AddComponent<CharacterCollisionManager>();
                Debug.Log($"{gameObject.name}: CharacterCollisionManager 컴포넌트를 자동으로 추가했습니다.");
            }
        }
        
        // 체력 시스템 초기화
        InitializeHealthSystem();
        
        // 콜리전 시스템 상태 확인 (디버그 모드에서만)
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[콜리전 시스템] {gameObject.name}: 콜리전 시스템 활성화 상태 = {enableCollisionSystem}");
        Debug.Log($"[콜리전 시스템] {gameObject.name}: CharacterCollisionManager = {(collisionManager != null ? "설정됨" : "NULL")}");
        if (collisionManager != null)
        {
            Debug.Log($"[콜리전 시스템] {gameObject.name}: Body 콜리전 활성화 = {IsCollisionTypeEnabled(CollisionType.Body)}");
            Debug.Log($"[콜리전 시스템] {gameObject.name}: Attack 콜리전 활성화 = {IsCollisionTypeEnabled(CollisionType.Attack)}");
            Debug.Log($"[콜리전 시스템] {gameObject.name}: Interaction 콜리전 활성화 = {IsCollisionTypeEnabled(CollisionType.Interaction)}");
        }
        #endif
        
        // 초기 정렬 순서 설정
        if (sortingGroup != null && enableSortingOrderAdjustment)
        {
            float yPosition = transform.position.y;
            lastSortingOrder = baseSortingOrder + Mathf.FloorToInt(yPosition * sortingOrderMultiplier);
            sortingGroup.sortingOrder = lastSortingOrder;
        }
        
        // 캐시된 변수들 초기화
        cachedPosition = transform.position;
        cachedVelocity = Vector3.zero;
        cachedSortingOrder = 0f;
        cachedHealthValue = currentHealth;
        
        // 공격 콜리전 초기화 (첫 번째 공격 문제 해결)
        if (collisionManager != null)
        {
            // 공격 콜리전이 비활성화 상태로 초기화되도록 보장
            DisableAttackCollision();
        }
    }

    protected virtual void Update()
    {
        // 하위 클래스에서 구현할 추상 메서드들 호출
        UpdateMovement();
        UpdateAnimation();
        
        // 체력 시스템 업데이트
        UpdateHealthSystem();
    }
    
    protected virtual void LateUpdate()
    {
        // Y축 위치에 따른 정렬 순서 조절 (SortingGroup 사용)
        UpdateSortingOrder();
    }
    
    /// <summary>
    /// Y축 위치에 따라 SortingGroup의 Order 값을 조절합니다.
    /// Y축이 높을수록 앞에 표시되고, Y축이 낮을수록 뒤에 표시됩니다.
    /// </summary>
    protected virtual void UpdateSortingOrder()
    {
        if (!enableSortingOrderAdjustment || sortingGroup == null) return;
        
        // 캐시된 변수 사용으로 메모리 할당 최적화
        cachedPosition = transform.position;
        cachedSortingOrder = cachedPosition.y * sortingOrderMultiplier;
        int newSortingOrder = baseSortingOrder + Mathf.FloorToInt(cachedSortingOrder);
        
        // 정렬 순서가 변경된 경우에만 업데이트 (중복 업데이트 방지)
        if (lastSortingOrder != newSortingOrder)
        {
            sortingGroup.sortingOrder = newSortingOrder;
            lastSortingOrder = newSortingOrder;
        }
    }

    #region ICharacterController Implementation

    /// <summary>
    /// 캐릭터의 이동을 업데이트합니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    public abstract void UpdateMovement();

    /// <summary>
    /// 캐릭터의 애니메이션을 업데이트합니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    public abstract void UpdateAnimation();

    /// <summary>
    /// 현재 이동 입력 값을 반환합니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    /// <returns>이동 방향 벡터 (정규화됨)</returns>
    public abstract Vector2 GetMovementInput();

    /// <summary>
    /// 현재 달리기 상태를 반환합니다.
    /// </summary>
    /// <returns>달리기 중이면 true</returns>
    public virtual bool IsRunning()
    {
        return isCurrentlyRunning;
    }

    /// <summary>
    /// 특수 애니메이션을 트리거합니다.
    /// </summary>
    /// <param name="animationType">애니메이션 타입</param>
    public virtual void TriggerSpecialAnimation(CharacterAnimationState animationType)
    {
        if (anim == null) return;
        
        switch (animationType)
        {
            case CharacterAnimationState.Attack:
                anim.SetTrigger(GameConstants.ANIM_ATTACK);
                break;
            case CharacterAnimationState.Ceremony:
                anim.SetTrigger(GameConstants.ANIM_CEREMONY);
                break;
            case CharacterAnimationState.Blank:
                anim.SetTrigger(GameConstants.ANIM_BLANK);
                break;
            case CharacterAnimationState.Death:
                anim.SetTrigger(GameConstants.ANIM_DEATH);
                break;
        }
    }

    /// <summary>
    /// 현재 애니메이션 상태에 따라 이동 가능 여부를 반환합니다.
    /// </summary>
    /// <returns>이동 가능하면 true</returns>
    public virtual bool CanMove()
    {
        switch (currentAnimationState)
        {
            case CharacterAnimationState.Death:
            case CharacterAnimationState.Attack:
            case CharacterAnimationState.Blank:
            case CharacterAnimationState.Ceremony:
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// 현재 애니메이션 상태를 반환합니다.
    /// </summary>
    /// <returns>현재 애니메이션 상태</returns>
    public virtual CharacterAnimationState GetCurrentAnimationState()
    {
        return currentAnimationState;
    }

    /// <summary>
    /// 특정 애니메이션 상태인지 확인합니다.
    /// </summary>
    /// <param name="state">확인할 애니메이션 상태</param>
    /// <returns>해당 상태이면 true</returns>
    public virtual bool IsInAnimationState(CharacterAnimationState state)
    {
        return currentAnimationState == state;
    }

    #endregion

    #region ICollisionHandler Implementation

    /// <summary>
    /// Body 콜리전 이벤트 처리 (적/플레이어 충돌, 피격 판정)
    /// </summary>
    /// <param name="other">충돌한 오브젝트</param>
    public virtual void OnBodyCollision(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 기본적인 피격 판정 처리
        HandleBodyCollision(other);
    }

    /// <summary>
    /// Attack 콜리전 이벤트 처리 (공격 범위 감지, 타격 판정)
    /// </summary>
    /// <param name="other">충돌한 오브젝트</param>
    public virtual void OnAttackCollision(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 기본적인 타격 판정 처리
        HandleAttackCollision(other);
    }

    /// <summary>
    /// Interaction 콜리전 이벤트 처리 (감지용, 상시 활성화)
    /// </summary>
    /// <param name="other">충돌한 오브젝트</param>
    public virtual void OnInteractionCollision(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 기본적인 감지 처리
        HandleInteractionCollision(other);
    }
    
    /// <summary>
    /// Interaction 콜리전에서 나갔을 때 처리 (감지 범위 벗어남)
    /// </summary>
    /// <param name="other">나간 오브젝트</param>
    public virtual void OnInteractionExit(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 기본적인 감지 범위 벗어남 처리
        HandleInteractionExit(other);
    }

    #endregion

    #region Protected Helper Methods

    /// <summary>
    /// 현재 애니메이션 상태를 업데이트합니다.
    /// </summary>
    protected virtual void UpdateAnimationState()
    {
        if (anim == null) return;
        
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        
        if (stateInfo.IsName("Death"))
        {
            currentAnimationState = CharacterAnimationState.Death;
        }
        else if (stateInfo.IsName("Attack"))
        {
            currentAnimationState = CharacterAnimationState.Attack;
        }
        else if (stateInfo.IsName("Blank"))
        {
            currentAnimationState = CharacterAnimationState.Blank;
        }
        else if (stateInfo.IsName("Ceremony"))
        {
            currentAnimationState = CharacterAnimationState.Ceremony;
        }
        else if (stateInfo.IsName("Run"))
        {
            currentAnimationState = CharacterAnimationState.Run;
        }
        else if (stateInfo.IsName("Walk"))
        {
            currentAnimationState = CharacterAnimationState.Walk;
        }
        else
        {
            currentAnimationState = CharacterAnimationState.Idle;
        }
    }

    /// <summary>
    /// 캐릭터의 방향을 X축 스케일을 이용해 반전시킵니다.
    /// </summary>
    /// <param name="moveX">X축 이동 값</param>
    protected virtual void FlipCharacter(float moveX)
    {
        if (moveX > 0)
        {
            // 양의 방향으로 이동 (오른쪽) -> 기본 스케일
            transform.localScale = new Vector3(1, 1, 1);
        }
        else if (moveX < 0)
        {
            // 음의 방향으로 이동 (왼쪽) -> X 스케일 -1
            transform.localScale = new Vector3(-1, 1, 1);
        }
    }

    /// <summary>
    /// 애니메이션 상태를 처리합니다.
    /// </summary>
    /// <param name="movementMagnitude">이동 크기</param>
    /// <param name="isRunning">달리기 상태 여부</param>
    protected virtual void HandleAnimations(float movementMagnitude, bool isRunning)
    {
        if (anim == null) return;
        
        // 이동 상태에 따른 애니메이션 트리거
        if (movementMagnitude > 0)
        {
            if (isRunning) // 달리기 상태일 때
            {
                anim.SetBool(GameConstants.ANIM_IS_RUNNING, true);
                anim.SetBool(GameConstants.ANIM_IS_MOVING, false);
                if (effectManager != null) effectManager.PlayMoveEffect(true);
            }
            else // 걷기 상태일 때
            {
                anim.SetBool(GameConstants.ANIM_IS_MOVING, true);
                anim.SetBool(GameConstants.ANIM_IS_RUNNING, false);
                if (effectManager != null) effectManager.PlayMoveEffect(true);
            }
        }
        else // 멈출 때
        {
            anim.SetBool(GameConstants.ANIM_IS_MOVING, false);
            anim.SetBool(GameConstants.ANIM_IS_RUNNING, false);
            if (effectManager != null) effectManager.PlayMoveEffect(false);
        }
    }

    /// <summary>
    /// 물리 기반 이동을 처리합니다.
    /// </summary>
    /// <param name="movement">이동 벡터</param>
    /// <param name="isRunning">달리기 상태 여부</param>
    protected virtual void HandlePhysicsMovement(Vector2 movement, bool isRunning)
    {
        if (rb == null) return;
        
        float currentSpeed = moveSpeed;
        if (isRunning)
        {
            currentSpeed *= runSpeedMultiplier;
        }
        
        // Rigidbody를 사용해 이동
        rb.linearVelocity = movement.normalized * currentSpeed;
        
        // 캐릭터 방향 전환 처리
        if (movement.x != 0)
        {
            FlipCharacter(movement.x);
        }
    }

    #endregion

    #region Collision Helper Methods

    /// <summary>
    /// Body 콜리전을 처리합니다.
    /// </summary>
    /// <param name="other">충돌한 오브젝트</param>
    protected virtual void HandleBodyCollision(Collider2D other)
    {
        // 기본 구현: 피격 판정만 처리
        if (other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            // 적이나 플레이어와의 충돌 처리
        }
        else if (other.CompareTag("Obstacle"))
        {
            // 장애물과의 충돌 처리
        }
    }

    /// <summary>
    /// Attack 콜리전을 처리합니다.
    /// </summary>
    /// <param name="other">충돌한 오브젝트</param>
    protected virtual void HandleAttackCollision(Collider2D other)
    {
        // 기본 구현: 타격 판정만 처리
        if (other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            // 타격받은 대상의 CharacterBase 컴포넌트 찾기
            CharacterBase targetCharacter = other.GetComponent<CharacterBase>();
            if (targetCharacter == null)
            {
                targetCharacter = other.GetComponentInParent<CharacterBase>();
            }
            
            if (targetCharacter != null && !targetCharacter.IsDead())
            {
                // 데미지 적용
                int damage = GetAttackPower();
                targetCharacter.TakeDamage(damage, this);
                
                // 타격받은 대상에게 Blank 애니메이션 실행
                TriggerTargetBlankAnimation(other);
                
                // 타격받은 대상에게 피격 이펙트 재생
                TriggerTargetDamageEffect(other);
            }
        }
        else if (other.CompareTag("Destructible"))
        {
            // 파괴 가능한 오브젝트에 대한 공격 처리
        }
    }

    /// <summary>
    /// Interaction 콜리전을 처리합니다 (감지용으로 사용).
    /// </summary>
    /// <param name="other">충돌한 오브젝트</param>
    protected virtual void HandleInteractionCollision(Collider2D other)
    {
        // 기본 구현: 감지 처리
        if (other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            // 적이나 플레이어 감지 시 처리 로직
            // 자식 클래스에서 오버라이드하여 구체적인 동작 구현
        }
        else if (other.CompareTag("Item"))
        {
            // 아이템과의 상호작용 처리
        }
        else if (other.CompareTag("Interactable"))
        {
            // 상호작용 가능한 오브젝트와의 처리
        }
        else if (other.CompareTag("NPC"))
        {
            // NPC와의 상호작용 처리
        }
    }
    
    /// <summary>
    /// Interaction 콜리전에서 나갔을 때 처리합니다 (감지 범위 벗어남).
    /// </summary>
    /// <param name="other">나간 오브젝트</param>
    protected virtual void HandleInteractionExit(Collider2D other)
    {
        // 기본 구현: 감지 범위 벗어남 처리
        if (other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            // 적이나 플레이어가 감지 범위를 벗어났을 때 처리 로직
            // 자식 클래스에서 오버라이드하여 구체적인 동작 구현
        }
    }

    #endregion


    #region ICharacterAttackEffect, ICharacterMoveEffect, ICharacterBlankEffect, ICharacterDamageEffect Implementation

    /// <summary>
    /// 먼지 이펙트를 재생하거나 정지합니다.
    /// </summary>
    /// <param name="play">true면 재생, false면 정지</param>
    public virtual void PlayMoveEffect(bool play)
    {
        if (effectManager != null)
        {
            effectManager.PlayMoveEffect(play);
        }
    }

    /// <summary>
    /// 공격 이펙트를 재생합니다.
    /// 애니메이션 이벤트에서 호출되어야 합니다.
    /// </summary>
    public virtual void PlayAttackEffect()
    {
        if (effectManager != null)
        {
            effectManager.PlayAttackEffect();
        }
        
        // 공격 콜리전 활성화 (이펙트와 동기화)
        // 첫 번째 공격 문제 해결을 위해 확실하게 활성화
        EnableAttackCollision();
        
        // 디버그 로그 (첫 번째 공격 문제 진단용)
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[공격 이펙트] {gameObject.name}: PlayAttackEffect 호출됨 - 공격 콜리전 활성화 시도");
        #endif
    }

    /// <summary>
    /// Blank 이펙트를 재생합니다.
    /// 애니메이션 이벤트에서 호출되어야 합니다.
    /// </summary>
    public virtual void PlayBlankEffect()
    {
        if (effectManager != null)
        {
            effectManager.PlayBlankEffect();
        }
    }

    /// <summary>
    /// 피격 이펙트를 재생합니다.
    /// 공격을 받았을 때 호출됩니다.
    /// </summary>
    public virtual void PlayDamageEffect()
    {
        if (effectManager != null)
        {
            effectManager.PlayDamageEffect();
        }
    }

    #endregion

    #region Attack Collision Control

    /// <summary>
    /// 타격받은 대상에게 Blank 애니메이션을 실행합니다.
    /// </summary>
    /// <param name="target">타격받은 대상</param>
    protected virtual void TriggerTargetBlankAnimation(Collider2D target)
    {
        if (target == null) return;
        
        // 타겟에서 CharacterBase 컴포넌트 찾기 (자신과 부모에서 검색)
        CharacterBase targetCharacter = target.GetComponent<CharacterBase>();
        if (targetCharacter == null)
        {
            // 자식에서 찾지 못했으면 부모에서 찾기
            targetCharacter = target.GetComponentInParent<CharacterBase>();
        }
        
        if (targetCharacter != null)
        {
            // 타겟에게 Blank 애니메이션 실행
            targetCharacter.TriggerSpecialAnimation(CharacterAnimationState.Blank);
        }
    }

    /// <summary>
    /// 타격받은 대상에게 피격 이펙트를 재생합니다.
    /// </summary>
    /// <param name="target">타격받은 대상</param>
    protected virtual void TriggerTargetDamageEffect(Collider2D target)
    {
        if (target == null) return;
        
        // 타겟에서 CharacterBase 컴포넌트 찾기 (자신과 부모에서 검색)
        CharacterBase targetCharacter = target.GetComponent<CharacterBase>();
        if (targetCharacter == null)
        {
            // 자식에서 찾지 못했으면 부모에서 찾기
            targetCharacter = target.GetComponentInParent<CharacterBase>();
        }
        
        if (targetCharacter != null)
        {
            // 타겟에게 피격 이펙트 재생
            targetCharacter.PlayDamageEffect();
        }
    }

    /// <summary>
    /// Attack 콜리전을 활성화합니다.
    /// </summary>
    public virtual void EnableAttackCollision()
    {
        if (collisionManager != null)
        {
            collisionManager.ActivateAttack();
            
            // 디버그 로그 (첫 번째 공격 문제 진단용)
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[공격 콜리전] {gameObject.name}: EnableAttackCollision 호출됨 - ActivateAttack 실행");
            #endif
        }
        else
        {
            // 디버그 로그 (첫 번째 공격 문제 진단용)
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[공격 콜리전] {gameObject.name}: collisionManager가 NULL입니다!");
            #endif
        }
    }

    /// <summary>
    /// Attack 콜리전을 비활성화합니다.
    /// </summary>
    public virtual void DisableAttackCollision()
    {
        if (collisionManager != null)
        {
            collisionManager.DeactivateAttack();
        }
    }

    /// <summary>
    /// Attack 콜리전이 활성화되어 있는지 확인합니다.
    /// </summary>
    /// <returns>Attack 콜리전이 활성화되어 있으면 true</returns>
    public virtual bool IsAttackCollisionEnabled()
    {
        if (collisionManager == null) return false;
        return collisionManager.IsAttackActive();
    }

    /// <summary>
    /// Attack 애니메이션이 끝났을 때 호출됩니다.
    /// 이제 AttackCollisionHandler가 자동으로 비활성화하므로 수동 호출이 필요하지 않습니다.
    /// </summary>
    public virtual void OnAttackAnimationEnd()
    {
        // AttackCollisionHandler가 자동으로 비활성화하므로 별도 처리 불필요
    }

    #endregion

    #region Collision System Control

    /// <summary>
    /// 콜리전 시스템을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public virtual void SetCollisionSystemEnabled(bool enabled)
    {
        enableCollisionSystem = enabled;
        
        if (collisionManager != null)
        {
            collisionManager.SetAllCollisionsEnabled(enabled);
        }
        
    }

    /// <summary>
    /// 특정 콜리전 타입을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="collisionType">콜리전 타입</param>
    /// <param name="enabled">활성화 여부</param>
    public virtual void SetCollisionTypeEnabled(CollisionType collisionType, bool enabled)
    {
        if (collisionManager == null) return;
        
        switch (collisionType)
        {
            case CollisionType.Body:
                collisionManager.SetBodyCollisionEnabled(enabled);
                break;
            case CollisionType.Attack:
                collisionManager.SetAttackCollisionEnabled(enabled);
                break;
            case CollisionType.Interaction:
                collisionManager.SetInteractionCollisionEnabled(enabled);
                break;
        }
        
    }

    /// <summary>
    /// 특정 콜리전 타입이 활성화되어 있는지 확인합니다.
    /// </summary>
    /// <param name="collisionType">확인할 콜리전 타입</param>
    /// <returns>활성화되어 있으면 true</returns>
    public virtual bool IsCollisionTypeEnabled(CollisionType collisionType)
    {
        if (collisionManager == null) return false;
        return collisionManager.IsCollisionEnabled(collisionType);
    }

    /// <summary>
    /// 콜리전 로깅을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public virtual void SetCollisionLoggingEnabled(bool enabled)
    {
        if (collisionManager == null) return;
        
        collisionManager.SetCollisionLoggingEnabled(enabled);
    }
    
    /// <summary>
    /// 정렬 순서 조절을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public virtual void SetSortingOrderAdjustmentEnabled(bool enabled)
    {
        enableSortingOrderAdjustment = enabled;
        
        if (enabled && sortingGroup != null)
        {
            // 즉시 현재 위치에 맞는 정렬 순서 적용
            UpdateSortingOrder();
        }
    }
    
    /// <summary>
    /// 수동으로 정렬 순서를 설정합니다.
    /// </summary>
    /// <param name="order">설정할 정렬 순서</param>
    public virtual void SetSortingOrder(int order)
    {
        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = order;
            lastSortingOrder = order;
        }
    }
    
    /// <summary>
    /// 현재 정렬 순서를 반환합니다.
    /// </summary>
    /// <returns>현재 정렬 순서</returns>
    public virtual int GetCurrentSortingOrder()
    {
        return sortingGroup != null ? sortingGroup.sortingOrder : 0;
    }

    #endregion
    
    #region Health System
    
    /// <summary>
    /// 체력 시스템 초기화
    /// </summary>
    protected virtual void InitializeHealthSystem()
    {
        // 체력 UI 먼저 초기화 (이벤트 리스너 등록 전)
        InitializeStatusUI();
        
        // 체력 설정 (UI 초기화 후)
        currentHealth = maxHealth;
        isDead = false;
        isInvincible = false;
        invincibilityTimer = 0f;
        
        // 초기 체력 UI 업데이트 (이벤트 없이 직접)
        if (statusBarUI != null)
        {
            statusBarUI.UpdateHealthDisplay(currentHealth, maxHealth);
        }
    }
    
    /// <summary>
    /// 상태 UI 초기화
    /// </summary>
    protected virtual void InitializeStatusUI()
    {
        if (!enableStatusBar) return;
        
        // 자동으로 StatusBarUI 컴포넌트 찾기
        statusBarUI = GetComponentInChildren<StatusBarUI>();
        
        if (statusBarUI == null)
        {
            // 자식 오브젝트에서 StatusBarUI 찾기
            Transform statusBarTransform = transform.Find("StatusBar");
            if (statusBarTransform != null)
            {
                statusBarUI = statusBarTransform.GetComponent<StatusBarUI>();
            }
        }
        
        if (statusBarUI != null)
        {
            // 상태바 설정 적용 (기본 오프셋 사용)
            statusBarUI.SetSettings(Vector3.zero, true, true);
            
            // 초기 상태바 표시
            statusBarUI.SetVisible(true);
        }
        else
        {
            Debug.LogWarning($"[상태 UI] {gameObject.name}: StatusBarUI 컴포넌트를 찾을 수 없습니다. 상태바가 표시되지 않습니다.");
        }
    }
    
    /// <summary>
    /// 체력 시스템 업데이트 (무적 시간 관리)
    /// </summary>
    protected virtual void UpdateHealthSystem()
    {
        if (isInvincible && invincibilityTimer > 0)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0)
            {
                isInvincible = false;
            }
        }
    }
    
    /// <summary>
    /// 데미지를 받습니다.
    /// </summary>
    /// <param name="damage">받을 데미지</param>
    /// <param name="attacker">공격자</param>
    public virtual void TakeDamage(int damage, CharacterBase attacker = null)
    {
        if (isDead || isInvincible) return;
        
        // 캐시된 변수 사용으로 메모리 할당 최적화
        cachedHealthValue = Mathf.Max(0, currentHealth - damage);
        currentHealth = cachedHealthValue;
        
        // 체력 변경 이벤트 호출 (최적화된 방식)
        if (healthChangedListeners != null)
        {
            for (int i = 0; i < healthChangedListeners.Count; i++)
            {
                healthChangedListeners[i]?.Invoke(currentHealth, maxHealth);
            }
        }
        
        // 무적 상태 시작
        StartInvincibility();
        
        // 피격 이펙트 재생
        PlayDamageEffect();
        
        // 체력이 0 이하가 되면 사망 처리
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    /// <summary>
    /// 체력을 회복합니다.
    /// </summary>
    /// <param name="healAmount">회복량</param>
    public virtual void Heal(int healAmount)
    {
        if (isDead) return;
        
        // 캐시된 변수 사용으로 메모리 할당 최적화
        cachedHealthValue = Mathf.Min(maxHealth, currentHealth + healAmount);
        currentHealth = cachedHealthValue;
        
        // 체력 변경 이벤트 호출 (최적화된 방식)
        if (healthChangedListeners != null)
        {
            for (int i = 0; i < healthChangedListeners.Count; i++)
            {
                healthChangedListeners[i]?.Invoke(currentHealth, maxHealth);
            }
        }
    }
    
    /// <summary>
    /// 무적 상태를 시작합니다.
    /// </summary>
    protected virtual void StartInvincibility()
    {
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;
    }
    
    /// <summary>
    /// 사망 처리
    /// </summary>
    protected virtual void Die()
    {
        if (isDead) return;
        
        isDead = true;
        
        // 체력바 숨기기
        if (statusBarUI != null)
        {
            statusBarUI.SetVisible(false);
        }
        
        // 사망 애니메이션 실행
        TriggerSpecialAnimation(CharacterAnimationState.Death);
        
        // 사망 이벤트 호출 (최적화된 방식)
        if (deathListeners != null)
        {
            for (int i = 0; i < deathListeners.Count; i++)
            {
                deathListeners[i]?.Invoke();
            }
        }
        
        // 사망 처리 (하위 클래스에서 오버라이드)
        OnDeath();
    }
    
    /// <summary>
    /// 사망 시 추가 처리 (하위 클래스에서 오버라이드)
    /// </summary>
    protected virtual void OnDeath()
    {
        // 기본 구현: 3초 후 오브젝트 비활성화
        Invoke(nameof(DisableAfterDeath), 3f);
    }
    
    /// <summary>
    /// 사망 후 오브젝트 비활성화
    /// </summary>
    protected virtual void DisableAfterDeath()
    {
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 공격력을 반환합니다.
    /// </summary>
    /// <returns>공격력</returns>
    public virtual int GetAttackPower()
    {
        return attackPower;
    }
    
    /// <summary>
    /// 현재 체력을 반환합니다.
    /// </summary>
    /// <returns>현재 체력</returns>
    public virtual int GetCurrentHealth()
    {
        return currentHealth;
    }
    
    /// <summary>
    /// 최대 체력을 반환합니다.
    /// </summary>
    /// <returns>최대 체력</returns>
    public virtual int GetMaxHealth()
    {
        return maxHealth;
    }
    
    /// <summary>
    /// 사망 상태를 반환합니다.
    /// </summary>
    /// <returns>사망 여부</returns>
    public virtual bool IsDead()
    {
        return isDead;
    }
    
    /// <summary>
    /// 무적 상태를 반환합니다.
    /// </summary>
    /// <returns>무적 여부</returns>
    public virtual bool IsInvincible()
    {
        return isInvincible;
    }
    
    /// <summary>
    /// 체력 변경 이벤트에 리스너를 추가합니다.
    /// </summary>
    /// <param name="callback">콜백 함수</param>
    public virtual void AddHealthChangedListener(System.Action<int, int> callback)
    {
        if (healthChangedListeners != null && !healthChangedListeners.Contains(callback))
        {
            healthChangedListeners.Add(callback);
        }
    }
    
    /// <summary>
    /// 체력 변경 이벤트에서 리스너를 제거합니다.
    /// </summary>
    /// <param name="callback">콜백 함수</param>
    public virtual void RemoveHealthChangedListener(System.Action<int, int> callback)
    {
        if (healthChangedListeners != null)
        {
            healthChangedListeners.Remove(callback);
        }
    }
    
    /// <summary>
    /// 사망 이벤트에 리스너를 추가합니다.
    /// </summary>
    /// <param name="callback">콜백 함수</param>
    public virtual void AddDeathListener(System.Action callback)
    {
        if (deathListeners != null && !deathListeners.Contains(callback))
        {
            deathListeners.Add(callback);
        }
    }
    
    /// <summary>
    /// 사망 이벤트에서 리스너를 제거합니다.
    /// </summary>
    /// <param name="callback">콜백 함수</param>
    public virtual void RemoveDeathListener(System.Action callback)
    {
        if (deathListeners != null)
        {
            deathListeners.Remove(callback);
        }
    }
    
    /// <summary>
    /// 체력바 표시/숨기기
    /// </summary>
    /// <param name="show">표시 여부</param>
    public virtual void SetHealthBarVisible(bool show)
    {
        if (statusBarUI != null)
        {
            statusBarUI.SetVisible(show);
        }
    }
    
    /// <summary>
    /// 체력바 오프셋 설정
    /// </summary>
    /// <param name="offset">오프셋</param>
    public virtual void SetHealthBarOffset(Vector3 offset)
    {
        if (statusBarUI != null)
        {
            statusBarUI.SetSettings(offset, true, true);
        }
    }
    
    /// <summary>
    /// 체력바 색상 설정
    /// </summary>
    /// <param name="healthy">건강한 상태 색상</param>
    /// <param name="warning">경고 상태 색상</param>
    /// <param name="danger">위험 상태 색상</param>
    public virtual void SetHealthBarColors(Color healthy, Color warning, Color danger)
    {
        if (statusBarUI != null)
        {
            statusBarUI.SetHealthColors(healthy, warning, danger);
        }
    }
    
    #endregion
    
    #region Memory Management
    
    /// <summary>
    /// 오브젝트가 파괴될 때 메모리 정리를 수행합니다.
    /// </summary>
    protected virtual void OnDestroy()
    {
        // 이벤트 정리
        onHealthChanged = null;
        onDeath = null;
        
        // 이벤트 리스너 리스트 정리
        if (healthChangedListeners != null)
        {
            healthChangedListeners.Clear();
            healthChangedListeners = null;
        }
        if (deathListeners != null)
        {
            deathListeners.Clear();
            deathListeners = null;
        }
        
        // 컴포넌트 참조 정리
        rb = null;
        anim = null;
        sortingGroup = null;
        effectManager = null;
        collisionManager = null;
        attackCollisionObject = null;
        statusBarUI = null;
    }
    
    #endregion
}
