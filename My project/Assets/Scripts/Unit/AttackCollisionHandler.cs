using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attack 콜리전 GameObject의 콜리전 이벤트를 처리하는 스크립트
/// </summary>
public class AttackCollisionHandler : MonoBehaviour
{
    [Header("Attack Collision Settings")]
    [SerializeField] private float attackDuration = 0.5f;  // 공격 지속 시간
    [SerializeField] private LayerMask targetLayers = -1;  // 타격 대상 레이어
    [SerializeField] private bool allowMultipleHitsPerTarget = false;  // 동일 대상에 대한 연속 타격 허용 여부
    
    private CharacterBase owner;  // 이 콜리전을 소유한 캐릭터
    private float attackTimer = 0f;  // 공격 타이머
    private bool isAttackActive = false;  // 공격 활성화 상태
    private Collider2D attackCollider;  // Attack 콜리전 컴포넌트
    private HashSet<Collider2D> hitTargets = new HashSet<Collider2D>();  // 이번 공격에서 이미 타격한 대상들
    
    private void Awake()
    {
        // 부모에서 CharacterBase 컴포넌트 찾기
        owner = GetComponentInParent<CharacterBase>();
        if (owner == null)
        {
            Debug.LogError($"{gameObject.name}: AttackCollisionHandler - 부모에서 CharacterBase를 찾을 수 없습니다.");
        }
        
        // Attack 콜리전 컴포넌트 찾기
        attackCollider = GetComponent<Collider2D>();
        if (attackCollider == null)
        {
            Debug.LogError($"{gameObject.name}: AttackCollisionHandler - Collider2D 컴포넌트를 찾을 수 없습니다.");
        }
    }
    
    private void Start()
    {
        // 시작 시 콜리전 비활성화
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }
    }
    
    private void Update()
    {
        if (isAttackActive)
        {
            // 공격 타이머 업데이트
            attackTimer -= Time.deltaTime;
            
            // 공격 시간이 끝나면 자동으로 비활성화
            if (attackTimer <= 0f)
            {
                DeactivateAttack();
            }
        }
    }
    
    /// <summary>
    /// 공격 콜리전을 활성화합니다.
    /// </summary>
    public void ActivateAttack()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = true;
        }
        isAttackActive = true;
        attackTimer = attackDuration;
        
        // 이번 공격에서 타격한 대상 목록 초기화
        hitTargets.Clear();
        
        Debug.Log($"[공격 콜리전] {gameObject.name}: Attack 콜리전 활성화 - {attackDuration}초 동안 타격 판정 시작");
    }
    
    /// <summary>
    /// 공격 콜리전을 비활성화합니다.
    /// </summary>
    public void DeactivateAttack()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }
        isAttackActive = false;
        attackTimer = 0f;
        
        // 타격한 대상 목록 초기화
        hitTargets.Clear();
        
        Debug.Log($"[공격 콜리전] {gameObject.name}: Attack 콜리전 비활성화 - 타격 판정 종료");
    }
    
    /// <summary>
    /// 공격이 활성화되어 있는지 확인합니다.
    /// </summary>
    /// <returns>공격이 활성화되어 있으면 true</returns>
    public bool IsAttackActive()
    {
        return isAttackActive && attackCollider != null && attackCollider.enabled;
    }
    
    /// <summary>
    /// 트리거 콜리전 이벤트 처리
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAttackActive || owner == null) return;
        
        Debug.Log($"[콜리전 디버그] {gameObject.name}: {other.gameObject.name}와 콜리전 발생 (공격 활성화: {isAttackActive})");
        
        // 타겟 레이어 확인
        if (((1 << other.gameObject.layer) & targetLayers) == 0)
        {
            Debug.Log($"[콜리전 디버그] {gameObject.name}: 레이어 불일치 - 대상 레이어: {other.gameObject.layer}, 허용 레이어: {targetLayers}");
            return;
        }
        
        // 적이나 플레이어인지 확인
        if (other.CompareTag("Enemy") || other.CompareTag("Player"))
        {
            // 연속 타격 허용 여부에 따른 중복 타격 방지
            if (!allowMultipleHitsPerTarget && hitTargets.Contains(other))
            {
                Debug.Log($"[콜리전 디버그] {gameObject.name}: {other.gameObject.name}는 이미 이번 공격에서 타격했습니다. 중복 타격 방지.");
                return;
            }
            
            // 타격한 대상 목록에 추가 (연속 타격 허용 시에도 기록)
            hitTargets.Add(other);
            
            Debug.Log($"[타격 감지] {gameObject.name}: {other.gameObject.name}와 Attack 콜리전 발생 - 타격 판정 시작");
            Debug.Log($"[콜리전 디버그] {gameObject.name}: 위치 - 나: {transform.position}, 대상: {other.transform.position}");
            
            // 소유자에게 Attack 콜리전 이벤트 전달
            owner.OnAttackCollision(other);
        }
        else
        {
            Debug.Log($"[콜리전 디버그] {gameObject.name}: 태그 불일치 - 대상 태그: {other.tag}");
        }
    }
    
    /// <summary>
    /// 공격 지속 시간을 설정합니다.
    /// </summary>
    /// <param name="duration">새로운 공격 지속 시간</param>
    public void SetAttackDuration(float duration)
    {
        attackDuration = Mathf.Max(0.1f, duration);
    }
    
    /// <summary>
    /// 타겟 레이어를 설정합니다.
    /// </summary>
    /// <param name="layers">새로운 타겟 레이어</param>
    public void SetTargetLayers(LayerMask layers)
    {
        targetLayers = layers;
    }
    
    /// <summary>
    /// Scene 뷰에서 콜리전 범위를 시각화합니다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (isAttackActive)
        {
            // 공격 중일 때는 노란색으로 표시
            Gizmos.color = Color.yellow;
        }
        else
        {
            // 비활성화 상태일 때는 회색으로 표시
            Gizmos.color = Color.gray;
        }
        
        // 콜리전 범위 표시
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            Vector3 center = transform.position + (Vector3)boxCollider.offset;
            Vector3 size = new Vector3(boxCollider.size.x, boxCollider.size.y, 0.1f);
            
            // Z축 위치를 0으로 고정하여 2D 평면에 표시
            center.z = 0;
            Gizmos.DrawWireCube(center, size);
            
            // 콜리전 활성화 상태 표시
            if (isAttackActive)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawCube(center, size);
            }
        }
    }
}
