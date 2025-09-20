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
    [SerializeField] protected float moveSpeed = GameConstants.DEFAULT_MOVE_SPEED;  // 이동 속도
    [SerializeField] protected float runSpeedMultiplier = GameConstants.DEFAULT_RUN_SPEED_MULTIPLIER;  // 달리기 속도 배수
    
    [Header("Sorting System")]
    [SerializeField] protected bool enableSortingOrderAdjustment = true; // Y축 기반 정렬 순서 조절 활성화 여부
    [SerializeField] protected int baseSortingOrder = GameConstants.DEFAULT_BASE_SORTING_ORDER; // 기본 정렬 순서
    [SerializeField] protected float sortingOrderMultiplier = GameConstants.DEFAULT_SORTING_ORDER_MULTIPLIER; // Y축에 곱할 배수 (음수: 위쪽이 앞, 양수: 아래쪽이 앞) - 더 세밀한 정렬을 위해 10배 증가

    [Header("Effects")]
    [SerializeField] protected EffectManager effectManager;  // 이펙트 관리자
    
    [Header("Collision System")]
    [SerializeField] protected CharacterCollisionManager collisionManager;  // 콜리전 관리자
    [SerializeField] protected bool enableCollisionSystem = true;  // 콜리전 시스템 활성화 여부
    
    [Header("Health System")]
    [SerializeField] protected int maxHealth = 100;  // 최대 체력
    [SerializeField] protected int currentHealth;  // 현재 체력
    [SerializeField] protected int attackPower = 20;  // 공격력
    [SerializeField] protected bool isDead = false;  // 사망 상태
    [SerializeField] protected float invincibilityDuration = GameConstants.PLAYER_INVINCIBILITY_DURATION;  // 무적 시간 (초)
    [SerializeField] protected bool isInvincible = false;  // 무적 상태
    
    [Header("Status UI")]
    [SerializeField] protected bool enableStatusBar = true;  // 상태바 표시 여부
    

    [Header("Animation")]
    [SerializeField] protected Animator anim;  // 애니메이터 컴포넌트 (Inspector에서 직접 연결)
    [SerializeField] protected Transform pivotTransform;  // 캐릭터 시각적 요소의 Pivot Transform (좌우 반전용)
    
    protected Rigidbody2D rb;
    protected CharacterAnimationState currentAnimationState = CharacterAnimationState.Idle;
    protected SortingGroup sortingGroup; // 정렬 순서 조절을 위한 SortingGroup 컴포넌트
    protected int lastSortingOrder = 0; // 마지막 정렬 순서 (중복 업데이트 방지)
    
    // 공격 상태 추적
    protected bool isAttacking = false; // 공격 중인지 여부
    
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
        // 이벤트 리스너 리스트 초기화
        healthChangedListeners = new List<System.Action<int, int>>();
        deathListeners = new List<System.Action>();
    }

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (effectManager == null) {effectManager = GetComponent<EffectManager>();}
        if (collisionManager == null) {collisionManager = GetComponent<CharacterCollisionManager>();}
        if (anim == null) {anim = GetComponent<Animator>();}
        if (pivotTransform == null) {pivotTransform = transform.Find("Pivot");}
        if (sortingGroup == null) {sortingGroup = GetComponent<SortingGroup>();}

        InitializeCollisionSystem();  // 콜리전 시스템 초기화
        InitializeHealthSystem();  // 체력 시스템 초기화
        
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
        
        SetAttackRangeCollisionEnabled(true);  // AttackRange 콜리전 초기화
    }

    protected virtual void Update()
    {
        UpdateMovement();  // 이동
        UpdateAnimation();  // 애니메이션
        UpdateHealthSystem();  // 체력시스템
    }
    
    protected virtual void LateUpdate()
    {
        UpdateSortingOrder();  // 정렬 순서 업데이트
    }
    
    // Y축 위치에 따라 SortingGroup의 Order값 조절
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

    public abstract void UpdateMovement();  // 이동

    public abstract void UpdateAnimation();  // 애니메이션

    public abstract Vector2 GetMovementInput();  // 이동 입력

    public virtual bool IsRunning() { return isCurrentlyRunning; }  // 달리기 상태

    // 특수 애니메이션 트리거 (Attack, Ceremony, Blank, Death)
    public virtual void TriggerSpecialAnimation(CharacterAnimationState animationType)
    {
        switch (animationType)
        {
            case CharacterAnimationState.Attack:
                isAttacking = true; // 공격 시작
                FaceNearestEnemy();  // 공격 전 가장 가까운 적을 바라보도록 Flip 처리
                anim.SetBool(GameConstants.ANIM_IS_ATTACKING, true);
                // AttackEffect는 OnAttackAnimationEvent()에서 실행되도록 함
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

    // 현재 애니메이션 상태에 따라 이동 가능 여부를 반환합니다. (Death, Attack, Blank, Ceremony 일때는 false, 나머지는 true)
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

    // 현재 애니메이션 상태를 반환합니다. return currentAnimationState;
    public virtual CharacterAnimationState GetCurrentAnimationState()
    { return currentAnimationState; }

    // 특정 애니메이션 상태인지 확인합니다. return currentAnimationState == state;
    public virtual bool IsInAnimationState(CharacterAnimationState state)
    { return currentAnimationState == state; }

    #endregion



    #region ICollisionHandler Implementation

    // Body 콜리전 이벤트 처리 (적/플레이어 충돌, 피격 판정)
    public virtual void OnBodyCollision(Collider2D other)
    { HandleBodyCollision(other); }

    // AttackRange 콜리전 이벤트 처리 (공격 범위 진입)
    public virtual void OnAttackRangeEnter(Collider2D other)
    { HandleAttackRangeEnter(other); }

    // AttackRange 콜리전에서 나갔을 때 처리 (공격 범위 벗어남)
    public virtual void OnAttackRangeExit(Collider2D other)
    { HandleAttackRangeExit(other); }  // 기본적인 공격 범위 벗어남 처리
    
    // 애니메이션 이벤트에서 호출되는 공격 성공 판정
    public virtual void OnAttackHit(Collider2D other)
    { HandleAttackHit(other); }  // 기본적인 공격 성공 처리

    #endregion



    #region Protected Helper Methods

    // 현재 애니메이션 상태를 업데이트합니다.
    protected virtual void UpdateAnimationState()
    {
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        
        if (stateInfo.IsName(GameConstants.ANIM_STATE_DEATH))
        {
            currentAnimationState = CharacterAnimationState.Death;
        }
        else if (stateInfo.IsName(GameConstants.ANIM_STATE_ATTACK))
        {
            currentAnimationState = CharacterAnimationState.Attack;
        }
        else if (stateInfo.IsName(GameConstants.ANIM_STATE_BLANK))
        {
            currentAnimationState = CharacterAnimationState.Blank;
        }
        else if (stateInfo.IsName(GameConstants.ANIM_STATE_CEREMONY))
        {
            currentAnimationState = CharacterAnimationState.Ceremony;
        }
        else if (stateInfo.IsName(GameConstants.ANIM_STATE_RUN))
        {
            currentAnimationState = CharacterAnimationState.Run;
        }
        else if (stateInfo.IsName(GameConstants.ANIM_STATE_WALK))
        {
            currentAnimationState = CharacterAnimationState.Walk;
        }
        else
        {
            currentAnimationState = CharacterAnimationState.Idle;
        }
    }

    // 캐릭터의 방향을 Pivot Transform의 X축 스케일을 이용해 반전시킵니다.
    protected virtual void FlipCharacter(float moveX)
    {
        if (moveX > 0)
        {
            pivotTransform.localScale = new Vector3(1, 1, 1);
        }
        else if (moveX < 0)
        {   
            pivotTransform.localScale = new Vector3(-1, 1, 1);
        }
    }

    // 기본, 걷기, 달리기 애니메이션 상태를 처리합니다.
    protected virtual void HandleAnimations(float movementMagnitude, bool isRunning)
    {
        // 이동 상태에 따른 애니메이션 트리거
        if (movementMagnitude > 0)
        {
            if (isRunning) // 달리기 상태일 때
            {
                anim.SetBool(GameConstants.ANIM_IS_RUNNING, true);
                anim.SetBool(GameConstants.ANIM_IS_WALKING, false);
                effectManager?.PlayMoveEffect(true);
            }
            else // 걷기 상태일 때
            {
                anim.SetBool(GameConstants.ANIM_IS_WALKING, true);
                anim.SetBool(GameConstants.ANIM_IS_RUNNING, false);
                effectManager?.PlayMoveEffect(true);
            }
        }
        else // 멈출 때
        {
            anim.SetBool(GameConstants.ANIM_IS_WALKING, false);
            anim.SetBool(GameConstants.ANIM_IS_RUNNING, false);
            effectManager?.PlayMoveEffect(false);
        }
    }

    // rigidbody를 사용해 물리 기반 이동을 처리합니다.
    protected virtual void HandlePhysicsMovement(Vector2 movement, bool isRunning)
    {
        float currentSpeed = moveSpeed;
        if (isRunning)
            currentSpeed *= runSpeedMultiplier;
        
        rb.linearVelocity = movement.normalized * currentSpeed;
        
        // 캐릭터 방향 전환 처리 (공격 중이 아닐 때만)
        if (movement.x != 0 && !isAttacking)
            FlipCharacter(movement.x);
    }

    #endregion



    #region Collision Helper Methods

    // Body 콜리전을 처리합니다.
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

    // AttackRange 콜리전을 처리합니다 (공격 범위 진입).
    protected virtual void HandleAttackRangeEnter(Collider2D other)
    {
        // 기본 구현: 공격 범위 진입 처리
        if (other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            // 적이나 플레이어가 공격 범위에 진입했을 때 처리 로직
            // 자식 클래스에서 오버라이드하여 구체적인 동작 구현
        }
    }

    // AttackRange 콜리전에서 나갔을 때 처리합니다 (공격 범위 벗어남).
    protected virtual void HandleAttackRangeExit(Collider2D other)
    {
        // 기본 구현: 공격 범위 벗어남 처리
        if (other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            // 적이나 플레이어가 공격 범위를 벗어났을 때 처리 로직
            // 자식 클래스에서 오버라이드하여 구체적인 동작 구현
        }
    }
    
    // 애니메이션 이벤트에서 호출되는 공격 성공 판정을 처리합니다.
    protected virtual void HandleAttackHit(Collider2D other)
    {
        // 기본 구현: 공격 성공 처리
        if (other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            // 타격받은 대상의 CharacterBase 컴포넌트 찾기
            CharacterBase targetCharacter = other.GetComponent<CharacterBase>();

            if (targetCharacter == null)
                targetCharacter = other.GetComponentInParent<CharacterBase>();
            
            if (targetCharacter != null && !targetCharacter.IsDead())  // 타격받은 대상이 살아있을 때
            {
                // AttackEffect는 OnAttackAnimationEvent()에서 실행되므로 여기서는 제거
                
                // 데미지 적용
                int damage = GetAttackPower();
                targetCharacter.TakeDamage(damage, this);
                
                TriggerTargetBlankAnimation(other);  // 타격받은 대상에게 Blank 애니메이션 실행
                TriggerTargetDamageEffect(other);  // 타격받은 대상에게 피격 이펙트 재생
            }
        }
        else if (other.CompareTag("Destructible"))
        {
            // 파괴 가능한 오브젝트에 대한 공격 처리
        }
    }

    #endregion


    #region ICharacterAttackEffect, ICharacterMoveEffect, ICharacterBlankEffect, ICharacterDamageEffect Implementation

    // 이동시 먼지 이펙트를 재생/정지
    public virtual void PlayMoveEffect(bool play)
    { effectManager?.PlayMoveEffect(play); }

    // 공격 이펙트를 재생합니다. (애니메이션 이벤트에서 호출되어야 합니다.)
    public virtual void PlayAttackEffect()
    { effectManager?.PlayAttackEffect(); }

    // Blank 이펙트를 재생합니다. (애니메이션 이벤트에서 호출되어야 합니다.)
    public virtual void PlayBlankEffect()
    { effectManager?.PlayBlankEffect(); }

    // 피격 이펙트를 재생합니다. (공격을 받았을 때 호출됩니다.)
    public virtual void PlayDamageEffect()
    { effectManager?.PlayDamageEffect(); }

    #endregion



    #region Attack Collision Control

    // 타격받은 대상에게 Blank 애니메이션을 실행합니다. (target : 타격받은 대상)
    protected virtual void TriggerTargetBlankAnimation(Collider2D target)
    {
        if (target == null) return;
        
        // 타겟에서 CharacterBase 컴포넌트 찾기 (타겟과 타겟의 부모 검색)
        CharacterBase targetCharacter = target.GetComponent<CharacterBase>();
        if (targetCharacter == null)
            targetCharacter = target.GetComponentInParent<CharacterBase>();
        
        if (targetCharacter != null)
        {
            // 공격 중이 아닐 때만 Blank 애니메이션 실행
            if (!targetCharacter.isAttacking)
                targetCharacter.TriggerSpecialAnimation(CharacterAnimationState.Blank);
        }
    }

    // 타격받은 대상에게 피격 이펙트를 재생합니다.
    protected virtual void TriggerTargetDamageEffect(Collider2D target)
    {
        if (target == null) return;
        
        CharacterBase targetCharacter = target.GetComponent<CharacterBase>();
        if (targetCharacter == null)
            targetCharacter = target.GetComponentInParent<CharacterBase>();
        
        if (targetCharacter != null)
            targetCharacter.PlayDamageEffect();
    }

    // AttackRange 콜리전을 활성화/비활성화합니다.
    public virtual void SetAttackRangeCollisionEnabled(bool enabled)
    { 
        if (collisionManager != null) 
            collisionManager.SetAttackRangeCollisionEnabled(enabled);
    }
    
    // AttackRange 콜리전을 활성화합니다. (편의 메서드)
    public virtual void EnableAttackRangeCollision()
    { SetAttackRangeCollisionEnabled(true); }

    // AttackRange 콜리전을 비활성화합니다. (편의 메서드)
    public virtual void DisableAttackRangeCollision()
    { SetAttackRangeCollisionEnabled(false); }

    // AttackRange 콜리전이 활성화되어 있는지 확인합니다.
    public virtual bool IsAttackRangeCollisionEnabled()
    {
        if (collisionManager == null) return false;
        return collisionManager.IsAttackRangeColliderEnabled();
    }
    
    // AttackRange 내 가장 가까운 적을 반환합니다.
    public virtual Collider2D GetNearestEnemy()
    {
        if (collisionManager == null)
            return null;

        return collisionManager.GetNearestEnemy();
    }
    
    // 공격 전 가장 가까운 적을 바라보도록 Flip 처리합니다. (Enemy 전용) (Player 전용은 사용하지 않습니다.)
    protected virtual void FaceNearestEnemy(bool force = false)
    {
        // 공격 중이 아닐 때만 타겟 변경 허용 (force가 true이면 예외)
        if (isAttacking && !force) return;
        
        Collider2D nearestEnemy = GetNearestEnemy();
        if (nearestEnemy != null)
        {
            Vector3 directionToTarget = (nearestEnemy.transform.position - transform.position).normalized;
            if (directionToTarget.x != 0)
                FlipCharacter(directionToTarget.x);
        }
    }
    
    // 공격을 시작합니다. (공격 쿨다운 리셋, 적을 바라보기, 애니메이션 시작)
    // 하위 클래스에서 구현해야 합니다.
    // 주의: 하위 클래스에서 isAttacking = true를 먼저 설정한 후 FaceNearestEnemy()를 호출해야 합니다.
    // AttackEffect는 OnAttackAnimationEvent()에서 실행됩니다.
    protected virtual void StartAttackSequence()
    {
        FaceNearestEnemy();  // 공격 전 가장 가까운 적을 바라보도록 Flip 처리
        // AttackEffect는 OnAttackAnimationEvent()에서 실행되도록 함
    }
    
    // AttackRange 내 적이 있는지 확인합니다.
    public virtual bool HasEnemiesInRange()
    {
        if (collisionManager == null) return false;
        return collisionManager.HasEnemiesInRange();
    }
    
    // AttackRange 내 적의 수를 반환합니다.
    public virtual int GetEnemyCountInRange()
    {
        if (collisionManager == null) return 0;
        return collisionManager.GetEnemyCountInRange();
    }
    
    // 애니메이션 이벤트에서 호출되는 공격 성공 판정 메서드
    public virtual void OnAttackAnimationEvent()
    {
        // AttackEffect 재생 (애니메이션 이벤트 타이밍에 맞춰 실행)
        PlayAttackEffect();
        
        // 공격 성공 판정
        collisionManager.OnAttackAnimationEvent();
    }
    
    // Death 애니메이션의 길이를 반환합니다.
    public virtual float GetDeathAnimationLength()
    {
        // Death 애니메이션 클립의 길이를 가져옵니다
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName(GameConstants.ANIM_STATE_DEATH))
            return stateInfo.length;
        
        // Death 애니메이션이 현재 재생 중이 아닌 경우, 애니메이션 클립에서 직접 찾기
        AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            if (clip.name.Contains(GameConstants.ANIM_STATE_DEATH))
                return clip.length;
        }
        
        return 2.0f; // 기본값 (2초)
    }
    
    /// <summary>
    /// Attack 애니메이션의 길이를 반환합니다.
    /// </summary>
    public virtual float GetAttackAnimationLength()
    {
        if (anim == null) return 1.0f; // 기본값
        
        // Attack 애니메이션 클립의 길이를 가져옵니다
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Attack"))
        {
            return stateInfo.length;
        }
        
        // Attack 애니메이션이 현재 재생 중이 아닌 경우, 애니메이션 클립에서 직접 찾기
        AnimationClip[] clips = anim.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            if (clip.name.ToLower().Contains("attack") || clip.name.ToLower().Contains("atk"))
            {
                return clip.length;
            }
        }
        
        return 1.0f; // 기본값 (1초)
    }

    #endregion

    #region Collision System Control

    /// <summary>
    /// 콜리전 시스템을 초기화합니다.
    /// enableCollisionSystem이 false인 경우 이벤트 핸들러를 등록하지 않습니다.
    /// </summary>
    protected virtual void InitializeCollisionSystem()
    {
        if (!enableCollisionSystem)
        {
            // 콜리전 시스템이 비활성화된 경우 CharacterCollisionManager 비활성화
            if (collisionManager != null)
            {
                collisionManager.enabled = false;
            }
            return;
        }
        
        // 콜리전 시스템이 활성화된 경우 정상 초기화
        if (collisionManager != null)
        {
            collisionManager.enabled = true;
        }
    }

    // 콜리전 시스템을 활성화/비활성화합니다. (enabled : 활성화 여부)
    public virtual void SetCollisionSystemEnabled(bool enabled)
    {
        enableCollisionSystem = enabled;
        
        if (collisionManager != null)
        {
            collisionManager.SetAllCollisionsEnabled(enabled);
        }
        
    }

    // 특정 콜리전 타입을 활성화/비활성화합니다. (collisionType : 콜리전 타입) (enabled : 활성화 여부)
    public virtual void SetCollisionTypeEnabled(CollisionType collisionType, bool enabled)
    {
        if (collisionManager == null) return;
        
        switch (collisionType)
        {
            case CollisionType.Body:
                collisionManager.SetBodyCollisionEnabled(enabled);
                break;
            case CollisionType.AttackRange:
                collisionManager.SetAttackRangeCollisionEnabled(enabled);
                break;
        }
        
    }

    // 특정 콜리전 타입이 활성화되어 있는지 확인합니다.
    public virtual bool IsCollisionTypeEnabled(CollisionType collisionType)
    {
        if (collisionManager == null) return false;
        return collisionManager.IsCollisionEnabled(collisionType);
    }

    // 콜리전 로깅을 활성화/비활성화합니다. (enabled : 활성화 여부)
    public virtual void SetCollisionLoggingEnabled(bool enabled)
    {
        if (collisionManager == null) return;
        
        collisionManager.SetCollisionLoggingEnabled(enabled);
    }
    
    // 정렬 순서 조절을 활성화/비활성화합니다. (enabled : 활성화 여부)
    public virtual void SetSortingOrderAdjustmentEnabled(bool enabled)
    {
        enableSortingOrderAdjustment = enabled;
        
        if (enabled && sortingGroup != null)
        {
            // 즉시 현재 위치에 맞는 정렬 순서 적용
            UpdateSortingOrder();
        }
    }
    
    // 수동으로 정렬 순서를 설정합니다. (order : 설정할 정렬 순서)
    public virtual void SetSortingOrder(int order)
    {
        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = order;
            lastSortingOrder = order;
        }
    }
    
    // 현재 정렬 순서를 반환합니다.
    public virtual int GetCurrentSortingOrder()
    {
        return sortingGroup != null ? sortingGroup.sortingOrder : 0;
    }
    
    // Pivot Transform을 설정합니다.
    public virtual void SetPivotTransform(Transform pivot)
    {
        pivotTransform = pivot;
    }
    
    // Pivot Transform을 반환합니다.
    public virtual Transform GetPivotTransform()
    {
        return pivotTransform;
    }

    #endregion
    
    #region Health System
    
    // 체력 시스템 초기화
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
            statusBarUI.UpdateHealthDisplay(currentHealth, maxHealth);
    }
    
    // 상태 UI 초기화
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
            // 상태바 설정 적용 (기본 오프셋 유지)
            statusBarUI.SetSettings(Vector3.zero, true, true);
            
            // 초기 상태바 표시
            statusBarUI.SetVisible(true);
            
            // 이벤트 리스너 직접 등록 (Start() 함수가 호출되지 않을 수 있으므로)
            statusBarUI.ReinitializeEventListeners(this);
        }
        else
        {
            Debug.LogWarning($"CharacterBase: StatusBarUI not found for {gameObject.name}");
        }
    }
    
    // 체력 시스템 업데이트 (무적 시간 관리)
    protected virtual void UpdateHealthSystem()
    {
        if (IsInvincible() && invincibilityTimer > 0)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0)
            {
                isInvincible = false;
            }
        }
    }
    
    // 데미지를 받습니다. (damage : 받을 데미지) (attacker : 공격자)
    public virtual void TakeDamage(int damage, CharacterBase attacker = null)
    {
        if (IsDead() || IsInvincible()) 
        {
            return;
        }
        
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
    
    // 체력을 회복합니다. (healAmount : 회복량)
    public virtual void Heal(int healAmount)
    {
        if (IsDead()) return;
        
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
    
    // 무적 상태를 시작합니다.
    protected virtual void StartInvincibility()
    {
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;
    }
    
    // 사망 처리
    protected virtual void Die()
    {
        isAttacking = false;
        isDead = true;
        collisionManager.SetAllCollisionsEnabled(false);  // 모든 콜리전 비활성화
        statusBarUI.SetVisible(false);  // 체력바 숨기기
        currentAnimationState = CharacterAnimationState.Death;
        TriggerSpecialAnimation(currentAnimationState);  // 사망 애니메이션 실행
        
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
    
    // 사망 시 추가 처리 (하위 클래스에서 오버라이드)
    protected virtual void OnDeath()
    {
        // 기본 구현: 3초 후 오브젝트 비활성화
        Invoke(nameof(DisableAfterDeath), 3f);
    }
    
    // 사망 후 오브젝트 비활성화
    protected virtual void DisableAfterDeath()
    {
        gameObject.SetActive(false);
    }
    
    // 공격력을 반환합니다.
    public virtual int GetAttackPower()
    {
        return attackPower;
    }
    
    // 현재 체력을 반환합니다.
    public virtual int GetCurrentHealth()
    {
        return currentHealth;
    }
    
    // 최대 체력을 반환합니다.
    public virtual int GetMaxHealth()
    {
        return maxHealth;
    }
    
    // 사망 상태를 반환합니다.
    public virtual bool IsDead()
    {
        return isDead;
    }
    
    // 공격 애니메이션 종료 이벤트에서 호출되는 메서드 (애니메이션 이벤트용)
    public virtual void OnAttackAnimationEnd()
    {
        // 기본 구현: 공격 상태 해제
        isAttacking = false;
        if (anim != null)
        {
            anim.SetBool(GameConstants.ANIM_IS_ATTACKING, false);
        }
    }
    
    // 무적 상태를 반환합니다.
    public virtual bool IsInvincible()
    {
        return isInvincible;
    }
    
    // 체력 변경 이벤트에 리스너를 추가합니다.
    public virtual void AddHealthChangedListener(System.Action<int, int> callback)
    {
        if (healthChangedListeners != null && !healthChangedListeners.Contains(callback))
        {
            healthChangedListeners.Add(callback);
        }
    }
    
    // 체력 변경 이벤트에서 리스너를 제거합니다.
    public virtual void RemoveHealthChangedListener(System.Action<int, int> callback)
    {
        if (healthChangedListeners != null)
        {
            healthChangedListeners.Remove(callback);
        }
    }
    
    // 사망 이벤트에 리스너를 추가합니다.
    public virtual void AddDeathListener(System.Action callback)
    {
        if (deathListeners != null && !deathListeners.Contains(callback))
        {
            deathListeners.Add(callback);
        }
    }
    
    // 사망 이벤트에서 리스너를 제거합니다.
    public virtual void RemoveDeathListener(System.Action callback)
    {
        if (deathListeners != null)
        {
            deathListeners.Remove(callback);
        }
    }
    
    // 체력바 표시/숨기기
    public virtual void SetHealthBarVisible(bool show)
    {
        if (statusBarUI != null)
        {
            statusBarUI.SetVisible(show);
        }
    }
    
    // 체력바 오프셋 설정
    public virtual void SetHealthBarOffset(Vector3 offset)
    {
        if (statusBarUI != null)
        {
            statusBarUI.SetSettings(offset, true, true);
        }
    }
    
    // 체력바 색상 설정
    public virtual void SetHealthBarColors(Color healthy, Color warning, Color danger)
    {
        if (statusBarUI != null)
        {
            statusBarUI.SetHealthColors(healthy, warning, danger);
        }
    }
    
    // StatusBarUI 초기화 (외부에서 호출 가능)
    public virtual void InitializeStatusBarUI()
    {
        InitializeStatusUI();
    }
    
    #endregion
    
    #region Memory Management
    
    // 오브젝트가 파괴될 때 메모리 정리를 수행합니다.
    protected virtual void OnDestroy()
    {
        ClearAllReferences();
    }
    
    // 오브젝트 풀링을 위한 상태 초기화 (Destroy 없이 재사용 시)
    public virtual void ResetForPooling()
    {
        // 상태 초기화
        isDead = false;
        isAttacking = false;
        currentAnimationState = CharacterAnimationState.Idle;
        
        // HP UI 초기화 및 이벤트 리스너 재등록 (풀링에서 재사용 시 중요!)
        if (statusBarUI != null)
        {
            statusBarUI.SetVisible(true);
            statusBarUI.UpdateHealthDisplay(currentHealth, maxHealth);
            
            // StatusBarUI의 이벤트 리스너 재등록 (풀링에서 재사용 시 필요)
            statusBarUI.ReinitializeEventListeners(this);
        }
        
        // 이벤트 리스너만 정리 (이벤트 자체는 유지)
        if (healthChangedListeners != null)
        {
            healthChangedListeners.Clear();
        }
        if (deathListeners != null)
        {
            deathListeners.Clear();
        }
        
        // 컴포넌트 참조는 유지 (재사용 시 필요)
        // rb, anim, sortingGroup, effectManager, collisionManager, statusBarUI는 그대로 유지
    }
    
    // 모든 참조를 정리합니다 (Destroy 시에만 사용)
    private void ClearAllReferences()
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
        statusBarUI = null;
    }
    
    #endregion
}
