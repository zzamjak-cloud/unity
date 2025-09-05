using UnityEngine;

/// <summary>
/// 캐릭터의 콜리전 이벤트를 처리하는 인터페이스
/// </summary>
public interface ICollisionHandler
{
    void OnBodyCollision(Collider2D other);  // Body 콜리전 이벤트 처리 (적/플레이어 충돌, 피격 판정)

    void OnAttackCollision(Collider2D other);  // Attack 콜리전 이벤트 처리 (공격 범위 감지, 타격 판정)

    void OnInteractionCollision(Collider2D other);  // Interaction 콜리전 이벤트 처리 (아이템/오브젝트 상호작용)
    
    void OnDetectionCollision(Collider2D other);  // Detection 콜리전 이벤트 처리 (적 감지용, 상시 활성화)
}
