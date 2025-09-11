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
        
        // 특수 애니메이션 입력 처리
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
    /// </summary>
    private void HandleSpecialAnimationInputs()
    {
        // Attack 애니메이션
        if (isAttackPressed)
        {
            Debug.Log($"[Player Attack] 수동 공격 입력 감지 - Time: {Time.time:F2}");
            // 수동 공격 실행 (StartAttack에서 Flip 처리)
            StartAttack();
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
    /// </summary>
    public void StartAttack()
    {
        // 공격 쿨다운 체크
        float attackAnimationLength = GetAttackAnimationLength();
        float cooldownTime = attackAnimationLength * attackCooldownMultiplier;
        float timeSinceLastAttack = Time.time - lastAttackTime;
        
        if (timeSinceLastAttack < cooldownTime)
        {
            Debug.Log($"[Player Attack] 공격 쿨다운 중 - 남은 시간: {cooldownTime - timeSinceLastAttack:F2}초");
            return;
        }
        
        // 공격 가능한 상태인지 확인
        if (!CanMove() || IsDead())
        {
            Debug.Log($"[Player Attack] 공격 불가능한 상태 - CanMove: {CanMove()}, IsDead: {IsDead()}");
            return;
        }
        
        Debug.Log($"[Player Attack] 수동 공격 실행 - Time: {Time.time:F2}");
        
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
        
        // Attack 애니메이션 실행
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
    /// </summary>
    public override void TakeDamage(int damage, CharacterBase attacker = null)
    {
        // 기본 피격 처리
        base.TakeDamage(damage, attacker);
        
        // 사망 시 콜리전 비활성화는 CharacterBase.Die()에서 처리됨
    }
    
    /// <summary>
    /// 플레이어 사망 시 추가 처리 (적들에게 사망 알림)
    /// </summary>
    protected override void OnDeath()
    {
        // 플레이어 사망 이벤트 발생
        OnPlayerDeath?.Invoke();
        
        Debug.Log("[Player] 플레이어가 사망했습니다. 모든 적들이 Idle 상태로 전환됩니다.");
        
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
        Debug.Log($"[Player Attack] OnAttackAnimationEvent 호출됨 - Time: {Time.time:F2}");
        
        // 공격 판정 직후 즉시 이동 허용
        isAttacking = false;
        
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
