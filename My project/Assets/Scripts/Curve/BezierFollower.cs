using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class BezierFollower : MonoBehaviour
{
    [Header("Path Settings")]
    [Tooltip("BezierPathData ScriptableObject (필수) - 런타임에서 변경 가능")]
    public BezierPathData pathData;
    
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
    private Transform _lastParentTransform; // 부모 Transform 변경 감지용
    private int _lastParentHash = 0; // 부모의 위치/회전 변경 감지용
    
    // ScriptableObject의 Transform 정보를 사용하기 위한 Matrix
    private Matrix4x4 _savedTransformMatrix = Matrix4x4.identity;
    private bool _useSavedTransform = false;
    private BezierPathData _lastPathData; // pathData 변경 감지용
    
    // UI 모드 제거 - World 모드만 사용합니다.

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
    public bool TransformDirty { get => _transformDirty; set => _transformDirty = value; }
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

        // Transform 초기화
        InitializeTransform();

        // pathData 변경 감지 초기화
        _lastPathData = pathData;
    }

    /// <summary>
    /// Transform 초기화 (ScriptableObject의 Transform 정보가 최우선, 없으면 자기 자신의 Transform 사용)
    /// </summary>
    private void InitializeTransform()
    {
        // ScriptableObject의 Transform 정보가 최우선
        if (pathData != null && pathData.IsValid())
        {
            // ScriptableObject가 있으면 ScriptableObject의 Transform 정보 사용
            _useSavedTransform = true;
            UpdateSavedTransformMatrix();
        }
        else
        {
            // ScriptableObject가 없으면 자기 자신의 Transform 사용
            _cachedPathTransform = transform;
            _lastTransformHash = _cachedPathTransform.GetHashCode();
            _useSavedTransform = false;
        }
    }

    /// <summary>
    /// ScriptableObject에 저장된 Transform 정보로 Matrix 업데이트 (부모 기준 로컬 좌표)
    /// </summary>
    private void UpdateSavedTransformMatrix()
    {
        if (pathData == null) return;
        
        // 부모 Transform 가져오기
        Transform parentTransform = transform.parent;
        
        if (parentTransform != null)
        {
            // ScriptableObject의 Transform 정보를 부모 기준 로컬 좌표로 직접 해석
            // (CopyFrom에서 부모가 있으면 로컬 좌표로 저장했으므로 그대로 사용)
            _savedTransformMatrix = Matrix4x4.TRS(
                pathData.SavedPosition,
                pathData.SavedRotation,
                pathData.SavedScale
            );
        }
        else
        {
            // 부모가 없으면 기존 방식 (월드 좌표)
            _savedTransformMatrix = Matrix4x4.TRS(
                pathData.SavedPosition,
                pathData.SavedRotation,
                pathData.SavedScale
            );
        }
    }

    private void Reset()
    {
        UpdatePosition();
    }

    private void OnValidate()
    {
        // pathData가 변경되었을 수 있으므로 재초기화
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
        // ScriptableObject의 Transform 정보를 사용하는 경우
        if (_useSavedTransform)
        {
            // pathData가 변경되었는지 확인하고 재초기화
            if (pathData == null || !pathData.IsValid())
            {
                InitializeTransform();
                _transformDirty = true;
                return;
            }

            // 부모 Transform 변경 감지 (부모가 이동할 때 캐시 무효화)
            Transform currentParent = transform.parent;
            if (currentParent != _lastParentTransform)
            {
                _lastParentTransform = currentParent;
                _lastParentHash = 0; // 부모가 바뀌면 해시 초기화
                _transformDirty = true;
                return;
            }

            // 부모가 있으면 부모의 위치/회전 변경 감지
            if (currentParent != null)
            {
                // 부모의 위치와 회전을 조합한 해시로 변경 감지
                int currentParentHash = currentParent.position.GetHashCode() ^ currentParent.rotation.GetHashCode();
                if (currentParentHash != _lastParentHash)
                {
                    _lastParentHash = currentParentHash;
                    _transformDirty = true;
                }
            }
            return;
        }

        // _cachedPathTransform이 null인지 확인
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
    /// 좌표 캐시 업데이트 (월드 좌표)
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
            Vector3 worldPos;
            Vector3 worldHandleIn;
            Vector3 worldHandleOut;

            if (_useSavedTransform)
            {
                // ScriptableObject의 Transform 정보 사용 (부모 기준 로컬 좌표)
                // 로컬 좌표로 변환된 포인트
                Vector3 localPos = _savedTransformMatrix.MultiplyPoint3x4(points[i].position);
                Vector3 localHandleIn = _savedTransformMatrix.MultiplyPoint3x4(points[i].handleIn);
                Vector3 localHandleOut = _savedTransformMatrix.MultiplyPoint3x4(points[i].handleOut);

                // 부모 Transform을 사용하여 로컬 좌표를 월드 좌표로 변환
                Transform parentTransform = transform.parent;
                if (parentTransform != null)
                {
                    worldPos = parentTransform.TransformPoint(localPos);
                    worldHandleIn = parentTransform.TransformPoint(localHandleIn);
                    worldHandleOut = parentTransform.TransformPoint(localHandleOut);
                }
                else
                {
                    // 부모가 없으면 로컬 좌표를 그대로 사용 (월드 좌표로 간주)
                    worldPos = localPos;
                    worldHandleIn = localHandleIn;
                    worldHandleOut = localHandleOut;
                }
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
    /// t(0~1)에 따른 곡선 상의 좌표를 반환합니다. (월드 좌표)
    /// </summary>
    public Vector3 GetPointAt(float t)
    {
        if (pathData == null || !pathData.IsValid())
        {
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

        // UpdateWorldCache()가 이미 캐시를 생성했다면 사용
        // 캐시가 없으면 UpdateWorldCache() 호출
        if (_transformDirty || _cachedWorldData.Count != sourcePoints.Count)
        {
            UpdateWorldCache();
        }

        CachedPointData p0Data, p1Data;

        if (!_cachedWorldData.TryGetValue(i, out p0Data))
        {
            if (!_useSavedTransform)
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

                // 로컬 좌표로 변환된 포인트
                Vector3 localPos = _savedTransformMatrix.MultiplyPoint3x4(p0.position);
                Vector3 localHandleIn = _savedTransformMatrix.MultiplyPoint3x4(p0.handleIn);
                Vector3 localHandleOut = _savedTransformMatrix.MultiplyPoint3x4(p0.handleOut);

                // 부모 Transform을 사용하여 로컬 좌표를 월드 좌표로 변환
                Transform parentTransform = transform.parent;
                if (parentTransform != null)
                {
                    p0Data = new CachedPointData
                    {
                        position = parentTransform.TransformPoint(localPos),
                        handleIn = parentTransform.TransformPoint(localHandleIn),
                        handleOut = parentTransform.TransformPoint(localHandleOut)
                    };
                }
                else
                {
                    // 부모가 없으면 로컬 좌표를 그대로 사용 (월드 좌표로 간주)
                    p0Data = new CachedPointData
                    {
                        position = localPos,
                        handleIn = localHandleIn,
                        handleOut = localHandleOut
                    };
                }
            }
        }

        if (!_cachedWorldData.TryGetValue(nextIndex, out p1Data))
        {
            if (!_useSavedTransform)
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

                // 로컬 좌표로 변환된 포인트
                Vector3 localPos = _savedTransformMatrix.MultiplyPoint3x4(p1.position);
                Vector3 localHandleIn = _savedTransformMatrix.MultiplyPoint3x4(p1.handleIn);
                Vector3 localHandleOut = _savedTransformMatrix.MultiplyPoint3x4(p1.handleOut);

                // 부모 Transform을 사용하여 로컬 좌표를 월드 좌표로 변환
                Transform parentTransform = transform.parent;
                if (parentTransform != null)
                {
                    p1Data = new CachedPointData
                    {
                        position = parentTransform.TransformPoint(localPos),
                        handleIn = parentTransform.TransformPoint(localHandleIn),
                        handleOut = parentTransform.TransformPoint(localHandleOut)
                    };
                }
                else
                {
                    // 부모가 없으면 로컬 좌표를 그대로 사용 (월드 좌표로 간주)
                    p1Data = new CachedPointData
                    {
                        position = localPos,
                        handleIn = localHandleIn,
                        handleOut = localHandleOut
                    };
                }
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

        // UpdateWorldCache()가 이미 캐시를 생성했다면 사용
        // 캐시가 없으면 UpdateWorldCache() 호출
        if (_transformDirty || _cachedWorldData.Count != sourcePoints.Count)
        {
            UpdateWorldCache();
        }

        CachedPointData p0Data, p1Data;

        if (!_cachedWorldData.TryGetValue(i, out p0Data))
        {
            if (!_useSavedTransform)
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

                // 로컬 좌표로 변환된 포인트
                Vector3 localPos = _savedTransformMatrix.MultiplyPoint3x4(p0.position);
                Vector3 localHandleIn = _savedTransformMatrix.MultiplyPoint3x4(p0.handleIn);
                Vector3 localHandleOut = _savedTransformMatrix.MultiplyPoint3x4(p0.handleOut);

                // 부모 Transform을 사용하여 로컬 좌표를 월드 좌표로 변환
                Transform parentTransform = transform.parent;
                if (parentTransform != null)
                {
                    p0Data = new CachedPointData
                    {
                        position = parentTransform.TransformPoint(localPos),
                        handleIn = parentTransform.TransformPoint(localHandleIn),
                        handleOut = parentTransform.TransformPoint(localHandleOut)
                    };
                }
                else
                {
                    // 부모가 없으면 로컬 좌표를 그대로 사용 (월드 좌표로 간주)
                    p0Data = new CachedPointData
                    {
                        position = localPos,
                        handleIn = localHandleIn,
                        handleOut = localHandleOut
                    };
                }
            }
        }

        if (!_cachedWorldData.TryGetValue(nextIndex, out p1Data))
        {
            if (!_useSavedTransform)
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

                // 로컬 좌표로 변환된 포인트
                Vector3 localPos = _savedTransformMatrix.MultiplyPoint3x4(p1.position);
                Vector3 localHandleIn = _savedTransformMatrix.MultiplyPoint3x4(p1.handleIn);
                Vector3 localHandleOut = _savedTransformMatrix.MultiplyPoint3x4(p1.handleOut);

                // 부모 Transform을 사용하여 로컬 좌표를 월드 좌표로 변환
                Transform parentTransform = transform.parent;
                if (parentTransform != null)
                {
                    p1Data = new CachedPointData
                    {
                        position = parentTransform.TransformPoint(localPos),
                        handleIn = parentTransform.TransformPoint(localHandleIn),
                        handleOut = parentTransform.TransformPoint(localHandleOut)
                    };
                }
                else
                {
                    // 부모가 없으면 로컬 좌표를 그대로 사용 (월드 좌표로 간주)
                    p1Data = new CachedPointData
                    {
                        position = localPos,
                        handleIn = localHandleIn,
                        handleOut = localHandleOut
                    };
                }
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
        transform.position = targetPos;

        // 회전 업데이트 (2D 기준 Z축 회전) - followRotation이 활성화된 경우에만 (성능 최적화)
        if (followRotation)
        {
            Vector3 dir = GetDirectionAt(progress);
            if (dir.sqrMagnitude > 0.0001f) // Vector3.zero 체크보다 sqrMagnitude가 더 빠름
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
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

            if (_useSavedTransform)
            {
                // 로컬 좌표로 변환된 포인트
                Vector3 localP0Pos = _savedTransformMatrix.MultiplyPoint3x4(p0.position);
                Vector3 localP0Out = _savedTransformMatrix.MultiplyPoint3x4(p0.handleOut);
                Vector3 localP1In = _savedTransformMatrix.MultiplyPoint3x4(p1.handleIn);
                Vector3 localP1Pos = _savedTransformMatrix.MultiplyPoint3x4(p1.position);

                // 부모 Transform을 사용하여 로컬 좌표를 월드 좌표로 변환
                Transform parentTransform = transform.parent;
                if (parentTransform != null)
                {
                    p0Pos = parentTransform.TransformPoint(localP0Pos);
                    p0Out = parentTransform.TransformPoint(localP0Out);
                    p1In = parentTransform.TransformPoint(localP1In);
                    p1Pos = parentTransform.TransformPoint(localP1Pos);
                }
                else
                {
                    // 부모가 없으면 로컬 좌표를 그대로 사용 (월드 좌표로 간주)
                    p0Pos = localP0Pos;
                    p0Out = localP0Out;
                    p1In = localP1In;
                    p1Pos = localP1Pos;
                }
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

