using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 캐릭터의 기본 기능을 제공하는 추상 클래스
/// 플레이어와 적 캐릭터 모두 이 클래스를 상속받아 공통 기능을 사용합니다.
/// </summary>
public abstract class CharacterBase : MonoBehaviour, ICharacterController, IAttackEffect, IMoveEffect, IBlankEffect
{
    [Header("Movement Settings")]
    [SerializeField] protected float moveSpeed = 5f;  // 이동 속도
    [SerializeField] protected float runSpeedMultiplier = 1.5f;  // 달리기 속도 배수
    [SerializeField] [Range(0.01f, 0.1f)] protected float zDepthWeight = 0.05f; // Z축 깊이 조절을 위한 가중치

    [System.Serializable]
    public struct EffectData
    {
        public Transform effectContainer;  // 이펙트를 담을 컨테이너 Transform
        public GameObject effectPrefab;  // 이펙트 프리팹
    }
    
    [Header("Effects")]
    [SerializeField] protected EffectData moveEffectData;  // 이동 이펙트 데이터
    [SerializeField] protected EffectData attackEffectData;  // 공격 이펙트 데이터
    [SerializeField] protected EffectData blankEffectData;  // Blank 이펙트 데이터
    
    // 활성화된 이펙트 인스턴스들을 추적
    protected ParticleSystem activeMoveEffect;  // 이동 이펙트는 1개만 유지
    
    // 오브젝트 풀링을 위한 이펙트 풀
    protected Queue<ParticleSystem> attackEffectPool = new Queue<ParticleSystem>();
    protected Queue<ParticleSystem> blankEffectPool = new Queue<ParticleSystem>();
    protected const int EFFECT_POOL_SIZE = 3;  // 풀 크기
    
    // 활성화된 이펙트들을 추적 (풀에서 나온 이펙트들)
    protected List<ParticleSystem> activeAttackEffects = new List<ParticleSystem>();
    protected List<ParticleSystem> activeBlankEffects = new List<ParticleSystem>();

    [Header("Animation")]
    [SerializeField] protected Animator anim;  // 애니메이터 컴포넌트 (Inspector에서 직접 연결)
    
    protected Rigidbody2D rb;
    protected CharacterAnimationState currentAnimationState = CharacterAnimationState.Idle;
    
    // 애니메이션 파라미터 이름들을 상수로 정의
    protected static readonly string ANIM_IS_MOVING = "IsMoving";
    protected static readonly string ANIM_IS_RUNNING = "IsRunning";
    protected static readonly string ANIM_ATTACK = "Attack";
    protected static readonly string ANIM_CEREMONY = "Ceremony";
    protected static readonly string ANIM_BLANK = "Blank";
    protected static readonly string ANIM_DEATH = "Death";

    // 현재 이동 상태
    protected Vector2 currentMovement = Vector2.zero;
    protected bool isCurrentlyRunning = false;

    protected virtual void Awake()
    {
        // 이펙트 풀 초기화
        InitializeEffectPools();
    }

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Animator 컴포넌트가 Inspector에 연결되지 않았을 경우, 자식 오브젝트에서 찾기
        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        if (rb == null)
        {
            Debug.LogError("Rigidbody2D 컴포넌트가 필요합니다.");
        }
        if (anim == null)
        {
            Debug.LogError("Animator 컴포넌트가 필요합니다. Inspector에 연결하거나 자식 오브젝트에 추가해주세요.");
        }
    }

    protected virtual void Update()
    {
        // 하위 클래스에서 구현할 추상 메서드들 호출
        UpdateMovement();
        UpdateAnimation();
    }
    
    protected virtual void LateUpdate()
    {
        // Y축 위치에 따른 Z축 깊이 조절
        Vector3 newPosition = transform.position;
        newPosition.z = transform.position.y * zDepthWeight;
        transform.position = newPosition;
    }

    #region ICharacterController Implementation

    /// <summary>
    /// 캐릭터의 이동을 업데이트합니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    public abstract void UpdateMovement();

    /// <summary>
    /// 캐릭터의 애니메이션을 업데이트합니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    public abstract void UpdateAnimation();

    /// <summary>
    /// 현재 이동 입력 값을 반환합니다. 하위 클래스에서 구현해야 합니다.
    /// </summary>
    /// <returns>이동 방향 벡터 (정규화됨)</returns>
    public abstract Vector2 GetMovementInput();

    /// <summary>
    /// 현재 달리기 상태를 반환합니다.
    /// </summary>
    /// <returns>달리기 중이면 true</returns>
    public virtual bool IsRunning()
    {
        return isCurrentlyRunning;
    }

    /// <summary>
    /// 특수 애니메이션을 트리거합니다.
    /// </summary>
    /// <param name="animationType">애니메이션 타입</param>
    public virtual void TriggerSpecialAnimation(CharacterAnimationState animationType)
    {
        if (anim == null) return;
        
        switch (animationType)
        {
            case CharacterAnimationState.Attack:
                anim.SetTrigger(ANIM_ATTACK);
                break;
            case CharacterAnimationState.Ceremony:
                anim.SetTrigger(ANIM_CEREMONY);
                break;
            case CharacterAnimationState.Blank:
                anim.SetTrigger(ANIM_BLANK);
                break;
            case CharacterAnimationState.Death:
                anim.SetTrigger(ANIM_DEATH);
                break;
        }
    }

    /// <summary>
    /// 현재 애니메이션 상태에 따라 이동 가능 여부를 반환합니다.
    /// </summary>
    /// <returns>이동 가능하면 true</returns>
    public virtual bool CanMove()
    {
        switch (currentAnimationState)
        {
            case CharacterAnimationState.Death:
            case CharacterAnimationState.Attack:
            case CharacterAnimationState.Blank:
            case CharacterAnimationState.Ceremony:
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// 현재 애니메이션 상태를 반환합니다.
    /// </summary>
    /// <returns>현재 애니메이션 상태</returns>
    public virtual CharacterAnimationState GetCurrentAnimationState()
    {
        return currentAnimationState;
    }

    /// <summary>
    /// 특정 애니메이션 상태인지 확인합니다.
    /// </summary>
    /// <param name="state">확인할 애니메이션 상태</param>
    /// <returns>해당 상태이면 true</returns>
    public virtual bool IsInAnimationState(CharacterAnimationState state)
    {
        return currentAnimationState == state;
    }

    #endregion

    #region Protected Helper Methods

    /// <summary>
    /// 현재 애니메이션 상태를 업데이트합니다.
    /// </summary>
    protected virtual void UpdateAnimationState()
    {
        if (anim == null) return;
        
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        
        if (stateInfo.IsName("Death"))
        {
            currentAnimationState = CharacterAnimationState.Death;
        }
        else if (stateInfo.IsName("Attack"))
        {
            currentAnimationState = CharacterAnimationState.Attack;
        }
        else if (stateInfo.IsName("Blank"))
        {
            currentAnimationState = CharacterAnimationState.Blank;
        }
        else if (stateInfo.IsName("Ceremony"))
        {
            currentAnimationState = CharacterAnimationState.Ceremony;
        }
        else if (stateInfo.IsName("Run"))
        {
            currentAnimationState = CharacterAnimationState.Run;
        }
        else if (stateInfo.IsName("Walk"))
        {
            currentAnimationState = CharacterAnimationState.Walk;
        }
        else
        {
            currentAnimationState = CharacterAnimationState.Idle;
        }
    }

    /// <summary>
    /// 캐릭터의 방향을 X축 스케일을 이용해 반전시킵니다.
    /// </summary>
    /// <param name="moveX">X축 이동 값</param>
    protected virtual void FlipCharacter(float moveX)
    {
        if (moveX > 0)
        {
            // 양의 방향으로 이동 (오른쪽) -> 기본 스케일
            transform.localScale = new Vector3(1, 1, 1);
        }
        else if (moveX < 0)
        {
            // 음의 방향으로 이동 (왼쪽) -> X 스케일 -1
            transform.localScale = new Vector3(-1, 1, 1);
        }
    }

    /// <summary>
    /// 애니메이션 상태를 처리합니다.
    /// </summary>
    /// <param name="movementMagnitude">이동 크기</param>
    /// <param name="isRunning">달리기 상태 여부</param>
    protected virtual void HandleAnimations(float movementMagnitude, bool isRunning)
    {
        if (anim == null) return;
        
        // 이동 상태에 따른 애니메이션 트리거
        if (movementMagnitude > 0)
        {
            if (isRunning) // 달리기 상태일 때
            {
                anim.SetBool(ANIM_IS_RUNNING, true);
                anim.SetBool(ANIM_IS_MOVING, false);
                PlayMoveEffect(true);  // Run 상태일 때 이펙트 재생
            }
            else // 걷기 상태일 때
            {
                anim.SetBool(ANIM_IS_MOVING, true);
                anim.SetBool(ANIM_IS_RUNNING, false);
                PlayMoveEffect(true);  // 걷기 상태일 때 이펙트 재생
            }
        }
        else // 멈출 때
        {
            anim.SetBool(ANIM_IS_MOVING, false);
            anim.SetBool(ANIM_IS_RUNNING, false);
            PlayMoveEffect(false);  // 이펙트 재생 멈추기
        }
    }

    /// <summary>
    /// 물리 기반 이동을 처리합니다.
    /// </summary>
    /// <param name="movement">이동 벡터</param>
    /// <param name="isRunning">달리기 상태 여부</param>
    protected virtual void HandlePhysicsMovement(Vector2 movement, bool isRunning)
    {
        if (rb == null) return;
        
        float currentSpeed = moveSpeed;
        if (isRunning)
        {
            currentSpeed *= runSpeedMultiplier;
        }
        
        // Rigidbody를 사용해 이동
        rb.linearVelocity = movement.normalized * currentSpeed;
        
        // 캐릭터 방향 전환 처리
        if (movement.x != 0)
        {
            FlipCharacter(movement.x);
        }
    }

    #endregion

    #region Effect System

    /// <summary>
    /// 이펙트 풀을 초기화합니다.
    /// </summary>
    protected virtual void InitializeEffectPools()
    {
        // Attack 이펙트 풀 초기화
        for (int i = 0; i < EFFECT_POOL_SIZE; i++)
        {
            CreateEffectForPool(attackEffectData, attackEffectPool);
        }
        
        // Blank 이펙트 풀 초기화
        for (int i = 0; i < EFFECT_POOL_SIZE; i++)
        {
            CreateEffectForPool(blankEffectData, blankEffectPool);
        }
    }

    /// <summary>
    /// 이펙트 풀에 사용할 이펙트를 생성합니다.
    /// </summary>
    /// <param name="effectData">이펙트 데이터</param>
    /// <param name="pool">대상 풀</param>
    protected virtual void CreateEffectForPool(EffectData effectData, Queue<ParticleSystem> pool)
    {
        if (effectData.effectPrefab == null || effectData.effectContainer == null) return;
        
        GameObject effectInstance = Instantiate(effectData.effectPrefab, effectData.effectContainer);
        effectInstance.transform.localPosition = Vector3.zero;
        effectInstance.transform.localRotation = Quaternion.identity;
        
        ParticleSystem effectPS = effectInstance.GetComponent<ParticleSystem>();
        if (effectPS != null)
        {
            // 풀에 추가하기 전에 비활성화
            effectInstance.SetActive(false);
            pool.Enqueue(effectPS);
        }
    }

    /// <summary>
    /// 풀에서 이펙트를 가져옵니다.
    /// </summary>
    /// <param name="pool">대상 풀</param>
    /// <param name="effectData">이펙트 데이터 (풀이 비었을 때 새로 생성용)</param>
    /// <returns>ParticleSystem 컴포넌트</returns>
    protected virtual ParticleSystem GetEffectFromPool(Queue<ParticleSystem> pool, EffectData effectData)
    {
        if (pool.Count > 0)
        {
            ParticleSystem effect = pool.Dequeue();
            effect.gameObject.SetActive(true);
            return effect;
        }
        
        // 풀이 비었으면 새로 생성
        if (effectData.effectPrefab != null && effectData.effectContainer != null)
        {
            GameObject effectInstance = Instantiate(effectData.effectPrefab, effectData.effectContainer);
            effectInstance.transform.localPosition = Vector3.zero;
            effectInstance.transform.localRotation = Quaternion.identity;
            
            ParticleSystem effectPS = effectInstance.GetComponent<ParticleSystem>();
            if (effectPS != null)
            {
                return effectPS;
            }
        }
        
        return null;
    }

    /// <summary>
    /// 이펙트를 풀로 반환합니다.
    /// </summary>
    /// <param name="effect">반환할 이펙트</param>
    /// <param name="pool">대상 풀</param>
    protected virtual void ReturnEffectToPool(ParticleSystem effect, Queue<ParticleSystem> pool)
    {
        if (effect == null) return;
        
        // 이펙트 정지 및 초기화
        effect.Stop();
        effect.Clear();
        effect.gameObject.SetActive(false);
        
        // 풀에 반환
        pool.Enqueue(effect);
    }

    /// <summary>
    /// 이펙트를 재생하거나 정지하는 범용 함수 (풀링 방식)
    /// </summary>
    /// <param name="effectData">이펙트 데이터 (프리팹과 컨테이너)</param>
    /// <param name="activeEffectsList">활성화된 이펙트 리스트</param>
    /// <param name="pool">이펙트 풀</param>
    /// <param name="play">true면 재생, false면 정지</param>
    protected virtual void PlayEffect(EffectData effectData, List<ParticleSystem> activeEffectsList, Queue<ParticleSystem> pool, bool play)
    {
        if (effectData.effectPrefab == null || effectData.effectContainer == null) 
        {
            Debug.LogWarning("이펙트 프리팹 또는 컨테이너가 할당되지 않았습니다.");
            return;
        }
        
        if (play)
        {
            // 풀에서 이펙트 가져오기
            ParticleSystem effectPS = GetEffectFromPool(pool, effectData);
            
            if (effectPS != null)
            {
                // 활성화된 이펙트 리스트에 추가
                activeEffectsList.Add(effectPS);
                
                // 이펙트 재생
                effectPS.Play();
                
                // 이펙트 완료 후 풀로 반환
                StartCoroutine(WaitForEffectToCompleteAndReturnToPool(effectPS, activeEffectsList, pool));
            }
        }
        else
        {
            // 모든 활성화된 이펙트 정지
            foreach (var effect in activeEffectsList.ToArray())
            {
                if (effect != null && effect.isPlaying)
                {
                    effect.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }

    /// <summary>
    /// 이펙트가 완전히 재생될 때까지 기다린 후 풀로 반환하는 코루틴
    /// </summary>
    /// <param name="effect">대기할 이펙트</param>
    /// <param name="activeEffectsList">활성화된 이펙트 리스트</param>
    /// <param name="pool">이펙트를 반환할 풀</param>
    protected virtual System.Collections.IEnumerator WaitForEffectToCompleteAndReturnToPool(ParticleSystem effect, List<ParticleSystem> activeEffectsList, Queue<ParticleSystem> pool)
    {
        if (effect == null) yield break;
        
        // 이펙트가 재생 중일 때까지 기다림
        while (effect.isPlaying)
        {
            yield return null;
        }
        
        // 리스트에서 제거
        if (activeEffectsList.Contains(effect))
        {
            activeEffectsList.Remove(effect);
        }
        
        // 풀로 반환 (Destroy 대신)
        ReturnEffectToPool(effect, pool);
    }

    #endregion

    #region IAttackEffect, IMoveEffect, IBlankEffect Implementation

    /// <summary>
    /// 먼지 이펙트를 재생하거나 정지합니다.
    /// </summary>
    /// <param name="play">true면 재생, false면 정지</param>
    public virtual void PlayMoveEffect(bool play)
    {
        if (moveEffectData.effectPrefab == null || moveEffectData.effectContainer == null) 
        {
            Debug.LogWarning("이동 이펙트 프리팹 또는 컨테이너가 할당되지 않았습니다.");
            return;
        }
        
        if (play)
        {
            // 이동 이펙트가 없으면 새로 생성
            if (activeMoveEffect == null)
            {
                GameObject effectInstance = Instantiate(moveEffectData.effectPrefab, moveEffectData.effectContainer);
                effectInstance.transform.localPosition = Vector3.zero;
                effectInstance.transform.localRotation = Quaternion.identity;
                
                activeMoveEffect = effectInstance.GetComponent<ParticleSystem>();
                if (activeMoveEffect != null)
                {
                    activeMoveEffect.Play();
                }
            }
            else
            {
                // 기존 이동 이펙트가 있으면 재생만
                if (!activeMoveEffect.isPlaying)
                {
                    activeMoveEffect.Play();
                }
            }
        }
        else
        {
            // 이동 이펙트 정지
            if (activeMoveEffect != null && activeMoveEffect.isPlaying)
            {
                activeMoveEffect.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    /// <summary>
    /// 공격 이펙트를 재생합니다.
    /// 애니메이션 이벤트에서 호출되어야 합니다.
    /// </summary>
    public virtual void PlayAttackEffect()
    {
        PlayEffect(attackEffectData, activeAttackEffects, attackEffectPool, true);
    }

    /// <summary>
    /// Blank 이펙트를 재생합니다.
    /// 애니메이션 이벤트에서 호출되어야 합니다.
    /// </summary>
    public virtual void PlayBlankEffect()
    {
        PlayEffect(blankEffectData, activeBlankEffects, blankEffectPool, true);
    }

    #endregion
}
