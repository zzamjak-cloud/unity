/// <summary>
/// 캐릭터의 콜리전 타입을 정의하는 열거형
/// </summary>
public enum CollisionType
{
    Body,           // 적/플레이어 충돌, 피격 판정
    Attack,         // 공격 범위 감지, 타격 판정
    Interaction     // 감지용, 상시 활성화
}
