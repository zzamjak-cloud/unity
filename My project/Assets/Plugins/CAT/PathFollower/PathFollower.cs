using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.Utility
{
    /// <summary>
    /// 런타임에서 자유롭게 수정 가능한 베지어 경로 추종 컴포넌트.
    /// ScriptableObject 없이 컴포넌트 자체에 경로 데이터를 저장한다.
    /// </summary>
    [System.Serializable]
    public class PathPoint
    {
        /// <summary>부모 Transform 기준 로컬 좌표 정점 위치</summary>
        public Vector3 position;
        /// <summary>들어오는 핸들 (부모 기준 로컬 좌표)</summary>
        public Vector3 handleIn;
        /// <summary>나가는 핸들 (부모 기준 로컬 좌표)</summary>
        public Vector3 handleOut;
        /// <summary>핸들 연결 끊기 여부 (Break 기능)</summary>
        public bool isBroken;

        public PathPoint()
        {
            position = Vector3.zero;
            handleIn = Vector3.left;
            handleOut = Vector3.right;
            isBroken = false;
        }

        public PathPoint(Vector3 pathPos)
        {
            position = pathPos;
            handleIn = pathPos + Vector3.left;
            handleOut = pathPos + Vector3.right;
            isBroken = false;
        }

        /// <summary>핸들 거리를 지정하여 초기화</summary>
        public PathPoint(Vector3 pathPos, float handleDistance)
        {
            position = pathPos;
            handleIn = pathPos + Vector3.left * handleDistance;
            handleOut = pathPos + Vector3.right * handleDistance;
            isBroken = false;
        }

        public PathPoint Clone()
        {
            return new PathPoint
            {
                position = position,
                handleIn = handleIn,
                handleOut = handleOut,
                isBroken = isBroken
            };
        }
    }

    /// <summary>
    /// 베지어 곡선 경로를 따라 이동하는 컴포넌트.
    /// 경로 데이터는 컴포넌트 자체에 저장되므로 런타임에서 자유롭게 수정 가능하다.
    ///
    /// [좌표계 규칙]
    /// - PathPoint의 모든 좌표는 부모(transform.parent) 기준 로컬 좌표로 저장된다.
    /// - 부모가 없으면 월드 좌표와 동일하다.
    /// - PathToWorld() / WorldToPath() 로 변환한다.
    /// - 오브젝트 자신이 이동해도 경로는 고정된다 (부모가 이동하면 경로도 함께 이동).
    /// </summary>
    public class PathFollower : MonoBehaviour
    {
        #region 열거형

        public enum LoopType
        {
            /// <summary>경로 끝에 도달하면 정지</summary>
            None,
            /// <summary>경로 끝에 도달하면 처음부터 다시 시작</summary>
            Restart,
            /// <summary>경로 끝에 도달하면 역방향으로 이동</summary>
            Yoyo
        }

        #endregion

        #region 직렬화 필드

        [Header("Path Settings")]
        [Tooltip("경로 포인트 목록 (부모 Transform 기준 로컬 좌표)")]
        [SerializeField] private List<PathPoint> _points = new List<PathPoint>();

        [Tooltip("경로 루프 여부 (마지막 포인트에서 첫 번째 포인트로 연결)")]
        [SerializeField] private bool _isLoop = false;

        [Header("Movement Settings")]
        [Tooltip("경로 전체를 이동하는 데 걸리는 시간 (초)")]
        public float duration = 5f;

        [Tooltip("이동 이징 커브 (0→1 진행에 따른 실제 위치 비율)")]
        public AnimationCurve movementCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("시작점 오프셋 (0~1). 0이면 처음부터, 0.5면 중간부터 시작")]
        [Range(0f, 1f)] public float startOffset = 0f;

        [Tooltip("이동 방향에 따라 오브젝트를 회전할지 여부")]
        public bool followRotation = false;

        [Tooltip("루프 방식 설정")]
        public LoopType loopType = LoopType.Restart;

        [Header("State")]
        [Tooltip("현재 경로 진행도 (0~1). 읽기 전용으로 상태 확인용")]
        [Range(0f, 1f)] public float progress = 0f;

        [Tooltip("재생 중 여부. false로 설정하면 일시 정지")]
        public bool isPlaying = true;

        #endregion

        #region 이벤트

        /// <summary>LoopType.None 일 때 경로 끝에 도달하면 호출</summary>
        public System.Action OnComplete;

        /// <summary>루프가 한 바퀴 완료될 때마다 호출 (Restart: 처음으로, Yoyo: 방향 반전 시)</summary>
        public System.Action OnLoop;

        #endregion

        #region 내부 상태

        private float _timer = 0f;
        private bool _isForward = true;

        // 월드 좌표 캐시 (배열 기반, 포인트 수 변경 시 재할당)
        private Vector3[] _cachedWorldPositions;
        private Vector3[] _cachedWorldHandlesIn;
        private Vector3[] _cachedWorldHandlesOut;
        private bool _transformDirty = true;
        private int _cachedPointCount = 0;

        // 부모 Transform 변경 감지용 행렬 캐시
        private Matrix4x4 _cachedParentMatrix;

        #endregion

        #region 에디터 전용 필드

#if UNITY_EDITOR
        [HideInInspector] public float _lastEditorUpdateTime = 0f;
        [HideInInspector] public bool isTestMode = false;
        [HideInInspector] public float testStartTime = 0f;
        [HideInInspector] public float testDuration = 10f;

        /// <summary>에디터에서 타이머 접근용 프로퍼티</summary>
        public float EditorTimer { get => _timer; set => _timer = value; }
        /// <summary>에디터에서 방향 접근용 프로퍼티</summary>
        public bool EditorIsForward { get => _isForward; set => _isForward = value; }
        /// <summary>에디터에서 캐시 무효화용 프로퍼티</summary>
        public bool EditorTransformDirty { set => _transformDirty = value; }
        /// <summary>에디터에서 루프 설정 접근용 프로퍼티</summary>
        public bool IsLoop { get => _isLoop; set => _isLoop = value; }
        /// <summary>에디터에서 포인트 리스트 직접 접근 (에디터 편집용)</summary>
        public List<PathPoint> EditorPoints => _points;

        /// <summary>
        /// 에디터 전용: 드 카스텔조 분할 후 계산된 PathPoint를 지정 인덱스에 직접 삽입한다.
        /// 일반 InsertPoint와 달리 핸들 값을 그대로 사용하며 월드 좌표 변환을 하지 않는다.
        /// </summary>
        public void EditorInsertPoint(int index, PathPoint pointInPathCoords)
        {
            if (_points == null) _points = new List<PathPoint>();
            index = Mathf.Clamp(index, 0, _points.Count);
            _points.Insert(index, pointInPathCoords);
            MarkDirty();
        }
#endif

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            // 포인트가 없으면 기본값으로 초기화
            if (_points == null || _points.Count < 2)
            {
                ResetToDefault();
            }

            _transformDirty = true;
        }

        private void Reset()
        {
            ResetToDefault();
            UpdatePosition();
        }

        private void OnValidate()
        {
            _transformDirty = true;
            if (_points != null && _points.Count >= 2)
            {
                UpdatePosition();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (!isPlaying || _points == null || _points.Count < 2) return;

            // 시간 진행 계산
            if (_isForward) _timer += Time.deltaTime / duration;
            else _timer -= Time.deltaTime / duration;

            // 루프 및 경계 처리
            HandleTimerBounds();

            UpdatePosition();
        }

        #endregion

        #region 좌표 변환 API

        /// <summary>
        /// path 좌표(부모 로컬)를 월드 좌표로 변환한다.
        /// 부모가 없으면 월드 좌표 = path 좌표.
        /// </summary>
        public Vector3 PathToWorld(Vector3 pathPos)
        {
            return transform.parent != null
                ? transform.parent.TransformPoint(pathPos)
                : pathPos;
        }

        /// <summary>
        /// 월드 좌표를 path 좌표(부모 로컬)로 변환한다.
        /// 부모가 없으면 path 좌표 = 월드 좌표.
        /// </summary>
        public Vector3 WorldToPath(Vector3 worldPos)
        {
            return transform.parent != null
                ? transform.parent.InverseTransformPoint(worldPos)
                : worldPos;
        }

        #endregion

        #region 공개 API - 이동 제어

        /// <summary>경로 이동을 시작(재개)한다.</summary>
        public void Play()
        {
            isPlaying = true;
        }

        /// <summary>경로 이동을 일시 정지한다.</summary>
        public void Pause()
        {
            isPlaying = false;
        }

        /// <summary>경로 이동을 정지하고 처음으로 되돌린다.</summary>
        public void Stop()
        {
            isPlaying = false;
            _timer = 0f;
            _isForward = true;
            UpdatePosition();
        }

        /// <summary>진행도를 직접 지정한다 (0~1).</summary>
        /// <param name="t">진행도 값 (0: 시작, 1: 끝)</param>
        public void SetProgress(float t)
        {
            _timer = Mathf.Clamp01(t);
            UpdatePosition();
        }

        #endregion

        #region 공개 API - 경로 조작

        /// <summary>현재 포인트 개수</summary>
        public int PointCount => _points != null ? _points.Count : 0;

        /// <summary>경로 루프 여부</summary>
        public bool IsLoopEnabled
        {
            get => _isLoop;
            set
            {
                _isLoop = value;
                _transformDirty = true;
            }
        }

        /// <summary>포인트 리스트를 복사본으로 교체한다.</summary>
        /// <param name="points">새 포인트 리스트 (path 좌표 기준)</param>
        public void SetPoints(List<PathPoint> points)
        {
            if (points == null)
            {
                Debug.LogWarning($"[PathFollower] {name}: SetPoints에 null이 전달되었습니다.");
                return;
            }

            _points = new List<PathPoint>(points.Count);
            foreach (var p in points)
            {
                _points.Add(p.Clone());
            }
            MarkDirty();
        }

        /// <summary>월드 좌표로 포인트를 경로 끝에 추가한다.</summary>
        /// <param name="worldPosition">추가할 포인트의 월드 좌표</param>
        public void AddPoint(Vector3 worldPosition)
        {
            if (_points == null) _points = new List<PathPoint>();

            Vector3 pathPos = WorldToPath(worldPosition);
            _points.Add(new PathPoint(pathPos));
            MarkDirty();
        }

        /// <summary>월드 좌표로 포인트를 지정 인덱스에 삽입한다.</summary>
        /// <param name="index">삽입할 인덱스 위치</param>
        /// <param name="worldPosition">삽입할 포인트의 월드 좌표</param>
        public void InsertPoint(int index, Vector3 worldPosition)
        {
            if (_points == null) _points = new List<PathPoint>();

            index = Mathf.Clamp(index, 0, _points.Count);
            Vector3 pathPos = WorldToPath(worldPosition);
            _points.Insert(index, new PathPoint(pathPos));
            MarkDirty();
        }

        /// <summary>지정 인덱스의 포인트를 제거한다.</summary>
        /// <param name="index">제거할 포인트 인덱스</param>
        public void RemovePoint(int index)
        {
            if (_points == null || index < 0 || index >= _points.Count) return;
            if (_points.Count <= 2)
            {
                Debug.LogWarning($"[PathFollower] {name}: 포인트는 최소 2개 이상이어야 합니다.");
                return;
            }

            _points.RemoveAt(index);
            MarkDirty();
        }

        /// <summary>모든 포인트를 제거하고 기본값으로 초기화한다.</summary>
        public void ClearPoints()
        {
            ResetToDefault();
            MarkDirty();
        }

        /// <summary>지정 인덱스의 포인트를 반환한다 (복사본).</summary>
        /// <param name="index">조회할 포인트 인덱스</param>
        /// <returns>PathPoint 복사본 (path 좌표 기준). 인덱스 범위 초과 시 null</returns>
        public PathPoint GetPoint(int index)
        {
            if (_points == null || index < 0 || index >= _points.Count) return null;
            return _points[index].Clone();
        }

        /// <summary>지정 인덱스의 포인트 데이터를 교체한다.</summary>
        /// <param name="index">교체할 포인트 인덱스</param>
        /// <param name="point">새 포인트 데이터 (path 좌표 기준)</param>
        public void SetPoint(int index, PathPoint point)
        {
            if (_points == null || index < 0 || index >= _points.Count) return;
            _points[index] = point.Clone();
            MarkDirty();
        }

        /// <summary>지정 인덱스 포인트의 위치를 월드 좌표로 설정한다 (핸들 오프셋 유지).</summary>
        /// <param name="index">변경할 포인트 인덱스</param>
        /// <param name="worldPosition">새 위치 (월드 좌표)</param>
        public void SetPointPosition(int index, Vector3 worldPosition)
        {
            if (_points == null || index < 0 || index >= _points.Count) return;

            Vector3 newPathPos = WorldToPath(worldPosition);
            Vector3 delta = newPathPos - _points[index].position;

            // 포인트와 핸들을 함께 이동 (오프셋 유지)
            _points[index].position = newPathPos;
            _points[index].handleIn += delta;
            _points[index].handleOut += delta;
            MarkDirty();
        }

        /// <summary>지정 인덱스 포인트의 월드 좌표를 반환한다.</summary>
        /// <param name="index">조회할 포인트 인덱스</param>
        public Vector3 GetPointWorldPosition(int index)
        {
            if (_points == null || index < 0 || index >= _points.Count) return transform.position;
            return PathToWorld(_points[index].position);
        }

        #endregion

        #region 공개 API - 경로 계산

        /// <summary>t(0~1)에 따른 곡선 상의 월드 좌표를 반환한다.</summary>
        /// <param name="t">경로 진행도 (0: 시작, 1: 끝)</param>
        public Vector3 GetPointAt(float t)
        {
            if (_points == null || _points.Count < 2) return transform.position;

            RefreshCacheIfNeeded();

            int numSegments = _isLoop ? _cachedPointCount : _cachedPointCount - 1;
            if (numSegments <= 0) return transform.position;

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

            int nextIndex = (_isLoop && i == _cachedPointCount - 1) ? 0 : i + 1;
            return EvaluateCubicBezier(i, nextIndex, segmentT);
        }

        /// <summary>t(0~1)에 따른 곡선 상의 이동 방향(접선 벡터)을 반환한다.</summary>
        /// <param name="t">경로 진행도 (0: 시작, 1: 끝)</param>
        public Vector3 GetDirectionAt(float t)
        {
            if (_points == null || _points.Count < 2) return Vector3.right;

            RefreshCacheIfNeeded();

            int numSegments = _isLoop ? _cachedPointCount : _cachedPointCount - 1;
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

            int nextIndex = (_isLoop && i == _cachedPointCount - 1) ? 0 : i + 1;
            Vector3 tangent = EvaluateCubicBezierTangent(i, nextIndex, segmentT);

            return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.right;
        }

        #endregion

        #region 공개 API - 경로 생성/편집 도구

        /// <summary>
        /// 현재 오브젝트 위치를 중심으로 정원(正圓) 경로를 생성한다.
        /// IsLoop를 true로 설정하고 기존 포인트를 모두 교체한다.
        /// </summary>
        /// <param name="radius">반지름 (path 좌표 기준)</param>
        /// <param name="segments">정점 수 (최소 3, 기본 4)</param>
        public void SetCircle(float radius, int segments = 4)
        {
            segments = Mathf.Max(3, segments);
            _points  = new List<PathPoint>(segments);
            _isLoop  = true;

            // 현재 오브젝트 위치를 원의 중심으로 사용
            Vector3 center    = WorldToPath(transform.position);
            // 큐빅 베지어로 원을 근사하는 핸들 길이: R * (4/3) * tan(π / (2N))
            float   handleLen = radius * (4f / 3f) * Mathf.Tan(Mathf.PI / (2f * segments));

            for (int i = 0; i < segments; i++)
            {
                float   angle   = 2f * Mathf.PI * i / segments;
                Vector3 pos     = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                // 접선 방향 (원주 위의 각 점에서 CCW 방향)
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);

                _points.Add(new PathPoint(pos)
                {
                    handleIn  = pos - tangent * handleLen,
                    handleOut = pos + tangent * handleLen,
                    isBroken  = false,
                });
            }
            MarkDirty();
        }

        /// <summary>
        /// 모든 정점을 경로 중심에서 법선(Normal) 방향으로 이동하여 경로를 확대/축소한다.
        /// </summary>
        /// <param name="amount">이동량 (양수: 확대, 음수: 축소)</param>
        public void ExpandPath(float amount)
        {
            if (_points == null || _points.Count < 2) return;

            // 모든 정점의 무게중심 계산
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < _points.Count; i++)
                centroid += _points[i].position;
            centroid /= _points.Count;

            for (int i = 0; i < _points.Count; i++)
            {
                Vector3 dir = _points[i].position - centroid;
                if (dir.sqrMagnitude < 1e-6f) continue; // 중심과 겹치는 정점은 스킵

                Vector3 delta = dir.normalized * amount;
                _points[i].position  += delta;
                _points[i].handleIn  += delta;
                _points[i].handleOut += delta;
            }
            MarkDirty();
        }

        /// <summary>
        /// Catmull-Rom 방식으로 모든 정점의 핸들을 자동 조정하여 곡선을 균일하게 만든다.
        /// </summary>
        public void RelaxPath()
        {
            if (_points == null || _points.Count < 2) return;
            for (int i = 0; i < _points.Count; i++)
                RelaxPointInternal(i);
            MarkDirty();
        }

        /// <summary>
        /// 지정 인덱스의 정점 핸들을 이웃 정점 기반으로 자동 조정한다.
        /// 핸들이 없거나 찌그러진 경우 우클릭 메뉴에서 호출한다.
        /// </summary>
        public void RelaxPoint(int index)
        {
            if (_points == null || index < 0 || index >= _points.Count) return;
            RelaxPointInternal(index);
            MarkDirty();
        }

        #endregion

        #region 에디터 테스트 지원

#if UNITY_EDITOR
        /// <summary>에디터 테스트를 시작한다.</summary>
        public void StartEditorTest(float testDurationSeconds = 10f)
        {
            isTestMode = true;
            testDuration = testDurationSeconds;
            testStartTime = (float)EditorApplication.timeSinceStartup;
            _timer = 0f;
            _isForward = true;
            isPlaying = true;
            _lastEditorUpdateTime = testStartTime;
            _transformDirty = true;
            UpdatePosition();
        }

        /// <summary>에디터 테스트를 중지한다.</summary>
        public void StopEditorTest()
        {
            isTestMode = false;
            isPlaying = false;
            _timer = 0f;
            _isForward = true;
            _transformDirty = true;
            UpdatePosition();
        }
#endif

        #endregion

        #region 내부 메서드

        /// <summary>
        /// 경로를 기본값 (포인트 2개)으로 초기화한다.
        /// 현재 오브젝트 위치를 기준으로 path 좌표계에 포인트를 배치한다.
        /// </summary>
        private void ResetToDefault()
        {
            if (_points == null) _points = new List<PathPoint>(4);
            else _points.Clear();

            // 현재 오브젝트 위치를 path 좌표계(부모 로컬)로 변환하여 초기 포인트 위치 결정
            Vector3 start = WorldToPath(transform.position);
            _points.Add(new PathPoint(start));
            _points.Add(new PathPoint(start + Vector3.right * 5f));
        }

        /// <summary>
        /// Catmull-Rom 방식으로 단일 정점의 핸들을 이웃 정점 기반으로 계산한다.
        /// 끝점(비루프)은 인접 방향으로 1/3 거리에 핸들을 배치한다.
        /// </summary>
        private void RelaxPointInternal(int i)
        {
            int count   = _points.Count;
            int prevIdx = _isLoop ? (i - 1 + count) % count : Mathf.Max(0, i - 1);
            int nextIdx = _isLoop ? (i + 1) % count         : Mathf.Min(count - 1, i + 1);

            Vector3 curr = _points[i].position;
            Vector3 prev = _points[prevIdx].position;
            Vector3 next = _points[nextIdx].position;

            if (i == 0 && !_isLoop)
            {
                // 첫 정점: 다음 방향으로 1/3 지점에 handleOut 배치
                _points[i].handleOut = Vector3.Lerp(curr, next, 1f / 3f);
                _points[i].handleIn  = curr - (_points[i].handleOut - curr);
            }
            else if (i == count - 1 && !_isLoop)
            {
                // 마지막 정점: 이전 방향으로 1/3 지점에 handleIn 배치
                _points[i].handleIn  = Vector3.Lerp(curr, prev, 1f / 3f);
                _points[i].handleOut = curr - (_points[i].handleIn - curr);
            }
            else
            {
                // 중간 정점: 양쪽 이웃 사이의 접선 방향으로 핸들 설정
                float   scale   = Mathf.Min(Vector3.Distance(curr, prev), Vector3.Distance(curr, next)) / 3f;
                Vector3 tangent = (next - prev).normalized;
                _points[i].handleOut = curr + tangent * scale;
                _points[i].handleIn  = curr - tangent * scale;
            }
            _points[i].isBroken = false;
        }

        /// <summary>캐시 무효화 플래그를 세트한다. 포인트 변경 시 호출한다.</summary>
        private void MarkDirty()
        {
            _transformDirty = true;
        }

        /// <summary>타이머 경계값 처리 및 루프 이벤트 발생</summary>
        private void HandleTimerBounds()
        {
            if (_timer >= 1f)
            {
                switch (loopType)
                {
                    case LoopType.Restart:
                        _timer = 0f;
                        OnLoop?.Invoke();
                        break;
                    case LoopType.Yoyo:
                        _timer = 1f;
                        _isForward = false;
                        OnLoop?.Invoke();
                        break;
                    default: // None
                        _timer = 1f;
                        isPlaying = false;
                        OnComplete?.Invoke();
                        break;
                }
            }
            else if (_timer <= 0f && loopType == LoopType.Yoyo)
            {
                _timer = 0f;
                _isForward = true;
                OnLoop?.Invoke();
            }
        }

        /// <summary>
        /// 월드 좌표 캐시를 갱신한다.
        /// 부모 Transform이 변경되었거나 포인트 데이터가 바뀐 경우에만 재계산한다.
        /// </summary>
        private void RefreshCacheIfNeeded()
        {
            int pointCount = _points != null ? _points.Count : 0;

            // 부모 Transform 변경 감지 (부모가 이동/회전/스케일 변경 시 캐시 무효화)
            Matrix4x4 currentParentMatrix = transform.parent != null
                ? transform.parent.localToWorldMatrix
                : Matrix4x4.identity;

            if (currentParentMatrix != _cachedParentMatrix)
            {
                _transformDirty = true;
                _cachedParentMatrix = currentParentMatrix;
            }

            if (!_transformDirty && _cachedPointCount == pointCount) return;

            _cachedPointCount = pointCount;

            // 배열 재할당 (포인트 수 변경 시에만)
            if (_cachedWorldPositions == null || _cachedWorldPositions.Length != pointCount)
            {
                _cachedWorldPositions = new Vector3[pointCount];
                _cachedWorldHandlesIn = new Vector3[pointCount];
                _cachedWorldHandlesOut = new Vector3[pointCount];
            }

            // path 좌표(부모 로컬) → 월드 좌표 변환
            for (int i = 0; i < pointCount; i++)
            {
                _cachedWorldPositions[i] = PathToWorld(_points[i].position);
                _cachedWorldHandlesIn[i]  = PathToWorld(_points[i].handleIn);
                _cachedWorldHandlesOut[i] = PathToWorld(_points[i].handleOut);
            }

            _transformDirty = false;
        }

        /// <summary>3차 베지어 곡선 위치 계산 (캐시 사용)</summary>
        private Vector3 EvaluateCubicBezier(int i, int nextIndex, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            // B(t) = (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3
            Vector3 p = uuu * _cachedWorldPositions[i];
            p += 3f * uu * t * _cachedWorldHandlesOut[i];
            p += 3f * u * tt * _cachedWorldHandlesIn[nextIndex];
            p += ttt * _cachedWorldPositions[nextIndex];
            return p;
        }

        /// <summary>3차 베지어 곡선 접선 계산 (캐시 사용)</summary>
        private Vector3 EvaluateCubicBezierTangent(int i, int nextIndex, float t)
        {
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;

            // B'(t) = -3(1-t)^2 P0 + 3(1-t)(1-3t) handleOut + 3t(2-3t) handleIn + 3t^2 P3
            Vector3 tangent = -3f * uu * _cachedWorldPositions[i];
            tangent += 3f * uu * _cachedWorldHandlesOut[i];
            tangent -= 6f * u * t * _cachedWorldHandlesOut[i];
            tangent += 6f * u * t * _cachedWorldHandlesIn[nextIndex];
            tangent -= 3f * tt * _cachedWorldHandlesIn[nextIndex];
            tangent += 3f * tt * _cachedWorldPositions[nextIndex];
            return tangent;
        }

        /// <summary>현재 진행도에 따라 오브젝트 위치/회전을 업데이트한다.</summary>
        public void UpdatePosition()
        {
            if (_points == null || _points.Count < 2) return;

            // AnimationCurve 이징 적용
            float baseProgress = movementCurve.Evaluate(_timer);

            // 시작점 오프셋 적용
            float rawProgress = baseProgress + startOffset;
            if (_isLoop && rawProgress >= 1f)
                progress = rawProgress % 1f;
            else
                progress = Mathf.Clamp01(rawProgress);

            // 위치 업데이트
            transform.position = GetPointAt(progress);

            // 회전 업데이트 (2D 기준 Z축 회전)
            if (followRotation)
            {
                Vector3 dir = GetDirectionAt(progress);
                if (dir.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                }
            }
        }

        #endregion

        #region 기즈모

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_points == null || _points.Count < 2) return;

            int numSegments = _isLoop ? _points.Count : _points.Count - 1;
            const int samplesPerSegment = 20;

            Gizmos.color = Color.green;

            for (int i = 0; i < numSegments; i++)
            {
                int nextIndex = (_isLoop && i == _points.Count - 1) ? 0 : i + 1;

                Vector3 p0Pos = PathToWorld(_points[i].position);
                Vector3 p0Out = PathToWorld(_points[i].handleOut);
                Vector3 p1In  = PathToWorld(_points[nextIndex].handleIn);
                Vector3 p1Pos = PathToWorld(_points[nextIndex].position);

                Vector3 prevPoint = p0Pos;
                for (int j = 1; j <= samplesPerSegment; j++)
                {
                    float t = j / (float)samplesPerSegment;
                    float u = 1f - t;
                    float tt = t * t;
                    float uu = u * u;
                    float uuu = uu * u;
                    float ttt = tt * t;

                    Vector3 point = uuu * p0Pos;
                    point += 3f * uu * t * p0Out;
                    point += 3f * u * tt * p1In;
                    point += ttt * p1Pos;

                    Gizmos.DrawLine(prevPoint, point);
                    prevPoint = point;
                }
            }
        }
#endif

        #endregion
    }
}
