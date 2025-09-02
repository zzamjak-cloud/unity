using UnityEngine;

public class CharacterMovement : MonoBehaviour
{
    // 이동 속도
    public float moveSpeed = 5f;
    public float runSpeedMultiplier = 1.5f;

    // Z축 깊이 조절을 위한 가중치
    [Range(0.01f, 0.1f)] 
    public float zDepthWeight = 0.05f;

    // 애니메이터 컴포넌트 (Inspector에서 직접 연결)
    public Animator anim;
    
    private Rigidbody2D rb;

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
        // 입력 값 받기
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        bool isShiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // 현재 재생 중인 애니메이션의 정보를 가져옵니다.
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);

        // 공격, Blank, Ceremony가 아닐 때만 이동 처리
        bool canMove = !stateInfo.IsName("Attack") && !stateInfo.IsName("Blank") && !stateInfo.IsName("Ceremony");
        
        // Death 상태일 때는 영원히 움직일 수 없습니다.
        if (stateInfo.IsName("Death"))
        {
            canMove = false;
        }

        Vector2 movement = Vector2.zero;
        if (canMove)
        {
            movement = new Vector2(moveX, moveY);
        }
        
        float currentSpeed = moveSpeed;
        if (isShiftPressed)
        {
            currentSpeed *= runSpeedMultiplier;
        }

        // Rigidbody를 사용해 이동
        rb.linearVelocity = movement.normalized * currentSpeed;

        // 캐릭터 방향 전환 처리
        FlipCharacter(moveX);

        // 애니메이션 상태 제어
        HandleAnimations(movement.magnitude, isShiftPressed);
    }
    
    void LateUpdate()
    {
        // Y축 위치에 따른 Z축 깊이 조절
        Vector3 newPosition = transform.position;
        newPosition.z = transform.position.y * zDepthWeight;
        transform.position = newPosition;
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
            if (isRunning)
            {
                anim.SetTrigger("Run");
                anim.ResetTrigger("Walk");
                anim.ResetTrigger("Idle");
            }
            else
            {
                anim.SetTrigger("Walk");
                anim.ResetTrigger("Run");
                anim.ResetTrigger("Idle");
            }
        }
        else
        {
            anim.SetTrigger("Idle");
            anim.ResetTrigger("Walk");
            anim.ResetTrigger("Run");
        }

        // 특정 키 입력에 따른 애니메이션 발동
        if (Input.GetKeyDown(KeyCode.Space))
        {
            anim.SetTrigger("Attack");
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            anim.SetTrigger("Ceremony");
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            anim.SetTrigger("Blank");
        }
        else if (Input.GetKeyDown(KeyCode.K))
        {
            anim.SetTrigger("Death");
        }
    }
}