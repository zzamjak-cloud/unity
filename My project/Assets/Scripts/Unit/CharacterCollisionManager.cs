using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 캐릭터의 3개 콜리전을 관리하는 컴포넌트
/// Body, Attack, Interaction 콜리전을 각각 처리합니다.
/// </summary>
public class CharacterCollisionManager : MonoBehaviour
{
    [Header("Collision Settings")]
    [SerializeField] private Collider2D bodyCollider;        // Body 콜리전 (적/플레이어 충돌, 피격 판정)
    [SerializeField] private Collider2D attackCollider;     // Attack 콜리전 (공격 범위 감지, 타격 판정)
    [SerializeField] private Collider2D interactionCollider; // Interaction 콜리전 (아이템/오브젝트 상호작용)
    
    [Header("Collision Layers")]
    [SerializeField] private LayerMask bodyCollisionMask = -1;        // Body 콜리전 대상 레이어
    [SerializeField] private LayerMask attackCollisionMask = -1;      // Attack 콜리전 대상 레이어
    [SerializeField] private LayerMask interactionCollisionMask = -1; // Interaction 콜리전 대상 레이어
    
    [Header("Collision Tags")]
    [SerializeField] private string[] bodyCollisionTags = { GameConstants.TAG_ENEMY, GameConstants.TAG_PLAYER, GameConstants.TAG_OBSTACLE };
    [SerializeField] private string[] attackCollisionTags = { GameConstants.TAG_ENEMY, GameConstants.TAG_PLAYER, GameConstants.TAG_DESTRUCTIBLE };
    [SerializeField] private string[] interactionCollisionTags = { GameConstants.TAG_ITEM, GameConstants.TAG_INTERACTABLE, GameConstants.TAG_NPC };
    
    [Header("Attack Collision Settings")]
    [SerializeField] private float attackDuration = GameConstants.DEFAULT_ATTACK_DURATION;
    [SerializeField] private bool allowMultipleHitsPerTarget = false;
    [SerializeField] private string[] attackIgnoreTags = { "Item" };
    [SerializeField] private bool onlyHitBodyCollision = true;
    
    // 콜리전 이벤트를 처리할 핸들러
    private ICollisionHandler collisionHandler;
    
    // 콜리전 활성화 상태
    private bool isBodyCollisionEnabled = true;
    private bool isAttackCollisionEnabled = true;
    private bool isInteractionCollisionEnabled = true;
    
    // Attack 콜리전 관련
    private float attackTimer = 0f;
    private bool isAttackActive = false;
    private HashSet<Collider2D> hitTargets = new HashSet<Collider2D>();
    
    // 메모리 최적화를 위한 캐시된 리스트
    private List<Collider2D> tempColliderList = new List<Collider2D>();
    
    // 콜리전 이벤트 로깅
    [Header("Debug")]
    [SerializeField] private bool enableCollisionLogging = false;  // 기본값을 false로 변경하여 성능 최적화
    
    private void Awake()
    {
        // ICollisionHandler 인터페이스를 구현한 컴포넌트 찾기
        collisionHandler = GetComponent<ICollisionHandler>();
        
        if (collisionHandler == null)
        {
            Debug.LogWarning($"CharacterCollisionManager: {gameObject.name}에 ICollisionHandler를 구현한 컴포넌트가 없습니다.");
        }
        
        // 콜리전 컴포넌트 검증
        ValidateColliders();
    }
    
    private void Start()
    {
        // 콜리전 초기 설정
        SetupColliders();
    }
    
    private void Update()
    {
        // Attack 콜리전 타이머 업데이트
        if (isAttackActive)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                DeactivateAttack();
            }
        }
    }
    
    /// <summary>
    /// 콜리전 컴포넌트들을 검증합니다.
    /// </summary>
    private void ValidateColliders()
    {
        if (bodyCollider == null)
        {
            Debug.LogError($"CharacterCollisionManager: {gameObject.name}에 Body 콜리전이 할당되지 않았습니다.");
        }
        
        if (attackCollider == null)
        {
            Debug.LogWarning($"CharacterCollisionManager: {gameObject.name}에 Attack 콜리전이 할당되지 않았습니다.");
        }
        
        if (interactionCollider == null)
        {
            Debug.LogWarning($"CharacterCollisionManager: {gameObject.name}에 Interaction 콜리전이 할당되지 않았습니다.");
        }
    }
    
    /// <summary>
    /// 콜리전들을 초기 설정합니다.
    /// </summary>
    private void SetupColliders()
    {
        // Body 콜리전 설정
        if (bodyCollider != null)
        {
            bodyCollider.isTrigger = false; // 물리적 충돌
            SetupCollider(bodyCollider, bodyCollisionMask);
        }
        
        // Attack 콜리전 설정
        if (attackCollider != null)
        {
            attackCollider.isTrigger = true; // 트리거로 설정
            SetupCollider(attackCollider, attackCollisionMask);
        }
        
        // Interaction 콜리전 설정
        if (interactionCollider != null)
        {
            interactionCollider.isTrigger = true; // 트리거로 설정
            SetupCollider(interactionCollider, interactionCollisionMask);
        }
    }
    
    /// <summary>
    /// 개별 콜리전을 설정합니다.
    /// </summary>
    /// <param name="collider">설정할 콜리전</param>
    /// <param name="layerMask">레이어 마스크</param>
    private void SetupCollider(Collider2D collider, LayerMask layerMask)
    {
        // 콜리전 활성화
        collider.enabled = true;
        
        // 레이어 마스크가 설정되어 있으면 적용
        if (layerMask != -1)
        {
            // 개별 콜리전에 대한 레이어 마스크는 수동으로 처리해야 함
            // OnTriggerEnter2D에서 레이어 체크를 수행
        }
    }
    
    #region Collision Event Handlers
    
    /// <summary>
    /// Body 콜리전 이벤트 (물리적 충돌)
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (enableCollisionLogging)
        {
            Debug.Log($"[콜리전 매니저] {gameObject.name}: OnCollisionEnter2D 감지 - {collision.gameObject.name} (Body 콜리전 활성화: {isBodyCollisionEnabled})");
        }
        
        if (!isBodyCollisionEnabled || bodyCollider == null) 
        {
            if (enableCollisionLogging)
            {
                Debug.LogWarning($"[콜리전 매니저] {gameObject.name}: Body 콜리전이 비활성화되어 있거나 bodyCollider가 NULL입니다.");
            }
            return;
        }
        
        // Body 콜리전과의 충돌인지 확인
        if (collision.collider == bodyCollider)
        {
            if (enableCollisionLogging)
            {
                Debug.Log($"[콜리전 매니저] {gameObject.name}: Body 콜리전 충돌 확인 - {collision.gameObject.name}");
            }
            HandleBodyCollision(collision.collider);
        }
        else
        {
            if (enableCollisionLogging)
            {
                Debug.Log($"[콜리전 매니저] {gameObject.name}: Body 콜리전이 아닌 다른 콜리전과 충돌 - {collision.gameObject.name}");
            }
        }
    }
    
    /// <summary>
    /// Attack 콜리전 이벤트 (트리거)
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (enableCollisionLogging)
        {
            Debug.Log($"[콜리전 매니저] {gameObject.name}: OnTriggerEnter2D 감지 - {other.gameObject.name} (태그: {other.tag})");
        }
        
        // Body 콜리전은 OnCollisionEnter2D에서 처리하므로 제외
        if (bodyCollider != null && other == bodyCollider)
        {
            if (enableCollisionLogging)
            {
                Debug.Log($"[콜리전 매니저] {gameObject.name}: Body 콜리전은 OnCollisionEnter2D에서 처리됩니다 - 무시");
            }
            return;
        }
        
        if (enableCollisionLogging)
        {
            Debug.Log($"[콜리전 매니저] {gameObject.name}: Attack 콜리전 상태 - 활성화: {isAttackCollisionEnabled}, 공격 중: {isAttackActive}, attackCollider: {(attackCollider != null ? "설정됨" : "NULL")}");
        }
        
        // Attack 콜리전 처리 - 다른 오브젝트가 이 오브젝트의 Attack 콜리전과 충돌했을 때
        if (attackCollider != null && other != attackCollider && isAttackCollisionEnabled && isAttackActive)
        {
            Debug.Log($"[콜리전 매니저] {gameObject.name}: Attack 콜리전 조건 만족 - {other.gameObject.name}");
            
            // 레이어 마스크 확인
            if (((1 << other.gameObject.layer) & attackCollisionMask) != 0)
            {
                if (enableCollisionLogging)
                {
                    Debug.Log($"[콜리전 이벤트] {gameObject.name}: 레이어 마스크 조건 만족 - {other.gameObject.name} (레이어: {other.gameObject.layer})");
                }
                HandleAttackCollision(other);
            }
            else
            {
                if (enableCollisionLogging)
                {
                    Debug.Log($"[콜리전 이벤트] {gameObject.name}: 레이어 마스크 조건 불만족 - {other.gameObject.name} (레이어: {other.gameObject.layer}, 마스크: {attackCollisionMask})");
                }
            }
        }
        // Interaction 콜리전 처리 - 다른 오브젝트가 이 오브젝트의 Interaction 콜리전과 충돌했을 때
        else if (interactionCollider != null && other != interactionCollider && isInteractionCollisionEnabled)
        {
            // 레이어 마스크 확인
            if (((1 << other.gameObject.layer) & interactionCollisionMask) != 0)
            {
                HandleInteractionCollision(other);
            }
        }
        else
        {
            if (enableCollisionLogging)
            {
                Debug.Log($"[콜리전 이벤트] {gameObject.name}: 콜리전 조건 불만족 - {other.gameObject.name} (attackCollider: {attackCollider != null}, other != attackCollider: {other != attackCollider}, isAttackCollisionEnabled: {isAttackCollisionEnabled}, isAttackActive: {isAttackActive})");
            }
        }
    }
    
    /// <summary>
    /// Body 콜리전을 처리합니다.
    /// </summary>
    /// <param name="other">충돌한 콜리전</param>
    private void HandleBodyCollision(Collider2D other)
    {
        if (collisionHandler != null)
        {
            collisionHandler.OnBodyCollision(other);
        }
        
        if (enableCollisionLogging)
        {
            Debug.Log($"Body 콜리전: {gameObject.name} -> {other.gameObject.name}");
        }
    }
    
    /// <summary>
    /// Attack 콜리전을 처리합니다.
    /// </summary>
    /// <param name="other">충돌한 콜리전</param>
    private void HandleAttackCollision(Collider2D other)
    {
        if (enableCollisionLogging)
        {
            Debug.Log($"[콜리전 감지] {gameObject.name}: {other.gameObject.name}와 Attack 콜리전 감지됨");
        }
        
        // 타겟 유효성 확인
        if (!IsValidAttackTarget(other))
        {
            if (enableCollisionLogging)
            {
                Debug.Log($"[콜리전 디버그] {gameObject.name}: {other.gameObject.name}는 유효하지 않은 타겟입니다.");
            }
            return;
        }
        
        // 중복 타격 방지
        if (!allowMultipleHitsPerTarget && hitTargets.Contains(other))
        {
            if (enableCollisionLogging)
            {
                Debug.Log($"[콜리전 디버그] {gameObject.name}: {other.gameObject.name}는 이미 타격했습니다.");
            }
            return;
        }
        
        // 타격한 대상 목록에 추가
        hitTargets.Add(other);
        
        // 콜리전 핸들러에 이벤트 전달
        if (collisionHandler != null)
        {
            collisionHandler.OnAttackCollision(other);
        }
        
        if (enableCollisionLogging)
        {
            Debug.Log($"[타격 성공] {gameObject.name}: {other.gameObject.name}와 Attack 콜리전 발생");
        }
    }
    
    /// <summary>
    /// Interaction 콜리전을 처리합니다.
    /// </summary>
    /// <param name="other">충돌한 콜리전</param>
    private void HandleInteractionCollision(Collider2D other)
    {
        if (collisionHandler != null)
        {
            collisionHandler.OnInteractionCollision(other);
        }
        
        if (enableCollisionLogging)
        {
            Debug.Log($"Interaction 콜리전: {gameObject.name} -> {other.gameObject.name}");
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Body 콜리전을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetBodyCollisionEnabled(bool enabled)
    {
        isBodyCollisionEnabled = enabled;
        if (bodyCollider != null)
        {
            bodyCollider.enabled = enabled;
        }
    }
    
    /// <summary>
    /// Attack 콜리전을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetAttackCollisionEnabled(bool enabled)
    {
        isAttackCollisionEnabled = enabled;
        
        if (enabled)
        {
            // 공격 콜리전 활성화 시 ActivateAttack 호출
            ActivateAttack();
        }
        else
        {
            // 공격 콜리전 비활성화 시 DeactivateAttack 호출
            DeactivateAttack();
        }
    }
    
    /// <summary>
    /// Interaction 콜리전을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetInteractionCollisionEnabled(bool enabled)
    {
        isInteractionCollisionEnabled = enabled;
        if (interactionCollider != null)
        {
            interactionCollider.enabled = enabled;
        }
    }
    
    /// <summary>
    /// 모든 콜리전을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetAllCollisionsEnabled(bool enabled)
    {
        SetBodyCollisionEnabled(enabled);
        SetAttackCollisionEnabled(enabled);
        SetInteractionCollisionEnabled(enabled);
    }
    
    /// <summary>
    /// 특정 콜리전이 활성화되어 있는지 확인합니다.
    /// </summary>
    /// <param name="collisionType">확인할 콜리전 타입</param>
    /// <returns>활성화되어 있으면 true</returns>
    public bool IsCollisionEnabled(CollisionType collisionType)
    {
        switch (collisionType)
        {
            case CollisionType.Body:
                return isBodyCollisionEnabled;
            case CollisionType.Attack:
                return isAttackCollisionEnabled;
            case CollisionType.Interaction:
                return isInteractionCollisionEnabled;
            default:
                return false;
        }
    }
    
    /// <summary>
    /// 콜리전 로깅을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetCollisionLoggingEnabled(bool enabled)
    {
        enableCollisionLogging = enabled;
    }
    
    /// <summary>
    /// Attack 콜리전을 활성화합니다.
    /// </summary>
    public void ActivateAttack()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = true;
        }
        
        isAttackActive = true;
        attackTimer = attackDuration;
        hitTargets.Clear();
    }
    
    /// <summary>
    /// Attack 콜리전을 비활성화합니다.
    /// </summary>
    public void DeactivateAttack()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }
        
        isAttackActive = false;
        attackTimer = 0f;
        hitTargets.Clear();
    }
    
    /// <summary>
    /// Attack 콜리전이 활성화되어 있는지 확인합니다.
    /// </summary>
    public bool IsAttackActive()
    {
        return isAttackActive && attackCollider != null && attackCollider.enabled;
    }
    
    /// <summary>
    /// 타겟이 유효한지 확인합니다.
    /// </summary>
    private bool IsValidAttackTarget(Collider2D target)
    {
        if (target == null) 
        {
            if (enableCollisionLogging)
            {
                Debug.Log($"[타겟 필터링] {gameObject.name}: 타겟이 null입니다.");
            }
            return false;
        }
        
        if (enableCollisionLogging)
        {
            Debug.Log($"[타겟 필터링] {gameObject.name}: {target.gameObject.name} 검증 시작 - 태그: {target.tag}, 이름: {target.gameObject.name}");
        }
        
        // 무시할 태그 확인
        foreach (string ignoreTag in attackIgnoreTags)
        {
            if (string.Equals(target.tag, ignoreTag, System.StringComparison.OrdinalIgnoreCase))
            {
                if (enableCollisionLogging)
                {
                    Debug.Log($"[타겟 필터링] {gameObject.name}: {target.gameObject.name}는 무시할 태그({ignoreTag})입니다.");
                }
                return false;
            }
        }
        
        // 타겟 태그 확인
        bool hasValidTag = false;
        foreach (string targetTag in attackCollisionTags)
        {
            if (string.Equals(target.tag, targetTag, System.StringComparison.OrdinalIgnoreCase))
            {
                hasValidTag = true;
                break;
            }
        }
        
        if (!hasValidTag) 
        {
            if (enableCollisionLogging)
            {
                Debug.Log($"[타겟 필터링] {gameObject.name}: {target.gameObject.name}는 유효한 타겟 태그가 아닙니다. (현재 태그: {target.tag})");
            }
            return false;
        }
        
        // Body 콜리전만 타격할지 확인
        if (onlyHitBodyCollision && !target.gameObject.name.Contains("Body"))
        {
            if (enableCollisionLogging)
            {
                Debug.Log($"[타겟 필터링] {gameObject.name}: {target.gameObject.name}는 Body 콜리전이 아닙니다. (onlyHitBodyCollision: {onlyHitBodyCollision})");
            }
            return false;
        }
        
        if (enableCollisionLogging)
        {
            Debug.Log($"[타겟 필터링] {gameObject.name}: {target.gameObject.name}는 유효한 타겟입니다.");
        }
        return true;
    }
    
    #endregion
    
    #region Gizmos
    
    /// <summary>
    /// Scene 뷰에서 콜리전 범위를 표시합니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Body 콜리전 표시
        if (bodyCollider != null)
        {
            Gizmos.color = Color.red;
            DrawColliderGizmo(bodyCollider);
        }
        
        // Attack 콜리전 표시
        if (attackCollider != null)
        {
            Gizmos.color = Color.yellow;
            DrawColliderGizmo(attackCollider);
        }
        
        // Interaction 콜리전 표시
        if (interactionCollider != null)
        {
            Gizmos.color = Color.blue;
            DrawColliderGizmo(interactionCollider);
        }
    }
    
    /// <summary>
    /// 콜리전의 Gizmo를 그립니다.
    /// </summary>
    /// <param name="collider">Gizmo를 그릴 콜리전</param>
    private void DrawColliderGizmo(Collider2D collider)
    {
        if (collider is BoxCollider2D boxCollider)
        {
            Vector3 size = new Vector3(boxCollider.size.x, boxCollider.size.y, 0.1f);
            Vector3 center = transform.position + (Vector3)boxCollider.offset;
            Gizmos.DrawWireCube(center, size);
        }
        else if (collider is CircleCollider2D circleCollider)
        {
            Vector3 center = transform.position + (Vector3)circleCollider.offset;
            Gizmos.DrawWireSphere(center, circleCollider.radius);
        }
    }
    
    #endregion
    
    /// <summary>
    /// 오브젝트가 파괴될 때 메모리 정리
    /// </summary>
    private void OnDestroy()
    {
        // HashSet과 List 정리
        if (hitTargets != null)
        {
            hitTargets.Clear();
        }
        
        if (tempColliderList != null)
        {
            tempColliderList.Clear();
        }
        
        // 참조 해제
        collisionHandler = null;
        bodyCollider = null;
        attackCollider = null;
        interactionCollider = null;
        
    }
}
