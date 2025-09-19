using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 적 캐릭터를 제어하는 컨트롤러
/// CharacterBase를 상속받아 정적 배치된 적 캐릭터를 관리합니다.
/// </summary>
public class EnemyController : CharacterBase
{
    [Header("Enemy Settings")]
    [SerializeField] private bool isStaticEnemy = false;  // 정적 적 여부 (플레이어 추적을 위해 false로 변경)
    [SerializeField] private Vector3 spawnPosition;      // 스폰 위치
    [SerializeField] private float detectionRange = 5f;  // 플레이어 감지 범위
    [SerializeField] private float chaseDelay = GameConstants.ENEMY_CHASE_DELAY;   // 플레이어 재추적 지연 시간
    [SerializeField] private float combatCooldown = 3f; // 전투 후 추적 재개까지의 시간
    
    [Header("Enemy Behavior")]
    [SerializeField] private float idleAnimationDelay = 2f;  // Idle 애니메이션 전환 지연 시간
    [SerializeField] private bool enableRandomAnimations = false;  // 랜덤 애니메이션 활성화 여부
    [SerializeField] private float randomAnimationChance = GameConstants.RANDOM_ANIMATION_CHANCE;  // 랜덤 애니메이션 확률 (프레임당)
    
    [Header("Enemy Stats")]
    [SerializeField] private int enemyMaxHealth = 80;  // 적 최대 체력
    [SerializeField] private int enemyAttackPower = 15;  // 적 공격력
    
    [Header("Enemy Status UI")]
    [SerializeField] private Vector3 enemyStatusBarOffset = new Vector3(0, GameConstants.ENEMY_STATUS_BAR_OFFSET_Y, 0);  // 적 상태바 오프셋
    [SerializeField] private GameObject enemyStatusBarObject;  // 적 상태바 GameObject (직접 연결)
    
    [Header("Auto Attack Settings")]
    [SerializeField] private bool enableAutoAttack = true;  // 자동 공격 활성화 여부
    [SerializeField] private float autoAttackCooldown = GameConstants.ENEMY_AUTO_ATTACK_COOLDOWN;  // 자동 공격 쿨다운 시간 (적은 조금 더 느리게)
    [SerializeField] private float attackDelay = GameConstants.ENEMY_ATTACK_DELAY;  // 플레이어 감지 후 공격 지연 시간
    [SerializeField] private float attackCooldown = GameConstants.DEFAULT_ATTACK_COOLDOWN;  // 공격 애니메이션 후 추가 대기 시간 (초)
    
    // 적 전용 상태 변수들
    private Vector3 initialPosition;
    private float idleTimer = 0f;
    
    // 랜덤 애니메이션용 변수
    private float lastRandomAnimationTime = 0f;
    private float randomAnimationCooldown = 5f;  // 랜덤 애니메이션 쿨다운
    
    // 자동 공격 관련 변수들
    private float lastAutoAttackTime = 0f;
    private List<Collider2D> detectedPlayers = new List<Collider2D>();  // 감지된 플레이어 목록
    private Collider2D currentTarget = null;  // 현재 타겟
    private float firstDetectionTime = 0f;  // 첫 감지 시간
    
    // 공격 쿨다운 관련 변수들
    private float lastAttackTime = 0f;  // 마지막 공격 시간
    private float attackAnimationEndTime = 0f;  // 공격 애니메이션 종료 예상 시간
    
    // 플레이어 추적 관련 변수들
    private Transform playerTransform = null;  // 플레이어 Transform
    private bool isChasingPlayer = false;      // 플레이어 추적 중인지
    private float lastPlayerDetectionTime = 0f; // 마지막 플레이어 감지 시간
    private float playerLostTime = 2f;         // 플레이어를 잃은 후 복귀까지의 시간
    private float lastChaseTime = 0f;          // 마지막 추적 시작 시간
    private bool canStartChasing = true;       // 추적을 시작할 수 있는지
    
    // 전투 상태 관련 변수들
    private bool isInCombat = false;           // 전투 중인지
    private float lastCombatTime = 0f;         // 마지막 전투 시간
    
    // 스폰 시스템 관련
    private EnemySpawnManager spawnManager;    // 스폰 매니저 참조
    
    // 플레이어 사망 상태 추적
    private bool isPlayerDead = false;         // 플레이어 사망 여부

    protected override void Start()
    {
        // 적 전용 체력 설정
        maxHealth = enemyMaxHealth;
        attackPower = enemyAttackPower;
        
        // 적 전용 이동 속도 설정 (플레이어보다 느리게)
        moveSpeed = GameConstants.ENEMY_MOVE_SPEED;
        
        base.Start();
        
        invincibilityDuration = GameConstants.ENEMY_INVINCIBILITY_DURATION;  // 무적 시간 설정
        SetupEnemyRigidbody();  // Rigidbody2D 설정 (적들 간 물리적 상호작용 방지)
        InitializeEnemyStatusUI();  // 적 전용 상태 UI 초기화
        InitializeEnemy();  // 적 전용 초기화
        
        if (spawnManager == null) {spawnManager = FindFirstObjectByType<EnemySpawnManager>();}
        
        PlayerController.OnPlayerDeath += OnPlayerDeath;  // 플레이어 사망 이벤트 구독
    }
    
    protected override void OnDestroy()
    {
        PlayerController.OnPlayerDeath -= OnPlayerDeath;  // 플레이어 사망 이벤트 구독 해제
        base.OnDestroy();
    }
    
    // Rigidbody2D 설정 (적들 간 물리적 충돌 방지)
    private void SetupEnemyRigidbody()
    {
        // 적들 간의 물리적 충돌을 방지하기 위한 설정
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 연속 충돌 감지
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep; // 절대 잠들지 않음
        rb.freezeRotation = true; // 회전 고정
        rb.gravityScale = 0f; // 중력 비활성화 (2D 탑다운 게임)
        
        SetupEnemyCollisionIgnore();  // 적들 간의 충돌을 무시하도록 설정
    }
    
    // 적들 간의 충돌을 무시하도록 설정
    private void SetupEnemyCollisionIgnore()
    {
        // 모든 적 오브젝트를 찾아서 이 적과의 충돌을 무시하도록 설정
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(GameConstants.TAG_ENEMY);
        foreach (GameObject enemy in enemies)
        {
            if (enemy != gameObject) // 자기 자신은 제외
            {
                // Body 콜리전 간의 충돌 무시
                Collider2D thisBodyCollider = collisionManager.GetBodyCollider();
                Collider2D otherBodyCollider = enemy.GetComponent<CharacterCollisionManager>()?.GetBodyCollider();
                
                if (thisBodyCollider != null && otherBodyCollider != null)
                    Physics2D.IgnoreCollision(thisBodyCollider, otherBodyCollider, true);  // Body 콜리전 간의 충돌 무시
            }
        }
    }
    
    // 적 전용 상태 UI 초기화
    private void InitializeEnemyStatusUI()
    {
        if (!enableStatusBar) return;  // 상태바 비활성화 시 초기화 중단
        
        // 직접 연결된 StatusBar GameObject가 있는지 확인
        if (enemyStatusBarObject != null)
        {
            statusBarUI = enemyStatusBarObject.GetComponent<StatusBarUI>();
            if (statusBarUI == null)
                statusBarUI = enemyStatusBarObject.GetComponentInChildren<StatusBarUI>();
        }
        
        if (statusBarUI != null)
        {
            // 적 전용 상태바 설정 적용
            statusBarUI.SetSettings(enemyStatusBarOffset, true, true);
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
            // 추적 중단
            isChasingPlayer = false;
            isInCombat = false;
            
            // 이동 중단
            rb.linearVelocity = Vector2.zero;
            
            // 공격 중단
            if (currentTarget != null)
            {
                OnAttackAnimationEnd();
                currentTarget = null;
            }
            
            // 모든 콜리전 비활성화
            if (collisionManager != null)
                collisionManager.SetAllCollisionsEnabled(false);
            
            return;
        }
        
        // 플레이어가 사망시 모든 행동 중단하고 Idle 상태로 전환
        if (isPlayerDead)
        {
            // 추적 중단
            isChasingPlayer = false;
            isInCombat = false;
            
            // 이동 중단
            rb.linearVelocity = Vector2.zero;
            
            // 공격 중단
            if (currentTarget != null)
            {
                OnAttackAnimationEnd();
                currentTarget = null;
            }
            
            // 애니메이션 상태 업데이트 (Idle 상태 유지)
            UpdateAnimationState();
            HandleAnimations(0f, false);
            
            return;
        }
        
        UpdateCombatState();  // 전투 상태 업데이트
        HandlePlayerDetectionAndChasing();  // 플레이어 감지 및 추적 처리
        CleanupDetectedPlayers();  // 감지된 플레이어 목록 정리 (사망한 플레이어 제거)
        
        // 자동 공격 처리 - AttackRange 내 가장 가까운 플레이어를 자동으로 공격
        if (enableAutoAttack && HasEnemiesInRange())
            HandleAutoAttack();
        
        // 현재 타겟 유효성 체크 - 타겟이 AttackRange 내에 없으면 즉시 공격 중단
        if (currentTarget != null && !IsPlayerInAttackRange(currentTarget))
        {
            OnAttackAnimationEnd(); // 진행 중인 공격 즉시 중단
            currentTarget = null;
        }
        
        base.Update();
    }



    #region ICharacterController Implementation

    // 적의 이동을 업데이트합니다.
    // 플레이어를 추적하거나 정적 적으로 동작합니다.
    public override void UpdateMovement()
    {
        if (isStaticEnemy)  // 정적 적이므로 이동하지 않음
        {
            // 위치가 변경되었다면 초기 위치로 복귀
            if (Vector3.Distance(transform.position, initialPosition) > GameConstants.POSITION_THRESHOLD)
                ReturnToInitialPosition();
            
            HandlePhysicsMovement(Vector2.zero, false);  // 물리 이동 정지
            
            // 현재 상태 저장
            currentMovement = Vector2.zero;
            isCurrentlyRunning = false;
        }
        else
            HandleEnemyMovement();  // 동적 적 - 플레이어 추적 또는 복귀
    }

    // 적의 애니메이션을 업데이트합니다.
    // Idle 상태와 랜덤 애니메이션을 처리합니다.
    public override void UpdateAnimation()
    {
        UpdateAnimationState();
        
        if (isStaticEnemy)
        {
            HandleIdleAnimation();  // Idle 애니메이션 처리
            
            // 랜덤 애니메이션 처리
            if (enableRandomAnimations)
                HandleRandomAnimations();
            
            HandleAnimations(0f, false);  // 이동 애니메이션은 항상 Idle 상태
        }
        else
            HandleAnimations(currentMovement.magnitude, isCurrentlyRunning);  // 이동 상태에 따른 애니메이션 처리
    }

    // 적의 이동 입력을 반환합니다.
    public override Vector2 GetMovementInput()
    {
        if (isAttacking) return Vector2.zero;  // 공격 중일 때는 이동 입력 무시
        if (isStaticEnemy) return Vector2.zero;  // 정적 적이므로 이동하지 않음
        else return currentMovement;
    }

    #endregion



    #region Enemy Initialization

    // 적 캐릭터를 초기화합니다.
    private void InitializeEnemy()
    {
        // 초기 위치 저장
        initialPosition = transform.position;
        spawnPosition = initialPosition;
        
        // 플레이어 찾기
        FindPlayer();
        
        // 추적 지연 초기화
        canStartChasing = true;
        lastChaseTime = 0f;
        
    }

    #endregion



    #region Position Management

    // 초기 위치로 복귀합니다.
    private void ReturnToInitialPosition()
    {
        rb.position = initialPosition;
        currentMovement = Vector2.zero;
        isCurrentlyRunning = false;
        HandlePhysicsMovement(Vector2.zero, false);
    }

    // 새로운 스폰 위치를 설정합니다.
    public void SetSpawnPosition(Vector3 newSpawnPosition)
    {
        spawnPosition = newSpawnPosition;
        initialPosition = newSpawnPosition;
        rb.position = newSpawnPosition;
    }

    #endregion



    #region Animation Management

    // Idle 애니메이션을 처리합니다.
    private void HandleIdleAnimation()
    {
        // Idle 상태일 때만 처리
        if (currentAnimationState == CharacterAnimationState.Idle)
        {
            idleTimer += Time.deltaTime;
            
            // 일정 시간 후 Idle 애니메이션 강제 실행
            if (idleTimer >= idleAnimationDelay) idleTimer = 0f;
        }
        else idleTimer = 0f;
    }

    // 랜덤 애니메이션을 처리합니다.
    private void HandleRandomAnimations()
    {
        // 쿨다운 체크
        if (Time.time - lastRandomAnimationTime < randomAnimationCooldown) return;
        
        // 랜덤 확률로 특수 애니메이션 실행
        if (Random.Range(0f, 1f) < randomAnimationChance)
        {
            // 이동 가능한 상태일 때만 랜덤 애니메이션 실행
            if (CanMove())
            {
                ExecuteRandomAnimation();
                lastRandomAnimationTime = Time.time;
            }
        }
    }

    // 랜덤 애니메이션을 실행합니다.
    private void ExecuteRandomAnimation()
    {
        // 랜덤하게 애니메이션 선택
        float randomValue = Random.Range(0f, 1f);
        
        if (randomValue < GameConstants.RANDOM_ANIMATION_ATTACK_CHANCE)
        {
            TriggerSpecialAnimation(CharacterAnimationState.Attack);
        }
        else if (randomValue < GameConstants.RANDOM_ANIMATION_BLANK_CHANCE)
        {
            TriggerSpecialAnimation(CharacterAnimationState.Blank);
        }
        else if (randomValue < GameConstants.RANDOM_ANIMATION_CEREMONY_CHANCE)
        {
            TriggerSpecialAnimation(CharacterAnimationState.Ceremony);
        }
        // Death는 랜덤으로 실행하지 않음
    }

    #endregion



    #region Public Methods

    // 적을 특정 위치로 즉시 이동시킵니다.
    public void TeleportTo(Vector3 position)
    {
        rb.position = position;
        currentMovement = Vector2.zero;
        isCurrentlyRunning = false;
        HandlePhysicsMovement(Vector2.zero, false);
    }

    // 적을 초기 위치로 즉시 이동시킵니다.
    public void ReturnToSpawn()
    { ReturnToInitialPosition(); }

    // 랜덤 애니메이션을 활성화/비활성화합니다.
    public void SetRandomAnimationsEnabled(bool enable)
    { enableRandomAnimations = enable; }

    // 랜덤 애니메이션 확률을 설정합니다.
    public void SetRandomAnimationChance(float chance)
    { randomAnimationChance = Mathf.Clamp01(chance); }

    // 랜덤 애니메이션 쿨다운을 설정합니다.
    public void SetRandomAnimationCooldown(float cooldown)
    { randomAnimationCooldown = Mathf.Max(0f, cooldown); }

    // Idle 애니메이션 전환 지연 시간을 설정합니다.
    public void SetIdleAnimationDelay(float delay)
    { idleAnimationDelay = Mathf.Max(0f, delay); }

    // 적이 정적 적인지 확인합니다.
    public bool IsStaticEnemy()
    { return isStaticEnemy; }

    // 적의 초기 위치를 반환합니다.
    public Vector3 GetInitialPosition()
    { return initialPosition; }

    #endregion



    #region Collision System Methods

    // 적의 Body 콜리전을 활성화/비활성화합니다.
    public void SetBodyCollisionEnabled(bool enabled)
    { SetCollisionTypeEnabled(CollisionType.Body, enabled); }


    // 적의 AttackRange 콜리전을 활성화/비활성화합니다 (감지용).
    public override void SetAttackRangeCollisionEnabled(bool enabled)
    { SetCollisionTypeEnabled(CollisionType.AttackRange, enabled); }

    // 공격 가능 여부를 확인합니다.
    public bool CanAttack()
    {
        if (IsDead()) return false;
        return GetTimeSinceLastAttack() >= GetAttackCooldownTime();
    }
    
    // 공격 쿨다운 시간을 계산합니다.
    private float GetAttackCooldownTime()
    { return GetAttackAnimationLength() + attackCooldown; }
    
    // 마지막 공격으로부터 경과된 시간을 반환합니다.
    private float GetTimeSinceLastAttack()
    { return Time.time - lastAttackTime; }
    
    
    // 공격을 시작합니다. (공격 쿨다운 리셋, 적을 바라보기, 애니메이션 시작, 이펙트 재생)
    protected override void StartAttackSequence()
    {
        // 공격 가능 여부 확인
        if (!CanAttack()) return;
        
        // 공격 쿨다운 리셋
        lastAttackTime = Time.time;
        
        // 공격 상태 먼저 설정 (방향 변경 방지)
        isAttacking = true;
        
        // 공격 전 가장 가까운 적을 바라보도록 Flip 처리 (강제)
        FaceNearestEnemy(true);
        
        // 공격 애니메이션 종료 시간 설정
        attackAnimationEndTime = Time.time + GetAttackAnimationLength();
        
        // Attack 애니메이션 실행
        TriggerSpecialAnimation(CharacterAnimationState.Attack);
        
        // IsAttacking 파라미터를 true로 설정
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



    #region Player Detection and Chasing

    // 플레이어 감지 및 추적을 처리합니다.
    private void HandlePlayerDetectionAndChasing()
    {
        if (isStaticEnemy) return;
        
        // 사망 시 추적 중단
        if (IsDead())
        {
            isChasingPlayer = false;
            return;
        }
        
        // 전투 중이면 추적하지 않음
        if (isInCombat)
        {
            isChasingPlayer = false;
            return;
        }
        
        // 플레이어 찾기
        if (playerTransform == null) FindPlayer();
        
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            
            if (distanceToPlayer <= detectionRange)
            {
                // 플레이어가 감지 범위 내에 있음
                lastPlayerDetectionTime = Time.time;
                
                // 전투 후 쿨다운 시간 체크
                bool canChaseAfterCombat = !isInCombat || (Time.time - lastCombatTime >= combatCooldown);
                
                // 추적 지연 시간 체크
                if (canStartChasing && canChaseAfterCombat && Time.time - lastChaseTime >= chaseDelay)
                {
                    isChasingPlayer = true;
                    canStartChasing = false;
                }
            }
            else if (isChasingPlayer && Time.time - lastPlayerDetectionTime > playerLostTime)
            {
                // 플레이어를 잃었고, 일정 시간이 지났으면 추적 중단
                isChasingPlayer = false;
                canStartChasing = true;
                lastChaseTime = Time.time; // 다음 추적을 위한 시간 기록
            }
        }
    }
    
    // 플레이어를 찾습니다.
    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag(GameConstants.TAG_PLAYER);
        if (player != null) 
            playerTransform = player.transform;
    }
    
    // 적의 이동을 처리합니다.
    private void HandleEnemyMovement()
    {
        Vector2 movement = Vector2.zero;
        bool isRunning = false;
        
        // 사망 시 이동 중단
        if (IsDead())
        {
            movement = Vector2.zero;
            isRunning = false;
        }
        // 전투 중이면 이동하지 않음
        else if (isInCombat)
        {
            movement = Vector2.zero;
            isRunning = false;
        }
        else if (isChasingPlayer && playerTransform != null)
        {
            // 플레이어를 향해 이동 (방향만 계산, 속도는 HandlePhysicsMovement에서 처리)
            Vector2 direction = (playerTransform.position - transform.position).normalized;
            movement = direction;
            isRunning = true;
        }
        else if (Vector3.Distance(transform.position, initialPosition) > GameConstants.POSITION_THRESHOLD)
        {
            // 초기 위치로 복귀 (방향만 계산, 속도는 HandlePhysicsMovement에서 처리)
            Vector2 direction = (initialPosition - transform.position).normalized;
            movement = direction;
            isRunning = false; // 복귀 시에는 걷기 속도
        }
        
        // 물리 이동 처리 (HandlePhysicsMovement에서 속도와 회전 처리)
        HandlePhysicsMovement(movement, isRunning);
        
        // 현재 상태 저장
        currentMovement = movement;
        isCurrentlyRunning = isRunning;
    }

    #endregion



    #region Override Methods

    // 적의 물리 이동을 처리합니다. (복귀 시 속도 조정)
    protected override void HandlePhysicsMovement(Vector2 movement, bool isRunning)
    {
        float currentSpeed = moveSpeed;
        
        // 복귀 중일 때는 더 느리게 이동
        if (!isChasingPlayer && Vector3.Distance(transform.position, initialPosition) > GameConstants.POSITION_THRESHOLD)
        {
            currentSpeed *= GameConstants.RETURN_SPEED_MULTIPLIER; // 복귀 시에는 매우 느리게
        }
        else if (isRunning)
        {
            currentSpeed *= runSpeedMultiplier;
        }
        
        // Rigidbody를 사용해 이동
        rb.linearVelocity = movement.normalized * currentSpeed;
        
        // 캐릭터 방향 전환 처리 (공격 중이 아닐 때만)
        if (movement.x != 0 && !isAttacking)
            FlipCharacter(movement.x);
    }
    
    // 공격 성공 판정을 처리합니다. (전투 상태 설정)
    protected override void HandleAttackHit(Collider2D other)
    {
        StartCombat();  // 전투 상태 시작
        base.HandleAttackHit(other);  // 기본 공격 처리
    }
    
    // 피격을 받았을 때 처리합니다. (전투 상태 설정)
    public override void TakeDamage(int damage, CharacterBase attacker = null)
    {
        StartCombat();  // 전투 상태 시작
        base.TakeDamage(damage, attacker);  // 기본 피격 처리
    }

    #endregion



    #region Combat State Management

    // 전투 상태를 시작합니다.
    private void StartCombat()
    {
        isInCombat = true;
        lastCombatTime = Time.time;
        
        // 추적 중단
        isChasingPlayer = false;
        canStartChasing = true;
        lastChaseTime = Time.time;
    }
    
    // 전투 상태를 종료합니다.
    private void EndCombat()
    { isInCombat = false; }
    
    // 플레이어 사망 시 호출되는 메서드
    private void OnPlayerDeath()
    {
        isPlayerDead = true;
        
        // 모든 행동 중단
        isChasingPlayer = false;
        isInCombat = false;
        rb.linearVelocity = Vector2.zero;
        
        // 공격 중단
        if (currentTarget != null)
        {
            OnAttackAnimationEnd();
            currentTarget = null;
        }
        
        Debug.Log($"[Enemy] {gameObject.name}: 플레이어 사망으로 인해 Idle 상태로 전환됩니다.");
    }
    
    // 플레이어 사망 시에도 정렬 순서 업데이트를 계속합니다.
    protected override void LateUpdate()
    { UpdateSortingOrder(); }
    
    // 전투 상태를 업데이트합니다.
    private void UpdateCombatState()
    {
        if (isInCombat && Time.time - lastCombatTime >= combatCooldown) 
            EndCombat();
    }

    #endregion

    // 적의 스폰 위치를 반환합니다.
    public Vector3 GetSpawnPosition()
    { return spawnPosition; }




    /*
    #region Debug Methods

    // 디버그 정보를 출력합니다.
    private void OnGUI()
    {
        if (Application.isEditor)
        {
            GUILayout.BeginArea(new Rect(10, 220, 300, 200));
            GUILayout.Label("=== Enemy Controller Debug ===");
            GUILayout.Label($"Position: {transform.position}");
            GUILayout.Label($"Initial Position: {initialPosition}");
            GUILayout.Label($"Spawn Position: {spawnPosition}");
            GUILayout.Label($"Is Static: {isStaticEnemy}");
            GUILayout.Label($"Can Move: {CanMove()}");
            GUILayout.Label($"Animation State: {GetCurrentAnimationState()}");
            GUILayout.Label($"Random Animations: {enableRandomAnimations}");
            GUILayout.Label($"Idle Timer: {idleTimer:F1}");
            GUILayout.EndArea();
        }
    }

    #endregion
    

    #region Gizmos

    // Scene 뷰에서 적의 정보를 표시합니다.
    private void OnDrawGizmosSelected()
    {
        // 초기 위치 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(initialPosition, 0.5f);
        
        // 스폰 위치 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(spawnPosition, 0.3f);
        
        // 현재 위치에서 초기 위치까지 선 그리기
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, initialPosition);
    }

    #endregion
    */

    #region AttackRange Collision Override (Detection용)

    // AttackRange 콜리전 이벤트 처리 (플레이어 감지 시 자동 공격)
    public override void OnAttackRangeEnter(Collider2D other)
    {
        // 플레이어 감지 시 목록에 추가
        if (other.CompareTag(GameConstants.TAG_PLAYER))
            AddDetectedPlayer(other);
        
        // 기본 AttackRange 진입 처리도 수행
        base.OnAttackRangeEnter(other);
    }
    
    // AttackRange 콜리전에서 나갔을 때 처리 (플레이어가 공격 범위를 벗어남)
    public override void OnAttackRangeExit(Collider2D other)
    {
        // 플레이어가 공격 범위를 벗어났을 때
        if (other.CompareTag(GameConstants.TAG_PLAYER))
        {
            // 현재 타겟이 이 플레이어면 즉시 공격 중단
            if (currentTarget == other)
                OnAttackAnimationEnd(); // 진행 중인 공격 즉시 중단

            RemoveDetectedPlayer(other);
        }
        
        // 기본 처리도 수행
        base.OnAttackRangeExit(other);
    }

    #endregion



    #region Attack Range Check Methods
    
    // 플레이어가 공격 범위 내에 있는지 확인합니다.
    private bool IsPlayerInAttackRange(Collider2D playerCollider)
    {
        if (playerCollider == null || collisionManager == null) return false;
        
        // 플레이어가 여전히 존재하고 활성화되어 있는지 확인
        if (!playerCollider.gameObject.activeInHierarchy) return false;
        
        // AttackRange 콜라이더가 설정되어 있는지 확인
        if (!collisionManager.HasAttackRangeCollider()) return false;
        
        // AttackRange 콜라이더와 플레이어의 거리 계산 (SqrMagnitude 사용으로 성능 최적화)
        float sqrDistance = (transform.position - playerCollider.transform.position).sqrMagnitude;
        
        // AttackRange 콜라이더의 공격 범위 가져오기
        float attackRange = collisionManager.GetAttackRange();
        return sqrDistance <= attackRange * attackRange;
    }
    
    #endregion



    #region Detection and Auto Attack Methods
    
    // Body 콜리전 이벤트 처리
    public override void OnBodyCollision(Collider2D other)
    { base.OnBodyCollision(other); }
    
    // AttackRange에 진입한 플레이어를 목록에 추가합니다.
    private void AddDetectedPlayer(Collider2D player)
    {
        if (player == null) return;
        
        // 이미 목록에 있는지 확인
        if (!detectedPlayers.Contains(player))
        {
            detectedPlayers.Add(player);
            
            // 첫 번째 플레이어가 감지되면 감지 시간 기록
            if (detectedPlayers.Count == 1)
                firstDetectionTime = Time.time;
            
            // 현재 타겟이 없거나 더 가까운 플레이어면 타겟 변경
            if (currentTarget == null || IsCloserThanCurrentTarget(player))
                SetCurrentTarget(player);
        }
    }
    
    // AttackRange에서 나간 플레이어를 목록에서 제거합니다.
    private void RemoveDetectedPlayer(Collider2D player)
    {
        if (player == null) return;
        
        if (detectedPlayers.Contains(player))
        {
            detectedPlayers.Remove(player);
            
            // 모든 플레이어가 제거되면 감지 시간 리셋
            if (detectedPlayers.Count == 0)
                firstDetectionTime = 0f;
            
            // 현재 타겟이 제거된 플레이어면 새로운 타겟 선택
            if (currentTarget == player)
                SelectNewTarget();
            
        }
    }
    
    // 감지된 플레이어 목록을 정리합니다 (사망한 플레이어 제거).
    private void CleanupDetectedPlayers()
    {
        for (int i = detectedPlayers.Count - 1; i >= 0; i--)
        {
            var player = detectedPlayers[i];
            if (player == null || IsPlayerDead(player))
                RemoveDetectedPlayer(player);
        }
    }
    
    // 플레이어가 사망했는지 확인합니다.
    private bool IsPlayerDead(Collider2D player)
    {
        if (player == null) return true;
        
        CharacterBase playerCharacter = player.GetComponent<CharacterBase>();
        if (playerCharacter == null)
            playerCharacter = player.GetComponentInParent<CharacterBase>();
        
        return playerCharacter != null && playerCharacter.IsDead();
    }
    
    // 새로운 타겟을 선택합니다.
    private void SelectNewTarget()
    {
        currentTarget = null;
        
        if (detectedPlayers.Count > 0)
        {
            // 가장 가까운 플레이어를 타겟으로 선택
            float closestDistance = float.MaxValue;
            foreach (var player in detectedPlayers)
            {
                if (player != null && !IsPlayerDead(player))
                {
                    float sqrDistance = (transform.position - player.transform.position).sqrMagnitude;
                    if (sqrDistance < closestDistance)
                    {
                        closestDistance = sqrDistance;
                        currentTarget = player;
                    }
                }
            }
        }
    }
    
    // 현재 타겟을 설정합니다.
    private void SetCurrentTarget(Collider2D player)
    { currentTarget = player; }
    
    // 주어진 플레이어가 현재 타겟보다 가까운지 확인합니다.
    private bool IsCloserThanCurrentTarget(Collider2D player)
    {
        if (currentTarget == null) return true;
        
        float currentSqrDistance = (transform.position - currentTarget.transform.position).sqrMagnitude;
        float newSqrDistance = (transform.position - player.transform.position).sqrMagnitude;
        
        return newSqrDistance < currentSqrDistance;
    }
    
    // 자동 공격을 처리합니다.
    private void HandleAutoAttack()
    {
        // 공격 중이 아닐 때만 타겟 변경 허용
        if (!isAttacking)
        {
            // AttackRange 내 가장 가까운 플레이어를 타겟으로 설정
            Collider2D nearestPlayer = GetNearestEnemy();
            if (nearestPlayer != null && nearestPlayer != currentTarget)
            {
                currentTarget = nearestPlayer;
                firstDetectionTime = Time.time; // 새로운 타겟 감지 시간 기록
            }
        }
        
        // 첫 감지 후 지연 시간 확인
        if (Time.time - firstDetectionTime >= attackDelay)
        {
            // 자동 공격 쿨다운 확인
            if (Time.time - lastAutoAttackTime >= autoAttackCooldown)
            {
                // 공격 가능한 상태인지 확인 (개별 공격 쿨다운도 체크)
                if (CanMove() && !IsDead() && currentTarget != null && CanAttack())
                {
                    // 타겟이 여전히 AttackRange 내에 있는지 확인
                    if (IsPlayerInAttackRange(currentTarget))
                    {
                        // 자동 공격 실행 (StartAttackSequence에서 Flip 처리)
                        StartAttackSequence();
                        lastAutoAttackTime = Time.time;
                    }
                    else
                    { currentTarget = null; }  // 타겟이 AttackRange를 벗어났으면 타겟 초기화
                }
            }
        }
    }
    
    // 모든 감지된 플레이어를 제거합니다.
    public void ClearAllDetectedPlayers()
    {
        detectedPlayers.Clear();
        currentTarget = null;
        firstDetectionTime = 0f;
    }
    
    // 자동 공격을 활성화/비활성화합니다.
    public void SetAutoAttackEnabled(bool enabled)
    {
        enableAutoAttack = enabled;
        if (!enabled)
            ClearAllDetectedPlayers();
    }

    #endregion



    #region Attack Animation Event Override

    // 애니메이션 이벤트에서 호출되는 공격 성공 판정 메서드 (EnemyController 오버라이드)
    public override void OnAttackAnimationEvent()
    {
        // 공격 판정 시점에서 이동 허용 (Flip 처리 완료 후)
        isAttacking = false;
        
        // IsAttacking 파라미터를 false로 설정
        anim.SetBool(GameConstants.ANIM_IS_ATTACKING, false);
        
        if (collisionManager != null)
            collisionManager.OnAttackAnimationEvent();
    }

    #endregion

    #region Spawn System

    // 감지 범위를 설정합니다. (스폰 시 호출)
    public void SetDetectionRange(float range)
    { detectionRange = range; }
    
    // 현재 감지 범위를 반환합니다.
    public float GetDetectionRange()
    { return detectionRange; }
    
    // 사망 시 풀로 복귀하도록 오버라이드
    protected override void Die()
    {
        base.Die();
        
        // Death 애니메이션 완료 후 풀로 복귀하도록 코루틴 시작
        StartCoroutine(WaitForDeathAnimationAndReturnToPool());
    }
    
    // Death 애니메이션 완료를 기다린 후 풀로 복귀하는 코루틴
    private System.Collections.IEnumerator WaitForDeathAnimationAndReturnToPool()
    {
        // Death 애니메이션 완료까지 대기 (실제 애니메이션 길이 사용)
        float deathAnimationDuration = GetDeathAnimationLength();
        yield return new WaitForSeconds(deathAnimationDuration);
        
        // 스폰 매니저에 풀로 복귀 요청
        if (spawnManager != null)
            spawnManager.ReturnEnemyToPool(gameObject);
        else
            Destroy(gameObject);  // 스폰 매니저가 없으면 파괴
    }
    
    // 스폰 매니저를 설정합니다.
    public void SetSpawnManager(EnemySpawnManager manager)
    { spawnManager = manager; }
    
    // 체력을 초기화합니다.
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        isInvincible = false;
        
        // 체력바 표시
        if (statusBarUI != null)
        {
            statusBarUI.SetVisible(true);
            statusBarUI.UpdateHealthDisplay(currentHealth, maxHealth);
        }
    }
    
    // 체력만 초기화합니다 (HP UI는 CharacterBase.ResetForPooling()에서 처리)
    public void ResetHealthOnly()
    {
        currentHealth = maxHealth;
        isDead = false;
        isInvincible = false;
    }
    
    // 사망 상태를 초기화합니다. (CharacterBase.ResetForPooling()에서 처리되므로 더 이상 필요하지 않음)
    [System.Obsolete("CharacterBase.ResetForPooling()에서 처리됩니다.")]
    public void ResetDeathState()
    { isDead = false; }
    
    // 이동 상태를 초기화합니다.
    public void ResetMovementState()
    {
        isChasingPlayer = false;
        isInCombat = false;
        canStartChasing = true;
        lastPlayerDetectionTime = 0f;
        lastChaseTime = 0f;
        lastCombatTime = 0f;
        
        // 초기 위치로 리셋
        transform.position = initialPosition;
        
        // 애니메이션 상태 초기화
        anim.SetBool(GameConstants.ANIM_IS_WALKING, false);
        anim.SetBool(GameConstants.ANIM_IS_RUNNING, false);
        anim.Play(GameConstants.ANIM_STATE_IDLE, 0, 0f); // Idle 상태로 강제 전환
    }
    
    // 공격 상태를 초기화합니다.
    public void ResetAttackState()
    {
        isAttacking = false;
        currentTarget = null;
        firstDetectionTime = 0f;
        lastAutoAttackTime = 0f;
        lastAttackTime = 0f;  // 공격 쿨다운 초기화
        attackAnimationEndTime = 0f;  // 공격 애니메이션 종료 시간 초기화
        
        // 감지된 플레이어 목록 초기화
        detectedPlayers.Clear();
        
        // 애니메이션 상태 초기화
        anim.SetBool(GameConstants.ANIM_IS_ATTACKING, false);
        anim.SetBool(GameConstants.ANIM_IS_WALKING, false);
        anim.SetBool(GameConstants.ANIM_IS_RUNNING, false);
        anim.Play(GameConstants.ANIM_STATE_IDLE, 0, 0f); // Idle 상태로 강제 전환
    }

    #endregion
}
