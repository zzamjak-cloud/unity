using UnityEngine;

/// <summary>
/// 캐릭터의 애니메이션 상태를 정의하는 열거형
/// </summary>
public enum CharacterAnimationState
{
    Idle,
    Walk,
    Run,
    Attack,
    Blank,
    Ceremony,
    Death,
}

// 캐릭터 이동 스크립트
public class CharacterMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;  // 이동 속도
    [SerializeField] private float runSpeedMultiplier = 1.5f;  // 달리기 속도 배수
    [SerializeField] [Range(0.01f, 0.1f)] private float zDepthWeight = 0.05f; // Z축 깊이 조절을 위한 가중치

    [Header("Effects")]
    [SerializeField] private ParticleSystem moveEffect;  // 이동시 이펙트 파티클

    [Header("Animation")]
    [SerializeField] private Animator anim;  // 애니메이터 컴포넌트 (Inspector에서 직접 연결)
    
    private Rigidbody2D rb;
    private CharacterAnimationState currentAnimationState = CharacterAnimationState.Idle;
    
    // 애니메이션 파라미터 이름들을 상수로 정의
    private static readonly string ANIM_IS_MOVING = "IsMoving";
    private static readonly string ANIM_IS_RUNNING = "IsRunning";
    private static readonly string ANIM_ATTACK = "Attack";
    private static readonly string ANIM_CEREMONY = "Ceremony";
    private static readonly string ANIM_BLANK = "Blank";
    private static readonly string ANIM_DEATH = "Death";

    // 추후 계속 확장해서 사용할 예정

    void Start()
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

    void Update()
    {
        // 이동에 대한 입력 값 받기
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // 현재 애니메이션 상태 확인
        UpdateAnimationState();
        
        // 이동 가능 여부 확인
        bool canMove = CanMove();

        Vector2 movement = Vector2.zero;
        if (canMove)
        {
            movement = new Vector2(moveX, moveY);
        }
        
        float currentSpeed = moveSpeed;
        if (isShiftPressed)  // 달리기 키 입력 시 속도 증가
        {
            currentSpeed *= runSpeedMultiplier;
        }

        // Rigidbody를 사용해 이동
        rb.linearVelocity = movement.normalized * currentSpeed;

        // 캐릭터 방향 전환 처리 (이동 가능한 상태일 때만)
        if (canMove)
        {
            FlipCharacter(moveX);
        }

        // 애니메이션 상태 제어
        HandleAnimations(movement.magnitude, isShiftPressed);
        
        // 특수 애니메이션 입력 처리
        HandleSpecialAnimationInputs();
    }
    
    void LateUpdate()
    {
        // Y축 위치에 따른 Z축 깊이 조절
        Vector3 newPosition = transform.position;
        newPosition.z = transform.position.y * zDepthWeight;
        transform.position = newPosition;
    }
    
    /// <summary>
    /// 현재 애니메이션 상태를 업데이트합니다.
    /// </summary>
    private void UpdateAnimationState()
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
    /// 현재 애니메이션 상태에 따라 이동 가능 여부를 반환합니다.
    /// </summary>
    private bool CanMove()
    {
        switch (currentAnimationState)
        {
            case CharacterAnimationState.Death:
            case CharacterAnimationState.Attack:
            case CharacterAnimationState.Blank:
            case CharacterAnimationState.Ceremony:
            // 이동 불가능한 상태들 확장해서 사용할 예정
                return false;
            default:
                return true;
        }
    }
    
    // 캐릭터의 방향을 X축 스케일을 이용해 반전시키는 함수
    private void FlipCharacter(float moveX)
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
    
    // 애니메이션 상태를 처리하는 함수
    private void HandleAnimations(float movementMagnitude, bool isRunning)
    {
        if (anim == null) return;
        
        // 이동 상태에 따른 애니메이션 트리거
        if (movementMagnitude > 0)
        {
            if (isRunning) // 달리기 상태일 때
            {
                anim.SetBool(ANIM_IS_RUNNING, true);
                anim.SetBool(ANIM_IS_MOVING, false);

                PlaymoveEffect(true);  // Run 상태일 때 이펙트 재생
            }
            else // 걷기 상태일 때
            {
                anim.SetBool(ANIM_IS_MOVING, true);
                anim.SetBool(ANIM_IS_RUNNING, false);
                
                PlaymoveEffect(true);  // 걷기 상태일 때 이펙트 재생
            }
        }
        else // 멈출 때
        {
            anim.SetBool(ANIM_IS_MOVING, false);
            anim.SetBool(ANIM_IS_RUNNING, false);
            
            PlaymoveEffect(false);  // 이펙트 재생 멈추기
        }
    }
    
    /// <summary>
    /// 특수 애니메이션 입력을 처리합니다.
    /// </summary>
    private void HandleSpecialAnimationInputs()
    {
        if (anim == null) return;
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            anim.SetTrigger(ANIM_ATTACK);
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            anim.SetTrigger(ANIM_CEREMONY);
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            anim.SetTrigger(ANIM_BLANK);
        }
        else if (Input.GetKeyDown(KeyCode.K))
        {
            anim.SetTrigger(ANIM_DEATH);
        }
    }
    
    /// <summary>
    /// 먼지 이펙트를 재생하거나 정지합니다.
    /// </summary>
    /// <param name="play">true면 재생, false면 정지</param>
    private void PlaymoveEffect(bool play)
    {
        if (moveEffect == null) return;
        
        if (play && !moveEffect.isPlaying)
        {
            moveEffect.Play();
        }
        else if (!play && moveEffect.isPlaying)
        {
            // 파티클 생성만 멈추고, 현재 파티클은 계속 재생
            moveEffect.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }
    
    /// <summary>
    /// 현재 애니메이션 상태를 반환합니다.
    /// </summary>
    public CharacterAnimationState GetCurrentAnimationState()
    {
        return currentAnimationState;
    }
    
    /// <summary>
    /// 특정 애니메이션 상태인지 확인합니다.
    /// </summary>
    public bool IsInAnimationState(CharacterAnimationState state)
    {
        return currentAnimationState == state;
    }
}