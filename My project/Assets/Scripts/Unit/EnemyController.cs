using UnityEngine;

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
    
    // 적 전용 상태 변수들
    private Vector3 initialPosition;
    private float idleTimer = 0f;
    
    // 랜덤 애니메이션용 변수
    private float lastRandomAnimationTime = 0f;
    private float randomAnimationCooldown = 5f;  // 랜덤 애니메이션 쿨다운

    protected override void Start()
    {
        base.Start();
        
        // 적 전용 초기화
        InitializeEnemy();
    }

    protected override void Update()
    {
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
    /// 적의 Attack 콜리전을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetAttackCollisionEnabled(bool enabled)
    {
        SetCollisionTypeEnabled(CollisionType.Attack, enabled);
    }

    /// <summary>
    /// 적의 Interaction 콜리전을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetInteractionCollisionEnabled(bool enabled)
    {
        SetCollisionTypeEnabled(CollisionType.Interaction, enabled);
    }

    /// <summary>
    /// 공격 중일 때 Attack 콜리전을 활성화합니다.
    /// </summary>
    public void EnableAttackCollision()
    {
        SetAttackCollisionEnabled(true);
    }

    /// <summary>
    /// 공격이 끝났을 때 Attack 콜리전을 비활성화합니다.
    /// </summary>
    public void DisableAttackCollision()
    {
        SetAttackCollisionEnabled(false);
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
}
