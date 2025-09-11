using UnityEngine;

/// <summary>
/// 스폰 포인트를 관리하는 컴포넌트
/// 스폰 위치와 관련 정보를 제공합니다.
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private bool isActive = true; // 스폰 포인트 활성화 여부
    [SerializeField] private float spawnRadius = 1f; // 스폰 반경 (랜덤 스폰용)
    [SerializeField] private bool useRandomOffset = false; // 랜덤 오프셋 사용 여부
    
    [Header("Visual Settings")]
    [SerializeField] private bool showGizmos = true; // 기즈모 표시 여부
    [SerializeField] private Color gizmoColor = Color.green; // 기즈모 색상
    
    /// <summary>
    /// 스폰 포인트가 활성화되어 있는지 확인합니다.
    /// </summary>
    public bool IsActive()
    {
        return isActive;
    }
    
    /// <summary>
    /// 스폰 포인트를 활성화/비활성화합니다.
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
    }
    
    /// <summary>
    /// 스폰 위치를 반환합니다. (랜덤 오프셋 적용)
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        if (useRandomOffset)
        {
            // 반경 내 랜덤 위치 생성
            Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
            return transform.position + new Vector3(randomOffset.x, randomOffset.y, 0);
        }
        else
        {
            // 정확한 포지션 사용
            return transform.position;
        }
    }
    
    /// <summary>
    /// 스폰 반경을 설정합니다.
    /// </summary>
    public void SetSpawnRadius(float radius)
    {
        spawnRadius = radius;
    }
    
    /// <summary>
    /// 랜덤 오프셋 사용 여부를 설정합니다.
    /// </summary>
    public void SetUseRandomOffset(bool use)
    {
        useRandomOffset = use;
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        // 스폰 포인트 위치 표시
        Gizmos.color = isActive ? gizmoColor : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // 스폰 반경 표시
        if (useRandomOffset)
        {
            Gizmos.color = gizmoColor * 0.5f;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
        
        // 스폰 포인트 이름 표시
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1f, gameObject.name);
        #endif
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        // 선택된 스폰 포인트 강조 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.7f);
        
        if (useRandomOffset)
        {
            Gizmos.color = Color.yellow * 0.3f;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }
}
