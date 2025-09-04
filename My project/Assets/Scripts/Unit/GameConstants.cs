using UnityEngine;

/// <summary>
/// 게임 전반에서 사용되는 상수들을 정의하는 클래스
/// </summary>
public static class GameConstants
{
    #region Animation Parameters
    public static readonly string ANIM_IS_MOVING = "IsMoving";
    public static readonly string ANIM_IS_RUNNING = "IsRunning";
    public static readonly string ANIM_ATTACK = "Attack";
    public static readonly string ANIM_CEREMONY = "Ceremony";
    public static readonly string ANIM_BLANK = "Blank";
    public static readonly string ANIM_DEATH = "Death";
    #endregion

    #region Tags
    public static readonly string TAG_ENEMY = "Enemy";
    public static readonly string TAG_PLAYER = "Player";
    public static readonly string TAG_NPC = "NPC";  // 아직 미사용
    public static readonly string TAG_ITEM = "Item";
    public static readonly string TAG_OBSTACLE = "Obstacle";
    public static readonly string TAG_DESTRUCTIBLE = "Destructible";  // 아직 미사용
    public static readonly string TAG_INTERACTION = "Interaction";
    public static readonly string TAG_INTERACTABLE = "Interactable";  // 아직 미사용
    #endregion

    #region Effect System
    public const int EFFECT_POOL_SIZE = 5;  // 이펙트 풀 크기
    public const int MAX_DAMAGE_EFFECTS = 5;  // 최대 피격 이펙트 개수
    public const float DAMAGE_EFFECT_COOLDOWN = 0.1f;  // 피격 이펙트 쿨다운
    #endregion

    #region Attack System
    public const float DEFAULT_ATTACK_DURATION = 0.2f;  // 공격 지속 시간
    public const float DEFAULT_ATTACK_COOLDOWN = 0.1f;  // 공격 쿨다운
    #endregion

    #region Movement
    public const float DEFAULT_MOVE_SPEED = 1f;  // 이동 속도
    public const float DEFAULT_RUN_SPEED_MULTIPLIER = 2f;  // 달리기 속도 배수
    #endregion

    #region Sorting
    public const int DEFAULT_BASE_SORTING_ORDER = 0;  // 기본 정렬 순서
    public const float DEFAULT_SORTING_ORDER_MULTIPLIER = -10f;  // Y축에 따라 SortOrder 배수 정밀도 (값이 낮을수록 정밀도 증가)
    #endregion
}
