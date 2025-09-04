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
    

    [Header("Animation")]
    [SerializeField] protected Animator anim;  // 애니메이터 컴포넌트 (Inspector에서 직접 연결)
    
    protected Rigidbody2D rb;
    protected CharacterAnimationState currentAnimationState = CharacterAnimationState.Idle;
    protected SortingGroup sortingGroup; // 정렬 순서 조절을 위한 SortingGroup 컴포넌트
    protected int lastSortingOrder = 0; // 마지막 정렬 순서 (중복 업데이트 방지)
    

    // 현재 이동 상태
    protected Vector2 currentMovement = Vector2.zero;
    protected bool isCurrentlyRunning = false;

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
        
        // 초기 정렬 순서 설정
        if (sortingGroup != null && enableSortingOrderAdjustment)
        {
            float yPosition = transform.position.y;
            lastSortingOrder = baseSortingOrder + Mathf.FloorToInt(yPosition * sortingOrderMultiplier);
            sortingGroup.sortingOrder = lastSortingOrder;
        }
    }

    protected virtual void Update()
    {
        // 하위 클래스에서 구현할 추상 메서드들 호출
        UpdateMovement();
        UpdateAnimation();
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
        
        // Y축 위치에 따른 정렬 순서 계산
        // Y축이 높을수록 음수값이 커져서 앞에 표시되고,
        // Y축이 낮을수록 양수값이 커져서 뒤에 표시됩니다.
        float yPosition = transform.position.y;
        int newSortingOrder = baseSortingOrder + Mathf.FloorToInt(yPosition * sortingOrderMultiplier);
        
        // 정렬 순서가 변경된 경우에만 업데이트 (중복 업데이트 방지)
        if (lastSortingOrder != newSortingOrder)
        {
            sortingGroup.sortingOrder = newSortingOrder;
            lastSortingOrder = newSortingOrder;
            
            // 디버그 로그 (개발 중에만)
            #if UNITY_EDITOR
            Debug.Log($"{gameObject.name}: Y={yPosition:F2}, SortOrder={newSortingOrder}");
            #endif
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
    /// Interaction 콜리전 이벤트 처리 (아이템/오브젝트 상호작용)
    /// </summary>
    /// <param name="other">충돌한 오브젝트</param>
    public virtual void OnInteractionCollision(Collider2D other)
    {
        if (!enableCollisionSystem) return;
        
        // 기본적인 상호작용 처리
        HandleInteractionCollision(other);
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
            Debug.Log($"{gameObject.name}: Body 콜리전 - {other.gameObject.name}와 충돌");
        }
        else if (other.CompareTag("Obstacle"))
        {
            // 장애물과의 충돌 처리
            Debug.Log($"{gameObject.name}: Body 콜리전 - 장애물 {other.gameObject.name}와 충돌");
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
            // 적이나 플레이어에 대한 공격 처리
            Debug.Log($"[타격 성공] {gameObject.name}: {other.gameObject.name}를 성공적으로 공격했습니다!");
            
            // 타격받은 대상에게 Blank 애니메이션 실행
            TriggerTargetBlankAnimation(other);
            
            // 타격받은 대상에게 피격 이펙트 재생
            TriggerTargetDamageEffect(other);
        }
        else if (other.CompareTag("Destructible"))
        {
            // 파괴 가능한 오브젝트에 대한 공격 처리
            Debug.Log($"[타격 성공] {gameObject.name}: 파괴 가능한 오브젝트 {other.gameObject.name}를 공격했습니다!");
        }
    }

    /// <summary>
    /// Interaction 콜리전을 처리합니다.
    /// </summary>
    /// <param name="other">충돌한 오브젝트</param>
    protected virtual void HandleInteractionCollision(Collider2D other)
    {
        // 기본 구현: 상호작용만 처리
        if (other.CompareTag("Item"))
        {
            // 아이템과의 상호작용 처리
            Debug.Log($"{gameObject.name}: Interaction 콜리전 - 아이템 {other.gameObject.name}와 상호작용");
        }
        else if (other.CompareTag("Interactable"))
        {
            // 상호작용 가능한 오브젝트와의 처리
            Debug.Log($"{gameObject.name}: Interaction 콜리전 - 상호작용 가능한 오브젝트 {other.gameObject.name}와 상호작용");
        }
        else if (other.CompareTag("NPC"))
        {
            // NPC와의 상호작용 처리
            Debug.Log($"{gameObject.name}: Interaction 콜리전 - NPC {other.gameObject.name}와 상호작용");
        }
        else if (other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            // 적이나 플레이어와의 상호작용 처리 (부모에서 CharacterBase 찾기)
            CharacterBase targetCharacter = other.GetComponent<CharacterBase>();
            if (targetCharacter == null)
            {
                targetCharacter = other.GetComponentInParent<CharacterBase>();
            }
            
            if (targetCharacter != null)
            {
                Debug.Log($"{gameObject.name}: Interaction 콜리전 - {targetCharacter.gameObject.name}와 상호작용");
                // 여기에 상호작용 로직 추가 가능
            }
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
        
        // Attack 콜리전이 비활성화되어 있다면 활성화 (이펙트와 동기화)
        if (!IsAttackCollisionEnabled())
        {
            EnableAttackCollision();
        }
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
        
        Debug.Log($"[Blank 애니메이션] {gameObject.name}: {target.gameObject.name}에게 Blank 애니메이션 실행 시도");
        
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
            Debug.Log($"[Blank 애니메이션] {gameObject.name}: {target.gameObject.name}에게 Blank 애니메이션 성공적으로 실행됨 (대상: {targetCharacter.gameObject.name})");
        }
        else
        {
            Debug.LogWarning($"[Blank 애니메이션] {gameObject.name}: {target.gameObject.name}에서 CharacterBase 컴포넌트를 찾을 수 없어 Blank 애니메이션을 실행할 수 없습니다.");
        }
    }

    /// <summary>
    /// 타격받은 대상에게 피격 이펙트를 재생합니다.
    /// </summary>
    /// <param name="target">타격받은 대상</param>
    protected virtual void TriggerTargetDamageEffect(Collider2D target)
    {
        if (target == null) return;
        
        Debug.Log($"[피격 이펙트] {gameObject.name}: {target.gameObject.name}에게 피격 이펙트 재생 시도");
        
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
            Debug.Log($"[피격 이펙트] {gameObject.name}: {target.gameObject.name}에게 피격 이펙트 성공적으로 재생됨 (대상: {targetCharacter.gameObject.name})");
        }
        else
        {
            Debug.LogWarning($"[피격 이펙트] {gameObject.name}: {target.gameObject.name}에서 CharacterBase 컴포넌트를 찾을 수 없어 피격 이펙트를 재생할 수 없습니다.");
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
            Debug.Log($"[공격 시작] {gameObject.name}: Attack 콜리전 활성화 요청 완료");
        }
        else
        {
            Debug.LogWarning($"[공격 시작] {gameObject.name}: CharacterCollisionManager 컴포넌트를 찾을 수 없습니다.");
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
            Debug.Log($"[공격 종료] {gameObject.name}: Attack 콜리전 비활성화 요청 완료");
        }
        else
        {
            Debug.LogWarning($"[공격 종료] {gameObject.name}: CharacterCollisionManager 컴포넌트를 찾을 수 없습니다.");
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
        Debug.Log($"[애니메이션] {gameObject.name}: Attack 애니메이션 종료 - Attack 콜리전은 자동으로 비활성화됩니다");
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
        
        Debug.Log($"{gameObject.name}: 콜리전 시스템 {(enabled ? "활성화" : "비활성화")}");
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
        
        Debug.Log($"{gameObject.name}: {collisionType} 콜리전 {(enabled ? "활성화" : "비활성화")}");
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
}
