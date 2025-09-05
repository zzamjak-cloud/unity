using UnityEngine;

/// <summary>
/// FollowCamera의 자연스러운 움직임을 유지하면서 성능만 최적화한 버전
/// Duration 기반 시스템 대신 기존의 부드러운 스무딩을 유지합니다.
/// </summary>
public class FollowCameraOptimized : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;  // 따라갈 대상 (Player)
    [SerializeField] private bool autoFindPlayer = true;  // 자동으로 Player 찾기
    
    // Public 접근자
    public Transform Target => target;
    
    [Header("Follow Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2, -10);  // 기본 오프셋
    [SerializeField] private float followSpeed = 3f;  // 따라가는 속도
    // [SerializeField] private float rotationSpeed = 1.5f;  // 회전 속도
    
    [Header("Lazy Follow Settings")]
    [SerializeField] private float lazyDistance = 2.5f;  // Lazy follow 거리
    [SerializeField] private float lazySpeed = 2.5f;  // Lazy follow 속도
    [SerializeField] private AnimationCurve lazyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);  // Lazy follow 커브
    
    [Header("Look Ahead Settings")]
    [SerializeField] private bool enableLookAhead = true;  // 전방 시야 확보 활성화
    [SerializeField] private float lookAheadDistance = 1.8f;  // 전방 시야 거리
    // [SerializeField] private float lookAheadSpeed = 2f;  // 전방 시야 이동 속도
    [SerializeField] private float lookAheadMultiplier = 1.2f;  // 달리기 시 전방 시야 배수
    [SerializeField] private float lookAheadSmoothing = 0.08f;  // 전방 시야 부드러움
    
    [Header("Default Look Ahead Settings")]
    [SerializeField] private bool enableDefaultLookAhead = true;  // 기본 전방 시야 활성화 (정지 상태)
    [SerializeField] private float defaultLookAheadDistance = 1.2f;  // 기본 전방 시야 거리
    [SerializeField] private float defaultLookAheadSmoothing = 0.05f;  // 기본 전방 시야 부드러움
    
    [Header("Boundary Settings")]
    [SerializeField] private bool enableBoundaries = false;  // 경계 제한 활성화
    [SerializeField] private Vector2 minBoundary = new Vector2(-10, -10);  // 최소 경계
    [SerializeField] private Vector2 maxBoundary = new Vector2(10, 10);  // 최대 경계
    
    [Header("Advanced Smoothing")]
    [SerializeField] private bool useAdvancedSmoothing = true;  // 고급 스무딩 사용
    [SerializeField] private float positionSmoothing = 0.06f;  // 위치 스무딩
    [SerializeField] private float velocitySmoothing = 0.05f;  // 속도 스무딩
    [SerializeField] private float maxVelocity = 5f;  // 최대 속도 제한
    
    [Header("Performance Optimization")]
    [SerializeField] private bool enablePerformanceMode = true;  // 성능 모드 활성화
    [SerializeField] private int updateFrequency = 1;  // 업데이트 빈도 (1 = 매 프레임, 2 = 2프레임마다)
    [SerializeField] private float lookAheadUpdateInterval = 0.1f;  // 전방 시야 업데이트 간격 (초)
    
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugInfo = false;  // 디버그 정보 표시
    [SerializeField] private bool showGizmos = true;  // 기즈모 표시
    
    // 내부 변수들
    private Vector3 targetPosition;
    private Vector3 lookAheadPosition;
    private Vector3 smoothedPosition;
    private Vector3 lastTargetPosition;
    private Vector3 targetVelocity;
    private Vector3 smoothedVelocity;
    private Vector3 currentLookAheadTarget;
    private float currentLookAheadDistance;
    private Vector3 lastLookAheadPosition;
    
    // 기본 전방 시야 관련 변수들
    private Vector3 defaultLookAheadPosition;
    private Vector3 lastFacingDirection = Vector3.right;  // 마지막으로 바라본 방향 (기본값: 오른쪽)
    private Vector3 currentFacingDirection = Vector3.right;
    
    // Player 관련 참조
    private PlayerController playerController;
    private Rigidbody2D playerRigidbody;
    
    // 성능 최적화 변수들
    private int frameCounter = 0;
    private float lastLookAheadUpdateTime = 0f;
    private Vector3 cachedMovementDirection = Vector3.zero;
    private bool cachedIsRunning = false;
    
    // 메모리 최적화를 위한 캐시된 Vector3들
    private Vector3 tempVector3_1 = Vector3.zero;
    private Vector3 tempVector3_2 = Vector3.zero;
    private Vector3 tempVector3_3 = Vector3.zero;
    
    // 초기화
    private void Start()
    {
        InitializeCamera();
    }
    
    // 매 프레임 업데이트
    private void LateUpdate()
    {
        if (target == null) return;
        
        // 성능 모드: 업데이트 빈도 조절
        if (enablePerformanceMode)
        {
            frameCounter++;
            if (frameCounter % updateFrequency != 0)
            {
                return; // 건너뛰기
            }
        }
        
        UpdateCameraPosition();
        UpdateCameraRotation();
        
        if (showDebugInfo)
        {
            DisplayDebugInfo();
        }
    }
    
    /// <summary>
    /// 카메라 초기화
    /// </summary>
    private void InitializeCamera()
    {
        // 자동으로 Player 찾기
        if (autoFindPlayer && target == null)
        {
            FindPlayer();
        }
        
        if (target != null)
        {
            // Player 관련 컴포넌트 가져오기
            playerController = target.GetComponent<PlayerController>();
            playerRigidbody = target.GetComponent<Rigidbody2D>();
            
            // 초기 위치 설정
            targetPosition = target.position + offset;
            smoothedPosition = targetPosition;
            lastTargetPosition = target.position;
            smoothedVelocity = Vector3.zero;
            currentLookAheadTarget = Vector3.zero;
            lastLookAheadPosition = Vector3.zero;
            defaultLookAheadPosition = Vector3.zero;
            lastFacingDirection = Vector3.right;
            currentFacingDirection = Vector3.right;
            
            // 카메라를 초기 위치로 이동
            transform.position = targetPosition;
            
        }
        else
        {
            Debug.LogWarning("[FollowCameraOptimized] Target이 설정되지 않았습니다!");
        }
    }
    
    /// <summary>
    /// Player 자동 찾기
    /// </summary>
    private void FindPlayer()
    {
        // 더 효율적인 방법으로 Player 찾기
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
        }
        else
        {
            Debug.LogWarning("[FollowCameraOptimized] Player를 찾을 수 없습니다!");
        }
    }
    
    /// <summary>
    /// 카메라 위치 업데이트 (FollowCamera와 동일한 로직)
    /// </summary>
    private void UpdateCameraPosition()
    {
        // 타겟 속도 계산 (부드럽게)
        Vector3 currentVelocity = (target.position - lastTargetPosition) / Time.deltaTime;
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, currentVelocity, velocitySmoothing);
        lastTargetPosition = target.position;
        
        // 기본 타겟 위치 계산
        Vector3 baseTargetPosition = target.position + offset;
        
        // 전방 시야 확보 (성능 최적화 버전)
        if (enableLookAhead)
        {
            CalculateLookAheadOptimized();
            baseTargetPosition += lookAheadPosition;
        }
        
        // 고급 스무딩 적용
        if (useAdvancedSmoothing)
        {
            ApplyAdvancedSmoothing(baseTargetPosition);
        }
        else
        {
            // 기본 Lazy Follow 적용
            ApplyBasicLazyFollow(baseTargetPosition);
        }
        
        // 경계 제한 적용
        if (enableBoundaries)
        {
            smoothedPosition = ApplyBoundaries(smoothedPosition);
        }
        
        // 최종 위치 적용
        transform.position = smoothedPosition;
    }
    
    /// <summary>
    /// 고급 스무딩 적용 (FollowCamera와 동일)
    /// </summary>
    /// <param name="targetPos">목표 위치</param>
    private void ApplyAdvancedSmoothing(Vector3 targetPos)
    {
        // 거리 기반 스무딩 팩터 계산
        float distance = Vector3.Distance(smoothedPosition, targetPos);
        float dynamicSmoothing = positionSmoothing;
        
        // 거리가 멀수록 더 빠르게 따라가도록 조정
        if (distance > lazyDistance)
        {
            dynamicSmoothing = Mathf.Lerp(positionSmoothing, positionSmoothing * 2f, 
                (distance - lazyDistance) / lazyDistance);
        }
        
        // 부드러운 위치 보간
        smoothedPosition = Vector3.Lerp(smoothedPosition, targetPos, dynamicSmoothing);
        
        // 속도 제한 적용
        Vector3 velocity = (smoothedPosition - transform.position) / Time.deltaTime;
        if (velocity.magnitude > maxVelocity)
        {
            velocity = velocity.normalized * maxVelocity;
            smoothedPosition = transform.position + velocity * Time.deltaTime;
        }
    }
    
    /// <summary>
    /// 기본 Lazy Follow 적용 (FollowCamera와 동일)
    /// </summary>
    /// <param name="targetPos">목표 위치</param>
    private void ApplyBasicLazyFollow(Vector3 targetPos)
    {
        float distance = Vector3.Distance(transform.position, targetPos);
        
        if (distance > lazyDistance)
        {
            targetPosition = Vector3.Lerp(targetPosition, targetPos, lazySpeed * Time.deltaTime);
        }
        else
        {
            targetPosition = targetPos;
        }
        
        smoothedPosition = Vector3.Lerp(smoothedPosition, targetPosition, positionSmoothing);
    }
    
    /// <summary>
    /// 전방 시야 확보 계산 (성능 최적화 버전 + 기본 전방 시야)
    /// </summary>
    private void CalculateLookAheadOptimized()
    {
        // 성능 최적화: 전방 시야 업데이트 간격 조절
        if (Time.time - lastLookAheadUpdateTime < lookAheadUpdateInterval)
        {
            return; // 캐시된 값 사용
        }
        
        lastLookAheadUpdateTime = Time.time;
        
        // Player의 이동 방향과 속도에 따른 전방 시야 계산
        Vector3 movementDirection = Vector3.zero;
        bool isMoving = false;
        
        if (playerController != null)
        {
            // Player의 현재 이동 입력 가져오기
            Vector2 playerInput = playerController.GetMovementInput();
            tempVector3_1.Set(playerInput.x, playerInput.y, 0);
            movementDirection = tempVector3_1;
            isMoving = movementDirection.magnitude > 0.1f;
            
            // 달리기 상태 캐시
            cachedIsRunning = playerController.IsRunning();
        }
        else if (playerRigidbody != null)
        {
            // Rigidbody 속도 기반으로 방향 계산 (부드럽게)
            Vector2 velocity = playerRigidbody.linearVelocity;
            if (velocity.magnitude > 0.1f)
            {
                tempVector3_1.Set(velocity.x, velocity.y, 0);
                movementDirection = tempVector3_1.normalized;
                isMoving = true;
            }
        }
        
        // 이동 중일 때는 기존 로직 사용
        if (isMoving)
        {
            // 이동 방향을 현재 바라보는 방향으로 업데이트
            currentFacingDirection = movementDirection.normalized;
            lastFacingDirection = currentFacingDirection;
            
            // 전방 시야 거리 계산
            currentLookAheadDistance = lookAheadDistance;
            
            // 달리기 중일 때 전방 시야 확대
            if (cachedIsRunning)
            {
                currentLookAheadDistance *= lookAheadMultiplier;
            }
            
            // 목표 전방 시야 위치 계산
            currentLookAheadTarget = movementDirection * currentLookAheadDistance;
            
            // 부드러운 전방 시야 전환 (급격한 변화 방지)
            lookAheadPosition = Vector3.Lerp(lookAheadPosition, currentLookAheadTarget, lookAheadSmoothing);
        }
        else
        {
            // 정지 상태일 때 기본 전방 시야 적용
            if (enableDefaultLookAhead)
            {
                // 마지막으로 이동했던 방향을 기본 전방 시야로 사용
                Vector3 defaultTarget = lastFacingDirection * defaultLookAheadDistance;
                
                // 기본 전방 시야를 부드럽게 적용
                defaultLookAheadPosition = Vector3.Lerp(defaultLookAheadPosition, defaultTarget, defaultLookAheadSmoothing);
                
                // 기본 전방 시야를 현재 전방 시야에 적용
                lookAheadPosition = Vector3.Lerp(lookAheadPosition, defaultLookAheadPosition, defaultLookAheadSmoothing);
            }
            else
            {
                // 기본 전방 시야가 비활성화된 경우 점진적으로 중앙으로 복귀
                lookAheadPosition = Vector3.Lerp(lookAheadPosition, Vector3.zero, lookAheadSmoothing * 0.5f);
            }
        }
        
        // 전방 시야가 너무 급격히 변하지 않도록 제한
        Vector3 lookAheadChange = lookAheadPosition - lastLookAheadPosition;
        if (lookAheadChange.magnitude > maxVelocity * 0.5f)
        {
            lookAheadChange = lookAheadChange.normalized * maxVelocity * 0.5f;
            lookAheadPosition = lastLookAheadPosition + lookAheadChange;
        }
        
        lastLookAheadPosition = lookAheadPosition;
    }
    
    /// <summary>
    /// 카메라 회전 업데이트 (선택적)
    /// </summary>
    private void UpdateCameraRotation()
    {
        // 필요시 카메라 회전 로직 추가
        // 현재는 기본 회전 유지
    }
    
    /// <summary>
    /// 경계 제한 적용
    /// </summary>
    /// <param name="position">제한할 위치</param>
    /// <returns>제한된 위치</returns>
    private Vector3 ApplyBoundaries(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, minBoundary.x, maxBoundary.x);
        position.y = Mathf.Clamp(position.y, minBoundary.y, maxBoundary.y);
        return position;
    }
    
    /// <summary>
    /// 디버그 정보 표시
    /// </summary>
    private void DisplayDebugInfo()
    {
        if (!Application.isEditor) return;
        
        GUILayout.BeginArea(new Rect(10, 220, 350, 200));
        GUILayout.Label("=== Follow Camera Optimized Debug ===");
        GUILayout.Label($"Target: {(target != null ? target.name : "None")}");
        GUILayout.Label($"Position: {transform.position}");
        GUILayout.Label($"Smoothed Position: {smoothedPosition}");
        GUILayout.Label($"Look Ahead: {lookAheadPosition}");
        GUILayout.Label($"Default Look Ahead: {defaultLookAheadPosition}");
        GUILayout.Label($"Facing Direction: {lastFacingDirection}");
        GUILayout.Label($"Smoothed Velocity: {smoothedVelocity}");
        GUILayout.Label($"Distance: {Vector3.Distance(transform.position, target.position):F2}");
        GUILayout.Label($"Advanced Smoothing: {useAdvancedSmoothing}");
        GUILayout.Label($"Performance Mode: {enablePerformanceMode}");
        GUILayout.Label($"Update Frequency: {updateFrequency}");
        GUILayout.EndArea();
    }
    
    /// <summary>
    /// 기즈모 그리기
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showGizmos || target == null) return;
        
        // 타겟 위치 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(target.position, 0.5f);
        
        // 카메라 위치 표시
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        
        // 전방 시야 표시
        if (enableLookAhead)
        {
            Gizmos.color = Color.yellow;
            Vector3 lookAheadPos = target.position + lookAheadPosition;
            Gizmos.DrawWireSphere(lookAheadPos, 0.2f);
            Gizmos.DrawLine(target.position, lookAheadPos);
        }
        
        // 기본 전방 시야 표시 (정지 상태)
        if (enableDefaultLookAhead)
        {
            Gizmos.color = Color.orange;
            Vector3 defaultLookAheadPos = target.position + defaultLookAheadPosition;
            Gizmos.DrawWireSphere(defaultLookAheadPos, 0.15f);
            Gizmos.DrawLine(target.position, defaultLookAheadPos);
            
            // 바라보는 방향 표시
            Gizmos.color = Color.cyan;
            Vector3 facingPos = target.position + lastFacingDirection * defaultLookAheadDistance;
            Gizmos.DrawWireSphere(facingPos, 0.1f);
            Gizmos.DrawLine(target.position, facingPos);
        }
        
        // 경계 표시
        if (enableBoundaries)
        {
            Gizmos.color = Color.green;
            tempVector3_1.Set((minBoundary.x + maxBoundary.x) / 2, (minBoundary.y + maxBoundary.y) / 2, 0);
            tempVector3_2.Set(maxBoundary.x - minBoundary.x, maxBoundary.y - minBoundary.y, 0);
            Gizmos.DrawWireCube(tempVector3_1, tempVector3_2);
        }
        
        // Lazy follow 거리 표시
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(target.position, lazyDistance);
    }
    
    #region Public Methods
    
    /// <summary>
    /// 타겟 설정
    /// </summary>
    /// <param name="newTarget">새로운 타겟</param>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            InitializeCamera();
        }
    }
    
    /// <summary>
    /// 오프셋 설정
    /// </summary>
    /// <param name="newOffset">새로운 오프셋</param>
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
    
    /// <summary>
    /// 따라가는 속도 설정
    /// </summary>
    /// <param name="newSpeed">새로운 속도</param>
    public void SetFollowSpeed(float newSpeed)
    {
        followSpeed = Mathf.Max(0.1f, newSpeed);
    }
    
    /// <summary>
    /// 전방 시야 거리 설정
    /// </summary>
    /// <param name="newDistance">새로운 거리</param>
    public void SetLookAheadDistance(float newDistance)
    {
        lookAheadDistance = Mathf.Max(0, newDistance);
    }
    
    /// <summary>
    /// 경계 설정
    /// </summary>
    /// <param name="min">최소 경계</param>
    /// <param name="max">최대 경계</param>
    public void SetBoundaries(Vector2 min, Vector2 max)
    {
        minBoundary = min;
        maxBoundary = max;
        enableBoundaries = true;
    }
    
    /// <summary>
    /// 경계 제한 활성화/비활성화
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetBoundariesEnabled(bool enabled)
    {
        enableBoundaries = enabled;
    }
    
    /// <summary>
    /// 카메라를 타겟 위치로 즉시 이동
    /// </summary>
    public void SnapToTarget()
    {
        if (target != null)
        {
            targetPosition = target.position + offset;
            transform.position = targetPosition;
            smoothedPosition = targetPosition;
            smoothedVelocity = Vector3.zero;
            lookAheadPosition = Vector3.zero;
            currentLookAheadTarget = Vector3.zero;
            defaultLookAheadPosition = Vector3.zero;
        }
    }
    
    /// <summary>
    /// 고급 스무딩 설정
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    /// <param name="positionSmooth">위치 스무딩</param>
    /// <param name="velocitySmooth">속도 스무딩</param>
    /// <param name="maxVel">최대 속도</param>
    public void SetAdvancedSmoothing(bool enabled, float positionSmooth = 0.06f, float velocitySmooth = 0.05f, float maxVel = 5f)
    {
        useAdvancedSmoothing = enabled;
        positionSmoothing = positionSmooth;
        velocitySmoothing = velocitySmooth;
        maxVelocity = maxVel;
    }
    
    /// <summary>
    /// 전방 시야 부드러움 설정
    /// </summary>
    /// <param name="smoothing">부드러움 값</param>
    public void SetLookAheadSmoothing(float smoothing)
    {
        lookAheadSmoothing = Mathf.Clamp01(smoothing);
    }
    
    /// <summary>
    /// 성능 모드 설정
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    /// <param name="frequency">업데이트 빈도</param>
    /// <param name="lookAheadInterval">전방 시야 업데이트 간격</param>
    public void SetPerformanceMode(bool enabled, int frequency = 1, float lookAheadInterval = 0.1f)
    {
        enablePerformanceMode = enabled;
        updateFrequency = Mathf.Max(1, frequency);
        lookAheadUpdateInterval = Mathf.Max(0.05f, lookAheadInterval);
    }
    
    /// <summary>
    /// 기본 전방 시야 설정
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    /// <param name="distance">기본 전방 시야 거리</param>
    /// <param name="smoothing">기본 전방 시야 부드러움</param>
    public void SetDefaultLookAhead(bool enabled, float distance = 1.2f, float smoothing = 0.05f)
    {
        enableDefaultLookAhead = enabled;
        defaultLookAheadDistance = Mathf.Max(0, distance);
        defaultLookAheadSmoothing = Mathf.Clamp01(smoothing);
    }
    
    /// <summary>
    /// 강제로 바라보는 방향 설정
    /// </summary>
    /// <param name="direction">바라볼 방향</param>
    public void SetFacingDirection(Vector3 direction)
    {
        if (direction.magnitude > 0.1f)
        {
            lastFacingDirection = direction.normalized;
            currentFacingDirection = lastFacingDirection;
        }
    }
    
    #endregion
    
    /// <summary>
    /// 오브젝트가 파괴될 때 리소스 정리
    /// </summary>
    private void OnDestroy()
    {
        // 참조 해제
        target = null;
        playerController = null;
        playerRigidbody = null;
        
        // 캐시된 변수들 초기화
        tempVector3_1 = Vector3.zero;
        tempVector3_2 = Vector3.zero;
        tempVector3_3 = Vector3.zero;
        
    }
}
