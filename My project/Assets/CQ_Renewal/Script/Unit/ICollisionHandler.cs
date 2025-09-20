using UnityEngine;

/// <summary>
/// 캐릭터의 콜리전 이벤트를 처리하는 인터페이스
/// </summary>
public interface ICollisionHandler
{
    void OnBodyCollision(Collider2D other);  // Body 콜리전 이벤트 처리 (적/플레이어 충돌, 피격 판정)

    void OnAttackRangeEnter(Collider2D other);  // AttackRange 콜리전 이벤트 처리 (공격 범위 진입)
    
    void OnAttackRangeExit(Collider2D other);  // AttackRange 콜리전에서 나갔을 때 처리 (공격 범위 벗어남)
    
    void OnAttackHit(Collider2D other);  // 애니메이션 이벤트에서 호출되는 공격 성공 판정
}
