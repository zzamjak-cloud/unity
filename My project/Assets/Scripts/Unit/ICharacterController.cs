using UnityEngine;

/// <summary>
/// 캐릭터의 기본 동작을 정의하는 인터페이스
/// 플레이어와 적 캐릭터 모두 이 인터페이스를 구현하여 동일한 동작을 보장합니다.
/// </summary>
public interface ICharacterController
{
    void UpdateMovement();  // 캐릭터의 이동을 업데이트합니다. 매 프레임마다 호출되어야 합니다.

    void UpdateAnimation();  // 캐릭터의 애니메이션을 업데이트합니다. 매 프레임마다 호출되어야 합니다.

    Vector2 GetMovementInput();  // 현재 이동 입력 값을 반환합니다. 플레이어는 키보드 입력, 적은 AI 계산 결과를 반환합니다. <returns>이동 방향 벡터 (정규화됨)</returns>

    bool IsRunning();  // 현재 달리기 상태를 반환합니다. <returns>달리기 중이면 true</returns>

    void TriggerSpecialAnimation(CharacterAnimationState animationType);  // 특수 애니메이션을 트리거합니다.

    bool CanMove();  // 캐릭터가 이동 가능한 상태인지 확인합니다. <returns>이동 가능하면 true</returns>

    CharacterAnimationState GetCurrentAnimationState();  // 현재 애니메이션 상태를 반환합니다. <returns>현재 애니메이션 상태</returns>

    bool IsInAnimationState(CharacterAnimationState state);  // 특정 애니메이션 상태인지 확인합니다. <returns>해당 상태이면 true</returns>
}
