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
    public static readonly string TAG_OBSTACLE = "Obstacle";
    public static readonly string TAG_DESTRUCTIBLE = "Destructible";
    public static readonly string TAG_ITEM = "Item";
    public static readonly string TAG_INTERACTABLE = "Interactable";
    public static readonly string TAG_NPC = "NPC";
    public static readonly string TAG_INTERACTION = "Interaction";
    #endregion

    #region Effect System
    public const int EFFECT_POOL_SIZE = 5;
    public const int MAX_DAMAGE_EFFECTS = 5;
    public const float DAMAGE_EFFECT_COOLDOWN = 0.1f;
    #endregion

    #region Attack System
    public const float DEFAULT_ATTACK_DURATION = 0.2f;
    public const float DEFAULT_ATTACK_COOLDOWN = 0.1f;
    #endregion

    #region Movement
    public const float DEFAULT_MOVE_SPEED = 5f;
    public const float DEFAULT_RUN_SPEED_MULTIPLIER = 1.5f;
    #endregion

    #region Sorting
    public const int DEFAULT_BASE_SORTING_ORDER = 0;
    public const float DEFAULT_SORTING_ORDER_MULTIPLIER = -1f;
    #endregion
}
