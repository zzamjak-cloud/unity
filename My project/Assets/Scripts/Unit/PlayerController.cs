using UnityEngine;

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
    
    [Header("Player Health UI")]
    [SerializeField] private Vector3 playerHealthBarOffset = new Vector3(0, 1.8f, 0);  // 플레이어 체력바 오프셋
    [SerializeField] private GameObject playerHealthBarObject;  // 플레이어 체력바 GameObject (직접 연결)
    
    // 입력 처리용 변수들
    private float moveX = 0f;
    private float moveY = 0f;
    private bool isShiftPressed = false;
    
    // 특수 애니메이션 입력 상태
    private bool isAttackPressed = false;
    private bool isCeremonyPressed = false;
    private bool isBlankPressed = false;
    private bool isDeathPressed = false;

    protected override void Start()
    {
        // 플레이어 전용 체력 설정
        maxHealth = playerMaxHealth;
        attackPower = playerAttackPower;
        
        base.Start();
        
        // 플레이어 전용 체력바 설정
        InitializePlayerHealthUI();
        
        // 플레이어 전용 초기화
        Debug.Log("PlayerController 초기화 완료");
    }
    
    /// <summary>
    /// 플레이어 전용 체력 UI 초기화
    /// </summary>
    private void InitializePlayerHealthUI()
    {
        if (!enableHealthBar) return;
        
        // 직접 연결된 HealthBar GameObject가 있는지 확인
        if (playerHealthBarObject != null)
        {
            healthBarUI = playerHealthBarObject.GetComponent<HealthBarUI>();
            if (healthBarUI == null)
            {
                // HealthBarUI 컴포넌트가 없으면 자식에서 찾기
                healthBarUI = playerHealthBarObject.GetComponentInChildren<HealthBarUI>();
            }
        }
        
        if (healthBarUI != null)
        {
            // 플레이어 전용 체력바 설정 적용
            healthBarUI.SetSettings(playerHealthBarOffset, true, true);
            healthBarUI.SetVisible(true);
            
            // 초기 체력 표시
            healthBarUI.UpdateHealthDisplay(currentHealth, maxHealth);
        }
    }

    protected override void Update()
    {
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
    /// 플레이어의 Interaction 콜리전을 활성화/비활성화합니다.
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
        Debug.Log("[플레이어 공격] 공격 시작 - Attack 애니메이션 실행 및 콜리전 활성화");
        
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
        Debug.Log("[플레이어 공격] 공격 종료 - Attack 콜리전은 자동으로 비활성화됩니다");
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
            GUILayout.EndArea();
        }
    }

    #endregion
}
