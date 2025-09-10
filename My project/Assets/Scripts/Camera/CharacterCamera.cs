using UnityEngine;

namespace GameCamera
{
    /// <summary>
    /// 캐릭터 전용 카메라 컴포넌트
    /// 캐릭터 이동 방향에 따라 카메라 오프셋을 자연스럽게 블렌딩 처리합니다.
    /// </summary>
    public class CharacterCamera : MonoBehaviour
    {
        [Header("카메라 오프셋 설정")]
        [SerializeField] private Vector3 defaultOffset = new Vector3(0, 0.54f, -10f);
        [SerializeField] private Vector3 moveOffset = new Vector3(5f, 1f, 0f);
        
        [Header("애니메이션 설정")]
        [SerializeField] private float duration = 0.3f;
        [SerializeField] private AnimationCurve easeCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f),  // 시작점에서 빠른 변화
            new Keyframe(0.5f, 0.8f, 1f, 1f),  // 중간점
            new Keyframe(1f, 1f, 0f, 0f)   // 끝점에서 완만한 변화
        );
        
        [Header("이동 감지 설정")]
        [SerializeField] private float movementThreshold = 0.1f;
        [SerializeField] private float stopThreshold = 0.05f;
        
        
        // 내부 변수들
        private Vector3 currentOffset;
        private Vector3 targetOffset;
        private Vector3 lastPosition; // 호환성을 위해 유지 (PlayerController가 없을 때 사용)
        private Vector3 currentVelocity; // 호환성을 위해 유지 (PlayerController가 없을 때 사용)
        private float animationTime;
        private bool isAnimating;
        private Vector3 startOffset;
        
        // 캐릭터 참조
        private Transform characterTransform;
        private PlayerController playerController;
        
        private void Start()
        {
            // 부모가 캐릭터라고 가정
            characterTransform = transform.parent;
            if (characterTransform == null)
            {
                Debug.LogError("CharacterCamera: 부모 Transform이 없습니다. 캐릭터의 자식으로 배치해주세요.");
                return;
            }
            
            // PlayerController 컴포넌트 찾기
            playerController = characterTransform.GetComponent<PlayerController>();
            if (playerController == null)
            {
                Debug.LogWarning("CharacterCamera: PlayerController 컴포넌트를 찾을 수 없습니다. Input 기반 최적화가 비활성화됩니다.");
            }
            
            // 초기 설정
            currentOffset = defaultOffset;
            targetOffset = defaultOffset;
            lastPosition = characterTransform.position;
            transform.localPosition = currentOffset;
            
            // Pivot 시스템 사용으로 카메라 스케일 고정 불필요
        }
        
        private void Update()
        {
            if (characterTransform == null) return;
            
            UpdateMovement();
            UpdateAnimation();
        }
        
        /// <summary>
        /// 캐릭터 이동 상태를 감지하고 목표 오프셋을 계산합니다.
        /// Input 시스템을 활용하여 성능을 최적화합니다.
        /// </summary>
        private void UpdateMovement()
        {
            Vector2 inputDirection = Vector2.zero;
            
            // PlayerController가 있으면 Input 값을 직접 사용 (최적화)
            if (playerController != null)
            {
                inputDirection = playerController.GetMovementInput();
            }
            else
            {
                // PlayerController가 없으면 기존 방식 사용 (호환성)
                Vector3 currentPosition = characterTransform.position;
                Vector3 movement = currentPosition - lastPosition;
                currentVelocity = movement / Time.deltaTime;
                inputDirection = currentVelocity.normalized;
                lastPosition = currentPosition;
            }
            
            float inputMagnitude = inputDirection.sqrMagnitude; // SqrMagnitude 사용으로 성능 최적화
            
            // 입력이 거의 없으면 제자리로 복귀
            if (inputMagnitude < stopThreshold * stopThreshold)
            {
                if (targetOffset != defaultOffset)
                {
                    SetTargetOffset(defaultOffset);
                }
            }
            // 입력이 감지되면 방향에 따른 오프셋 계산
            else if (inputMagnitude > movementThreshold * movementThreshold)
            {
                Vector3 newTargetOffset = CalculateOffsetFromDirection(inputDirection);
                
                // 목표 오프셋이 현재 목표와 다르면 새로운 애니메이션 시작
                if (newTargetOffset != targetOffset)
                {
                    SetTargetOffset(newTargetOffset);
                }
            }
        }
        
        /// <summary>
        /// 이동 방향에 따라 목표 오프셋을 계산합니다.
        /// </summary>
        /// <param name="direction">정규화된 이동 방향 (Vector2)</param>
        /// <returns>계산된 오프셋</returns>
        private Vector3 CalculateOffsetFromDirection(Vector2 direction)
        {
            Vector3 offset = defaultOffset;
            
            // X축 이동 (좌우) - moveOffset 범위 내에서만 계산
            // Pivot 시스템 사용으로 캐릭터 스케일 반전이 카메라에 영향을 주지 않음
            if (Mathf.Abs(direction.x) > 0.1f)
            {
                float targetX = direction.x > 0 ? moveOffset.x : -moveOffset.x;
                offset.x = defaultOffset.x + targetX;
            }
            
            // Y축 이동 (상하) - moveOffset 범위 내에서만 계산
            if (Mathf.Abs(direction.y) > 0.1f)
            {
                float targetY = direction.y > 0 ? moveOffset.y : -moveOffset.y;
                offset.y = defaultOffset.y + targetY;
            }
            
            // Z축은 항상 기본값 유지 (거리 유지)
            offset.z = defaultOffset.z;
            
            return offset;
        }
        
        /// <summary>
        /// 목표 오프셋을 설정하고 애니메이션을 시작합니다.
        /// </summary>
        /// <param name="newTargetOffset">새로운 목표 오프셋</param>
        private void SetTargetOffset(Vector3 newTargetOffset)
        {
            if (targetOffset == newTargetOffset) return;
            
            // 이미 같은 방향으로 애니메이션 중이면 무시 (떨림 방지)
            if (isAnimating && Vector3.SqrMagnitude(newTargetOffset - targetOffset) < 0.01f) return;
            
            targetOffset = newTargetOffset;
            startOffset = transform.localPosition; // 현재 실제 로컬 위치에서 시작
            animationTime = 0f;
            isAnimating = true;
        }
        
        /// <summary>
        /// 오프셋 애니메이션을 업데이트합니다.
        /// </summary>
        private void UpdateAnimation()
        {
            if (!isAnimating) return;
            
            animationTime += Time.deltaTime;
            float progress = Mathf.Clamp01(animationTime / duration);
            
            // 커브를 사용한 보간
            float curveValue = easeCurve.Evaluate(progress);
            currentOffset = Vector3.Lerp(startOffset, targetOffset, curveValue);
            
            // 로컬 위치 업데이트 (로컬 좌표계 사용)
            transform.localPosition = currentOffset;
            
            // 애니메이션 완료 체크
            if (progress >= 1f)
            {
                isAnimating = false;
                currentOffset = targetOffset;
                transform.localPosition = currentOffset;
            }
        }
        
        /// <summary>
        /// 런타임에서 설정값을 변경할 수 있는 메서드들
        /// </summary>
        public void SetDefaultOffset(Vector3 offset)
        {
            defaultOffset = offset;
            if (!isAnimating)
            {
                SetTargetOffset(defaultOffset);
            }
        }
        
        public void SetMoveOffset(Vector3 offset)
        {
            moveOffset = offset;
        }
        
        public void SetDuration(float newDuration)
        {
            duration = Mathf.Max(0.01f, newDuration);
        }
        
        public void SetEaseCurve(AnimationCurve curve)
        {
            easeCurve = curve;
        }
        
        /// <summary>
        /// 현재 상태 정보를 반환합니다.
        /// </summary>
        public Vector3 GetCurrentOffset() => currentOffset;
        public Vector3 GetTargetOffset() => targetOffset;
        public bool IsAnimating() => isAnimating;
        public Vector3 GetCurrentVelocity() => currentVelocity;
        public float GetAnimationProgress() => isAnimating ? Mathf.Clamp01(animationTime / duration) : 1f;
        public float GetRemainingAnimationTime() => isAnimating ? Mathf.Max(0, duration - animationTime) : 0f;
        
        private void OnDrawGizmosSelected()
        {
            // 현재 오프셋 표시
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            
            // 목표 오프셋 표시
            if (characterTransform != null)
            {
                Vector3 targetWorldPos = characterTransform.position + targetOffset;
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(targetWorldPos, 0.15f);
                
                // 연결선 그리기
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, targetWorldPos);
            }
        }
    }
}
