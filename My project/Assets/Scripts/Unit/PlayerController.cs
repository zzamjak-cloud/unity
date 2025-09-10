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
    
    [Header("Player Status UI")]
    [SerializeField] private Vector3 playerStatusBarOffset = new Vector3(0, 1.8f, 0);  // 플레이어 상태바 오프셋
    [SerializeField] private GameObject playerStatusBarObject;  // 플레이어 상태바 GameObject (직접 연결)
    
    [Header("Auto Attack Settings")]
    [SerializeField] private bool enableAutoAttack = true;  // 자동 공격 활성화 여부
    [SerializeField] private float autoAttackCooldown = 1.0f;  // 자동 공격 쿨다운 시간
    [SerializeField] private float attackDelay = 0.05f;  // 적 감지 후 공격 지연 시간 (Player는 더 빠르게)
    
    // 입력 처리용 변수들
    private float moveX = 0f;
    private float moveY = 0f;
    private bool isShiftPressed = false;
    
    // 특수 애니메이션 입력 상태
    private bool isAttackPressed = false;
    private bool isCeremonyPressed = false;
    private bool isBlankPressed = false;
    private bool isDeathPressed = false;
    
    // 자동 공격 관련 변수들
    private float lastAutoAttackTime = 0f;
    private List<Collider2D> detectedEnemies = new List<Collider2D>();  // 감지된 적 목록
    private Collider2D currentTarget = null;  // 현재 타겟
    private float firstDetectionTime = 0f;  // 첫 감지 시간

    protected override void Start()
    {
        // 플레이어 전용 체력 설정
        maxHealth = playerMaxHealth;
        attackPower = playerAttackPower;
        
        base.Start();
        
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
        // 입력 처리
        if (enableKeyboardInput)
        {
            HandleKeyboardInput();
        }
        
        // 감지된 적 목록 정리 (사망한 적 제거)
        CleanupDetectedEnemies();
        
        // 자동 공격 처리
        if (enableAutoAttack && detectedEnemies.Count > 0)
        {
            HandleAutoAttack();
        }
        
        // 공격 범위 체크 - 감지된 적 중 공격 범위를 벗어난 적들 제거
        CheckAndRemoveOutOfRangeEnemies();
        
        // 현재 타겟 유효성 체크 - 타겟이 감지 목록에 없으면 즉시 공격 중단
        if (currentTarget != null && !detectedEnemies.Contains(currentTarget))
        {
            EndAttack(); // 진행 중인 공격 즉시 중단
            currentTarget = null;
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
        // WASD 또는 화살표 키 입력 받기
        moveX = Input.GetAxisRaw("Horizontal");
        moveY = Input.GetAxisRaw("Vertical");
        
        // 입력 벡터 생성 및 정규화
        Vector2 input = new Vector2(moveX, moveY);
        return input.normalized;
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
            TriggerSpecialAnimation(CharacterAnimationState.Attack);
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


    #region Interaction Collision Override (Detection용)

    /// <summary>
    /// Interaction 콜리전 이벤트 처리 (적 감지 시 자동 공격)
    /// </summary>
    /// <param name="other">감지된 오브젝트</param>
    public override void OnInteractionCollision(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 적 감지 시 목록에 추가 (Attack 콜리전은 제외)
        if (other.CompareTag("Enemy"))
        {
            // Attack 콜리전인지 확인
            CharacterCollisionManager otherCollisionManager = other.GetComponentInParent<CharacterCollisionManager>();
            if (otherCollisionManager == null || !otherCollisionManager.IsAttackCollider(other))
            {
                AddDetectedEnemy(other);
            }
        }
        
        // 기본 상호작용 처리도 수행
        base.OnInteractionCollision(other);
    }
    
    /// <summary>
    /// Interaction 콜리전에서 나갔을 때 처리 (적이 감지 범위를 벗어남)
    /// </summary>
    /// <param name="other">나간 오브젝트</param>
    public override void OnInteractionExit(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 적이 감지 범위를 벗어났을 때 (Attack 콜리전은 제외)
        if (other.CompareTag("Enemy"))
        {
            // Attack 콜리전인지 확인
            CharacterCollisionManager otherCollisionManager = other.GetComponentInParent<CharacterCollisionManager>();
            if (otherCollisionManager == null || !otherCollisionManager.IsAttackCollider(other))
            {
                // 감지 범위를 벗어났지만, 공격 범위 내에 있는지 확인
                if (!IsEnemyInAttackRange(other))
                {
                    // 현재 타겟이 이 적이면 즉시 공격 중단
                    if (currentTarget == other)
                    {
                        EndAttack(); // 진행 중인 공격 즉시 중단
                    }
                    RemoveDetectedEnemy(other);
                }
            }
        }
        
        // 기본 처리도 수행
        base.OnInteractionExit(other);
    }

    #endregion

    #region Attack Range Check Methods
    
    /// <summary>
    /// 적이 공격 범위 내에 있는지 확인합니다.
    /// </summary>
    /// <param name="enemyCollider">확인할 적의 콜라이더</param>
    /// <returns>공격 범위 내에 있으면 true</returns>
    private bool IsEnemyInAttackRange(Collider2D enemyCollider)
    {
        if (enemyCollider == null || collisionManager == null) return false;
        
        // 적이 여전히 존재하고 활성화되어 있는지 확인
        if (!enemyCollider.gameObject.activeInHierarchy) return false;
        
        // Attack 콜라이더가 설정되어 있는지 확인 (활성화 상태는 확인하지 않음)
        if (!collisionManager.HasAttackCollider()) return false;
        
        // Attack 콜라이더와 적의 거리 계산 (SqrMagnitude 사용으로 성능 최적화)
        float sqrDistance = (transform.position - enemyCollider.transform.position).sqrMagnitude;
        
        // Attack 콜라이더의 공격 범위 가져오기
        float attackRange = collisionManager.GetAttackRange();
        return sqrDistance <= attackRange * attackRange;
    }
    
    /// <summary>
    /// 감지된 적 중 공격 범위를 벗어난 적들을 제거합니다.
    /// </summary>
    private void CheckAndRemoveOutOfRangeEnemies()
    {
        if (detectedEnemies.Count == 0) return;
        
        // 감지된 적 목록을 복사하여 순회 (제거 시 목록 변경으로 인한 오류 방지)
        var enemiesToCheck = new List<Collider2D>(detectedEnemies);
        
        foreach (var enemy in enemiesToCheck)
        {
            if (enemy == null) continue;
            
            // 공격 범위를 벗어났는지 확인
            if (!IsEnemyInAttackRange(enemy))
            {
                RemoveDetectedEnemy(enemy);
            }
        }
    }
    
    #endregion

    #region Detection and Auto Attack Methods

    
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
    
    /// <summary>
    /// 감지된 적을 목록에 추가합니다.
    /// </summary>
    /// <param name="enemy">감지된 적</param>
    private void AddDetectedEnemy(Collider2D enemy)
    {
        if (enemy == null) return;
        
        // 이미 목록에 있는지 확인
        if (!detectedEnemies.Contains(enemy))
        {
            detectedEnemies.Add(enemy);
            
            // 첫 번째 적이 감지되면 감지 시간 기록
            if (detectedEnemies.Count == 1)
            {
                firstDetectionTime = Time.time;
            }
            
            // 현재 타겟이 없거나 더 가까운 적이면 타겟 변경
            if (currentTarget == null || IsCloserThanCurrentTarget(enemy))
            {
                SetCurrentTarget(enemy);
            }
            
        }
    }
    
    /// <summary>
    /// 감지된 적을 목록에서 제거합니다.
    /// </summary>
    /// <param name="enemy">제거할 적</param>
    private void RemoveDetectedEnemy(Collider2D enemy)
    {
        if (enemy == null) return;
        
        if (detectedEnemies.Contains(enemy))
        {
            detectedEnemies.Remove(enemy);
            
            // 모든 적이 제거되면 감지 시간 리셋
            if (detectedEnemies.Count == 0)
            {
                firstDetectionTime = 0f;
            }
            
            // 현재 타겟이 제거된 적이면 새로운 타겟 선택
            if (currentTarget == enemy)
            {
                SelectNewTarget();
            }
            
        }
    }
    
    /// <summary>
    /// 감지된 적 목록을 정리합니다 (사망한 적 제거).
    /// </summary>
    private void CleanupDetectedEnemies()
    {
        for (int i = detectedEnemies.Count - 1; i >= 0; i--)
        {
            var enemy = detectedEnemies[i];
            if (enemy == null || IsEnemyDead(enemy))
            {
                RemoveDetectedEnemy(enemy);
            }
        }
    }
    
    /// <summary>
    /// 적이 사망했는지 확인합니다.
    /// </summary>
    /// <param name="enemy">확인할 적</param>
    /// <returns>사망했으면 true</returns>
    private bool IsEnemyDead(Collider2D enemy)
    {
        if (enemy == null) return true;
        
        CharacterBase enemyCharacter = enemy.GetComponent<CharacterBase>();
        if (enemyCharacter == null)
        {
            enemyCharacter = enemy.GetComponentInParent<CharacterBase>();
        }
        
        return enemyCharacter != null && enemyCharacter.IsDead();
    }
    
    /// <summary>
    /// 새로운 타겟을 선택합니다.
    /// </summary>
    private void SelectNewTarget()
    {
        currentTarget = null;
        
        if (detectedEnemies.Count > 0)
        {
            // 가장 가까운 적을 타겟으로 선택
            float closestDistance = float.MaxValue;
            foreach (var enemy in detectedEnemies)
            {
                if (enemy != null && !IsEnemyDead(enemy))
                {
                    float sqrDistance = (transform.position - enemy.transform.position).sqrMagnitude;
                    if (sqrDistance < closestDistance)
                    {
                        closestDistance = sqrDistance;
                        currentTarget = enemy;
                    }
                }
            }
            
        }
    }
    
    /// <summary>
    /// 현재 타겟을 설정합니다.
    /// </summary>
    /// <param name="enemy">새로운 타겟</param>
    private void SetCurrentTarget(Collider2D enemy)
    {
        currentTarget = enemy;
    }
    
    /// <summary>
    /// 주어진 적이 현재 타겟보다 가까운지 확인합니다.
    /// </summary>
    /// <param name="enemy">확인할 적</param>
    /// <returns>더 가까우면 true</returns>
    private bool IsCloserThanCurrentTarget(Collider2D enemy)
    {
        if (currentTarget == null) return true;
        
        float currentSqrDistance = (transform.position - currentTarget.transform.position).sqrMagnitude;
        float newSqrDistance = (transform.position - enemy.transform.position).sqrMagnitude;
        
        return newSqrDistance < currentSqrDistance;
    }
    
    /// <summary>
    /// 자동 공격을 처리합니다.
    /// </summary>
    private void HandleAutoAttack()
    {
        // 첫 감지 후 지연 시간 확인
        if (Time.time - firstDetectionTime >= attackDelay)
        {
            // 쿨다운 확인
            if (Time.time - lastAutoAttackTime >= autoAttackCooldown)
            {
                // 공격 가능한 상태인지 확인
                if (CanMove() && !IsDead() && currentTarget != null)
                {
                    // 타겟이 여전히 감지된 적 목록에 있는지 확인
                    if (detectedEnemies.Contains(currentTarget))
                    {
                        // 자동 공격 실행
                        StartAttack();
                        lastAutoAttackTime = Time.time;
                    }
                    else
                    {
                        // 타겟이 감지 목록에서 제거되었으면 타겟 초기화
                        currentTarget = null;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 모든 감지된 적을 제거합니다.
    /// </summary>
    public void ClearAllDetectedEnemies()
    {
        detectedEnemies.Clear();
        currentTarget = null;
        firstDetectionTime = 0f;
    }
    
    /// <summary>
    /// 자동 공격을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetAutoAttackEnabled(bool enabled)
    {
        enableAutoAttack = enabled;
        if (!enabled)
        {
            ClearAllDetectedEnemies();
        }
    }

    #endregion

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
    /// 플레이어의 Interaction 콜리전을 활성화/비활성화합니다 (감지용).
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetInteractionCollisionEnabled(bool enabled)
    {
        SetCollisionTypeEnabled(CollisionType.Interaction, enabled);
    }

    /// <summary>
    /// 공격 중일 때 Attack 콜리전을 활성화합니다.
    /// </summary>
    public override void EnableAttackCollision()
    {
        SetCollisionTypeEnabled(CollisionType.Attack, true);
    }

    /// <summary>
    /// 공격이 끝났을 때 Attack 콜리전을 비활성화합니다.
    /// </summary>
    public override void DisableAttackCollision()
    {
        SetCollisionTypeEnabled(CollisionType.Attack, false);
    }

    /// <summary>
    /// 공격 애니메이션을 시작합니다.
    /// </summary>
    public void StartAttack()
    {
        // Attack 애니메이션 실행
        TriggerSpecialAnimation(CharacterAnimationState.Attack);
        
        // Attack 콜리전 즉시 활성화 (타격 판정 시작)
        EnableAttackCollision();
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
            GUILayout.Label($"Auto Attack: {enableAutoAttack}");
            GUILayout.Label($"Detected Enemies: {detectedEnemies.Count}");
            GUILayout.Label($"Current Target: {(currentTarget != null ? currentTarget.gameObject.name : "None")}");
            GUILayout.Label($"Attack Delay: {attackDelay}s");
            GUILayout.Label($"Last Auto Attack: {Time.time - lastAutoAttackTime:F1}s ago");
            GUILayout.EndArea();
        }
    }

    #endregion
}
