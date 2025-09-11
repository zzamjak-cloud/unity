using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 캐릭터의 2개 콜리전을 관리하는 컴포넌트
/// Body, AttackRange 콜리전을 각각 처리합니다.
/// </summary>
public class CharacterCollisionManager : MonoBehaviour
{
    [Header("Collision Settings")]
    [SerializeField] private Collider2D bodyCollider;        // Body 콜리전 (적/플레이어 충돌, 피격 판정)
    [SerializeField] private Collider2D attackRangeCollider; // AttackRange 콜리전 (공격 범위 감지, 상시 활성화)
    
    [Header("Collision Layers")]
    [SerializeField] private LayerMask bodyCollisionMask = -1;        // Body 콜리전 대상 레이어
    [SerializeField] private LayerMask attackRangeCollisionMask = -1; // AttackRange 콜리전 대상 레이어
    
    [Header("Collision Tags")]
    [SerializeField] private string[] bodyCollisionTags = { GameConstants.TAG_ENEMY, GameConstants.TAG_PLAYER, GameConstants.TAG_OBSTACLE };
    [SerializeField] private string[] attackRangeCollisionTags = { GameConstants.TAG_ENEMY, GameConstants.TAG_PLAYER }; // AttackRange는 공격 대상 감지용
    
    [Header("Attack Range Settings")]
    [SerializeField] private string[] attackRangeIgnoreTags = { "Item" };
    [SerializeField] private bool onlyDetectBodyCollision = true;
    
    // 콜리전 이벤트를 처리할 핸들러
    private ICollisionHandler collisionHandler;
    
    // 콜리전 활성화 상태
    private bool isBodyCollisionEnabled = true;
    private bool isAttackRangeCollisionEnabled = true;
    
    // AttackRange 콜리전 관련 - 공격 범위 내 적들 관리
    private List<Collider2D> enemiesInRange = new List<Collider2D>();
    private Collider2D nearestEnemy = null;
    
    // 메모리 최적화를 위한 캐시된 리스트
    private List<Collider2D> tempColliderList = new List<Collider2D>();
    
    // 메모리 할당 최적화를 위한 캐시된 변수들
    private Vector3 cachedPosition;
    private Vector3 cachedScale;
    
    // 콜리전 이벤트 로깅
    [Header("Debug")]
    [SerializeField] private bool enableCollisionLogging = false;  // 기본값을 false로 변경하여 성능 최적화
    
    private void Awake()
    {
        // ICollisionHandler 인터페이스를 구현한 컴포넌트 찾기
        collisionHandler = GetComponent<ICollisionHandler>();
        
        if (collisionHandler == null)
        {
        }
        
        // 콜리전 컴포넌트 검증
        ValidateColliders();
        
        // 캐시된 변수들 초기화
        cachedPosition = Vector3.zero;
        cachedScale = Vector3.one;
    }
    
    private void Start()
    {
        // 콜리전 초기 설정
        SetupColliders();
    }
    
    private void Update()
    {
        // AttackRange 내 가장 가까운 적 업데이트
        UpdateNearestEnemy();
    }
    
    /// <summary>
    /// 콜리전 컴포넌트들을 검증합니다.
    /// </summary>
    private void ValidateColliders()
    {
        if (bodyCollider == null)
        {
        }
        
        if (attackRangeCollider == null)
        {
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
        
        // AttackRange 콜리전 설정
        if (attackRangeCollider != null)
        {
            attackRangeCollider.isTrigger = true; // 트리거로 설정
            SetupCollider(attackRangeCollider, attackRangeCollisionMask);
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
        
        if (!isBodyCollisionEnabled || bodyCollider == null) 
        {
            if (enableCollisionLogging)
            {
            }
            return;
        }
        
        // Body 콜리전과의 충돌인지 확인
        if (collision.collider == bodyCollider)
        {
            if (enableCollisionLogging)
            {
            }
            HandleBodyCollision(collision.collider);
        }
        else
        {
            if (enableCollisionLogging)
            {
            }
        }
    }
    
    /// <summary>
    /// AttackRange 콜리전 이벤트 (트리거)
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        
        // Body 콜리전은 OnCollisionEnter2D에서 처리하므로 제외
        if (bodyCollider != null && other == bodyCollider)
        {
            if (enableCollisionLogging)
            {
            }
            return;
        }
        
        // AttackRange 콜리전 처리 - 다른 오브젝트가 이 오브젝트의 AttackRange 콜리전과 충돌했을 때
        if (attackRangeCollider != null && other != attackRangeCollider && isAttackRangeCollisionEnabled)
        {
            // AttackRange 콜리전끼리의 충돌은 무시
            CharacterCollisionManager otherCollisionManager = other.GetComponentInParent<CharacterCollisionManager>();
            if (otherCollisionManager != null && otherCollisionManager.IsAttackRangeCollider(other))
            {
                if (enableCollisionLogging)
                {
                }
                return;
            }
            
            // 레이어 마스크 확인
            if (((1 << other.gameObject.layer) & attackRangeCollisionMask) != 0)
            {
                if (enableCollisionLogging)
                {
                }
                HandleAttackRangeCollision(other);
            }
            else
            {
                if (enableCollisionLogging)
                {
                }
            }
        }
    }
    
    /// <summary>
    /// 트리거에서 나갔을 때 호출
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        
        // AttackRange 콜리전 처리 - 다른 오브젝트가 이 오브젝트의 AttackRange 콜리전에서 나갔을 때
        if (attackRangeCollider != null && other != attackRangeCollider && isAttackRangeCollisionEnabled)
        {
            // 레이어 마스크 확인
            if (((1 << other.gameObject.layer) & attackRangeCollisionMask) != 0)
            {
                HandleAttackRangeExit(other);
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
        
    }
    
    /// <summary>
    /// AttackRange 콜리전을 처리합니다.
    /// </summary>
    /// <param name="other">충돌한 콜리전</param>
    private void HandleAttackRangeCollision(Collider2D other)
    {
        // 타겟 유효성 확인
        if (!IsValidAttackRangeTarget(other))
        {
            if (enableCollisionLogging)
            {
            }
            return;
        }
        
        // 이미 리스트에 있는지 확인
        if (!enemiesInRange.Contains(other))
        {
            enemiesInRange.Add(other);
            
            // 콜리전 핸들러에 이벤트 전달
            if (collisionHandler != null)
            {
                collisionHandler.OnAttackRangeEnter(other);
            }
        }
    }
    
    
    /// <summary>
    /// AttackRange 콜리전에서 나갔을 때 처리합니다.
    /// </summary>
    /// <param name="other">나간 콜리전</param>
    private void HandleAttackRangeExit(Collider2D other)
    {
        // 리스트에서 제거
        if (enemiesInRange.Contains(other))
        {
            enemiesInRange.Remove(other);
            
            // 콜리전 핸들러에 이벤트 전달
            if (collisionHandler != null)
            {
                collisionHandler.OnAttackRangeExit(other);
            }
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
    /// AttackRange 콜리전을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetAttackRangeCollisionEnabled(bool enabled)
    {
        isAttackRangeCollisionEnabled = enabled;
        if (attackRangeCollider != null)
        {
            attackRangeCollider.enabled = enabled;
        }
    }
    
    
    
    /// <summary>
    /// 모든 콜리전을 활성화/비활성화합니다.
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetAllCollisionsEnabled(bool enabled)
    {
        SetBodyCollisionEnabled(enabled);
        SetAttackRangeCollisionEnabled(enabled);
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
            case CollisionType.AttackRange:
                return isAttackRangeCollisionEnabled;
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
    /// 주어진 콜리전이 이 캐릭터의 AttackRange 콜리전인지 확인합니다.
    /// </summary>
    /// <param name="collider">확인할 콜리전</param>
    /// <returns>AttackRange 콜리전이면 true</returns>
    public bool IsAttackRangeCollider(Collider2D collider)
    {
        return attackRangeCollider == collider;
    }
    
    /// <summary>
    /// AttackRange 타겟이 유효한지 확인합니다.
    /// </summary>
    private bool IsValidAttackRangeTarget(Collider2D target)
    {
        if (target == null) 
        {
            if (enableCollisionLogging)
            {
            }
            return false;
        }
        
        // 무시할 태그 확인
        foreach (string ignoreTag in attackRangeIgnoreTags)
        {
            if (string.Equals(target.tag, ignoreTag, System.StringComparison.OrdinalIgnoreCase))
            {
                if (enableCollisionLogging)
                {
                }
                return false;
            }
        }
        
        // 타겟 태그 확인
        bool hasValidTag = false;
        foreach (string targetTag in attackRangeCollisionTags)
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
            }
            return false;
        }
        
        // Body 콜리전만 감지할지 확인
        if (onlyDetectBodyCollision && !target.gameObject.name.Contains("Body"))
        {
            if (enableCollisionLogging)
            {
            }
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// AttackRange 내 가장 가까운 적을 업데이트합니다.
    /// </summary>
    private void UpdateNearestEnemy()
    {
        if (enemiesInRange.Count == 0)
        {
            nearestEnemy = null;
            return;
        }
        
        float nearestDistance = float.MaxValue;
        Collider2D newNearestEnemy = null;
        
        foreach (Collider2D enemy in enemiesInRange)
        {
            if (enemy == null) continue;
            
            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                newNearestEnemy = enemy;
            }
        }
        
        nearestEnemy = newNearestEnemy;
    }
    
    /// <summary>
    /// AttackRange 내 적 목록을 반환합니다.
    /// </summary>
    /// <returns>AttackRange 내 적 목록</returns>
    public List<Collider2D> GetEnemiesInRange()
    {
        return new List<Collider2D>(enemiesInRange);
    }
    
    /// <summary>
    /// AttackRange 내 가장 가까운 적을 반환합니다.
    /// </summary>
    /// <returns>가장 가까운 적, 없으면 null</returns>
    public Collider2D GetNearestEnemy()
    {
        return nearestEnemy;
    }
    
    /// <summary>
    /// AttackRange 내 적이 있는지 확인합니다.
    /// </summary>
    /// <returns>적이 있으면 true</returns>
    public bool HasEnemiesInRange()
    {
        return enemiesInRange.Count > 0;
    }
    
    /// <summary>
    /// AttackRange 내 적의 수를 반환합니다.
    /// </summary>
    /// <returns>적의 수</returns>
    public int GetEnemyCountInRange()
    {
        return enemiesInRange.Count;
    }
    
    /// <summary>
    /// 애니메이션 이벤트에서 호출되는 공격 성공 판정 메서드
    /// </summary>
    public void OnAttackAnimationEvent()
    {
        if (nearestEnemy != null)
        {
            // 콜리전 핸들러에 공격 성공 이벤트 전달
            if (collisionHandler != null)
            {
                collisionHandler.OnAttackHit(nearestEnemy);
            }
            else
            {
                Debug.LogWarning("[AttackAnimationEvent] collisionHandler가 null입니다!");
            }
        }
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
        
        // AttackRange 콜리전 표시
        if (attackRangeCollider != null)
        {
            Gizmos.color = Color.green;
            DrawColliderGizmo(attackRangeCollider);
        }
        
        // 가장 가까운 적 표시
        if (nearestEnemy != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, nearestEnemy.transform.position);
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
        // List 정리
        if (enemiesInRange != null)
        {
            enemiesInRange.Clear();
        }
        
        if (tempColliderList != null)
        {
            tempColliderList.Clear();
        }
        
        // 참조 해제
        collisionHandler = null;
        bodyCollider = null;
        attackRangeCollider = null;
        nearestEnemy = null;
        
        // 캐시된 변수들 정리
        cachedPosition = Vector3.zero;
        cachedScale = Vector3.zero;
    }
    
    /// <summary>
    /// AttackRange 콜라이더의 공격 범위를 가져옵니다.
    /// </summary>
    /// <returns>공격 범위 (반지름)</returns>
    public float GetAttackRange()
    {
        if (attackRangeCollider == null) return 2.0f; // 기본 범위
        
        CircleCollider2D attackRangeCircle = attackRangeCollider as CircleCollider2D;
        if (attackRangeCircle != null)
        {
            return attackRangeCircle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        }
        
        return 2.0f; // 기본 범위
    }
    
    /// <summary>
    /// AttackRange 콜라이더가 활성화되어 있는지 확인합니다.
    /// </summary>
    /// <returns>활성화되어 있으면 true</returns>
    public bool IsAttackRangeColliderEnabled()
    {
        return attackRangeCollider != null && attackRangeCollider.enabled;
    }
    
    /// <summary>
    /// AttackRange 콜라이더가 설정되어 있는지 확인합니다.
    /// </summary>
    /// <returns>설정되어 있으면 true</returns>
    public bool HasAttackRangeCollider()
    {
        return attackRangeCollider != null;
    }
}
