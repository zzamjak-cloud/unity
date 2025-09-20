using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 플레이어 캐릭터를 제어하는 컨트롤러
/// CharacterBase를 상속받아 키보드 입력을 처리합니다.
/// </summary>
public class PlayerController : CharacterBase
{
    [Header("Player Input Settings")]
    [SerializeField] private bool enableKeyboardInput = true;  // 키보드 입력 활성화 여부
    
    [Header("Player Stats")]
    [SerializeField] private int playerMaxHealth = 150;  // 플레이어 최대 체력
    [SerializeField] private int playerAttackPower = 25;  // 플레이어 공격력
    [SerializeField] private float damageMovementSpeedMultiplier = 0.7f;  // 피격 시 이동속도 배수
    
    public static System.Action OnPlayerDeath; // 플레이어 사망 이벤트
    
    [Header("Player Status UI")]
    [SerializeField] private Vector3 playerStatusBarOffset = new Vector3(0, 1.8f, 0);  // 플레이어 상태바 오프셋
    [SerializeField] private GameObject playerStatusBarObject;  // 플레이어 상태바 GameObject (직접 연결)
    
    [Header("Attack Settings")]
    [SerializeField] private float attackCooldown = GameConstants.DEFAULT_ATTACK_COOLDOWN;  // 공격 애니메이션 후 추가 대기 시간 (초)
    
    // 입력 처리용 변수들
    private float moveX = 0f;
    private float moveY = 0f;
    private bool isShiftPressed = false;
    
    // 특수 애니메이션 입력 상태
    private bool isAttackPressed = false;
    private bool isCeremonyPressed = false;
    private bool isBlankPressed = false;
    private bool isDeathPressed = false;
    
    // 공격 관련 변수들
    private float lastAttackTime = 0f;
    private float lastAttackEventTime = 0f; // 마지막 공격 이벤트 시간
    private float attackAnimationEndTime = 0f; // 공격 애니메이션 종료 예상 시간
    
    // 이동 상태 추적
    private bool isMoving = false;  // 현재 이동 중인지
    private Vector2 lastMovementInput = Vector2.zero;  // 마지막 이동 입력
    private float lastMovementTime = 0f;  // 마지막 이동 시간

    protected override void Start()
    {
        // 플레이어 전용 체력 설정
        maxHealth = playerMaxHealth;
        attackPower = playerAttackPower;
        
        base.Start();
        
        // 플레이어 전용 무적 시간 설정 (CharacterBase의 기본값을 덮어쓰기)
        invincibilityDuration = GameConstants.PLAYER_INVINCIBILITY_DURATION;
        
        InitializePlayerStatusUI();  // 플레이어 전용 체력 UI 초기화
    }
    
    // 플레이어 체력바 UI 초기화
    private void InitializePlayerStatusUI()
    {
        if (!enableStatusBar) return;
        
        // 직접 연결된 StatusBar GameObject가 있는지 확인
        if (playerStatusBarObject != null)
        {
            statusBarUI = playerStatusBarObject.GetComponent<StatusBarUI>();
            // StatusBarUI 컴포넌트가 없으면 자식에서 찾기
            if (statusBarUI == null)
            { statusBarUI = playerStatusBarObject.GetComponentInChildren<StatusBarUI>(); }
        }
        
        if (statusBarUI != null)
        {
            statusBarUI.SetSettings(playerStatusBarOffset, true, true);  // 플레이어 전용 상태바 설정 적용
            statusBarUI.SetVisible(true);  // 초기 상태바 표시
            statusBarUI.UpdateHealthDisplay(currentHealth, maxHealth);  // 초기 체력 표시
        }
    }

    protected override void Update()
    {
        if (IsDead())  // 사망시 처리
        {
            rb.linearVelocity = Vector2.zero;   // 이동 중단
            OnAttackAnimationEnd();             // 공격 중단
            collisionManager.SetAllCollisionsEnabled(false);  // 모든 콜리전 비활성화
            return;
        }
        
        if (enableKeyboardInput)  // 키보드 입력 처리
        { HandleKeyboardInput(); }

        base.Update();  // UpdateMovement, UpdateAnimation 실행 (부모 클래스의 Update 호출)
    }

    #region Core Gameplay Systems

    // 이동 처리
    public override void UpdateMovement()
    {
        if (!CanMove())  // 이동 가능 여부 확인 후 정지
        {
            HandlePhysicsMovement(Vector2.zero, false);
            return;
        }
        
        Vector2 movement = GetMovementInput();  // 현재 입력 값으로 이동 처리
        bool isRunning = IsRunning();  // 달리기 상태 확인확인
        HandlePhysicsMovement(movement, isRunning);  // 물리 기반 이동 처리
        
        currentMovement = movement;  // 현재 이동 상태 저장
        isCurrentlyRunning = isRunning;  // 현재 달리기 상태 저장
    }

    // 애니메이션 처리
    public override void UpdateAnimation()
    {
        UpdateAnimationState();  // 애니메이션 상태 업데이트
        HandleAnimations(currentMovement.magnitude, isCurrentlyRunning);  // 이동 애니메이션 처리
        HandleSpecialAnimationInputs(); // 특수 애니메이션 입력 처리
    }

    // 이동 입력 처리
    public override Vector2 GetMovementInput()
    {
        moveX = Input.GetAxisRaw("Horizontal");
        moveY = Input.GetAxisRaw("Vertical");
        
        // 입력 벡터 생성 및 정규화
        Vector2 input = new Vector2(moveX, moveY);
        Vector2 normalizedInput = input.normalized;
        
        // 이동 상태 추적
        isMoving = input.magnitude > GameConstants.MOVEMENT_THRESHOLD; // 임계값 이상일 때 이동으로 판단
        
        if (isMoving)
        {
            lastMovementInput = normalizedInput;
            lastMovementTime = Time.time;
        }
        
        return normalizedInput;
    }

    #endregion

    #region Character State Management

    // 달리기 상태 확인
    public override bool IsRunning()  
    { return isShiftPressed; }

    public override bool CanMove()
    {
        if (IsDead()) return false;  // 사망 상태일 때는 이동 불가
        if (isAttacking) return false;  // 공격 중일 때는 이동 불가
        if (IsInvincible())  // 무적일 때는 사망 상태만 체크
            return currentAnimationState != CharacterAnimationState.Death;
        
        return base.CanMove(); // 기본 이동 가능 여부 확인
    }

    // 공격 가능 여부 확인
    public bool CanAttack()
    {
        if (IsDead()) return false;
        return GetTimeSinceLastAttack() >= GetAttackCooldownTime();
    }
    
    //공격 쿨다운 시간을 계산합니다.
    private float GetAttackCooldownTime()
    { return GetAttackAnimationLength() + attackCooldown; }
    
    //마지막 공격으로부터 경과된 시간을 반환합니다.
    private float GetTimeSinceLastAttack()
    { return Time.time - lastAttackTime; }

    //플레이어의 애니메이션 상태를 업데이트합니다.
    protected override void UpdateAnimationState()
    {
        // 사망 상태일 때는 Death 애니메이션 강제 유지
        if (IsDead())
        {
            if (currentAnimationState != CharacterAnimationState.Death)
            {
                currentAnimationState = CharacterAnimationState.Death;
                anim.SetBool(GameConstants.ANIM_IS_ATTACKING, false);
                anim.SetTrigger(GameConstants.ANIM_DEATH);
            }
            return; // 사망 상태일 때는 base.UpdateAnimationState() 호출하지 않음
        }
        
        // 공격 중일 경우 처리
        if (isAttacking)
        {
            // 공격 애니메이션 종료시 Idle 상태로 강제 전환
            if (Time.time >= attackAnimationEndTime)
            {
                isAttacking = false;
                anim.SetBool(GameConstants.ANIM_IS_ATTACKING, false);
                currentAnimationState = CharacterAnimationState.Idle;
                base.UpdateAnimationState();
                return;
            }
            
            currentAnimationState = CharacterAnimationState.Attack;
            anim.SetBool(GameConstants.ANIM_IS_ATTACKING, true);
            return;
        }
        
        // Blank일 경우 공격 입력이 있으면 공격 애니메이션으로 덮어쓰기
        if (currentAnimationState == CharacterAnimationState.Blank && isAttackPressed)
        {
            StartAttackSequence();
            return;
        }
   
        base.UpdateAnimationState();
    }
    
    // 공격을 시작합니다. (공격 쿨다운 리셋, 애니메이션 시작, 이펙트 재생)
    // 주의: PlayerController는 사용자가 직접 방향을 제어하므로 FaceNearestEnemy()를 호출하지 않습니다.
    protected override void StartAttackSequence()
    {
        // 공격 가능 여부 확인
        if (!CanAttack()) return;
        
        // 공격 쿨다운 리셋
        lastAttackTime = Time.time;
        
        // 공격 애니메이션 시작
        currentAnimationState = CharacterAnimationState.Attack;
        isAttacking = true;
        attackAnimationEndTime = Time.time + GetAttackAnimationLength();
        anim.SetBool(GameConstants.ANIM_IS_ATTACKING, true);
        
        // 공격 이펙트는 OnAttackAnimationEvent()에서 재생됨
    }

    // 공격 애니메이션 종료 이벤트에서 호출되는 메서드 (애니메이션 이벤트용)
    public override void OnAttackAnimationEnd()
    {
        isAttacking = false;
        anim.SetBool(GameConstants.ANIM_IS_ATTACKING, false);
    }

    #endregion

    #region Input System

    ///키보드 입력을 처리합니다. 이동, 달리기, 특수 애니메이션 입력을 처리합니다.
    private void HandleKeyboardInput()
    {
        // 이동 입력
        moveX = Input.GetAxisRaw("Horizontal");
        moveY = Input.GetAxisRaw("Vertical");
        
        // 달리기 입력 (Shift 키)
        isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        
        // 특수 애니메이션 입력 (한 번만 트리거)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isAttackPressed = true;
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            isCeremonyPressed = true;
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            isBlankPressed = true;
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            isDeathPressed = true;
        }
    }

    //특수 애니메이션 Attack, Ceremony, Blank, Death 애니메이션을 처리합니다.
    private void HandleSpecialAnimationInputs()
    {
        // 사망시 모든 입력 무시
        if (IsDead())
        {
            // 입력 상태 초기화
            isAttackPressed = false;
            isCeremonyPressed = false;
            isBlankPressed = false;
            isDeathPressed = false;
            return;
        }
        
        // Attack 애니메이션 - 피격 중에도 반드시 처리
        if (isAttackPressed)
        {
            // 공격중에 추가 공격 입력 방지
            if (currentAnimationState == CharacterAnimationState.Attack && isAttacking)
            {
                isAttackPressed = false;
                return;
            }
            // 쿨다운 체크
            float cooldownTime = GetAttackCooldownTime();
            
            // 쿨다운 체크 후 공격 가능하면 공격 애니메이션 실행
            if (GetTimeSinceLastAttack() >= cooldownTime && !IsDead())
            {
                StartAttackSequence();
            }
            isAttackPressed = false;
        }
        
        // Ceremony 애니메이션
        if (isCeremonyPressed)
        {
            TriggerSpecialAnimation(CharacterAnimationState.Ceremony);
            isCeremonyPressed = false;
        }
        
        // Blank 애니메이션
        if (isBlankPressed)
        {
            TriggerSpecialAnimation(CharacterAnimationState.Blank);
            isBlankPressed = false;
        }
        
        // Death 애니메이션
        if (isDeathPressed)
        {
            TriggerSpecialAnimation(CharacterAnimationState.Death);
            isDeathPressed = false;
        }
    }

    #endregion

    #region Public Interface

    // 키보드 입력을 활성화/비활성화합니다.
    public void SetKeyboardInputEnabled(bool enable)
    {
        enableKeyboardInput = enable;
        
        if (!enable)
        {
            // 입력 비활성화 시 현재 이동 정지
            currentMovement = Vector2.zero;
            isCurrentlyRunning = false;
            HandlePhysicsMovement(Vector2.zero, false);
        }
    }

    // 현재 키보드 입력 상태를 반환합니다.
    public bool IsKeyboardInputEnabled()
    { return enableKeyboardInput; }

    #endregion

    #region Collision System

    public override void OnAttackRangeEnter(Collider2D other)
    { base.OnAttackRangeEnter(other); }
    
    public override void OnAttackRangeExit(Collider2D other)
    { base.OnAttackRangeExit(other); }

    public override void OnBodyCollision(Collider2D other)
    { base.OnBodyCollision(other); }

    public void SetBodyCollisionEnabled(bool enabled)
    { SetCollisionTypeEnabled(CollisionType.Body, enabled); }

    public override void SetAttackRangeCollisionEnabled(bool enabled)
    {  SetCollisionTypeEnabled(CollisionType.AttackRange, enabled); }

    #endregion

    #region Health & Damage System

    // 피격을 받았을 때 처리합니다. (사망 시 콜리전 비활성화)
    // 공격 중일 때는 피격 애니메이션이 우선순위를 가지지 않습니다.
    public override void TakeDamage(int damage, CharacterBase attacker = null)
    {
        if (IsDead() || IsInvincible()) return;
        
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
        
        StartInvincibility();  // 무적 상태
        PlayDamageEffect();
        
        // 체력이 0 이하가 되면 사망 처리
        if (currentHealth <= 0)
        {
            Debug.Log($"Player 사망");
            Die();
        }
    }
    
    // 플레이어 사망 처리 (Death 애니메이션 포함)
    protected override void Die()
    {
        base.Die();
    }
    
    // 플레이어 사망시 추가 처리 (적들에게 사망 알림)
    protected override void OnDeath()
    {
        OnPlayerDeath?.Invoke();
    }
    
    // 플레이어 사망 후 오브젝트를 비활성화하지 않음 (카메라 유지를 위해)
    protected override void DisableAfterDeath()
    {
        // 플레이어 사망 후에도 오브젝트를 비활성화하지 않음
        // 카메라가 계속 작동하도록 함
        // (아무것도 하지 않음 - 오브젝트 유지)
    }
    
    // 공격 애니메이션 이벤트에서 호출되는 메서드 (공격 판정만 처리)
    public override void OnAttackAnimationEvent()
    {
        // 사망 상태일 때는 공격 이벤트 무시
        if (IsDead()) return;
        
        // 중복 호출 방지 (시간 기반)
        float currentTime = Time.time;
        if (currentTime - lastAttackEventTime < GameConstants.ANIMATION_EVENT_COOLDOWN) return;
        
        // 중복 호출 방지 (상태 기반)
        if (!isAttacking) return;
        
        // 마지막 이벤트 시간 업데이트
        lastAttackEventTime = currentTime;
        
        // 기본 공격 처리
        base.OnAttackAnimationEvent();
    }
    
    // 물리 기반 이동을 처리합니다. (피격 시 이동 속도 조정)
    protected override void HandlePhysicsMovement(Vector2 movement, bool isRunning)
    {
        if (rb == null) return;
        
        float currentSpeed = moveSpeed;
        
        // 피격 중일 때는 이동 속도를 제한 (완전히 막지 않음)
        if (IsInvincible())
            currentSpeed *= damageMovementSpeedMultiplier;
        
        if (isRunning)
            currentSpeed *= runSpeedMultiplier;
  
        rb.linearVelocity = movement.normalized * currentSpeed;
        
        // 캐릭터 방향 전환 처리 (공격 중이 아닐 때만)
        if (movement.x != 0 && !isAttacking)
            FlipCharacter(movement.x);
    }

    #endregion
}

