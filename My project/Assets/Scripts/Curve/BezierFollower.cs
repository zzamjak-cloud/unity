using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class BezierFollower : MonoBehaviour
{
    /// <summary>
    /// 좌표 공간 타입
    /// </summary>
    public enum CoordinateSpace
    {
        World,  // 월드 공간 (기존 방식)
        UI      // UI Canvas 공간
    }

    [Header("Path Settings")]
    [Tooltip("BezierPathData ScriptableObject (필수) - 런타임에서 변경 가능")]
    public BezierPathData pathData;
    
    [Header("Coordinate Space")]
    [Tooltip("좌표 공간 타입 - World: 월드 공간, UI: UI Canvas 공간")]
    public CoordinateSpace coordinateSpace = CoordinateSpace.World;
    
    [Header("Transform Reference")]
    [Tooltip("경로의 Transform (ScriptableObject가 있으면 ScriptableObject의 Transform 정보가 우선 사용됩니다)")]
    public Transform pathTransform;
    
    [Header("Movement Settings")]
    public float duration = 5f; // 이동에 걸리는 시간
    public AnimationCurve movementCurve = AnimationCurve.Linear(0, 0, 1, 1); // EaseIn/Out 제어
    [Range(0f, 1f)] public float startOffset = 0f; // 시작점 오프셋 (0~1)
    public bool followRotation = true; // 방향에 따라 회전할지 여부

    public enum LoopType { None, Restart, Yoyo }
    public LoopType loopType = LoopType.Restart;

    [Header("State")]
    [Range(0f, 1f)] public float progress = 0f;
    public bool isPlaying = true;

    private float _timer = 0f;
    private bool _isForward = true;
    
    // Transform 캐싱 (성능 최적화)
    private Transform _cachedPathTransform;
    private int _lastTransformHash;
    private bool _transformDirty = true;
    private Dictionary<int, CachedPointData> _cachedWorldData;
    
    // ScriptableObject의 Transform 정보를 사용하기 위한 Matrix
    private Matrix4x4 _savedTransformMatrix = Matrix4x4.identity;
    private bool _useSavedTransform = false;
    private BezierPathData _lastPathData; // pathData 변경 감지용
    
    // UI 모드 지원
    private RectTransform _rectTransform;
    private RectTransform _pathRectTransform; // UI 모드에서 경로의 RectTransform
    private Canvas _canvas;
    private bool _isUIMode = false;

    private struct CachedPointData
    {
        public Vector3 position;
        public Vector3 handleIn;
        public Vector3 handleOut;
    }
    
#if UNITY_EDITOR
    [HideInInspector] public float _lastEditorUpdateTime = 0f;
    [HideInInspector] public bool isTestMode = false;
    [HideInInspector] public float testStartTime = 0f;
    [HideInInspector] public float testDuration = 10f;
    
    // Editor에서 접근하기 위한 프로퍼티
    public float Timer { get => _timer; set => _timer = value; }
    public bool IsForward { get => _isForward; set => _isForward = value; }
#endif

    private void Awake()
    {
        // 경로 유효성 검사
        if (pathData == null)
        {
            Debug.LogWarning($"BezierFollower on {gameObject.name}: BezierPathData가 할당되지 않았습니다.");
        }
        
        // Dictionary 초기 용량 설정 (성능 최적화)
        _cachedWorldData = new Dictionary<int, CachedPointData>(16); // 일반적으로 4-16개 포인트 예상
        
        // UI 모드 초기화
        InitializeUIMode();
        
        // Transform 초기화
        InitializeTransform();
        
        // pathData 변경 감지 초기화
        _lastPathData = pathData;
    }
    
    /// <summary>
    /// UI 모드 초기화
    /// </summary>
    private void InitializeUIMode()
    {
        _isUIMode = (coordinateSpace == CoordinateSpace.UI);
        
        if (_isUIMode)
        {
            // RectTransform 확인
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                Debug.LogWarning($"BezierFollower on {gameObject.name}: UI 모드에서는 RectTransform이 필요합니다. 월드 모드로 전환합니다.");
                coordinateSpace = CoordinateSpace.World;
                _isUIMode = false;
                return;
            }
            
            // Canvas 확인
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                Debug.LogWarning($"BezierFollower on {gameObject.name}: UI 모드에서는 Canvas가 필요합니다. 월드 모드로 전환합니다.");
                coordinateSpace = CoordinateSpace.World;
                _isUIMode = false;
                return;
            }
            
            // pathTransform이 RectTransform인지 확인
            if (pathTransform != null)
            {
                _pathRectTransform = pathTransform.GetComponent<RectTransform>();
            }
            
            // pathTransform이 없으면 BezierPath 컴포넌트에서 RectTransform 찾기
            if (_pathRectTransform == null && pathData != null)
            {
                // ScriptableObject에 저장된 Transform 정보는 사용하지 않음 (UI 모드에서는)
                // 대신 pathTransform이 BezierPath 컴포넌트를 가진 GameObject인지 확인
                if (pathTransform != null)
                {
                    BezierPath bezierPath = pathTransform.GetComponent<BezierPath>();
                    if (bezierPath != null)
                    {
                        _pathRectTransform = pathTransform.GetComponent<RectTransform>();
                    }
                }
            }
        }
        else
        {
            _rectTransform = null;
            _canvas = null;
            _pathRectTransform = null;
        }
    }
    
    /// <summary>
    /// Transform 초기화 (ScriptableObject의 Transform 정보가 최우선, 없으면 pathTransform 사용)
    /// </summary>
    private void InitializeTransform()
    {
        // UI 모드에서는 UI 좌표계 사용
        if (_isUIMode)
        {
            // UI 모드에서도 ScriptableObject의 Transform 정보(특히 scale)를 고려해야 함
            // pathRectTransform 또는 자기 자신의 RectTransform 사용
            if (_pathRectTransform != null)
            {
                _cachedPathTransform = _pathRectTransform;
                _lastTransformHash = _cachedPathTransform.GetHashCode();
                // UI 모드에서도 ScriptableObject의 Transform 정보를 사용 (scale 적용을 위해)
                _useSavedTransform = (pathData != null && pathData.IsValid());
                if (_useSavedTransform)
                {
                    UpdateSavedTransformMatrix();
                }
            }
            else
            {
                // UI 모드에서는 자기 자신의 RectTransform 사용
                _cachedPathTransform = _rectTransform;
                _lastTransformHash = _cachedPathTransform.GetHashCode();
                // UI 모드에서도 ScriptableObject의 Transform 정보를 사용 (scale 적용을 위해)
                _useSavedTransform = (pathData != null && pathData.IsValid());
                if (_useSavedTransform)
                {
                    UpdateSavedTransformMatrix();
                }
            }
            return;
        }
        
        // 월드 모드: 기존 로직
        if (pathData != null && pathData.IsValid())
        {
            // ScriptableObject가 있으면 무조건 ScriptableObject의 Transform 정보 사용 (최우선)
            _useSavedTransform = true;
            UpdateSavedTransformMatrix();
        }
        else if (pathTransform != null)
        {
            // ScriptableObject가 없고 pathTransform이 지정되어 있으면 사용
            _cachedPathTransform = pathTransform;
            _lastTransformHash = _cachedPathTransform.GetHashCode();
            _useSavedTransform = false;
        }
        else
        {
            // 둘 다 없으면 자기 자신의 Transform 사용
            _cachedPathTransform = transform;
            _lastTransformHash = _cachedPathTransform.GetHashCode();
            _useSavedTransform = false;
        }
    }
    
    /// <summary>
    /// ScriptableObject에 저장된 Transform 정보로 Matrix 업데이트
    /// </summary>
    private void UpdateSavedTransformMatrix()
    {
        if (pathData == null) return;
        
        // Matrix4x4 생성: TRS (Translation, Rotation, Scale)
        _savedTransformMatrix = Matrix4x4.TRS(
            pathData.SavedPosition,
            pathData.SavedRotation,
            pathData.SavedScale
        );
    }

    private void Reset()
    {
        UpdatePosition();
    }

    private void OnValidate()
    {
        // UI 모드 재초기화
        InitializeUIMode();
        
        // pathData가 변경되었거나 pathTransform이 변경되었을 수 있으므로 재초기화
        if (_lastPathData != pathData)
        {
            _lastPathData = pathData;
        }
        InitializeTransform();
        _transformDirty = true;
        if (pathData != null)
        {
            UpdatePosition();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        
        if (pathData == null || !isPlaying) return;

        // UI 모드 재확인 (coordinateSpace 변경 시)
        bool shouldBeUIMode = (coordinateSpace == CoordinateSpace.UI);
        if (_isUIMode != shouldBeUIMode)
        {
            InitializeUIMode();
            InitializeTransform();
            _transformDirty = true;
        }

        // pathData 변경 감지 (성능 최적화: 변경된 경우에만 재초기화)
        if (_lastPathData != pathData)
        {
            _lastPathData = pathData;
            InitializeTransform();
            _transformDirty = true;
        }
        
        // Transform 변경 감지
        CheckTransformChanged();

        // 1. 시간 진행 계산
        if (_isForward) _timer += Time.deltaTime / duration;
        else _timer -= Time.deltaTime / duration;

        // 2. Loop 및 경계 처리
        bool isLoop = pathData.IsLoop;

        if (_timer >= 1f)
        {
            if (loopType == LoopType.Restart)
            {
                _timer = 0f;
            }
            else if (loopType == LoopType.Yoyo)
            {
                _timer = 1f;
                _isForward = false;
            }
            else // None
            {
                _timer = 1f;
                isPlaying = false;
            }
        }
        else if (_timer <= 0f)
        {
            if (loopType == LoopType.Yoyo)
            {
                _timer = 0f;
                _isForward = true;
            }
        }

        UpdatePosition();
    }

    /// <summary>
    /// Transform이 변경되었는지 확인
    /// </summary>
    private void CheckTransformChanged()
    {
        // UI 모드에서는 RectTransform 변경 감지
        if (_isUIMode)
        {
            RectTransform currentRectTransform = _pathRectTransform != null ? _pathRectTransform : _rectTransform;
            if (currentRectTransform != _cachedPathTransform)
            {
                InitializeTransform();
                _transformDirty = true;
                return;
            }
            
            if (currentRectTransform == null)
            {
                InitializeTransform();
                _transformDirty = true;
                return;
            }
            
            int rectHash = currentRectTransform.GetHashCode();
            if (rectHash != _lastTransformHash)
            {
                _transformDirty = true;
                _lastTransformHash = rectHash;
            }
            return;
        }
        
        // 월드 모드: 기존 로직
        // ScriptableObject의 Transform 정보를 사용하는 경우
        if (_useSavedTransform)
        {
            // pathData가 변경되었는지 확인하고 재초기화
            if (pathData == null || !pathData.IsValid())
            {
                InitializeTransform();
                _transformDirty = true;
            }
            return;
        }
        
        // pathTransform이 변경되었는지 확인
        if (pathTransform != _cachedPathTransform)
        {
            InitializeTransform();
            _transformDirty = true;
            return;
        }
        
        if (_cachedPathTransform == null)
        {
            InitializeTransform();
            _transformDirty = true;
            return;
        }

        int currentHash = _cachedPathTransform.GetHashCode();
        if (currentHash != _lastTransformHash)
        {
            _transformDirty = true;
            _lastTransformHash = currentHash;
        }
    }

    /// <summary>
    /// 좌표 캐시 업데이트 (월드 또는 UI 좌표)
    /// </summary>
    private void UpdateWorldCache()
    {
        if (pathData == null || !pathData.IsValid())
        {
            _cachedWorldData.Clear();
            return;
        }

        // Transform 초기화 확인
        if (!_useSavedTransform)
        {
            if (_cachedPathTransform == null)
            {
                InitializeTransform();
            }
        }
        else
        {
            // ScriptableObject의 Transform 정보 업데이트 (변경 시에만)
            UpdateSavedTransformMatrix();
        }

        // Dictionary 초기 용량 설정 (재할당 방지)
        var points = pathData.Points;
        int pointCount = points.Count;
        
        // Dictionary가 null이거나 포인트 개수가 변경된 경우 재생성
        if (_cachedWorldData == null)
        {
            _cachedWorldData = new Dictionary<int, CachedPointData>(pointCount);
        }
        else if (_cachedWorldData.Count != pointCount)
        {
            // 포인트 개수가 변경된 경우 Dictionary 재생성 (용량 최적화)
            _cachedWorldData = new Dictionary<int, CachedPointData>(pointCount);
        }
        else
        {
            _cachedWorldData.Clear();
        }

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 worldPos, worldHandleIn, worldHandleOut;
            
            if (_isUIMode)
            {
                // UI 모드: ScriptableObject의 scale만 적용 (position, rotation은 무시)
                if (_useSavedTransform && pathData != null)
                {
                    // ScriptableObject의 scale만 적용 (UI 모드에서는 로컬 좌표에 scale만 적용)
                    Vector3 scale = pathData.SavedScale;
                    Vector3 scaledPos = Vector3.Scale(points[i].position, scale);
                    Vector3 scaledHandleIn = Vector3.Scale(points[i].handleIn, scale);
                    Vector3 scaledHandleOut = Vector3.Scale(points[i].handleOut, scale);
                    
                    // scale이 적용된 로컬 좌표를 그대로 사용 (UI 모드에서는 로컬 좌표 기준)
                    worldPos = scaledPos;
                    worldHandleIn = scaledHandleIn;
                    worldHandleOut = scaledHandleOut;
                }
                else
                {
                    // ScriptableObject가 없으면 기존 로직 사용 (pathRectTransform 기준)
                    Vector3 localPosition = points[i].position;
                    Vector3 localHandleIn = points[i].handleIn;
                    Vector3 localHandleOut = points[i].handleOut;
                    
                    if (_pathRectTransform != null && _rectTransform != null)
                    {
                        // pathRectTransform 기준 로컬 좌표를 월드 좌표로 변환
                        Vector3 worldPosFromPath = _pathRectTransform.TransformPoint(localPosition);
                        Vector3 worldHandleInFromPath = _pathRectTransform.TransformPoint(localHandleIn);
                        Vector3 worldHandleOutFromPath = _pathRectTransform.TransformPoint(localHandleOut);
                        
                        // 월드 좌표를 BezierFollower의 RectTransform 기준 로컬 좌표로 변환
                        worldPos = _rectTransform.InverseTransformPoint(worldPosFromPath);
                        worldHandleIn = _rectTransform.InverseTransformPoint(worldHandleInFromPath);
                        worldHandleOut = _rectTransform.InverseTransformPoint(worldHandleOutFromPath);
                    }
                    else if (_rectTransform != null)
                    {
                        // pathRectTransform이 없으면 자기 자신의 RectTransform 기준
                        worldPos = localPosition;
                        worldHandleIn = localHandleIn;
                        worldHandleOut = localHandleOut;
                    }
                    else
                    {
                        // RectTransform이 없으면 그대로 사용
                        worldPos = localPosition;
                        worldHandleIn = localHandleIn;
                        worldHandleOut = localHandleOut;
                    }
                }
            }
            else if (_useSavedTransform)
            {
                // ScriptableObject의 Transform 정보 사용 (월드 모드)
                worldPos = _savedTransformMatrix.MultiplyPoint3x4(points[i].position);
                worldHandleIn = _savedTransformMatrix.MultiplyPoint3x4(points[i].handleIn);
                worldHandleOut = _savedTransformMatrix.MultiplyPoint3x4(points[i].handleOut);
            }
            else
            {
                // pathTransform 사용 (월드 모드)
                worldPos = _cachedPathTransform.TransformPoint(points[i].position);
                worldHandleIn = _cachedPathTransform.TransformPoint(points[i].handleIn);
                worldHandleOut = _cachedPathTransform.TransformPoint(points[i].handleOut);
            }
            
            _cachedWorldData[i] = new CachedPointData
            {
                position = worldPos,
                handleIn = worldHandleIn,
                handleOut = worldHandleOut
            };
        }

        _transformDirty = false;
    }
    
    /// <summary>
    /// UI 좌표로 변환 (RectTransform 기준)
    /// localPoint는 BezierPath의 RectTransform 기준 로컬 좌표
    /// 이를 BezierFollower의 RectTransform 기준 좌표로 변환
    /// </summary>
    private Vector3 TransformUIPoint(Vector3 localPoint, RectTransform pathRectTransform)
    {
        if (pathRectTransform == null) return localPoint;
        
        // pathRectTransform이 있으면: pathRectTransform의 로컬 좌표를 월드 좌표로 변환 후
        // BezierFollower의 RectTransform 기준 로컬 좌표로 변환
        if (_rectTransform != null)
        {
            // 1. pathRectTransform 기준 로컬 좌표를 월드 좌표로 변환
            Vector3 worldPos = pathRectTransform.TransformPoint(localPoint);
            
            // 2. 월드 좌표를 BezierFollower의 RectTransform 기준 로컬 좌표로 변환
            Vector3 localPos = _rectTransform.InverseTransformPoint(worldPos);
            
            return localPos;
        }
        
        // BezierFollower에 RectTransform이 없으면 월드 좌표로 변환만 수행
        return pathRectTransform.TransformPoint(localPoint);
    }

    /// <summary>
    /// t(0~1)에 따른 곡선 상의 좌표를 반환합니다. (월드 또는 UI 좌표)
    /// </summary>
    public Vector3 GetPointAt(float t)
    {
        if (pathData == null || !pathData.IsValid())
        {
            if (_isUIMode && _rectTransform != null)
                return _rectTransform.anchoredPosition;
            return transform.position;
        }

        // 프로퍼티 접근 최적화: 한 번만 호출
        var points = pathData.Points;
        int pointCount = points.Count;
        bool isLoop = pathData.IsLoop;
        
        if (_transformDirty || _cachedWorldData.Count != pointCount)
        {
            UpdateWorldCache();
        }

        int numSegments = isLoop ? pointCount : pointCount - 1;
        if (numSegments <= 0)
        {
            if (_isUIMode && _rectTransform != null)
                return _rectTransform.anchoredPosition;
            return transform.position;
        }

        float segmentT = t * numSegments;
        int i = Mathf.FloorToInt(segmentT);

        if (i >= numSegments)
        {
            i = numSegments - 1;
            segmentT = 1f;
        }
        else
        {
            segmentT -= i;
        }

        return GetCubicBezierPoint(i, segmentT, points, isLoop);
    }

    /// <summary>
    /// 특정 세그먼트(i)와 로컬 t값으로 3차 베지어 좌표 계산 (캐시 사용)
    /// </summary>
    private Vector3 GetCubicBezierPoint(int i, float t, List<BezierPoint> sourcePoints, bool sourceIsLoop)
    {
        int nextIndex = (sourceIsLoop && i == sourcePoints.Count - 1) ? 0 : i + 1;

        // 캐시에서 월드 좌표 가져오기 (캐시가 없으면 즉시 계산)
        CachedPointData p0Data, p1Data;
        
        if (!_cachedWorldData.TryGetValue(i, out p0Data))
        {
            if (_isUIMode)
            {
                BezierPoint p0 = sourcePoints[i];
                
                // ScriptableObject의 scale 적용
                Vector3 scaledPosition = p0.position;
                Vector3 scaledHandleIn = p0.handleIn;
                Vector3 scaledHandleOut = p0.handleOut;
                if (_useSavedTransform && pathData != null)
                {
                    Vector3 scale = pathData.SavedScale;
                    scaledPosition = Vector3.Scale(scaledPosition, scale);
                    scaledHandleIn = Vector3.Scale(scaledHandleIn, scale);
                    scaledHandleOut = Vector3.Scale(scaledHandleOut, scale);
                }
                
                // pathRectTransform 기준 로컬 좌표를 월드 좌표로 변환
                Vector3 worldPos = _pathRectTransform.TransformPoint(scaledPosition);
                Vector3 worldHandleIn = _pathRectTransform.TransformPoint(scaledHandleIn);
                Vector3 worldHandleOut = _pathRectTransform.TransformPoint(scaledHandleOut);
                
                // 월드 좌표를 BezierFollower의 RectTransform 기준 로컬 좌표로 변환
                p0Data = new CachedPointData
                {
                    position = _rectTransform.InverseTransformPoint(worldPos),
                    handleIn = _rectTransform.InverseTransformPoint(worldHandleIn),
                    handleOut = _rectTransform.InverseTransformPoint(worldHandleOut)
                };
            }
            else if (_isUIMode && _rectTransform != null)
            {
                // pathRectTransform이 없으면 자기 자신의 RectTransform 기준 (동일한 RectTransform)
                BezierPoint p0 = sourcePoints[i];
                
                // ScriptableObject의 scale 적용
                Vector3 scaledPosition = p0.position;
                Vector3 scaledHandleIn = p0.handleIn;
                Vector3 scaledHandleOut = p0.handleOut;
                if (_useSavedTransform && pathData != null)
                {
                    Vector3 scale = pathData.SavedScale;
                    scaledPosition = Vector3.Scale(scaledPosition, scale);
                    scaledHandleIn = Vector3.Scale(scaledHandleIn, scale);
                    scaledHandleOut = Vector3.Scale(scaledHandleOut, scale);
                }
                
                p0Data = new CachedPointData
                {
                    position = scaledPosition,
                    handleIn = scaledHandleIn,
                    handleOut = scaledHandleOut
                };
            }
            else if (!_useSavedTransform)
            {
                if (_cachedPathTransform == null)
                {
                    InitializeTransform();
                }
                BezierPoint p0 = sourcePoints[i];
                p0Data = new CachedPointData
                {
                    position = _cachedPathTransform.TransformPoint(p0.position),
                    handleIn = _cachedPathTransform.TransformPoint(p0.handleIn),
                    handleOut = _cachedPathTransform.TransformPoint(p0.handleOut)
                };
            }
            else
            {
                UpdateSavedTransformMatrix();
                BezierPoint p0 = sourcePoints[i];
                p0Data = new CachedPointData
                {
                    position = _savedTransformMatrix.MultiplyPoint3x4(p0.position),
                    handleIn = _savedTransformMatrix.MultiplyPoint3x4(p0.handleIn),
                    handleOut = _savedTransformMatrix.MultiplyPoint3x4(p0.handleOut)
                };
            }
        }
        
        if (!_cachedWorldData.TryGetValue(nextIndex, out p1Data))
        {
            if (_isUIMode && _pathRectTransform != null && _rectTransform != null)
            {
                BezierPoint p1 = sourcePoints[nextIndex];
                
                // ScriptableObject의 scale 적용
                Vector3 scaledPosition = p1.position;
                Vector3 scaledHandleIn = p1.handleIn;
                Vector3 scaledHandleOut = p1.handleOut;
                if (_useSavedTransform && pathData != null)
                {
                    Vector3 scale = pathData.SavedScale;
                    scaledPosition = Vector3.Scale(scaledPosition, scale);
                    scaledHandleIn = Vector3.Scale(scaledHandleIn, scale);
                    scaledHandleOut = Vector3.Scale(scaledHandleOut, scale);
                }
                
                // pathRectTransform 기준 로컬 좌표를 월드 좌표로 변환
                Vector3 worldPos = _pathRectTransform.TransformPoint(scaledPosition);
                Vector3 worldHandleIn = _pathRectTransform.TransformPoint(scaledHandleIn);
                Vector3 worldHandleOut = _pathRectTransform.TransformPoint(scaledHandleOut);
                
                // 월드 좌표를 BezierFollower의 RectTransform 기준 로컬 좌표로 변환
                p1Data = new CachedPointData
                {
                    position = _rectTransform.InverseTransformPoint(worldPos),
                    handleIn = _rectTransform.InverseTransformPoint(worldHandleIn),
                    handleOut = _rectTransform.InverseTransformPoint(worldHandleOut)
                };
            }
            else if (_isUIMode && _rectTransform != null)
            {
                // pathRectTransform이 없으면 자기 자신의 RectTransform 기준 (동일한 RectTransform)
                BezierPoint p1 = sourcePoints[nextIndex];
                
                // ScriptableObject의 scale 적용
                Vector3 scaledPosition = p1.position;
                Vector3 scaledHandleIn = p1.handleIn;
                Vector3 scaledHandleOut = p1.handleOut;
                if (_useSavedTransform && pathData != null)
                {
                    Vector3 scale = pathData.SavedScale;
                    scaledPosition = Vector3.Scale(scaledPosition, scale);
                    scaledHandleIn = Vector3.Scale(scaledHandleIn, scale);
                    scaledHandleOut = Vector3.Scale(scaledHandleOut, scale);
                }
                
                p1Data = new CachedPointData
                {
                    position = scaledPosition,
                    handleIn = scaledHandleIn,
                    handleOut = scaledHandleOut
                };
            }
            else if (!_useSavedTransform)
            {
                if (_cachedPathTransform == null)
                {
                    InitializeTransform();
                }
                BezierPoint p1 = sourcePoints[nextIndex];
                p1Data = new CachedPointData
                {
                    position = _cachedPathTransform.TransformPoint(p1.position),
                    handleIn = _cachedPathTransform.TransformPoint(p1.handleIn),
                    handleOut = _cachedPathTransform.TransformPoint(p1.handleOut)
                };
            }
            else
            {
                UpdateSavedTransformMatrix();
                BezierPoint p1 = sourcePoints[nextIndex];
                p1Data = new CachedPointData
                {
                    position = _savedTransformMatrix.MultiplyPoint3x4(p1.position),
                    handleIn = _savedTransformMatrix.MultiplyPoint3x4(p1.handleIn),
                    handleOut = _savedTransformMatrix.MultiplyPoint3x4(p1.handleOut)
                };
            }
        }

        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        // B(t) = (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3
        Vector3 p = uuu * p0Data.position;
        p += 3f * uu * t * p0Data.handleOut;
        p += 3f * u * tt * p1Data.handleIn;
        p += ttt * p1Data.position;

        return p;
    }

    /// <summary>
    /// 곡선 상의 특정 위치에서의 이동 방향(접선)을 반환합니다.
    /// </summary>
    public Vector3 GetDirectionAt(float t)
    {
        if (pathData == null || !pathData.IsValid())
            return Vector3.right;

        // 프로퍼티 접근 최적화: 한 번만 호출
        var points = pathData.Points;
        int pointCount = points.Count;
        bool isLoop = pathData.IsLoop;
        
        if (_transformDirty || _cachedWorldData.Count != pointCount)
        {
            UpdateWorldCache();
        }

        int numSegments = isLoop ? pointCount : pointCount - 1;
        if (numSegments <= 0) return Vector3.right;

        float segmentT = t * numSegments;
        int i = Mathf.FloorToInt(segmentT);

        if (i >= numSegments)
        {
            i = numSegments - 1;
            segmentT = 1f;
        }
        else
        {
            segmentT -= i;
        }

        return GetCubicBezierTangent(i, segmentT, points, isLoop);
    }

    /// <summary>
    /// 베지어 곡선의 미분 공식을 사용한 접선 계산 (최적화: 캐시 사용)
    /// </summary>
    private Vector3 GetCubicBezierTangent(int i, float t, List<BezierPoint> sourcePoints, bool sourceIsLoop)
    {
        int nextIndex = (sourceIsLoop && i == sourcePoints.Count - 1) ? 0 : i + 1;

        // 캐시에서 월드 좌표 가져오기 (캐시가 없으면 즉시 계산)
        CachedPointData p0Data, p1Data;
        
        if (!_cachedWorldData.TryGetValue(i, out p0Data))
        {
            if (_isUIMode)
            {
                // UI 모드: ScriptableObject의 scale만 적용 (position, rotation은 무시)
                BezierPoint p0 = sourcePoints[i];
                
                if (_useSavedTransform && pathData != null)
                {
                    // ScriptableObject의 scale만 적용
                    Vector3 scale = pathData.SavedScale;
                    Vector3 scaledPos = Vector3.Scale(p0.position, scale);
                    Vector3 scaledHandleIn = Vector3.Scale(p0.handleIn, scale);
                    Vector3 scaledHandleOut = Vector3.Scale(p0.handleOut, scale);
                    
                    // scale이 적용된 로컬 좌표를 그대로 사용 (UI 모드에서는 로컬 좌표 기준)
                    p0Data = new CachedPointData
                    {
                        position = scaledPos,
                        handleIn = scaledHandleIn,
                        handleOut = scaledHandleOut
                    };
                }
                else
                {
                    // ScriptableObject가 없으면 기존 로직 사용 (pathRectTransform 기준)
                    if (_pathRectTransform != null && _rectTransform != null)
                    {
                        // pathRectTransform 기준 로컬 좌표를 월드 좌표로 변환
                        Vector3 worldPos = _pathRectTransform.TransformPoint(p0.position);
                        Vector3 worldHandleIn = _pathRectTransform.TransformPoint(p0.handleIn);
                        Vector3 worldHandleOut = _pathRectTransform.TransformPoint(p0.handleOut);
                        
                        // 월드 좌표를 BezierFollower의 RectTransform 기준 로컬 좌표로 변환
                        p0Data = new CachedPointData
                        {
                            position = _rectTransform.InverseTransformPoint(worldPos),
                            handleIn = _rectTransform.InverseTransformPoint(worldHandleIn),
                            handleOut = _rectTransform.InverseTransformPoint(worldHandleOut)
                        };
                    }
                    else if (_rectTransform != null)
                    {
                        // pathRectTransform이 없으면 자기 자신의 RectTransform 기준
                        p0Data = new CachedPointData
                        {
                            position = p0.position,
                            handleIn = p0.handleIn,
                            handleOut = p0.handleOut
                        };
                    }
                    else
                    {
                        // RectTransform이 없으면 그대로 사용
                        p0Data = new CachedPointData
                        {
                            position = p0.position,
                            handleIn = p0.handleIn,
                            handleOut = p0.handleOut
                        };
                    }
                }
            }
            else if (!_useSavedTransform)
            {
                if (_cachedPathTransform == null)
                {
                    InitializeTransform();
                }
                BezierPoint p0 = sourcePoints[i];
                p0Data = new CachedPointData
                {
                    position = _cachedPathTransform.TransformPoint(p0.position),
                    handleIn = _cachedPathTransform.TransformPoint(p0.handleIn),
                    handleOut = _cachedPathTransform.TransformPoint(p0.handleOut)
                };
            }
            else
            {
                UpdateSavedTransformMatrix();
                BezierPoint p0 = sourcePoints[i];
                p0Data = new CachedPointData
                {
                    position = _savedTransformMatrix.MultiplyPoint3x4(p0.position),
                    handleIn = _savedTransformMatrix.MultiplyPoint3x4(p0.handleIn),
                    handleOut = _savedTransformMatrix.MultiplyPoint3x4(p0.handleOut)
                };
            }
        }
        
        if (!_cachedWorldData.TryGetValue(nextIndex, out p1Data))
        {
            if (_isUIMode)
            {
                // UI 모드: ScriptableObject의 scale만 적용 (position, rotation은 무시)
                BezierPoint p1 = sourcePoints[nextIndex];
                
                if (_useSavedTransform && pathData != null)
                {
                    // ScriptableObject의 scale만 적용
                    Vector3 scale = pathData.SavedScale;
                    Vector3 scaledPos = Vector3.Scale(p1.position, scale);
                    Vector3 scaledHandleIn = Vector3.Scale(p1.handleIn, scale);
                    Vector3 scaledHandleOut = Vector3.Scale(p1.handleOut, scale);
                    
                    // scale이 적용된 로컬 좌표를 그대로 사용 (UI 모드에서는 로컬 좌표 기준)
                    p1Data = new CachedPointData
                    {
                        position = scaledPos,
                        handleIn = scaledHandleIn,
                        handleOut = scaledHandleOut
                    };
                }
                else
                {
                    // ScriptableObject가 없으면 기존 로직 사용 (pathRectTransform 기준)
                    if (_pathRectTransform != null && _rectTransform != null)
                    {
                        // pathRectTransform 기준 로컬 좌표를 월드 좌표로 변환
                        Vector3 worldPos = _pathRectTransform.TransformPoint(p1.position);
                        Vector3 worldHandleIn = _pathRectTransform.TransformPoint(p1.handleIn);
                        Vector3 worldHandleOut = _pathRectTransform.TransformPoint(p1.handleOut);
                        
                        // 월드 좌표를 BezierFollower의 RectTransform 기준 로컬 좌표로 변환
                        p1Data = new CachedPointData
                        {
                            position = _rectTransform.InverseTransformPoint(worldPos),
                            handleIn = _rectTransform.InverseTransformPoint(worldHandleIn),
                            handleOut = _rectTransform.InverseTransformPoint(worldHandleOut)
                        };
                    }
                    else if (_rectTransform != null)
                    {
                        // pathRectTransform이 없으면 자기 자신의 RectTransform 기준
                        p1Data = new CachedPointData
                        {
                            position = p1.position,
                            handleIn = p1.handleIn,
                            handleOut = p1.handleOut
                        };
                    }
                    else
                    {
                        // RectTransform이 없으면 그대로 사용
                        p1Data = new CachedPointData
                        {
                            position = p1.position,
                            handleIn = p1.handleIn,
                            handleOut = p1.handleOut
                        };
                    }
                }
            }
            else if (!_useSavedTransform)
            {
                if (_cachedPathTransform == null)
                {
                    InitializeTransform();
                }
                BezierPoint p1 = sourcePoints[nextIndex];
                p1Data = new CachedPointData
                {
                    position = _cachedPathTransform.TransformPoint(p1.position),
                    handleIn = _cachedPathTransform.TransformPoint(p1.handleIn),
                    handleOut = _cachedPathTransform.TransformPoint(p1.handleOut)
                };
            }
            else
            {
                UpdateSavedTransformMatrix();
                BezierPoint p1 = sourcePoints[nextIndex];
                p1Data = new CachedPointData
                {
                    position = _savedTransformMatrix.MultiplyPoint3x4(p1.position),
                    handleIn = _savedTransformMatrix.MultiplyPoint3x4(p1.handleIn),
                    handleOut = _savedTransformMatrix.MultiplyPoint3x4(p1.handleOut)
                };
            }
        }

        float u = 1f - t;
        float uu = u * u;
        float tt = t * t;

        // B'(t) = -3(1-t)^2 P0 + 3(1-t)(1-3t) P1 + 3t(2-3t) P2 + 3t^2 P3
        Vector3 tangent = -3f * uu * p0Data.position;
        tangent += 3f * uu * p0Data.handleOut;
        tangent -= 6f * u * t * p0Data.handleOut;
        tangent += 6f * u * t * p1Data.handleIn;
        tangent -= 3f * tt * p1Data.handleIn;
        tangent += 3f * tt * p1Data.position;

        Vector3 normalized = tangent.normalized;
        return normalized != Vector3.zero ? normalized : Vector3.right;
    }

#if UNITY_EDITOR
    public void StartTest(float testDurationSeconds = 10f)
    {
        isTestMode = true;
        testDuration = testDurationSeconds;
        testStartTime = (float)EditorApplication.timeSinceStartup;
        _timer = 0f;
        _isForward = true;
        isPlaying = true;
        _lastEditorUpdateTime = testStartTime;
        UpdatePosition();
    }
    
    public void StopTest()
    {
        isTestMode = false;
        isPlaying = false;
        _timer = 0f;
        _isForward = true;
        UpdatePosition();
    }
#endif

    public void UpdatePosition()
    {
        if (pathData == null) return;

        // Animation Curve를 통한 Easing 적용
        float baseProgress = movementCurve.Evaluate(_timer);
        
        // 시작점 오프셋 적용 (루프 활성화 시 래핑 처리)
        float rawProgress = baseProgress + startOffset;
        bool isLoop = pathData.IsLoop;

        if (isLoop && rawProgress >= 1f)
        {
            progress = rawProgress % 1f; // 루프 래핑
        }
        else
        {
            progress = Mathf.Clamp01(rawProgress); // 일반 클램프
        }

        // 위치 업데이트
        Vector3 targetPos = GetPointAt(progress);
        
        if (_isUIMode && _rectTransform != null)
        {
            // UI 모드: RectTransform.anchoredPosition 사용
            _rectTransform.anchoredPosition = new Vector2(targetPos.x, targetPos.y);
        }
        else
        {
            // 월드 모드: Transform.position 사용
        transform.position = targetPos;
        }

        // 회전 업데이트 (2D 기준 Z축 회전) - followRotation이 활성화된 경우에만 (성능 최적화)
        if (followRotation)
        {
            Vector3 dir = GetDirectionAt(progress);
            if (dir.sqrMagnitude > 0.0001f) // Vector3.zero 체크보다 sqrMagnitude가 더 빠름
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                if (_isUIMode && _rectTransform != null)
                {
                    // UI 모드: RectTransform.rotation 사용
                    _rectTransform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                }
                else
                {
                    // 월드 모드: Transform.rotation 사용
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }
}
    }
    
    /// <summary>
    /// 씬뷰에 ScriptableObject의 경로를 녹색으로 그리기 (에디터 전용)
    /// </summary>
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (pathData == null || !pathData.IsValid())
            return;
        
        // Transform 정보 확인
        if (!_useSavedTransform && _cachedPathTransform == null)
        {
            InitializeTransform();
        }
        
        if (_useSavedTransform)
        {
            UpdateSavedTransformMatrix();
        }
        
        var points = pathData.Points;
        bool isLoop = pathData.IsLoop;
        
        if (points == null || points.Count < 2)
            return;
        
        // 녹색으로 경로 그리기
        Gizmos.color = Color.green;
        
        // 각 세그먼트를 여러 점으로 샘플링하여 그리기
        int numSegments = isLoop ? points.Count : points.Count - 1;
        const int samplesPerSegment = 20; // 세그먼트당 샘플링 점 개수
        
        for (int i = 0; i < numSegments; i++)
        {
            int nextIndex = (isLoop && i == points.Count - 1) ? 0 : i + 1;
            
            BezierPoint p0 = points[i];
            BezierPoint p1 = points[nextIndex];
            
            // 월드 좌표로 변환 (BezierFollower가 실제 이동할 경로)
            Vector3 p0Pos, p0Out, p1In, p1Pos;
            
            if (_isUIMode)
            {
                // UI 모드: ScriptableObject의 points를 그대로 사용 (이미 UI 좌표계 로컬 좌표)
                if (_useSavedTransform && pathData != null)
                {
                    // ScriptableObject에 저장된 points는 이미 UI 좌표계 로컬 좌표이므로 그대로 사용
                    // ScriptableObject의 Transform 정보를 적용하여 월드 좌표로 변환 (Gizmos 그리기용)
                    Matrix4x4 uiTransformMatrix = Matrix4x4.TRS(
                        pathData.SavedPosition,
                        pathData.SavedRotation,
                        pathData.SavedScale
                    );
                    
                    // UI 로컬 좌표에 Transform 적용
                    Vector3 transformedP0Pos = uiTransformMatrix.MultiplyPoint3x4(p0.position);
                    Vector3 transformedP0Out = uiTransformMatrix.MultiplyPoint3x4(p0.handleOut);
                    Vector3 transformedP1In = uiTransformMatrix.MultiplyPoint3x4(p1.handleIn);
                    Vector3 transformedP1Pos = uiTransformMatrix.MultiplyPoint3x4(p1.position);
                    
                    // Canvas의 root Canvas를 찾아 월드 좌표로 변환
                    if (_canvas != null)
                    {
                        RectTransform canvasRectTransform = _canvas.GetComponent<RectTransform>();
                        if (canvasRectTransform != null)
                        {
                            // Canvas의 root Canvas 찾기
                            Canvas rootCanvas = _canvas.rootCanvas;
                            if (rootCanvas != null)
                            {
                                RectTransform rootCanvasRect = rootCanvas.GetComponent<RectTransform>();
                                if (rootCanvasRect != null)
                                {
                                    // UI 좌표를 월드 좌표로 변환
                                    p0Pos = rootCanvasRect.TransformPoint(transformedP0Pos);
                                    p0Out = rootCanvasRect.TransformPoint(transformedP0Out);
                                    p1In = rootCanvasRect.TransformPoint(transformedP1In);
                                    p1Pos = rootCanvasRect.TransformPoint(transformedP1Pos);
                                }
                                else
                                {
                                    // rootCanvas RectTransform이 없으면 그대로 사용
                                    p0Pos = transformedP0Pos;
                                    p0Out = transformedP0Out;
                                    p1In = transformedP1In;
                                    p1Pos = transformedP1Pos;
                                }
                            }
                            else
                            {
                                // rootCanvas가 없으면 canvasRectTransform 사용
                                p0Pos = canvasRectTransform.TransformPoint(transformedP0Pos);
                                p0Out = canvasRectTransform.TransformPoint(transformedP0Out);
                                p1In = canvasRectTransform.TransformPoint(transformedP1In);
                                p1Pos = canvasRectTransform.TransformPoint(transformedP1Pos);
                            }
                        }
                        else
                        {
                            // Canvas RectTransform이 없으면 그대로 사용
                            p0Pos = transformedP0Pos;
                            p0Out = transformedP0Out;
                            p1In = transformedP1In;
                            p1Pos = transformedP1Pos;
                        }
                    }
                    else
                    {
                        // Canvas가 없으면 그대로 사용
                        p0Pos = transformedP0Pos;
                        p0Out = transformedP0Out;
                        p1In = transformedP1In;
                        p1Pos = transformedP1Pos;
                    }
                }
                else
                {
                    // ScriptableObject가 없으면 기존 로직 사용 (pathRectTransform 기준)
                    Vector3 localP0Pos = p0.position;
                    Vector3 localP0Out = p0.handleOut;
                    Vector3 localP1In = p1.handleIn;
                    Vector3 localP1Pos = p1.position;
                    
                    if (_pathRectTransform != null && _rectTransform != null)
                    {
                        // pathRectTransform 기준 로컬 좌표를 월드 좌표로 변환
                        Vector3 worldP0Pos = _pathRectTransform.TransformPoint(localP0Pos);
                        Vector3 worldP0Out = _pathRectTransform.TransformPoint(localP0Out);
                        Vector3 worldP1In = _pathRectTransform.TransformPoint(localP1In);
                        Vector3 worldP1Pos = _pathRectTransform.TransformPoint(localP1Pos);
                        
                        // 월드 좌표를 BezierFollower의 RectTransform 기준 로컬 좌표로 변환
                        Vector3 localP0PosInFollower = _rectTransform.InverseTransformPoint(worldP0Pos);
                        Vector3 localP0OutInFollower = _rectTransform.InverseTransformPoint(worldP0Out);
                        Vector3 localP1InInFollower = _rectTransform.InverseTransformPoint(worldP1In);
                        Vector3 localP1PosInFollower = _rectTransform.InverseTransformPoint(worldP1Pos);
                        
                        // BezierFollower의 RectTransform 기준 로컬 좌표를 다시 월드 좌표로 변환 (Gizmos 그리기용)
                        p0Pos = _rectTransform.TransformPoint(localP0PosInFollower);
                        p0Out = _rectTransform.TransformPoint(localP0OutInFollower);
                        p1In = _rectTransform.TransformPoint(localP1InInFollower);
                        p1Pos = _rectTransform.TransformPoint(localP1PosInFollower);
                    }
                    else if (_rectTransform != null)
                    {
                        // pathRectTransform이 없으면 자기 자신의 RectTransform 사용
                        p0Pos = _rectTransform.TransformPoint(localP0Pos);
                        p0Out = _rectTransform.TransformPoint(localP0Out);
                        p1In = _rectTransform.TransformPoint(localP1In);
                        p1Pos = _rectTransform.TransformPoint(localP1Pos);
                    }
                    else
                    {
                        // RectTransform이 없으면 그대로 사용 (월드 좌표로 간주)
                        p0Pos = localP0Pos;
                        p0Out = localP0Out;
                        p1In = localP1In;
                        p1Pos = localP1Pos;
                    }
                }
            }
            else if (_useSavedTransform)
            {
                p0Pos = _savedTransformMatrix.MultiplyPoint3x4(p0.position);
                p0Out = _savedTransformMatrix.MultiplyPoint3x4(p0.handleOut);
                p1In = _savedTransformMatrix.MultiplyPoint3x4(p1.handleIn);
                p1Pos = _savedTransformMatrix.MultiplyPoint3x4(p1.position);
            }
            else
            {
                if (_cachedPathTransform == null)
                {
                    InitializeTransform();
                }
                p0Pos = _cachedPathTransform.TransformPoint(p0.position);
                p0Out = _cachedPathTransform.TransformPoint(p0.handleOut);
                p1In = _cachedPathTransform.TransformPoint(p1.handleIn);
                p1Pos = _cachedPathTransform.TransformPoint(p1.position);
            }
            
            // 베지어 곡선을 여러 점으로 샘플링하여 그리기
            Vector3 prevPoint = p0Pos;
            for (int j = 1; j <= samplesPerSegment; j++)
            {
                float t = j / (float)samplesPerSegment;
                
                // 베지어 곡선 공식: B(t) = (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3
                float u = 1f - t;
                float tt = t * t;
                float uu = u * u;
                float uuu = uu * u;
                float ttt = tt * t;
                
                Vector3 currentPoint = uuu * p0Pos;
                currentPoint += 3f * uu * t * p0Out;
                currentPoint += 3f * u * tt * p1In;
                currentPoint += ttt * p1Pos;
                
                Gizmos.DrawLine(prevPoint, currentPoint);
                prevPoint = currentPoint;
            }
        }
    }
#endif
}
