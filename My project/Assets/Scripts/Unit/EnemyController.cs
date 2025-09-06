using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 적 캐릭터를 제어하는 컨트롤러
/// CharacterBase를 상속받아 정적 배치된 적 캐릭터를 관리합니다.
/// </summary>
public class EnemyController : CharacterBase
{
    [Header("Enemy Settings")]
    [SerializeField] private bool isStaticEnemy = true;  // 정적 적 여부 (현재는 true로 고정)
    [SerializeField] private Vector3 spawnPosition;      // 스폰 위치
    
    [Header("Enemy Behavior")]
    [SerializeField] private float idleAnimationDelay = 2f;  // Idle 애니메이션 전환 지연 시간
    [SerializeField] private bool enableRandomAnimations = false;  // 랜덤 애니메이션 활성화 여부
    [SerializeField] private float randomAnimationChance = 0.1f;  // 랜덤 애니메이션 확률 (프레임당)
    
    [Header("Enemy Stats")]
    [SerializeField] private int enemyMaxHealth = 80;  // 적 최대 체력
    [SerializeField] private int enemyAttackPower = 15;  // 적 공격력
    
    [Header("Enemy Status UI")]
    [SerializeField] private Vector3 enemyStatusBarOffset = new Vector3(0, 1.5f, 0);  // 적 상태바 오프셋
    [SerializeField] private GameObject enemyStatusBarObject;  // 적 상태바 GameObject (직접 연결)
    
    [Header("Auto Attack Settings")]
    [SerializeField] private bool enableAutoAttack = true;  // 자동 공격 활성화 여부
    [SerializeField] private float autoAttackCooldown = 1.5f;  // 자동 공격 쿨다운 시간 (적은 조금 더 느리게)
    [SerializeField] private float attackDelay = 0.1f;  // 플레이어 감지 후 공격 지연 시간
    
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

    protected override void Start()
    {
        // 적 전용 체력 설정
        maxHealth = enemyMaxHealth;
        attackPower = enemyAttackPower;
        
        base.Start();
        
        // 적 전용 체력바 설정
        InitializeEnemyStatusUI();
        
        // 적 전용 초기화
        InitializeEnemy();
    }
    
    /// <summary>
    /// 적 전용 상태 UI 초기화
    /// </summary>
    private void InitializeEnemyStatusUI()
    {
        if (!enableStatusBar) return;
        
        // 직접 연결된 StatusBar GameObject가 있는지 확인
        if (enemyStatusBarObject != null)
        {
            statusBarUI = enemyStatusBarObject.GetComponent<StatusBarUI>();
            if (statusBarUI == null)
            {
                // StatusBarUI 컴포넌트가 없으면 자식에서 찾기
                statusBarUI = enemyStatusBarObject.GetComponentInChildren<StatusBarUI>();
            }
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
        // 감지된 플레이어 목록 정리 (사망한 플레이어 제거)
        CleanupDetectedPlayers();
        
        // 자동 공격 처리
        if (enableAutoAttack && detectedPlayers.Count > 0)
        {
            HandleAutoAttack();
        }
        
        // 공격 범위 체크 - 감지된 플레이어 중 공격 범위를 벗어난 플레이어들 제거
        CheckAndRemoveOutOfRangePlayers();
        
        // 정적 적이므로 입력 처리는 하지 않음
        // 부모 클래스의 Update 호출 (UpdateMovement, UpdateAnimation 실행)
        base.Update();
    }

    #region ICharacterController Implementation

    /// <summary>
    /// 적의 이동을 업데이트합니다.
    /// 정적 적이므로 이동하지 않습니다.
    /// </summary>
    public override void UpdateMovement()
    {
        // 정적 적이므로 이동하지 않음
        // 위치가 변경되었다면 초기 위치로 복귀
        if (isStaticEnemy && Vector3.Distance(transform.position, initialPosition) > 0.1f)
        {
            ReturnToInitialPosition();
        }
        
        // 물리 이동 정지
        HandlePhysicsMovement(Vector2.zero, false);
        
        // 현재 상태 저장
        currentMovement = Vector2.zero;
        isCurrentlyRunning = false;
    }

    /// <summary>
    /// 적의 애니메이션을 업데이트합니다.
    /// Idle 상태와 랜덤 애니메이션을 처리합니다.
    /// </summary>
    public override void UpdateAnimation()
    {
        // 애니메이션 상태 업데이트
        UpdateAnimationState();
        
        // Idle 애니메이션 처리
        HandleIdleAnimation();
        
        // 랜덤 애니메이션 처리
        if (enableRandomAnimations)
        {
            HandleRandomAnimations();
        }
        
        // 이동 애니메이션은 항상 Idle 상태
        HandleAnimations(0f, false);
    }

    /// <summary>
    /// 적은 이동하지 않으므로 항상 Vector2.zero를 반환합니다.
    /// </summary>
    /// <returns>이동하지 않음 (0, 0)</returns>
    public override Vector2 GetMovementInput()
    {
        // 정적 적이므로 이동하지 않음
        return Vector2.zero;
    }

    #endregion

    #region Enemy Initialization

    /// <summary>
    /// 적 캐릭터를 초기화합니다.
    /// </summary>
    private void InitializeEnemy()
    {
        // 초기 위치 저장
        initialPosition = transform.position;
        spawnPosition = initialPosition;
        
        // 정적 적 설정
        isStaticEnemy = true;
        
        Debug.Log($"EnemyController 초기화 완료 - 위치: {initialPosition}");
    }

    #endregion

    #region Position Management

    /// <summary>
    /// 초기 위치로 복귀합니다.
    /// </summary>
    private void ReturnToInitialPosition()
    {
        if (rb != null)
        {
            // Rigidbody 위치 설정
            rb.position = initialPosition;
            
            // 이동 상태 초기화
            currentMovement = Vector2.zero;
            isCurrentlyRunning = false;
            HandlePhysicsMovement(Vector2.zero, false);
            
            Debug.Log($"적이 초기 위치로 복귀: {initialPosition}");
        }
    }

    /// <summary>
    /// 새로운 스폰 위치를 설정합니다.
    /// </summary>
    /// <param name="newSpawnPosition">새로운 스폰 위치</param>
    public void SetSpawnPosition(Vector3 newSpawnPosition)
    {
        spawnPosition = newSpawnPosition;
        initialPosition = newSpawnPosition;
        
        // 즉시 새 위치로 이동
        if (rb != null)
        {
            rb.position = newSpawnPosition;
        }
        
        Debug.Log($"적 스폰 위치 변경: {newSpawnPosition}");
    }

    #endregion

    #region Animation Management

    /// <summary>
    /// Idle 애니메이션을 처리합니다.
    /// </summary>
    private void HandleIdleAnimation()
    {
        // Idle 상태일 때만 처리
        if (currentAnimationState == CharacterAnimationState.Idle)
        {
            idleTimer += Time.deltaTime;
            
            // 일정 시간 후 Idle 애니메이션 강제 실행
            if (idleTimer >= idleAnimationDelay)
            {
                // Idle 애니메이션 상태 유지
                idleTimer = 0f;
            }
        }
        else
        {
            // 다른 애니메이션 상태일 때 타이머 리셋
            idleTimer = 0f;
        }
    }

    /// <summary>
    /// 랜덤 애니메이션을 처리합니다.
    /// </summary>
    private void HandleRandomAnimations()
    {
        // 쿨다운 체크
        if (Time.time - lastRandomAnimationTime < randomAnimationCooldown)
        {
            return;
        }
        
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

    /// <summary>
    /// 랜덤 애니메이션을 실행합니다.
    /// </summary>
    private void ExecuteRandomAnimation()
    {
        // 랜덤하게 애니메이션 선택
        float randomValue = Random.Range(0f, 1f);
        
        if (randomValue < 0.3f)
        {
            TriggerSpecialAnimation(CharacterAnimationState.Attack);
            Debug.Log("적이 랜덤 공격 애니메이션 실행");
        }
        else if (randomValue < 0.6f)
        {
            TriggerSpecialAnimation(CharacterAnimationState.Blank);
            Debug.Log("적이 랜덤 Blank 애니메이션 실행");
        }
        else if (randomValue < 0.8f)
        {
            TriggerSpecialAnimation(CharacterAnimationState.Ceremony);
            Debug.Log("적이 랜덤 Ceremony 애니메이션 실행");
        }
        // Death는 랜덤으로 실행하지 않음
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 적을 특정 위치로 즉시 이동시킵니다.
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
            
            Debug.Log($"적이 위치로 이동: {position}");
        }
    }

    /// <summary>
    /// 적을 초기 위치로 즉시 이동시킵니다.
    /// </summary>
    public void ReturnToSpawn()
    {
        ReturnToInitialPosition();
    }

    /// <summary>
    /// 랜덤 애니메이션을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enable">활성화 여부</param>
    public void SetRandomAnimationsEnabled(bool enable)
    {
        enableRandomAnimations = enable;
        Debug.Log($"적 랜덤 애니메이션: {(enable ? "활성화" : "비활성화")}");
    }

    /// <summary>
    /// 랜덤 애니메이션 확률을 설정합니다.
    /// </summary>
    /// <param name="chance">확률 (0.0 ~ 1.0)</param>
    public void SetRandomAnimationChance(float chance)
    {
        randomAnimationChance = Mathf.Clamp01(chance);
        Debug.Log($"적 랜덤 애니메이션 확률 설정: {randomAnimationChance}");
    }

    /// <summary>
    /// 랜덤 애니메이션 쿨다운을 설정합니다.
    /// </summary>
    /// <param name="cooldown">쿨다운 시간 (초)</param>
    public void SetRandomAnimationCooldown(float cooldown)
    {
        randomAnimationCooldown = Mathf.Max(0f, cooldown);
        Debug.Log($"적 랜덤 애니메이션 쿨다운 설정: {randomAnimationCooldown}초");
    }

    /// <summary>
    /// Idle 애니메이션 전환 지연 시간을 설정합니다.
    /// </summary>
    /// <param name="delay">지연 시간 (초)</param>
    public void SetIdleAnimationDelay(float delay)
    {
        idleAnimationDelay = Mathf.Max(0f, delay);
        Debug.Log($"적 Idle 애니메이션 지연 시간 설정: {idleAnimationDelay}초");
    }

    /// <summary>
    /// 적이 정적 적인지 확인합니다.
    /// </summary>
    /// <returns>정적 적이면 true</returns>
    public bool IsStaticEnemy()
    {
        return isStaticEnemy;
    }

    /// <summary>
    /// 적의 초기 위치를 반환합니다.
    /// </summary>
    /// <returns>초기 위치</returns>
    public Vector3 GetInitialPosition()
    {
        return initialPosition;
    }

    #endregion

    #region Collision System Methods

    /// <summary>
    /// 적의 Body 콜리전을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetBodyCollisionEnabled(bool enabled)
    {
        SetCollisionTypeEnabled(CollisionType.Body, enabled);
    }


    /// <summary>
    /// 적의 Interaction 콜리전을 활성화/비활성화합니다 (감지용).
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
        Debug.Log("[적 공격] 공격 시작 - Attack 애니메이션 실행 및 콜리전 활성화");
        
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
        Debug.Log("[적 공격] 공격 종료 - Attack 콜리전은 자동으로 비활성화됩니다");
        OnAttackAnimationEnd();
    }

    #endregion

    /// <summary>
    /// 적의 스폰 위치를 반환합니다.
    /// </summary>
    /// <returns>스폰 위치</returns>
    public Vector3 GetSpawnPosition()
    {
        return spawnPosition;
    }

    #region Debug Methods

    /// <summary>
    /// 디버그 정보를 출력합니다.
    /// </summary>
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

    /// <summary>
    /// Scene 뷰에서 적의 정보를 표시합니다.
    /// </summary>
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


    #region Interaction Collision Override (Detection용)

    /// <summary>
    /// Interaction 콜리전 이벤트 처리 (플레이어 감지 시 자동 공격)
    /// </summary>
    /// <param name="other">감지된 오브젝트</param>
    public override void OnInteractionCollision(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 플레이어 감지 시 목록에 추가 (Attack 콜리전은 제외)
        if (other.CompareTag("Player"))
        {
            // Attack 콜리전인지 확인
            CharacterCollisionManager otherCollisionManager = other.GetComponentInParent<CharacterCollisionManager>();
            if (otherCollisionManager == null || !otherCollisionManager.IsAttackCollider(other))
            {
                AddDetectedPlayer(other);
                Debug.Log($"[적 감지] Interaction Collision으로 플레이어 감지됨: {other.gameObject.name}");
            }
        }
        
        // 기본 상호작용 처리도 수행
        base.OnInteractionCollision(other);
    }
    
    /// <summary>
    /// Interaction 콜리전에서 나갔을 때 처리 (플레이어가 감지 범위를 벗어남)
    /// </summary>
    /// <param name="other">나간 오브젝트</param>
    public override void OnInteractionExit(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 플레이어가 감지 범위를 벗어났을 때 (Attack 콜리전은 제외)
        if (other.CompareTag("Player"))
        {
            // Attack 콜리전인지 확인
            CharacterCollisionManager otherCollisionManager = other.GetComponentInParent<CharacterCollisionManager>();
            if (otherCollisionManager == null || !otherCollisionManager.IsAttackCollider(other))
            {
                // 감지 범위를 벗어났지만, 공격 범위 내에 있는지 확인
                if (IsPlayerInAttackRange(other))
                {
                    Debug.Log($"[적 감지] 감지 범위 벗어남, 하지만 공격 범위 내에 있음: {other.gameObject.name}");
                    // 공격 범위 내에 있으면 제거하지 않음
                }
                else
                {
                    RemoveDetectedPlayer(other);
                    Debug.Log($"[적 감지] 공격 범위도 벗어나서 플레이어 제거됨: {other.gameObject.name}");
                }
            }
        }
        
        // 기본 처리도 수행
        base.OnInteractionExit(other);
    }

    #endregion

    #region Attack Range Check Methods
    
    /// <summary>
    /// 플레이어가 공격 범위 내에 있는지 확인합니다.
    /// </summary>
    /// <param name="playerCollider">확인할 플레이어의 콜라이더</param>
    /// <returns>공격 범위 내에 있으면 true</returns>
    private bool IsPlayerInAttackRange(Collider2D playerCollider)
    {
        if (playerCollider == null || collisionManager == null) return false;
        
        // Attack 콜라이더가 설정되어 있는지 확인 (활성화 상태는 확인하지 않음)
        if (!collisionManager.HasAttackCollider()) return false;
        
        // Attack 콜라이더와 플레이어의 거리 계산
        float distance = Vector2.Distance(transform.position, playerCollider.transform.position);
        
        // Attack 콜라이더의 공격 범위 가져오기
        float attackRange = collisionManager.GetAttackRange();
        bool inRange = distance <= attackRange;
        
        Debug.Log($"[공격 범위 체크] {playerCollider.name}: 거리={distance:F2}, 공격범위={attackRange:F2}, 범위내={inRange}");
        
        return inRange;
    }
    
    /// <summary>
    /// 감지된 플레이어 중 공격 범위를 벗어난 플레이어들을 제거합니다.
    /// </summary>
    private void CheckAndRemoveOutOfRangePlayers()
    {
        if (detectedPlayers.Count == 0) return;
        
        // 감지된 플레이어 목록을 복사하여 순회 (제거 시 목록 변경으로 인한 오류 방지)
        var playersToCheck = new List<Collider2D>(detectedPlayers);
        
        foreach (var player in playersToCheck)
        {
            if (player == null) continue;
            
            // 공격 범위를 벗어났는지 확인
            if (!IsPlayerInAttackRange(player))
            {
                RemoveDetectedPlayer(player);
                Debug.Log($"[적 감지] 공격 범위 벗어남으로 플레이어 제거됨: {player.gameObject.name}");
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
    /// 감지된 플레이어를 목록에 추가합니다.
    /// </summary>
    /// <param name="player">감지된 플레이어</param>
    private void AddDetectedPlayer(Collider2D player)
    {
        if (player == null) return;
        
        // 이미 목록에 있는지 확인
        if (!detectedPlayers.Contains(player))
        {
            detectedPlayers.Add(player);
            
            // 첫 번째 플레이어가 감지되면 감지 시간 기록
            if (detectedPlayers.Count == 1)
            {
                firstDetectionTime = Time.time;
            }
            
            // 현재 타겟이 없거나 더 가까운 플레이어면 타겟 변경
            if (currentTarget == null || IsCloserThanCurrentTarget(player))
            {
                SetCurrentTarget(player);
            }
            
            Debug.Log($"[적 감지] 플레이어 추가됨: {player.gameObject.name} (총 {detectedPlayers.Count}명)");
        }
    }
    
    /// <summary>
    /// 감지된 플레이어를 목록에서 제거합니다.
    /// </summary>
    /// <param name="player">제거할 플레이어</param>
    private void RemoveDetectedPlayer(Collider2D player)
    {
        if (player == null) return;
        
        if (detectedPlayers.Contains(player))
        {
            detectedPlayers.Remove(player);
            
            // 모든 플레이어가 제거되면 감지 시간 리셋
            if (detectedPlayers.Count == 0)
            {
                firstDetectionTime = 0f;
            }
            
            // 현재 타겟이 제거된 플레이어면 새로운 타겟 선택
            if (currentTarget == player)
            {
                SelectNewTarget();
            }
            
            Debug.Log($"[적 감지] 플레이어 제거됨: {player.gameObject.name} (총 {detectedPlayers.Count}명)");
        }
    }
    
    /// <summary>
    /// 감지된 플레이어 목록을 정리합니다 (사망한 플레이어 제거).
    /// </summary>
    private void CleanupDetectedPlayers()
    {
        for (int i = detectedPlayers.Count - 1; i >= 0; i--)
        {
            var player = detectedPlayers[i];
            if (player == null || IsPlayerDead(player))
            {
                RemoveDetectedPlayer(player);
            }
        }
    }
    
    /// <summary>
    /// 플레이어가 사망했는지 확인합니다.
    /// </summary>
    /// <param name="player">확인할 플레이어</param>
    /// <returns>사망했으면 true</returns>
    private bool IsPlayerDead(Collider2D player)
    {
        if (player == null) return true;
        
        CharacterBase playerCharacter = player.GetComponent<CharacterBase>();
        if (playerCharacter == null)
        {
            playerCharacter = player.GetComponentInParent<CharacterBase>();
        }
        
        return playerCharacter != null && playerCharacter.IsDead();
    }
    
    /// <summary>
    /// 새로운 타겟을 선택합니다.
    /// </summary>
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
                    float distance = Vector2.Distance(transform.position, player.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        currentTarget = player;
                    }
                }
            }
            
            if (currentTarget != null)
            {
                Debug.Log($"[적 타겟] 새로운 타겟 선택: {currentTarget.gameObject.name}");
            }
        }
    }
    
    /// <summary>
    /// 현재 타겟을 설정합니다.
    /// </summary>
    /// <param name="player">새로운 타겟</param>
    private void SetCurrentTarget(Collider2D player)
    {
        currentTarget = player;
        Debug.Log($"[적 타겟] 타겟 설정: {player.gameObject.name}");
    }
    
    /// <summary>
    /// 주어진 플레이어가 현재 타겟보다 가까운지 확인합니다.
    /// </summary>
    /// <param name="player">확인할 플레이어</param>
    /// <returns>더 가까우면 true</returns>
    private bool IsCloserThanCurrentTarget(Collider2D player)
    {
        if (currentTarget == null) return true;
        
        float currentDistance = Vector2.Distance(transform.position, currentTarget.transform.position);
        float newDistance = Vector2.Distance(transform.position, player.transform.position);
        
        return newDistance < currentDistance;
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
                    // 자동 공격 실행
                    StartAttack();
                    lastAutoAttackTime = Time.time;
                    
                    Debug.Log($"[적 자동공격] 타겟 {currentTarget.gameObject.name}에 대한 자동 공격 실행 (지연: {attackDelay}초)");
                }
            }
        }
    }
    
    /// <summary>
    /// 모든 감지된 플레이어를 제거합니다.
    /// </summary>
    public void ClearAllDetectedPlayers()
    {
        detectedPlayers.Clear();
        currentTarget = null;
        firstDetectionTime = 0f;
        Debug.Log("[적 감지] 모든 감지된 플레이어를 제거했습니다.");
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
            ClearAllDetectedPlayers();
        }
    }

    #endregion
}
