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
    //[SerializeField] private bool enableMouseInput = false;    // 마우스 입력 활성화 여부 (향후 확장용)
    
    [Header("Player Stats")]
    [SerializeField] private int playerMaxHealth = 150;  // 플레이어 최대 체력
    [SerializeField] private int playerAttackPower = 25;  // 플레이어 공격력
    [SerializeField] private float playerInvincibilityDuration = 1.0f;  // 플레이어 무적 시간 (초)
    [SerializeField] private float damageMovementSpeedMultiplier = 0.7f;  // 피격 시 이동속도 배수
    
    // 플레이어 사망 이벤트
    public static System.Action OnPlayerDeath;
    
    [Header("Player Status UI")]
    [SerializeField] private Vector3 playerStatusBarOffset = new Vector3(0, 1.8f, 0);  // 플레이어 상태바 오프셋
    [SerializeField] private GameObject playerStatusBarObject;  // 플레이어 상태바 GameObject (직접 연결)
    
    [Header("Attack Settings")]
    [SerializeField] private float attackCooldownMultiplier = 1.2f;  // 공격 애니메이션 길이에 곱할 배수
    
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
        
        // 플레이어 전용 무적 시간 설정
        invincibilityDuration = playerInvincibilityDuration;
        
        // 플레이어 전용 체력바 설정
        InitializePlayerStatusUI();
        
        // 플레이어 전용 초기화
    }
    
    /// <summary>
    /// 플레이어 전용 체력 UI 초기화
    /// </summary>
    private void InitializePlayerStatusUI()
    {
        if (!enableStatusBar) return;
        
        // 직접 연결된 StatusBar GameObject가 있는지 확인
        if (playerStatusBarObject != null)
        {
            statusBarUI = playerStatusBarObject.GetComponent<StatusBarUI>();
            if (statusBarUI == null)
            {
                // StatusBarUI 컴포넌트가 없으면 자식에서 찾기
                statusBarUI = playerStatusBarObject.GetComponentInChildren<StatusBarUI>();
            }
        }
        
        if (statusBarUI != null)
        {
            // 플레이어 전용 상태바 설정 적용
            statusBarUI.SetSettings(playerStatusBarOffset, true, true);
            statusBarUI.SetVisible(true);
            
            // 초기 체력 표시
            statusBarUI.UpdateHealthDisplay(currentHealth, maxHealth);
        }
    }

    protected override void Update()
    {
        // 사망 시 모든 행동 중단
        if (IsDead())
        {
            // 이동 중단
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            
            // 공격 중단
            EndAttack();
            
            // 모든 콜리전 비활성화
            if (collisionManager != null)
            {
                collisionManager.SetAllCollisionsEnabled(false);
            }
            
            return; // 사망 시 더 이상 처리하지 않음
        }
        
        // 입력 처리
        if (enableKeyboardInput)
        {
            HandleKeyboardInput();
        }
        
        
        // 부모 클래스의 Update 호출 (UpdateMovement, UpdateAnimation 실행)
        base.Update();
    }

    #region ICharacterController Implementation

    /// <summary>
    /// 플레이어의 이동을 업데이트합니다.
    /// 키보드 입력을 받아 물리 이동을 처리합니다.
    /// </summary>
    public override void UpdateMovement()
    {
        // 이동 가능 여부 확인
        if (!CanMove())
        {
            // 이동 불가능한 상태면 정지
            HandlePhysicsMovement(Vector2.zero, false);
            return;
        }
        
        // 현재 입력 값으로 이동 처리
        Vector2 movement = GetMovementInput();
        bool isRunning = IsRunning();
        
        // 물리 기반 이동 처리
        HandlePhysicsMovement(movement, isRunning);
        
        // 현재 상태 저장
        currentMovement = movement;
        isCurrentlyRunning = isRunning;
    }

    /// <summary>
    /// 플레이어의 애니메이션을 업데이트합니다.
    /// 이동 상태와 특수 애니메이션을 처리합니다.
    /// </summary>
    public override void UpdateAnimation()
    {
        // 애니메이션 상태 업데이트
        UpdateAnimationState();
        
        // 이동 애니메이션 처리
        HandleAnimations(currentMovement.magnitude, isCurrentlyRunning);
        
        // 특수 애니메이션 입력 처리 (피격 중에도 공격 입력 처리)
        HandleSpecialAnimationInputs();
    }

    /// <summary>
    /// 현재 키보드 입력 값을 반환합니다.
    /// </summary>
    /// <returns>이동 방향 벡터 (정규화됨)</returns>
    public override Vector2 GetMovementInput()
    {
        // CanMove()에서 이미 이동 가능 여부를 체크하므로 여기서는 입력만 처리
        
        // WASD 또는 화살표 키 입력 받기
        moveX = Input.GetAxisRaw("Horizontal");
        moveY = Input.GetAxisRaw("Vertical");
        
        // 입력 벡터 생성 및 정규화
        Vector2 input = new Vector2(moveX, moveY);
        Vector2 normalizedInput = input.normalized;
        
        // 이동 상태 추적
        bool wasMoving = isMoving;
        isMoving = input.magnitude > 0.1f; // 임계값 이상일 때 이동으로 판단
        
        if (isMoving)
        {
            lastMovementInput = normalizedInput;
            lastMovementTime = Time.time;
        }
        // 이동 상태 변경 시 쿨다운 조정 제거 - 항상 규칙적인 공격 속도 유지
        
        return normalizedInput;
    }

    #endregion

    #region Override Methods

    /// <summary>
    /// 현재 달리기 상태를 반환합니다.
    /// Shift 키가 눌려있으면 true를 반환합니다.
    /// </summary>
    /// <returns>달리기 중이면 true</returns>
    public override bool IsRunning()
    {
        return isShiftPressed;
    }

    /// <summary>
    /// 플레이어의 이동 가능 여부를 반환합니다.
    /// 무적 상태일 때는 공격 중이어도 이동을 허용합니다.
    /// </summary>
    /// <returns>이동 가능하면 true</returns>
    public override bool CanMove()
    {
        // 무적 상태일 때는 공격 중이어도 이동 허용
        if (isInvincible)
        {
            // 사망 상태만 체크
            return currentAnimationState != CharacterAnimationState.Death;
        }
        
        // 기본 이동 가능 여부 체크
        return base.CanMove();
    }
    
    /// <summary>
    /// 플레이어의 공격 가능 여부를 반환합니다.
    /// 피격 중이어도 쿨다운이 지났으면 공격을 허용합니다.
    /// </summary>
    /// <returns>공격 가능하면 true</returns>
    public bool CanAttack()
    {
        // 사망 상태는 공격 불가
        if (IsDead())
        {
            return false;
        }
        
        // 공격 쿨다운 체크
        float attackAnimationLength = GetAttackAnimationLength();
        float cooldownTime = attackAnimationLength * attackCooldownMultiplier;
        float timeSinceLastAttack = Time.time - lastAttackTime;
        
        return timeSinceLastAttack >= cooldownTime;
    }
    

    /// <summary>
    /// 플레이어의 애니메이션 상태를 업데이트합니다.
    /// 공격 중일 때는 다른 애니메이션이 우선순위를 가지지 않도록 처리합니다.
    /// 단, 사망 상태일 때는 Death 애니메이션이 최우선입니다.
    /// </summary>
    protected override void UpdateAnimationState()
    {
        if (anim == null) return;
        
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        
        // 사망 상태일 때는 Death 애니메이션이 최우선
        if (IsDead())
        {
            // 공격 상태를 즉시 해제
            isAttacking = false;
            
            if (currentAnimationState != CharacterAnimationState.Death)
            {
                Debug.Log($"[Player Death] 사망 상태 감지, Death 애니메이션 강제 실행");
                currentAnimationState = CharacterAnimationState.Death;
                if (anim != null)
                {
                    // IsAttacking 파라미터를 false로 설정
                    anim.SetBool("IsAttacking", false);
                    anim.SetTrigger(GameConstants.ANIM_DEATH);
                }
            }
            return;
        }
        
        // 공격 애니메이션 중일 때는 다른 애니메이션으로 상태 변경하지 않음
        if (currentAnimationState == CharacterAnimationState.Attack)
        {
            // 공격 애니메이션이 완료되었는지 확인
            if (!stateInfo.IsName("Attack") || stateInfo.normalizedTime >= 1.0f)
            {
                // 공격 애니메이션이 완료되었으면 상태 초기화
                Debug.Log($"[Player Attack] 공격 애니메이션 완료 - 상태 초기화, normalizedTime: {stateInfo.normalizedTime:F2}");
                isAttacking = false;
                currentAnimationState = CharacterAnimationState.Idle; // 명시적으로 Idle로 설정
                
                // Animator 파라미터 설정
                if (anim != null)
                {
                    anim.SetBool("IsAttacking", false);
                }
                // 일반 상태 업데이트 수행
                base.UpdateAnimationState();
            }
            else
            {
                // 공격 애니메이션 중이면 상태 강제 유지 (피격 애니메이션 무시)
                currentAnimationState = CharacterAnimationState.Attack;
                isAttacking = true;
                
                // 피격 애니메이션이나 다른 애니메이션이 실행되려고 하면 강제로 공격 애니메이션으로 되돌림
                if (!stateInfo.IsName("Attack"))
                {
                    Debug.Log($"[Player Attack] 다른 애니메이션 감지 ({stateInfo.shortNameHash}), 공격 애니메이션으로 강제 복원");
                    // IsAttacking 파라미터로 공격 애니메이션 강제 실행
                    if (anim != null)
                    {
                        anim.SetBool("IsAttacking", true);
                    }
                }
            }
            return;
        }
        
        // Blank 상태일 때는 공격 입력이 있으면 공격 애니메이션으로 덮어쓰기
        if (currentAnimationState == CharacterAnimationState.Blank && isAttackPressed)
        {
            Debug.Log($"[Player Attack] Blank 상태에서 공격 입력 감지, 공격 애니메이션으로 덮어쓰기");
            // 공격 상태 강제 설정
            currentAnimationState = CharacterAnimationState.Attack;
            isAttacking = true;
            
            // Animator 파라미터 설정
            if (anim != null)
            {
                anim.SetBool("IsAttacking", true);
            }
            
            // Attack 애니메이션은 이미 위에서 트리거됨
            return;
        }
        
        // 공격 중이 아닐 때는 기본 상태 업데이트 수행
        base.UpdateAnimationState();
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// 키보드 입력을 처리합니다. 이동, 달리기, 특수 애니메이션 입력을 처리합니다.
    /// </summary>
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

    /// <summary>
    /// 특수 애니메이션 Attack, Ceremony, Blank, Death 애니메이션을 처리합니다.
    /// 피격 중에도 공격 입력이 처리됩니다.
    /// </summary>
    private void HandleSpecialAnimationInputs()
    {
        // 사망 상태일 때는 모든 입력 무시
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
            // 공격 중일 때는 추가 공격 입력 무시 (중복 공격 방지)
            if (currentAnimationState == CharacterAnimationState.Attack && isAttacking)
            {
                Debug.Log($"[Player Attack] 공격 중이므로 추가 공격 입력 무시 - CurrentState: {currentAnimationState}, IsAttacking: {isAttacking}");
                isAttackPressed = false;
                return;
            }
            
            Debug.Log($"[Player Attack] 수동 공격 입력 감지 - Time: {Time.time:F2}, CurrentState: {currentAnimationState}, IsInvincible: {isInvincible}");
            
            // 쿨다운 체크
            float attackAnimationLength = GetAttackAnimationLength();
            float cooldownTime = attackAnimationLength * attackCooldownMultiplier;
            float timeSinceLastAttack = Time.time - lastAttackTime;
            
            // 무적 상태일 때는 쿨다운을 더 짧게 적용 (반격 기회 제공)
            if (isInvincible)
            {
                cooldownTime *= 0.3f; // 무적 상태일 때는 쿨다운을 30%로 단축
                Debug.Log($"[Player Attack] 무적 상태 - 쿨다운 단축: {cooldownTime:F2}초");
            }
            
            // Blank 상태일 때는 쿨다운을 더욱 짧게 적용 (피격 중 반격)
            if (currentAnimationState == CharacterAnimationState.Blank)
            {
                cooldownTime *= 0.2f; // Blank 상태일 때는 쿨다운을 20%로 단축
                Debug.Log($"[Player Attack] Blank 상태 - 쿨다운 대폭 단축: {cooldownTime:F2}초");
            }
            
            if (timeSinceLastAttack >= cooldownTime && !IsDead())
            {
                Debug.Log($"[Player Attack] 공격 실행 - 쿨다운 통과, 강제 공격 실행");
                
                // 공격 상태 강제 설정 (피격 애니메이션 무시)
                currentAnimationState = CharacterAnimationState.Attack;
                isAttacking = true;
                
                // Animator 파라미터 설정
                if (anim != null)
                {
                    anim.SetBool("IsAttacking", true);
                    Debug.Log($"[Player Attack] IsAttacking 파라미터를 true로 설정 - Animator: {anim.name}");
                    
                    // 현재 애니메이션 상태 확인
                    AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                    Debug.Log($"[Player Attack] 현재 애니메이션 상태: {stateInfo.shortNameHash}, IsName('Attack'): {stateInfo.IsName("Attack")}");
                    
                    // 모든 가능한 Attack 상태 이름 확인
                    Debug.Log($"[Player Attack] IsName('Attack'): {stateInfo.IsName("Attack")}");
                    Debug.Log($"[Player Attack] IsName('attack'): {stateInfo.IsName("attack")}");
                    Debug.Log($"[Player Attack] IsName('Attack_01'): {stateInfo.IsName("Attack_01")}");
                    Debug.Log($"[Player Attack] IsName('Player_Attack'): {stateInfo.IsName("Player_Attack")}");
                    
                    // IsAttacking 파라미터 값 확인
                    bool isAttackingParam = anim.GetBool("IsAttacking");
                    Debug.Log($"[Player Attack] IsAttacking 파라미터 값: {isAttackingParam}");
                    
                    // Animator Controller의 모든 파라미터 확인
                    Debug.Log($"[Player Attack] Animator 파라미터 개수: {anim.parameterCount}");
                    for (int i = 0; i < anim.parameterCount; i++)
                    {
                        var param = anim.GetParameter(i);
                        Debug.Log($"[Player Attack] 파라미터 {i}: {param.name} ({param.type})");
                    }
                }
                else
                {
                    Debug.LogError("[Player Attack] Animator가 null입니다!");
                }
                
                // 공격 쿨타임을 현재 시간으로 설정
                lastAttackTime = Time.time;
                
                // 공격 전 가장 가까운 적을 바라보도록 Flip 처리
                Collider2D nearestEnemy = GetNearestEnemy();
                if (nearestEnemy != null)
                {
                    Vector3 directionToTarget = (nearestEnemy.transform.position - transform.position).normalized;
                    if (directionToTarget.x != 0)
                    {
                        FlipCharacter(directionToTarget.x);
                    }
                }
                
                // Attack 애니메이션은 IsAttacking 파라미터로 관리됨
                Debug.Log($"[Player Attack] 공격 애니메이션 강제 실행 완료 - 피격 애니메이션 덮어씀");
            }
            else if (currentAnimationState == CharacterAnimationState.Blank)
            {
                // Blank 상태일 때는 쿨다운이 조금 남아있어도 공격 허용 (반격 기회)
                float blankCooldownTime = cooldownTime * 0.1f; // Blank 상태일 때는 10% 쿨다운만 적용
                if (timeSinceLastAttack >= blankCooldownTime && !IsDead())
                {
                    Debug.Log($"[Player Attack] Blank 상태 반격 - 쿨다운 단축 적용, 공격 실행");
                    
                    // 공격 상태 강제 설정
                    currentAnimationState = CharacterAnimationState.Attack;
                    isAttacking = true;
                    
                    // Animator 파라미터 설정
                    if (anim != null)
                    {
                        anim.SetBool("IsAttacking", true);
                    }
                    
                    // 공격 쿨타임을 현재 시간으로 설정
                    lastAttackTime = Time.time;
                    
                    // 공격 전 가장 가까운 적을 바라보도록 Flip 처리
                    Collider2D nearestEnemy = GetNearestEnemy();
                    if (nearestEnemy != null)
                    {
                        Vector3 directionToTarget = (nearestEnemy.transform.position - transform.position).normalized;
                        if (directionToTarget.x != 0)
                        {
                            FlipCharacter(directionToTarget.x);
                        }
                    }
                    
                    // Attack 애니메이션은 IsAttacking 파라미터로 관리됨
                    Debug.Log($"[Player Attack] Blank 상태 반격 공격 실행 완료");
                }
                else
                {
                    Debug.Log($"[Player Attack] Blank 상태 반격 불가 - 쿨다운: {blankCooldownTime - timeSinceLastAttack:F2}초 남음");
                }
            }
            else
            {
                Debug.Log($"[Player Attack] 공격 불가 - 쿨다운: {cooldownTime - timeSinceLastAttack:F2}초 남음, IsDead: {IsDead()}, IsInvincible: {isInvincible}");
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

    #region Public Methods

    /// <summary>
    /// 키보드 입력을 활성화/비활성화합니다.
    /// </summary>
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

    /// <summary>
    /// 현재 키보드 입력 상태를 반환합니다.
    /// </summary>
    /// <returns>키보드 입력이 활성화되어 있으면 true</returns>
    public bool IsKeyboardInputEnabled()
    {
        return enableKeyboardInput;
    }

    /// <summary>
    /// 플레이어를 특정 위치로 즉시 이동시킵니다.
    /// </summary>
    /// <param name="position">목표 위치</param>
    public void TeleportTo(Vector3 position)
    {
        if (rb != null)
        {
            // Rigidbody 위치 설정
            rb.position = position;
            
            // 이동 상태 초기화
            currentMovement = Vector2.zero;
            isCurrentlyRunning = false;
            HandlePhysicsMovement(Vector2.zero, false);
        }
    }

    /// <summary>
    /// 플레이어의 이동 속도를 조정합니다.
    /// </summary>
    /// <param name="newSpeed">새로운 이동 속도</param>
    public void SetMoveSpeed(float newSpeed)
    {
        if (newSpeed >= 0)
        {
            moveSpeed = newSpeed;
        }
    }

    /// <summary>
    /// 플레이어의 달리기 속도 배수를 조정합니다.
    /// </summary>
    /// <param name="newMultiplier">새로운 달리기 속도 배수</param>
    public void SetRunSpeedMultiplier(float newMultiplier)
    {
        if (newMultiplier >= 1.0f)
        {
            runSpeedMultiplier = newMultiplier;
        }
    }

    #endregion


    #region AttackRange Collision Override (Detection용)

    /// <summary>
    /// AttackRange 콜리전 이벤트 처리 (기본 처리만 수행)
    /// </summary>
    /// <param name="other">감지된 오브젝트</param>
    public override void OnAttackRangeEnter(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 기본 AttackRange 진입 처리만 수행
        base.OnAttackRangeEnter(other);
    }
    
    /// <summary>
    /// AttackRange 콜리전에서 나갔을 때 처리 (기본 처리만 수행)
    /// </summary>
    /// <param name="other">나간 오브젝트</param>
    public override void OnAttackRangeExit(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 기본 처리만 수행
        base.OnAttackRangeExit(other);
    }

    #endregion


    /// <summary>
    /// Body 콜리전 이벤트 처리
    /// </summary>
    /// <param name="other">충돌한 오브젝트</param>
    public override void OnBodyCollision(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 기본 Body Collision 처리
        base.OnBodyCollision(other);
    }

    #region Collision System Methods

    /// <summary>
    /// 플레이어의 Body 콜리전을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetBodyCollisionEnabled(bool enabled)
    {
        SetCollisionTypeEnabled(CollisionType.Body, enabled);
    }


    /// <summary>
    /// 플레이어의 AttackRange 콜리전을 활성화/비활성화합니다 (감지용).
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetAttackRangeCollisionEnabled(bool enabled)
    {
        SetCollisionTypeEnabled(CollisionType.AttackRange, enabled);
    }

    /// <summary>
    /// AttackRange 콜리전을 활성화합니다.
    /// </summary>
    public override void EnableAttackRangeCollision()
    {
        SetCollisionTypeEnabled(CollisionType.AttackRange, true);
    }

    /// <summary>
    /// AttackRange 콜리전을 비활성화합니다.
    /// </summary>
    public override void DisableAttackRangeCollision()
    {
        SetCollisionTypeEnabled(CollisionType.AttackRange, false);
    }

    /// <summary>
    /// 수동 공격 애니메이션을 시작합니다.
    /// 피격 중에도 일정 쿨타임 내에서 공격이 가능합니다.
    /// </summary>
    public void StartAttack()
    {
        // 공격 가능 여부 체크 (피격 중이어도 쿨다운이 지났으면 허용)
        if (!CanAttack())
        {
            float attackAnimationLength = GetAttackAnimationLength();
            float cooldownTime = attackAnimationLength * attackCooldownMultiplier;
            float timeSinceLastAttack = Time.time - lastAttackTime;
            Debug.Log($"[Player Attack] 공격 불가 - 쿨다운 중: {cooldownTime - timeSinceLastAttack:F2}초 남음, IsDead: {IsDead()}");
            return;
        }
        
        Debug.Log($"[Player Attack] 수동 공격 실행 - Time: {Time.time:F2}, IsInvincible: {isInvincible}");
        
        // 공격 전 가장 가까운 적을 바라보도록 Flip 처리
        Collider2D nearestEnemy = GetNearestEnemy();
        if (nearestEnemy != null)
        {
            Vector3 directionToTarget = (nearestEnemy.transform.position - transform.position).normalized;
            if (directionToTarget.x != 0)
            {
                FlipCharacter(directionToTarget.x);
            }
        }
        
        // 공격 쿨타임을 현재 시간으로 설정
        lastAttackTime = Time.time;
        
        // Attack 애니메이션 실행 (피격 중이어도 실행)
        TriggerSpecialAnimation(CharacterAnimationState.Attack);
    }

    /// <summary>
    /// 공격 애니메이션이 끝났을 때 호출됩니다.
    /// 이제 AttackCollisionHandler가 자동으로 비활성화하므로 수동 호출이 필요하지 않습니다.
    /// </summary>
    public void EndAttack()
    {
        OnAttackAnimationEnd();
    }

    #endregion

    #region Override Methods

    /// <summary>
    /// 피격을 받았을 때 처리합니다. (사망 시 콜리전 비활성화)
    /// 공격 중일 때는 피격 애니메이션이 우선순위를 가지지 않습니다.
    /// </summary>
    public override void TakeDamage(int damage, CharacterBase attacker = null)
    {
        if (isDead || isInvincible) 
        {
            return;
        }
        
        // 공격 중일 때는 피격 애니메이션을 트리거하지 않음
        bool wasAttacking = (currentAnimationState == CharacterAnimationState.Attack);
        
        Debug.Log($"[Player Damage] 피격 받음 - Damage: {damage}, WasAttacking: {wasAttacking}, CurrentState: {currentAnimationState}");
        
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
        
        // 피격 이펙트 재생 (애니메이션과 무관하게 이펙트는 재생)
        // 중복 재생 방지를 위해 조건부 실행
        if (effectManager != null)
        {
            PlayDamageEffect();
        }
        
        // 공격 중이 아닐 때만 피격 애니메이션 트리거
        if (!wasAttacking)
        {
            // 피격 애니메이션 트리거 (기본 구현에서는 처리하지 않음)
            // 필요시 여기에 피격 애니메이션 로직 추가
            Debug.Log($"[Player Damage] 피격 애니메이션 트리거 (공격 중이 아님)");
        }
        else
        {
            Debug.Log($"[Player Damage] 공격 중이므로 피격 애니메이션 트리거하지 않음");
            
            // 공격 애니메이션 상태 강제 유지
            currentAnimationState = CharacterAnimationState.Attack;
            isAttacking = true;
            
            // Animator 파라미터 설정
            if (anim != null)
            {
                anim.SetBool("IsAttacking", true);
            }
            
            // 공격 애니메이션은 IsAttacking 파라미터로 관리됨
            Debug.Log($"[Player Damage] 공격 애니메이션으로 피격 애니메이션 덮어쓰기");
        }
        
        // 체력이 0 이하가 되면 사망 처리
        if (currentHealth <= 0)
        {
            Debug.Log($"[Player Death] 체력 0 이하, 사망 처리 시작");
            
            // 사망 시 공격 상태 즉시 해제
            isAttacking = false;
            if (anim != null)
            {
                anim.SetBool("IsAttacking", false);
            }
            
            Die();
        }
    }
    
    /// <summary>
    /// 플레이어 사망 시 추가 처리 (적들에게 사망 알림)
    /// </summary>
    protected override void OnDeath()
    {
        // 플레이어 사망 이벤트 발생
        OnPlayerDeath?.Invoke();
        
        Debug.Log("[Player] 플레이어가 사망했습니다. 모든 적들이 Idle 상태로 전환됩니다.");
        
        // Death 애니메이션 강제 실행 (공격 애니메이션 보호 로직 무시)
        if (anim != null)
        {
            Debug.Log("[Player Death] Death 애니메이션 강제 실행");
            currentAnimationState = CharacterAnimationState.Death;
            anim.SetTrigger(GameConstants.ANIM_DEATH);
        }
        
        // 기본 사망 처리는 하지 않음 (DisableAfterDeath 호출 방지)
        // 사망 애니메이션과 콜리전 비활성화는 CharacterBase.Die()에서 이미 처리됨
    }
    
    /// <summary>
    /// 플레이어 사망 후 오브젝트를 비활성화하지 않음 (카메라 유지를 위해)
    /// </summary>
    protected override void DisableAfterDeath()
    {
        // 플레이어 사망 후에도 오브젝트를 비활성화하지 않음
        // 카메라가 계속 작동하도록 함
        Debug.Log("[Player] 플레이어가 사망했지만 카메라 유지를 위해 오브젝트를 비활성화하지 않습니다.");
    }
    
    /// <summary>
    /// 공격 애니메이션 이벤트에서 호출되는 메서드 (즉시 이동 허용)
    /// </summary>
    public override void OnAttackAnimationEvent()
    {
        // 사망 상태일 때는 공격 이벤트 무시
        if (IsDead())
        {
            Debug.Log($"[Player Attack] OnAttackAnimationEvent 사망 상태로 인해 무시 - Time: {Time.time:F2}");
            return;
        }
        
        // 중복 호출 방지 (시간 기반)
        float currentTime = Time.time;
        if (currentTime - lastAttackEventTime < 0.1f) // 0.1초 내 중복 호출 방지
        {
            Debug.Log($"[Player Attack] OnAttackAnimationEvent 시간 기반 중복 호출 방지 - Time: {currentTime:F2}, LastEvent: {lastAttackEventTime:F2}");
            return;
        }
        
        // 중복 호출 방지 (상태 기반)
        if (!isAttacking)
        {
            Debug.Log($"[Player Attack] OnAttackAnimationEvent 상태 기반 중복 호출 방지 - isAttacking: {isAttacking}, CurrentState: {currentAnimationState}");
            return;
        }
        
        Debug.Log($"[Player Attack] OnAttackAnimationEvent 호출됨 - Time: {currentTime:F2}");
        
        // 마지막 이벤트 시간 업데이트
        lastAttackEventTime = currentTime;
        
        // 공격 판정 직후 즉시 이동 허용
        isAttacking = false;
        
        // Animator 파라미터 설정
        if (anim != null)
        {
            anim.SetBool("IsAttacking", false);
        }
        
        // 기본 공격 처리
        base.OnAttackAnimationEvent();
    }
    
    /// <summary>
    /// 물리 기반 이동을 처리합니다. (피격 시 이동 속도 조정)
    /// </summary>
    protected override void HandlePhysicsMovement(Vector2 movement, bool isRunning)
    {
        if (rb == null) return;
        
        float currentSpeed = moveSpeed;
        
        // 피격 중일 때는 이동 속도를 제한 (완전히 막지 않음)
        if (isInvincible)
        {
            currentSpeed *= damageMovementSpeedMultiplier;
        }
        
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

    #region Debug Methods

    /// <summary>
    /// 디버그 정보를 출력합니다.
    /// </summary>
    private void OnGUI()
    {
        if (Application.isEditor)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("=== Player Controller Debug ===");
            GUILayout.Label($"Position: {transform.position}");
            GUILayout.Label($"Movement: {currentMovement}");
            GUILayout.Label($"Is Running: {isCurrentlyRunning}");
            GUILayout.Label($"Can Move: {CanMove()}");
            GUILayout.Label($"Can Attack: {CanAttack()}");
            GUILayout.Label($"Animation State: {GetCurrentAnimationState()}");
            GUILayout.Label($"Move Speed: {moveSpeed}");
            GUILayout.Label($"Run Multiplier: {runSpeedMultiplier}");
            GUILayout.Label($"Last Attack: {Time.time - lastAttackTime:F1}s ago");
            GUILayout.Label($"Is Invincible: {isInvincible}");
            GUILayout.Label($"Invincibility Timer: {invincibilityTimer:F2}s");
            GUILayout.Label($"Damage Speed Multiplier: {damageMovementSpeedMultiplier:F2}");
            GUILayout.EndArea();
        }
    }

    #endregion
}
