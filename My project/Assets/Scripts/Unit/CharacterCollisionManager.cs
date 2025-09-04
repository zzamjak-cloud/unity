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
    [SerializeField] private string[] bodyCollisionTags = { "Enemy", "Player", "Obstacle" };
    [SerializeField] private string[] attackCollisionTags = { "Enemy", "Player", "Destructible" };
    [SerializeField] private string[] interactionCollisionTags = { "Item", "Interactable", "NPC" };
    
    // 콜리전 이벤트를 처리할 핸들러
    private ICollisionHandler collisionHandler;
    
    // 콜리전 활성화 상태
    private bool isBodyCollisionEnabled = true;
    private bool isAttackCollisionEnabled = true;
    private bool isInteractionCollisionEnabled = true;
    
    // 콜리전 이벤트 로깅
    [Header("Debug")]
    [SerializeField] private bool enableCollisionLogging = true;
    
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
        if (!isBodyCollisionEnabled || bodyCollider == null) return;
        
        // Body 콜리전과의 충돌인지 확인
        if (collision.collider == bodyCollider)
        {
            HandleBodyCollision(collision.collider);
        }
    }
    
    /// <summary>
    /// Attack 콜리전 이벤트 (트리거)
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (attackCollider != null && other == attackCollider)
        {
            if (isAttackCollisionEnabled)
            {
                HandleAttackCollision(other);
            }
        }
        else if (interactionCollider != null && other == interactionCollider)
        {
            if (isInteractionCollisionEnabled)
            {
                HandleInteractionCollision(other);
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
        if (collisionHandler != null)
        {
            collisionHandler.OnAttackCollision(other);
        }
        
        if (enableCollisionLogging)
        {
            Debug.Log($"Attack 콜리전: {gameObject.name} -> {other.gameObject.name}");
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
        if (attackCollider != null)
        {
            attackCollider.enabled = enabled;
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
}
