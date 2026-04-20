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
    /// 경로를 독립적인 타이밍으로 따라가는 에이전트 데이터.
    /// AddAgent()로 등록하면 startTime이 현재 시간으로 설정되어 독립 진행된다.
    /// </summary>
    [System.Serializable]
    public class PathFollowerAgent
    {
        /// <summary>경로를 따라 이동할 대상 Transform</summary>
        public Transform target;
        /// <summary>등록 시점의 Time.time (직렬화 제외, 런타임 전용)</summary>
        [System.NonSerialized] public float startTime;
        /// <summary>현재 진행도 0~1 (직렬화 제외, 읽기 전용)</summary>
        [System.NonSerialized] public float progress;
    }

    /// <summary>
    /// PathFollower 경로 데이터의 스냅샷.
    /// 여러 경로 형태를 리스트로 저장하고 SwitchToSnapshot으로 전환한다.
    /// </summary>
    [System.Serializable]
    public class PathSnapshot
    {
        /// <summary>스냅샷 이름 (에디터 표시용)</summary>
        public string name = "Snapshot";
        /// <summary>저장된 포인트 목록 (path 좌표 기준)</summary>
        [SerializeField] public List<PathPoint> points = new List<PathPoint>();
        /// <summary>저장된 루프 여부</summary>
        [SerializeField] public bool isLoop = false;
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

        [Tooltip("경로 포인트 목록 (부모 Transform 기준 로컬 좌표)")]
        [SerializeField] private List<PathPoint> _points = new List<PathPoint>();

        [Tooltip("경로 루프 여부 (마지막 포인트에서 첫 번째 포인트로 연결)")]
        [SerializeField] private bool _isLoop = false;

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

        [Tooltip("현재 경로 진행도 (0~1). 읽기 전용으로 상태 확인용")]
        [Range(0f, 1f)] public float progress = 0f;

        [Tooltip("재생 중 여부. false로 설정하면 일시 정지")]
        public bool isPlaying = true;

        // ── 에이전트 ──────────────────────────────────────
        [Tooltip("독립 타이밍으로 경로를 따르는 에이전트 목록 (직렬화: target 참조만 저장)")]
        [SerializeField] private List<PathFollowerAgent> _agents = new List<PathFollowerAgent>();

        // ── 스냅샷 ────────────────────────────────────────
        [Tooltip("저장된 경로 스냅샷 목록")]
        [SerializeField] private List<PathSnapshot> _snapshots = new List<PathSnapshot>();

        [Tooltip("현재 적용된 스냅샷 인덱스 (-1 = 스냅샷 없음)")]
        [SerializeField] private int _currentSnapshotIndex = -1;

        [Tooltip("SwitchToSnapshot 호출 시 모핑 시간 (초). 0이면 즉시 전환")]
        public float morphingDuration = 0.5f;

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
        /// <summary>PathToWorld/WorldToPath에서 transform.parent 반복 접근 방지 (모바일 최적화)</summary>
        private Transform _cachedParent;

        // 모핑 상태 (직렬화 제외, 런타임 전용)
        private bool _isMorphing = false;
        private float _morphTimer = 0f;
        private float _morphDuration = 0f;
        private List<PathPoint> _morphFrom;
        private List<PathPoint> _morphTo;
        private bool _morphTargetLoop;

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
            _cachedParent = transform.parent;

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

            // ── 모핑 처리 ──────────────────────────────────
            if (_isMorphing)
            {
                _morphTimer += Time.deltaTime;
                float mt = Mathf.Clamp01(_morphTimer / _morphDuration);

                // _morphFrom 과 _morphTo 사이 선형 보간
                for (int i = 0; i < _points.Count; i++)
                {
                    _points[i].position  = Vector3.Lerp(_morphFrom[i].position,  _morphTo[i].position,  mt);
                    _points[i].handleIn  = Vector3.Lerp(_morphFrom[i].handleIn,  _morphTo[i].handleIn,  mt);
                    _points[i].handleOut = Vector3.Lerp(_morphFrom[i].handleOut, _morphTo[i].handleOut, mt);
                }
                MarkDirty();

                if (mt >= 1f)
                {
                    _isMorphing = false;
                    _isLoop = _morphTargetLoop;
                    MarkDirty();
                }
            }

            // ── 에이전트 이동 ───────────────────────────────
            UpdateAgents();

            // ── 자신의 이동 ──────────────────────────────────
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
            if (_cachedParent == null) _cachedParent = transform.parent;
            return _cachedParent != null ? _cachedParent.TransformPoint(pathPos) : pathPos;
        }

        /// <summary>
        /// 월드 좌표를 path 좌표(부모 로컬)로 변환한다.
        /// 부모가 없으면 path 좌표 = 월드 좌표.
        /// </summary>
        public Vector3 WorldToPath(Vector3 worldPos)
        {
            if (_cachedParent == null) _cachedParent = transform.parent;
            return _cachedParent != null ? _cachedParent.InverseTransformPoint(worldPos) : worldPos;
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
                MarkDirty();
            }
        }

        /// <summary>
        /// 경로 데이터가 변경될 때마다 증가하는 버전 번호.
        /// PathRibbon 등 외부 구독자가 단순 int 비교로 변경 감지 가능(GC 없음).
        /// </summary>
        public int PathVersion { get; private set; }

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
        /// <remarks>모바일: 매 호출 시 힙 할당이 발생합니다. Update 등 반복 경로에서는 GetPointWorldPosition(int) 또는 위치만 필요 시 GetPointPositionLocal(int) 사용 권장.</remarks>
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

        /// <summary>지정 인덱스 포인트의 월드 좌표를 반환한다. 할당 없음 (모바일 권장).</summary>
        /// <param name="index">조회할 포인트 인덱스</param>
        public Vector3 GetPointWorldPosition(int index)
        {
            if (_points == null || index < 0 || index >= _points.Count) return transform.position;
            return PathToWorld(_points[index].position);
        }

        /// <summary>지정 인덱스 포인트의 path(로컬) 좌표를 반환한다. 할당 없음 (모바일 권장).</summary>
        /// <param name="index">조회할 포인트 인덱스</param>
        public Vector3 GetPointPositionLocal(int index)
        {
            if (_points == null || index < 0 || index >= _points.Count) return Vector3.zero;
            return _points[index].position;
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
        /// 현재 오브젝트 위치를 중심으로 정다각형 경로를 생성한다.
        /// cornerRoundness 0=날카로운 모서리, 1=원형에 가까운 둥근 모서리.
        /// </summary>
        /// <param name="sides">변 수 (최소 3)</param>
        /// <param name="radius">외접원 반지름</param>
        /// <param name="rotation">회전 각도 (도, 기본 0 = 위쪽 꼭짓점부터)</param>
        /// <param name="cornerRoundness">모서리 둥글기 (0~1, 기본 0)</param>
        public void SetPolygon(int sides, float radius, float rotation = 0f, float cornerRoundness = 0f)
        {
            sides            = Mathf.Max(3, sides);
            cornerRoundness  = Mathf.Clamp01(cornerRoundness);
            _points          = new List<PathPoint>(sides);
            _isLoop          = true;

            Vector3 center   = WorldToPath(transform.position);
            float   rotRad   = rotation * Mathf.Deg2Rad;
            // roundness=1일 때 원형 근사에 사용할 최대 핸들 길이
            float   handleLenMax = radius * (4f / 3f) * Mathf.Tan(Mathf.PI / (2f * sides));

            // 각 꼭짓점 위치 사전 계산 (-π/2 오프셋: 위쪽부터 시작)
            var positions = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides + rotRad - Mathf.PI / 2f;
                positions[i] = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);
            }

            for (int i = 0; i < sides; i++)
            {
                int     prevIdx = (i - 1 + sides) % sides;
                int     nextIdx = (i + 1) % sides;
                Vector3 pos     = positions[i];

                // 직선 모서리용 핸들: 인접 꼭짓점 방향으로 1/3 거리 (완전한 직선 세그먼트)
                Vector3 sharpHandleIn  = pos + (positions[prevIdx] - pos) / 3f;
                Vector3 sharpHandleOut = pos + (positions[nextIdx] - pos) / 3f;

                // 원형 근사용 핸들: 접선 방향 (SetCircle과 동일한 방식)
                float   angle   = 2f * Mathf.PI * i / sides + rotRad - Mathf.PI / 2f;
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                Vector3 roundHandleIn  = pos - tangent * handleLenMax;
                Vector3 roundHandleOut = pos + tangent * handleLenMax;

                _points.Add(new PathPoint(pos)
                {
                    handleIn  = Vector3.Lerp(sharpHandleIn,  roundHandleIn,  cornerRoundness),
                    handleOut = Vector3.Lerp(sharpHandleOut, roundHandleOut, cornerRoundness),
                    isBroken  = false,
                });
            }
            MarkDirty();
        }

        /// <summary>
        /// 현재 오브젝트 위치를 중심으로 별모양 경로를 생성한다.
        /// 외부 꼭짓점(outerRadius)과 내부 꼭짓점(innerRadius)이 교대로 배치된다.
        /// </summary>
        /// <param name="points">꼭짓점 수 (별의 뾰족한 끝 개수, 최소 2)</param>
        /// <param name="outerRadius">외부 꼭짓점 반지름</param>
        /// <param name="innerRadius">내부 꼭짓점 반지름</param>
        /// <param name="rotation">회전 각도 (도, 기본 0 = 위쪽 외부 꼭짓점부터)</param>
        public void SetStar(int points, float outerRadius, float innerRadius, float rotation = 0f)
        {
            points      = Mathf.Max(2, points);
            _points     = new List<PathPoint>(points * 2);
            _isLoop     = true;

            Vector3 center     = WorldToPath(transform.position);
            float   rotRad     = rotation * Mathf.Deg2Rad;
            int     totalVerts = points * 2;

            // 각 꼭짓점 위치: 외부/내부 교대 (-π/2 오프셋: 위쪽부터 시작)
            var positions = new Vector3[totalVerts];
            for (int i = 0; i < totalVerts; i++)
            {
                float angle = 2f * Mathf.PI * i / totalVerts + rotRad - Mathf.PI / 2f;
                float r     = (i % 2 == 0) ? outerRadius : innerRadius;
                positions[i] = center + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
            }

            for (int i = 0; i < totalVerts; i++)
            {
                int     prevIdx = (i - 1 + totalVerts) % totalVerts;
                int     nextIdx = (i + 1) % totalVerts;
                Vector3 pos     = positions[i];

                // 날카로운 모서리: 인접 꼭짓점 방향으로 1/3 거리 (직선 세그먼트)
                _points.Add(new PathPoint(pos)
                {
                    handleIn  = pos + (positions[prevIdx] - pos) / 3f,
                    handleOut = pos + (positions[nextIdx] - pos) / 3f,
                    isBroken  = false,
                });
            }
            MarkDirty();
        }

        /// <summary>
        /// 지정된 정점들을 무게중심 기준으로 회전한다.
        /// 스냅샷용으로 원형/다각형/별 생성 후 Start Point 정렬에 유용하다.
        /// </summary>
        /// <param name="angleDegrees">회전 각도 (도, 양수=반시계방향)</param>
        /// <param name="indices">회전할 정점 인덱스 (null 또는 빈 목록이면 전체)</param>
        public void RotatePath(float angleDegrees, System.Collections.Generic.IList<int> indices = null)
        {
            if (_points == null || _points.Count < 2) return;

            var list = GetIndicesOrAll(indices);
            if (list == null || list.Count == 0) return;

            Vector3 centroid = ComputeCentroid(list);
            float rad = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            foreach (int i in list)
            {
                // 정점: 무게중심 기준 회전
                Vector3 dPos = _points[i].position - centroid;
                _points[i].position = centroid + new Vector3(dPos.x * cos - dPos.y * sin, dPos.x * sin + dPos.y * cos, dPos.z);

                // 핸들도 같은 중심·각도로 회전 (꼬임 방지)
                Vector3 dIn = _points[i].handleIn - centroid;
                _points[i].handleIn = centroid + new Vector3(dIn.x * cos - dIn.y * sin, dIn.x * sin + dIn.y * cos, dIn.z);
                Vector3 dOut = _points[i].handleOut - centroid;
                _points[i].handleOut = centroid + new Vector3(dOut.x * cos - dOut.y * sin, dOut.x * sin + dOut.y * cos, dOut.z);
            }
            MarkDirty();
        }

        /// <summary>
        /// 지정된 정점들을 무게중심 기준으로 스케일한다.
        /// </summary>
        /// <param name="scale">스케일 배율 (1=유지, 2=2배 확대, 0.5=절반)</param>
        /// <param name="indices">스케일할 정점 인덱스 (null 또는 빈 목록이면 전체)</param>
        public void ScalePath(float scale, System.Collections.Generic.IList<int> indices = null)
        {
            if (_points == null || _points.Count < 2) return;
            if (Mathf.Abs(scale - 1f) < 0.0001f) return;

            var list = GetIndicesOrAll(indices);
            if (list == null || list.Count == 0) return;

            Vector3 centroid = ComputeCentroid(list);

            foreach (int i in list)
            {
                // 정점: 무게중심 기준 스케일
                Vector3 dPos = _points[i].position - centroid;
                _points[i].position = centroid + dPos * scale;

                // 핸들도 같은 중심·배율로 스케일 (꼬임 방지)
                Vector3 dIn = _points[i].handleIn - centroid;
                _points[i].handleIn = centroid + dIn * scale;
                Vector3 dOut = _points[i].handleOut - centroid;
                _points[i].handleOut = centroid + dOut * scale;
            }
            MarkDirty();
        }

        private System.Collections.Generic.List<int> GetIndicesOrAll(System.Collections.Generic.IList<int> indices)
        {
            if (indices == null || indices.Count == 0)
            {
                var all = new System.Collections.Generic.List<int>(_points.Count);
                for (int i = 0; i < _points.Count; i++) all.Add(i);
                return all;
            }
            return new System.Collections.Generic.List<int>(indices);
        }

        private Vector3 ComputeCentroid(System.Collections.Generic.List<int> list)
        {
            Vector3 c = Vector3.zero;
            foreach (int i in list) c += _points[i].position;
            return c / list.Count;
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

        #region 공개 API - 에이전트 (독립 타이밍 이동)

        /// <summary>에이전트 수</summary>
        public int AgentCount => _agents != null ? _agents.Count : 0;

        /// <summary>
        /// Transform을 에이전트로 등록한다. 등록 시점을 시작 시간으로 설정하여
        /// 이후 경로를 독립적으로 따라가게 된다.
        /// </summary>
        /// <param name="target">경로를 따라 이동할 Transform</param>
        public void AddAgent(Transform target)
        {
            if (target == null)
            {
                Debug.LogWarning($"[PathFollower] {name}: AddAgent에 null이 전달되었습니다.");
                return;
            }

            if (_agents == null) _agents = new List<PathFollowerAgent>();

            // 이미 등록된 경우 시작 시간만 갱신
            foreach (var a in _agents)
            {
                if (a.target == target)
                {
                    a.startTime = Time.time;
                    return;
                }
            }

            _agents.Add(new PathFollowerAgent
            {
                target    = target,
                startTime = Time.time,
                progress  = 0f,
            });
        }

        /// <summary>에이전트를 목록에서 제거한다.</summary>
        public void RemoveAgent(Transform target)
        {
            if (_agents == null) return;
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                if (_agents[i].target == target)
                {
                    _agents.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>모든 에이전트를 제거한다.</summary>
        public void ClearAgents()
        {
            _agents?.Clear();
        }

        /// <summary>지정 에이전트의 현재 진행도(0~1)를 반환한다. 미등록 시 -1 반환.</summary>
        public float GetAgentProgress(Transform target)
        {
            if (_agents == null) return -1f;
            foreach (var a in _agents)
            {
                if (a.target == target) return a.progress;
            }
            return -1f;
        }

        #endregion

        #region 공개 API - 스냅샷 / 모핑

        /// <summary>현재 저장된 스냅샷 수</summary>
        public int SnapshotCount => _snapshots != null ? _snapshots.Count : 0;

        /// <summary>현재 적용된 스냅샷 인덱스 (-1 = 없음)</summary>
        public int CurrentSnapshotIndex => _currentSnapshotIndex;

        /// <summary>현재 경로(_points)를 스냅샷으로 저장한다.</summary>
        /// <param name="snapshotName">스냅샷 이름 (빈 문자열이면 "Snapshot N" 자동 부여)</param>
        public void SaveAsSnapshot(string snapshotName = "")
        {
            if (_snapshots == null) _snapshots = new List<PathSnapshot>();
            if (string.IsNullOrEmpty(snapshotName))
                snapshotName = $"Snapshot {_snapshots.Count + 1}";

            var snap = new PathSnapshot
            {
                name   = snapshotName,
                isLoop = _isLoop,
                points = new List<PathPoint>(_points.Count),
            };
            foreach (var p in _points) snap.points.Add(p.Clone());
            _snapshots.Add(snap);
        }

        /// <summary>
        /// 지정 인덱스의 스냅샷으로 전환한다.
        /// morphingDuration > 0 이고 포인트 수가 같으면 모핑 애니메이션이 재생된다.
        /// </summary>
        /// <param name="index">전환할 스냅샷 인덱스</param>
        public void SwitchToSnapshot(int index)
        {
            SwitchToSnapshot(index, morphingDuration);
        }

        /// <summary>
        /// 지정 인덱스의 스냅샷으로 전환한다 (모핑 시간 직접 지정).
        /// overrideDuration = 0 이면 즉시 전환, 포인트 수가 다르면 항상 즉시 전환.
        /// </summary>
        /// <param name="index">전환할 스냅샷 인덱스</param>
        /// <param name="overrideDuration">모핑 시간 (초), 0 = 즉시</param>
        public void SwitchToSnapshot(int index, float overrideDuration)
        {
            if (_snapshots == null || index < 0 || index >= _snapshots.Count)
            {
                Debug.LogWarning($"[PathFollower] {name}: 스냅샷 인덱스 {index}가 범위를 벗어났습니다.");
                return;
            }

            var snap = _snapshots[index];
            bool canMorph = overrideDuration > 0f
                         && snap.points.Count == _points.Count
                         && _points.Count >= 2;

            if (canMorph)
            {
                _morphFrom = new List<PathPoint>(_points.Count);
                foreach (var p in _points) _morphFrom.Add(p.Clone());
                _morphTo = new List<PathPoint>(snap.points.Count);
                foreach (var p in snap.points) _morphTo.Add(p.Clone());
                _morphTargetLoop = snap.isLoop;
                _morphDuration   = overrideDuration;
                _morphTimer      = 0f;
                _isMorphing      = true;
            }
            else
            {
                _isMorphing = false;
                _points = new List<PathPoint>(snap.points.Count);
                foreach (var p in snap.points) _points.Add(p.Clone());
                _isLoop = snap.isLoop;
                MarkDirty();
            }

            _currentSnapshotIndex = index;
        }

        /// <summary>현재 경로(_points, _isLoop)로 지정 인덱스의 스냅샷을 덮어쓴다. 이름은 유지.</summary>
        /// <param name="index">갱신할 스냅샷 인덱스</param>
        public void OverwriteSnapshot(int index)
        {
            if (_snapshots == null || index < 0 || index >= _snapshots.Count)
            {
                Debug.LogWarning($"[PathFollower] {name}: 스냅샷 인덱스 {index}가 범위를 벗어났습니다.");
                return;
            }
            if (_points == null || _points.Count < 2)
            {
                Debug.LogWarning($"[PathFollower] {name}: 갱신할 경로 포인트가 2개 미만입니다.");
                return;
            }

            string existingName = _snapshots[index].name;
            _snapshots[index] = new PathSnapshot
            {
                name   = existingName,
                isLoop = _isLoop,
                points = new List<PathPoint>(_points.Count),
            };
            foreach (var p in _points) _snapshots[index].points.Add(p.Clone());
        }

        /// <summary>지정 인덱스의 스냅샷을 삭제한다.</summary>
        public void RemoveSnapshot(int index)
        {
            if (_snapshots == null || index < 0 || index >= _snapshots.Count) return;
            _snapshots.RemoveAt(index);

            // 현재 인덱스 보정
            if (_currentSnapshotIndex >= _snapshots.Count)
                _currentSnapshotIndex = _snapshots.Count - 1;
        }

        /// <summary>지정 인덱스의 스냅샷 데이터를 반환한다 (null = 범위 초과).</summary>
        public PathSnapshot GetSnapshot(int index)
        {
            if (_snapshots == null || index < 0 || index >= _snapshots.Count) return null;
            return _snapshots[index];
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

        /// <summary>에디터에서 모핑 테스트를 시작한다.</summary>
        /// <param name="targetSnapshotIndex">목표 스냅샷 인덱스</param>
        /// <param name="duration">모핑 시간 (초)</param>
        public void EditorStartMorphing(int targetSnapshotIndex, float duration)
        {
            if (_snapshots == null || targetSnapshotIndex < 0 || targetSnapshotIndex >= _snapshots.Count) return;

            var snap = _snapshots[targetSnapshotIndex];
            bool canMorph = duration > 0f && snap.points.Count == _points.Count && _points.Count >= 2;

            if (canMorph)
            {
                _morphFrom = new List<PathPoint>(_points.Count);
                foreach (var p in _points) _morphFrom.Add(p.Clone());
                _morphTo = new List<PathPoint>(snap.points.Count);
                foreach (var p in snap.points) _morphTo.Add(p.Clone());
                _morphTargetLoop = snap.isLoop;
                _morphDuration   = duration;
                _morphTimer      = 0f;
                _isMorphing      = true;
            }
            else
            {
                _isMorphing = false;
                _points = new List<PathPoint>(snap.points.Count);
                foreach (var p in snap.points) _points.Add(p.Clone());
                _isLoop = snap.isLoop;
                MarkDirty();
            }

            _currentSnapshotIndex = targetSnapshotIndex;
            _lastEditorUpdateTime = (float)EditorApplication.timeSinceStartup;
        }

        /// <summary>에디터에서 모핑을 업데이트한다.</summary>
        /// <param name="deltaTime">프레임 간 시간</param>
        public void EditorUpdateMorphing(float deltaTime)
        {
            if (!_isMorphing || _morphFrom == null || _morphTo == null) return;

            _morphTimer += deltaTime;
            float mt = Mathf.Clamp01(_morphTimer / _morphDuration);

            for (int i = 0; i < _points.Count; i++)
            {
                _points[i].position  = Vector3.Lerp(_morphFrom[i].position,  _morphTo[i].position,  mt);
                _points[i].handleIn  = Vector3.Lerp(_morphFrom[i].handleIn,  _morphTo[i].handleIn,  mt);
                _points[i].handleOut = Vector3.Lerp(_morphFrom[i].handleOut, _morphTo[i].handleOut, mt);
            }
            MarkDirty();

            if (mt >= 1f)
            {
                _isMorphing = false;
                _isLoop = _morphTargetLoop;
                MarkDirty();
            }
        }

        /// <summary>에디터에서 모핑을 중지한다.</summary>
        public void EditorStopMorphing()
        {
            _isMorphing = false;
            _morphFrom  = null;
            _morphTo    = null;
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
        /// handleOut은 다음 세그먼트 길이, handleIn은 이전 세그먼트 길이 기준으로
        /// 각각 독립 계산하여 원형 경로에서의 왜곡을 줄인다.
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
                // 중간 정점: 각 세그먼트 길이의 1/3 을 독립적으로 사용 (원형 경로 왜곡 방지)
                float   distPrev = Vector3.Distance(curr, prev);
                float   distNext = Vector3.Distance(curr, next);
                float   outLen   = distNext / 3f;   // handleOut: 다음 세그먼트 길이의 1/3
                float   inLen    = distPrev / 3f;   // handleIn:  이전 세그먼트 길이의 1/3
                Vector3 tangent  = (next - prev).normalized;
                _points[i].handleOut = curr + tangent * outLen;
                _points[i].handleIn  = curr - tangent * inLen;
            }
            _points[i].isBroken = false;
        }

        /// <summary>
        /// 등록된 에이전트를 각자의 시작 시간 기준으로 이동시킨다.
        /// null target은 자동 제거된다.
        /// </summary>
        private void UpdateAgents()
        {
            if (_agents == null || _agents.Count == 0) return;

            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (agent.target == null) { _agents.RemoveAt(i); continue; }

                float elapsed = (Time.time - agent.startTime) / duration;
                float t;

                switch (loopType)
                {
                    case LoopType.Restart:
                        t = elapsed % 1f;
                        break;
                    case LoopType.Yoyo:
                        float cycle = elapsed % 2f;
                        t = cycle < 1f ? cycle : 2f - cycle;
                        break;
                    default: // None
                        t = Mathf.Clamp01(elapsed);
                        break;
                }

                // AnimationCurve + startOffset 적용
                float curved = movementCurve.Evaluate(t);
                float final  = (_isLoop && curved + startOffset >= 1f)
                    ? (curved + startOffset) % 1f
                    : Mathf.Clamp01(curved + startOffset);

                agent.progress = final;
                agent.target.position = GetPointAt(final);
            }
        }

        /// <summary>캐시 무효화 플래그를 세트한다. 포인트 변경 시 호출한다.</summary>
        private void MarkDirty()
        {
            _transformDirty = true;
            // 외부 구독자(PathRibbon 등)가 감지할 수 있도록 버전 증가
            unchecked { PathVersion++; }
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
            Transform parent = transform.parent;
            Matrix4x4 currentParentMatrix = parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;

            if (currentParentMatrix != _cachedParentMatrix)
            {
                _transformDirty = true;
                _cachedParentMatrix = currentParentMatrix;
                _cachedParent = parent;
            }

            if (!_transformDirty && _cachedPointCount == pointCount) return;

            _cachedPointCount = pointCount;
            _cachedParent = parent;

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
