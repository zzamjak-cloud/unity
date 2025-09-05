using UnityEngine;

/// <summary>
/// 카메라 설정을 관리하는 ScriptableObject
/// 다양한 카메라 프리셋을 저장하고 관리할 수 있습니다.
/// </summary>
[CreateAssetMenu(fileName = "CameraSettings", menuName = "Camera/Camera Settings")]
public class CameraSettings : ScriptableObject
{
    [Header("Follow Camera Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2, -10);
    [SerializeField] private float followSpeed = 3f;  // 부드럽게 조정
    [SerializeField] private float rotationSpeed = 1.5f;  // 부드럽게 조정
    
    [Header("Lazy Follow Settings")]
    [SerializeField] private float lazyDistance = 2.5f;  // 더 여유롭게
    [SerializeField] private float lazySpeed = 2.5f;  // 더 부드럽게
    
    [Header("Look Ahead Settings")]
    [SerializeField] private bool enableLookAhead = true;
    [SerializeField] private float lookAheadDistance = 1.8f;  // 더 자연스럽게
    [SerializeField] private float lookAheadSpeed = 2f;  // 더 부드럽게
    [SerializeField] private float lookAheadMultiplier = 1.2f;  // 더 자연스럽게
    [SerializeField] private float lookAheadSmoothing = 0.08f;  // 더 부드럽게
    
    [Header("Boundary Settings")]
    [SerializeField] private bool enableBoundaries = false;
    [SerializeField] private Vector2 minBoundary = new Vector2(-10, -10);
    [SerializeField] private Vector2 maxBoundary = new Vector2(10, 10);
    
    [Header("Advanced Smoothing")]
    [SerializeField] private bool useAdvancedSmoothing = true;
    [SerializeField] private float positionSmoothing = 0.06f;  // 더 부드럽게
    [SerializeField] private float velocitySmoothing = 0.05f;  // 더 부드럽게
    [SerializeField] private float maxVelocity = 5f;  // 더 부드럽게
    
    [Header("Camera Effects")]
    [SerializeField] private bool enableScreenShake = true;
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float shakeDuration = 0.3f;
    
    [Header("Zoom Settings")]
    [SerializeField] private float defaultOrthographicSize = 5f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 8f;
    
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
