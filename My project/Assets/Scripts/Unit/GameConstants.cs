using UnityEngine;

/// <summary>
/// 게임 전반에서 사용되는 상수들을 정의하는 클래스
/// </summary>
public static class GameConstants
{
    #region Animation Parameters
    public static readonly string ANIM_IS_WALKING = "IsWalking";
    public static readonly string ANIM_IS_RUNNING = "IsRunning";
    public static readonly string ANIM_IS_ATTACKING = "IsAttacking";
    public static readonly string ANIM_CEREMONY = "Ceremony";
    public static readonly string ANIM_BLANK = "Blank";
    public static readonly string ANIM_DEATH = "Death";
    #endregion

    #region Animation States
    public static readonly string ANIM_STATE_IDLE = "Idle";
    public static readonly string ANIM_STATE_WALK = "Walk";
    public static readonly string ANIM_STATE_RUN = "Run";
    public static readonly string ANIM_STATE_ATTACK = "Attack";
    public static readonly string ANIM_STATE_CEREMONY = "Ceremony";
    public static readonly string ANIM_STATE_BLANK = "Blank";
    public static readonly string ANIM_STATE_DEATH = "Death";
    #endregion

    #region Tags
    public static readonly string TAG_ENEMY = "Enemy";
    public static readonly string TAG_PLAYER = "Player";
    public static readonly string TAG_ITEM = "Item";
    public static readonly string TAG_OBSTACLE = "Obstacle";
    #endregion

    #region Effect System
    public const int EFFECT_POOL_SIZE = 5;  // 이펙트 풀 크기
    public const int MAX_DAMAGE_EFFECTS = 5;  // 최대 피격 이펙트 개수
    public const float DAMAGE_EFFECT_COOLDOWN = 0.1f;  // 피격 이펙트 쿨다운
    #endregion

    #region Attack System
    public const float DEFAULT_ATTACK_COOLDOWN = 0.2f;  // 공격 애니메이션 후 추가 대기 시간
    #endregion

    #region Character Stats
    public const float PLAYER_INVINCIBILITY_DURATION = 1.0f;  // 플레이어 무적 시간
    public const float ENEMY_INVINCIBILITY_DURATION = 0.5f;  // 적 무적 시간
    #endregion

    #region Movement
    public const float DEFAULT_MOVE_SPEED = 1f;  // 이동 속도
    public const float DEFAULT_RUN_SPEED_MULTIPLIER = 2f;  // 달리기 속도 배수
    #endregion

    #region Sorting
    public const int DEFAULT_BASE_SORTING_ORDER = 0;  // 기본 정렬 순서
    public const float DEFAULT_SORTING_ORDER_MULTIPLIER = -10f;  // Y축에 따라 SortOrder 배수 정밀도 (값이 낮을수록 정밀도 증가)
    #endregion

    #region Movement Thresholds
    public const float MOVEMENT_THRESHOLD = 0.1f;  // 이동 판정 임계값
    public const float POSITION_THRESHOLD = 0.1f;  // 위치 비교 임계값
    #endregion

    #region Attack System Multipliers
    public const float INVINCIBLE_COOLDOWN_MULTIPLIER = 0.3f;  // 무적 상태 공격 쿨다운 배수
    public const float BLANK_COOLDOWN_MULTIPLIER = 0.2f;  // Blank 상태 공격 쿨다운 배수
    public const float BLANK_QUICK_COOLDOWN_MULTIPLIER = 0.1f;  // Blank 상태 빠른 공격 쿨다운 배수
    public const float RETURN_SPEED_MULTIPLIER = 0.3f;  // 복귀 시 이동 속도 배수
    #endregion

    #region Animation Timing
    public const float ANIMATION_EVENT_COOLDOWN = 0.1f;  // 애니메이션 이벤트 중복 호출 방지 시간
    public const float ANIMATION_COMPLETE_THRESHOLD = 1.0f;  // 애니메이션 완료 판정 임계값
    #endregion

    #region Random Animation
    public const float RANDOM_ANIMATION_CHANCE = 0.1f;  // 랜덤 애니메이션 확률
    public const float RANDOM_ANIMATION_ATTACK_CHANCE = 0.3f;  // 랜덤 공격 애니메이션 확률
    public const float RANDOM_ANIMATION_BLANK_CHANCE = 0.6f;  // 랜덤 Blank 애니메이션 확률
    public const float RANDOM_ANIMATION_CEREMONY_CHANCE = 0.8f;  // 랜덤 Ceremony 애니메이션 확률
    #endregion

    #region Enemy Settings
    public const float ENEMY_MOVE_SPEED = 1.5f;  // 적 기본 이동 속도
    public const float ENEMY_CHASE_DELAY = 1.5f;  // 적 추적 지연 시간
    public const float ENEMY_AUTO_ATTACK_COOLDOWN = 1.5f;  // 적 자동 공격 쿨다운
    public const float ENEMY_ATTACK_DELAY = 0.1f;  // 적 공격 지연 시간
    public const float ENEMY_STATUS_BAR_OFFSET_Y = 1.5f;  // 적 상태바 Y 오프셋
    #endregion
}
