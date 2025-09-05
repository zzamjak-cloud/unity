using UnityEngine;

/// <summary>
/// 카메라 설정을 관리하는 ScriptableObject
/// 다양한 카메라 프리셋을 저장하고 관리할 수 있습니다.
/// </summary>
[CreateAssetMenu(fileName = "CameraSettings", menuName = "Camera/Camera Settings")]
public class CameraSettings : ScriptableObject
{
    [Header("Follow Camera Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 0.4f, -10);
    [SerializeField] private float followSpeed = 2f;  // 카메라가 따라가는 속도
    [SerializeField] private float rotationSpeed = 1.5f;  // 카메라 회전 속도
    
    [Header("Lazy Follow Settings")]
    [SerializeField] private float lazyDistance = 2.5f;  // 카메라 Lazy Follow 거리
    [SerializeField] private float lazySpeed = 2.5f;  // 카메라 Lazy Follow 속도
    
    [Header("Look Ahead Settings")]
    [SerializeField] private bool enableLookAhead = true;
    [SerializeField] private float lookAheadDistance = 1.8f;  // 시야까지 이동 거리
    [SerializeField] private float lookAheadSpeed = 2f;  // 시야까지 이동 속도
    [SerializeField] private float lookAheadMultiplier = 1.2f;  // 시야까지 이동 배수
    [SerializeField] private float lookAheadSmoothing = 0.08f;  // 시야까지 이동 부드러움
    
    [Header("Boundary Settings")]
    [SerializeField] private bool enableBoundaries = false;  // 경계 제한 활성화
    [SerializeField] private Vector2 minBoundary = new Vector2(-10, -10);  // 최소 경계
    [SerializeField] private Vector2 maxBoundary = new Vector2(10, 10);  // 최대 경계
    
    [Header("Advanced Smoothing")]
    [SerializeField] private bool useAdvancedSmoothing = true;  // 고급 스무딩 사용
    [SerializeField] private float positionSmoothing = 0.06f;  // 위치 부드러움
    [SerializeField] private float velocitySmoothing = 0.05f;  // 속도 부드러움
    [SerializeField] private float maxVelocity = 5f;  // 최대 속도 제한
    
    [Header("Camera Effects")]
    [SerializeField] private bool enableScreenShake = true;  // 화면 흔들림 효과 활성화
    [SerializeField] private float shakeIntensity = 0.5f;  // 화면 흔들림 강도
    [SerializeField] private float shakeDuration = 0.3f;  // 화면 흔들림 지속 시간
    
    [Header("Zoom Settings")]
    [SerializeField] private float defaultOrthographicSize = 5f;  // 기본 직교 크기
    [SerializeField] private float zoomSpeed = 2f;  // 줌 속도
    [SerializeField] private float minZoom = 3f;  // 최소 줌
    [SerializeField] private float maxZoom = 8f;  // 최대 줌
    
    #region Properties
    
    public Vector3 Offset => offset;
    public float FollowSpeed => followSpeed;
    public float RotationSpeed => rotationSpeed;
    public float LazyDistance => lazyDistance;
    public float LazySpeed => lazySpeed;
    public bool EnableLookAhead => enableLookAhead;
    public float LookAheadDistance => lookAheadDistance;
    public float LookAheadSpeed => lookAheadSpeed;
    public float LookAheadMultiplier => lookAheadMultiplier;
    public float LookAheadSmoothing => lookAheadSmoothing;
    public bool EnableBoundaries => enableBoundaries;
    public Vector2 MinBoundary => minBoundary;
    public Vector2 MaxBoundary => maxBoundary;
    public bool UseAdvancedSmoothing => useAdvancedSmoothing;
    public float PositionSmoothing => positionSmoothing;
    public float VelocitySmoothing => velocitySmoothing;
    public float MaxVelocity => maxVelocity;
    public bool EnableScreenShake => enableScreenShake;
    public float ShakeIntensity => shakeIntensity;
    public float ShakeDuration => shakeDuration;
    public float DefaultOrthographicSize => defaultOrthographicSize;
    public float ZoomSpeed => zoomSpeed;
    public float MinZoom => minZoom;
    public float MaxZoom => maxZoom;
    
    #endregion
    
    /// <summary>
    /// FollowCameraOptimized에 설정 적용
    /// </summary>
    /// <param name="followCamera">적용할 FollowCameraOptimized</param>
    public void ApplyToFollowCamera(FollowCameraOptimized followCamera)
    {
        if (followCamera == null) return;
        
        followCamera.SetOffset(offset);
        followCamera.SetFollowSpeed(followSpeed);
        followCamera.SetLookAheadDistance(lookAheadDistance);
        followCamera.SetLookAheadSmoothing(lookAheadSmoothing);
        followCamera.SetAdvancedSmoothing(useAdvancedSmoothing, positionSmoothing, velocitySmoothing, maxVelocity);
        
        if (enableBoundaries)
        {
            followCamera.SetBoundaries(minBoundary, maxBoundary);
        }
        else
        {
            followCamera.SetBoundariesEnabled(false);
        }
    }
    
    /// <summary>
    /// CameraManager에 설정 적용
    /// </summary>
    /// <param name="cameraManager">적용할 CameraManager</param>
    public void ApplyToCameraManager(CameraManager cameraManager)
    {
        if (cameraManager == null) return;
        
        cameraManager.SetCameraEffects(enableScreenShake, true, true);
    }
    
    /// <summary>
    /// 기본 설정으로 복원
    /// </summary>
    [ContextMenu("Reset to Default")]
    public void ResetToDefault()
    {
        offset = new Vector3(0, 2, -10);
        followSpeed = 3f;  // 부드럽게 조정
        rotationSpeed = 1.5f;  // 부드럽게 조정
        lazyDistance = 2.5f;  // 더 여유롭게
        lazySpeed = 2.5f;  // 더 부드럽게
        enableLookAhead = true;
        lookAheadDistance = 1.8f;  // 더 자연스럽게
        lookAheadSpeed = 2f;  // 더 부드럽게
        lookAheadMultiplier = 1.2f;  // 더 자연스럽게
        lookAheadSmoothing = 0.08f;  // 더 부드럽게
        enableBoundaries = false;
        minBoundary = new Vector2(-10, -10);
        maxBoundary = new Vector2(10, 10);
        useAdvancedSmoothing = true;
        positionSmoothing = 0.06f;  // 더 부드럽게
        velocitySmoothing = 0.05f;  // 더 부드럽게
        maxVelocity = 5f;  // 더 부드럽게
        enableScreenShake = true;
        shakeIntensity = 0.5f;
        shakeDuration = 0.3f;
        defaultOrthographicSize = 5f;
        zoomSpeed = 2f;
        minZoom = 3f;
        maxZoom = 8f;
    }
}
