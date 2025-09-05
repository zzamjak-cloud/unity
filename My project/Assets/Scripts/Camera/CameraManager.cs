using UnityEngine;

/// <summary>
/// 카메라 시스템을 관리하는 매니저 클래스
/// FollowCameraOptimized와 다른 카메라 효과들을 통합 관리합니다.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private FollowCameraOptimized followCamera;
    
    [Header("Camera Effects")]
    [SerializeField] private bool enableScreenShake = true;  // 화면 흔들림 효과
    [SerializeField] private bool enableZoomEffect = true;  // 줌 효과
    [SerializeField] private bool enableTransitionEffect = true;  // 전환 효과
    
    [Header("Screen Shake Settings")]
    [SerializeField] private float shakeIntensity = 0.5f;  // 흔들림 강도
    [SerializeField] private float shakeDuration = 0.3f;  // 흔들림 지속 시간
    [SerializeField] private AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);  // 흔들림 커브
    
    [Header("Zoom Settings")]
    [SerializeField] private float defaultOrthographicSize = 5f;  // 기본 직교 크기
    [SerializeField] private float zoomSpeed = 2f;  // 줌 속도
    [SerializeField] private float minZoom = 3f;  // 최소 줌
    [SerializeField] private float maxZoom = 8f;  // 최대 줌
    
    // 내부 변수들
    private Vector3 originalCameraPosition;
    private float originalOrthographicSize;
    private bool isShaking = false;
    private float shakeTimer = 0f;
    private Vector3 shakeOffset = Vector3.zero;
    
    // 메모리 최적화를 위한 캐시된 Vector3
    private Vector3 tempShakeOffset = Vector3.zero;
    
    // 싱글톤 인스턴스
    public static CameraManager Instance { get; private set; }
    
    private void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        InitializeCameraManager();
    }
    
    private void Update()
    {
        UpdateScreenShake();
        UpdateZoomEffect();
    }
    
    /// <summary>
    /// 카메라 매니저 초기화
    /// </summary>
    private void InitializeCameraManager()
    {
        // 메인 카메라 자동 찾기 (안전한 방식)
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // FindObjectsByType은 비용이 크므로 한 번만 호출
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cameras.Length > 0)
                {
                    mainCamera = cameras[0]; // 첫 번째 카메라 사용
                }
            }
        }
        
        // FollowCameraOptimized 자동 찾기 (안전한 방식)
        if (followCamera == null)
        {
            followCamera = GetComponent<FollowCameraOptimized>();
            if (followCamera == null)
            {
                // FindObjectsByType은 비용이 크므로 한 번만 호출
                FollowCameraOptimized[] cameras = FindObjectsByType<FollowCameraOptimized>(FindObjectsSortMode.None);
                if (cameras.Length > 0)
                {
                    followCamera = cameras[0]; // 첫 번째 카메라 사용
                }
            }
        }
        
        // 초기값 저장
        if (mainCamera != null)
        {
            originalCameraPosition = mainCamera.transform.position;
            originalOrthographicSize = mainCamera.orthographicSize;
        }
    }
    
    /// <summary>
    /// 화면 흔들림 업데이트
    /// </summary>
    private void UpdateScreenShake()
    {
        if (!enableScreenShake || !isShaking) return;
        
        shakeTimer -= Time.deltaTime;
        
        if (shakeTimer <= 0f)
        {
            // 흔들림 종료
            isShaking = false;
            shakeOffset = Vector3.zero;
        }
        else
        {
            // 흔들림 계산
            float shakeProgress = 1f - (shakeTimer / shakeDuration);
            float currentIntensity = shakeIntensity * shakeCurve.Evaluate(shakeProgress);
            
            // 랜덤 흔들림 오프셋 생성 (메모리 최적화)
            tempShakeOffset.Set(
                Random.Range(-1f, 1f) * currentIntensity,
                Random.Range(-1f, 1f) * currentIntensity,
                0f
            );
            shakeOffset = tempShakeOffset;
        }
        
        // 카메라 위치에 흔들림 적용
        if (mainCamera != null)
        {
            mainCamera.transform.position = originalCameraPosition + shakeOffset;
        }
    }
    
    /// <summary>
    /// 줌 효과 업데이트
    /// </summary>
    private void UpdateZoomEffect()
    {
        if (!enableZoomEffect || mainCamera == null) return;
        
        // 마우스 휠로 줌 조절
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.1f)
        {
            float newSize = mainCamera.orthographicSize - scroll * zoomSpeed;
            newSize = Mathf.Clamp(newSize, minZoom, maxZoom);
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, newSize, Time.deltaTime * zoomSpeed);
        }
    }
    
    #region Public Methods
    
    /// <summary>
    /// 화면 흔들림 효과 시작
    /// </summary>
    /// <param name="intensity">흔들림 강도</param>
    /// <param name="duration">지속 시간</param>
    public void StartScreenShake(float intensity = -1f, float duration = -1f)
    {
        if (!enableScreenShake) return;
        
        shakeIntensity = intensity > 0 ? intensity : shakeIntensity;
        shakeDuration = duration > 0 ? duration : shakeDuration;
        
        isShaking = true;
        shakeTimer = shakeDuration;
        
    }
    
    /// <summary>
    /// 화면 흔들림 효과 중지
    /// </summary>
    public void StopScreenShake()
    {
        isShaking = false;
        shakeTimer = 0f;
        shakeOffset = Vector3.zero;
        
        if (mainCamera != null)
        {
            mainCamera.transform.position = originalCameraPosition;
        }
    }
    
    /// <summary>
    /// 줌 설정
    /// </summary>
    /// <param name="size">직교 크기</param>
    /// <param name="smooth">부드러운 전환 여부</param>
    public void SetZoom(float size, bool smooth = true)
    {
        if (!enableZoomEffect || mainCamera == null) return;
        
        size = Mathf.Clamp(size, minZoom, maxZoom);
        
        if (smooth)
        {
            StartCoroutine(SmoothZoom(size));
        }
        else
        {
            mainCamera.orthographicSize = size;
        }
    }
    
    /// <summary>
    /// 부드러운 줌 전환 코루틴
    /// </summary>
    /// <param name="targetSize">목표 크기</param>
    /// <returns></returns>
    private System.Collections.IEnumerator SmoothZoom(float targetSize)
    {
        float startSize = mainCamera.orthographicSize;
        float elapsedTime = 0f;
        float zoomTime = 1f; // 줌 전환 시간
        
        while (elapsedTime < zoomTime)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / zoomTime;
            mainCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, progress);
            yield return null;
        }
        
        mainCamera.orthographicSize = targetSize;
    }
    
    /// <summary>
    /// 기본 줌으로 복원
    /// </summary>
    public void ResetZoom()
    {
        SetZoom(defaultOrthographicSize, true);
    }
    
    /// <summary>
    /// 카메라를 특정 위치로 이동
    /// </summary>
    /// <param name="position">목표 위치</param>
    /// <param name="smooth">부드러운 전환 여부</param>
    public void MoveTo(Vector3 position, bool smooth = true)
    {
        if (followCamera != null)
        {
            followCamera.SetTarget(null); // FollowCameraOptimized 비활성화
        }
        
        if (smooth)
        {
            StartCoroutine(SmoothMoveTo(position));
        }
        else
        {
            if (mainCamera != null)
            {
                mainCamera.transform.position = position;
            }
        }
    }
    
    /// <summary>
    /// 부드러운 카메라 이동 코루틴
    /// </summary>
    /// <param name="targetPosition">목표 위치</param>
    /// <returns></returns>
    private System.Collections.IEnumerator SmoothMoveTo(Vector3 targetPosition)
    {
        Vector3 startPosition = mainCamera.transform.position;
        float elapsedTime = 0f;
        float moveTime = 1f; // 이동 시간
        
        while (elapsedTime < moveTime)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / moveTime;
            mainCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
            yield return null;
        }
        
        mainCamera.transform.position = targetPosition;
    }
    
    /// <summary>
    /// FollowCameraOptimized 활성화
    /// </summary>
    /// <param name="target">따라갈 대상</param>
    public void EnableFollowCamera(Transform target = null)
    {
        if (followCamera != null)
        {
            if (target != null)
            {
                followCamera.SetTarget(target);
            }
            followCamera.enabled = true;
        }
    }
    
    /// <summary>
    /// FollowCameraOptimized 비활성화
    /// </summary>
    public void DisableFollowCamera()
    {
        if (followCamera != null)
        {
            followCamera.enabled = false;
        }
    }
    
    /// <summary>
    /// 카메라 효과 설정
    /// </summary>
    /// <param name="screenShake">화면 흔들림</param>
    /// <param name="zoom">줌 효과</param>
    /// <param name="transition">전환 효과</param>
    public void SetCameraEffects(bool screenShake, bool zoom, bool transition)
    {
        enableScreenShake = screenShake;
        enableZoomEffect = zoom;
        enableTransitionEffect = transition;
    }
    
    #endregion
    
    #region Static Methods
    
    /// <summary>
    /// 화면 흔들림 효과 (정적 메서드)
    /// </summary>
    /// <param name="intensity">흔들림 강도</param>
    /// <param name="duration">지속 시간</param>
    public static void ShakeScreen(float intensity = 0.5f, float duration = 0.3f)
    {
        if (Instance != null)
        {
            Instance.StartScreenShake(intensity, duration);
        }
    }
    
    /// <summary>
    /// 줌 설정 (정적 메서드)
    /// </summary>
    /// <param name="size">직교 크기</param>
    /// <param name="smooth">부드러운 전환 여부</param>
    public static void SetCameraZoom(float size, bool smooth = true)
    {
        if (Instance != null)
        {
            Instance.SetZoom(size, smooth);
        }
    }
    
    #endregion
    
    /// <summary>
    /// 오브젝트가 파괴될 때 리소스 정리
    /// </summary>
    private void OnDestroy()
    {
        // 싱글톤 인스턴스 정리
        if (Instance == this)
        {
            Instance = null;
        }
        
        // 참조 해제
        mainCamera = null;
        followCamera = null;
        
        // 캐시된 변수들 초기화
        tempShakeOffset = Vector3.zero;
        
    }
}
