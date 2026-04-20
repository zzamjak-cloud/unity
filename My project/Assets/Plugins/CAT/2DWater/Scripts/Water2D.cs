using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.Water2D
{
    /// <summary>
    /// Splash 이벤트. (world position, 적용된 force)
    /// </summary>
    [System.Serializable]
    public class Water2DSplashEvent : UnityEvent<Vector2, float> { }

    /// <summary>
    /// 버텍스 기반 2D 물 시뮬레이션. 가로로 배치된 표면 포인트를 스프링처럼 거동시키고
    /// 좌·우 이웃으로 파동을 전파한다. 상단은 웨이브, 하단은 고정된 사각형 quad strip.
    ///
    /// [렌더링]
    /// - World-space MeshRenderer 방식. Material 은 사용자가 인스펙터에서 지정한다.
    ///   지정하지 않으면 기본 MeshRenderer 의 Missing Material(분홍) 으로 표시됨.
    ///
    /// [상호작용]
    /// - BoxCollider2D(Trigger) 가 자동 구성된다.
    /// - Rigidbody2D 가 부착된 Collider2D 가 진입하면 속도·질량 기반 impulse 가
    ///   해당 x 좌표의 포인트에 주입된다.
    ///
    /// [공개 API]
    /// - Splash(localX, force): 외부 스크립트에서 임의 위치에 힘 주입
    /// - OnSplash UnityEvent: splash 발생 시 파티클 등 훅 연결
    ///
    /// [모바일 최적화]
    /// - Mesh 는 OnEnable 에서 1회 생성 (HideFlags.DontSave)
    /// - 정점/삼각형 배열은 pointCount 변경 시에만 재할당
    /// - 시뮬레이션은 고정 스텝 1/60s 어큐뮬레이터로 프레임레이트 독립
    /// - Update 에서 new/LINQ 금지
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("CAT/Effects/2D Water")]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D))]
    public class Water2D : MonoBehaviour
    {
        #region 직렬화 필드

        [SerializeField, Tooltip("물 본체 가로 폭")]
        private float _width = 4f;

        [SerializeField, Tooltip("물 본체 세로 깊이 (상단=0, 하단=-depth)")]
        private float _depth = 2f;

        [SerializeField, Range(8, 128), Tooltip("표면 포인트 수. 많을수록 부드러우나 연산 증가")]
        private int _pointCount = 24;

        [SerializeField, Range(0.001f, 0.2f), Tooltip("복원력 강도 (k). 클수록 빠르게 평형으로 돌아옴")]
        private float _springConstant = 0.025f;

        [SerializeField, Range(0.001f, 0.1f), Tooltip("감쇠 계수. 클수록 진동이 빨리 사라짐")]
        private float _damping = 0.025f;

        [SerializeField, Range(0f, 0.5f), Tooltip("좌·우 이웃 전파율. 클수록 파동이 멀리 퍼짐")]
        private float _spread = 0.25f;

        [SerializeField, Tooltip("진입 속도(Y)에 대한 impulse 계수")]
        private float _velocityMultiplier = 0.1f;

        [SerializeField, Tooltip("질량에 대한 impulse 계수")]
        private float _massMultiplier = 0.05f;

        [SerializeField, Min(0f), Tooltip("단일 진입당 최대 impulse 절댓값 (클램프)")]
        private float _maxImpulse = 5f;

        [SerializeField, Tooltip("부력 시스템 활성화. 물에 잠긴 Rigidbody2D 에 매 FixedUpdate 로 힘을 가한다.")]
        private bool _buoyancyEnabled = false;

        [SerializeField, Min(0f), Tooltip("단위 잠김 깊이 × 질량당 위 방향 힘. 기본값 30 은 일반 2D 씬 중력(-9.81)에서 자연스러운 부유.")]
        private float _buoyancyForce = 30f;

        [SerializeField, Range(0f, 20f), Tooltip("수중 선형 감쇠(초당). 수직·수평 속도에 적용되어 물 속에서 천천히 감속.")]
        private float _linearDrag = 3f;

        [SerializeField, Range(0f, 20f), Tooltip("수중 각속도 감쇠(초당).")]
        private float _angularDrag = 1f;

        [SerializeField, Tooltip("MeshRenderer 의 Sorting Layer ID (SpriteRenderer 와 공유).")]
        private int _sortingLayerID = 0;

        [SerializeField, Tooltip("MeshRenderer 의 Order in Layer. SpriteRenderer 와 같은 레이어 내에서 앞뒤 정렬.")]
        private int _sortingOrder = 0;

        [SerializeField, Tooltip("Splash 발생 시 호출: (world position, 적용된 force)")]
        private Water2DSplashEvent _onSplash = new Water2DSplashEvent();

        #endregion

        #region 런타임 상태

        // 시뮬레이션 데이터
        private WaterPoint[] _points;
        private float[] _leftDeltas;
        private float[] _rightDeltas;

        // 메시
        private Mesh _mesh;
        private Vector3[] _vertices = System.Array.Empty<Vector3>();
        private Vector2[] _uvs = System.Array.Empty<Vector2>();
        private int[] _triangles = System.Array.Empty<int>();

        // 컴포넌트 캐시
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private BoxCollider2D _collider;

        // 재할당 플래그
        private int _allocatedPointCount = -1;

        // 고정 스텝 시뮬레이션 어큐뮬레이터
        private float _simAccumulator;
        private const float SimStepSeconds = 1f / 60f;

        // 물 속에 잠긴 Rigidbody2D 추적 (부력 적용 대상)
        private readonly System.Collections.Generic.HashSet<Rigidbody2D> _submergedBodies
            = new System.Collections.Generic.HashSet<Rigidbody2D>();

        // 메시 업데이트 플래그 (모바일 최적화)
        private const MeshUpdateFlags MeshFlags =
            MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;

        // OnValidate 에서 에디터 시 재빌드가 필요함을 지연 표시
        private bool _rebuildRequested;

        #endregion

        #region 공개 프로퍼티·API

        /// <summary>물 본체 가로 폭 (로컬).</summary>
        public float Width => _width;

        /// <summary>물 본체 세로 깊이.</summary>
        public float Depth => _depth;

        /// <summary>표면 포인트 수.</summary>
        public int PointCount => _pointCount;

        /// <summary>Splash 이벤트 훅.</summary>
        public Water2DSplashEvent OnSplash => _onSplash;

        /// <summary>Sorting Layer ID (Unity 내부 hash).</summary>
        public int SortingLayerID
        {
            get => _sortingLayerID;
            set { _sortingLayerID = value; ApplySortingToRenderer(); }
        }

        /// <summary>Order in Layer.</summary>
        public int SortingOrder
        {
            get => _sortingOrder;
            set { _sortingOrder = value; ApplySortingToRenderer(); }
        }

        /// <summary>
        /// 임의 위치에 파동을 주입한다.
        /// </summary>
        /// <param name="localX">-width/2 ~ +width/2 범위의 로컬 X 좌표</param>
        /// <param name="force">표면 포인트 수직 속도에 가산되는 impulse. 양수는 표면이 위로 솟는 방향, 음수는 아래로 찍히는 방향.</param>
        public void Splash(float localX, float force)
        {
            if (_points == null || _points.Length == 0) return;

            int idx = LocalXToIndex(localX);
            _points[idx].Velocity += force;

            if (_onSplash != null)
            {
                Vector3 worldPos = transform.TransformPoint(new Vector3(localX, _points[idx].Height, 0f));
                _onSplash.Invoke(new Vector2(worldPos.x, worldPos.y), force);
            }
        }

        /// <summary>표면을 평형 상태로 리셋한다.</summary>
        public void ResetSurface()
        {
            if (_points == null) return;
            for (int i = 0; i < _points.Length; i++)
            {
                _points[i].Height = _points[i].TargetHeight;
                _points[i].Velocity = 0f;
            }
            UpdateMeshVertices();
        }

        /// <summary>에디터에서 pointCount/width/depth 변경 후 즉시 메시를 다시 만든다.</summary>
        public void RebuildMeshIfDirty()
        {
            _rebuildRequested = true;
            EnsureAllocated();
            BuildMeshTopology();
            UpdateMeshVertices();
            SetupCollider();
            ApplySortingToRenderer();
        }

        #endregion

        #region 라이프사이클

        private void Awake()
        {
            CacheComponents();
            EnsureAllocated();
            BuildMeshTopology();
            UpdateMeshVertices();
            SetupCollider();
            ApplySortingToRenderer();
        }

        private void OnEnable()
        {
            CacheComponents();
            EnsureAllocated();
            BuildMeshTopology();
            UpdateMeshVertices();
            SetupCollider();
            ApplySortingToRenderer();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= EditorTick;
                EditorApplication.update += EditorTick;
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
                _mesh = null;
            }
        }

        private void OnValidate()
        {
            if (_width < 0.01f) _width = 0.01f;
            if (_depth < 0.01f) _depth = 0.01f;
            _pointCount = Mathf.Clamp(_pointCount, 8, 128);
            _rebuildRequested = true;
        }

        private void Update()
        {
            if (_rebuildRequested)
            {
                EnsureAllocated();
                BuildMeshTopology();
                SetupCollider();
                ApplySortingToRenderer();
                _rebuildRequested = false;
            }

            if (Application.isPlaying)
            {
                StepSimulation(Time.deltaTime);
                UpdateMeshVertices();
            }
        }

        #endregion

        #region 초기화 / 메시 구성

        private void CacheComponents()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();

            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    name = "Water2D Mesh",
                    hideFlags = HideFlags.DontSave
                };
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;
            }
        }

        private void EnsureAllocated()
        {
            int n = Mathf.Max(2, _pointCount);
            if (_points != null && _allocatedPointCount == n) return;

            _points = new WaterPoint[n];
            _leftDeltas = new float[n];
            _rightDeltas = new float[n];
            for (int i = 0; i < n; i++)
            {
                _points[i].Height = 0f;
                _points[i].Velocity = 0f;
                _points[i].TargetHeight = 0f;
            }

            int vcount = n * 2;
            _vertices = new Vector3[vcount];
            _uvs = new Vector2[vcount];
            _triangles = new int[(n - 1) * 6];
            _allocatedPointCount = n;
        }

        /// <summary>정점 레이아웃과 삼각형 인덱스를 설정한다. 정점 Y 값은 별도 UpdateMeshVertices 에서 갱신.</summary>
        private void BuildMeshTopology()
        {
            if (_mesh == null) return;

            int n = _points.Length;
            float halfW = _width * 0.5f;
            float dx = _width / (n - 1);

            // 상단 행 (i = 0..n-1), 하단 행 (i = n..2n-1)
            for (int i = 0; i < n; i++)
            {
                float x = -halfW + i * dx;
                _vertices[i] = new Vector3(x, 0f, 0f); // 상단 (Height 는 UpdateMeshVertices 에서 적용)
                _vertices[n + i] = new Vector3(x, -_depth, 0f);

                float u = (float)i / (n - 1);
                _uvs[i] = new Vector2(u, 1f);
                _uvs[n + i] = new Vector2(u, 0f);
            }

            // quad strip: 각 세그먼트당 2개 삼각형 (CCW)
            int ti = 0;
            for (int i = 0; i < n - 1; i++)
            {
                int topL = i;
                int topR = i + 1;
                int botL = n + i;
                int botR = n + i + 1;

                _triangles[ti++] = topL;
                _triangles[ti++] = topR;
                _triangles[ti++] = botL;

                _triangles[ti++] = topR;
                _triangles[ti++] = botR;
                _triangles[ti++] = botL;
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices, 0, _vertices.Length, MeshFlags);
            _mesh.SetUVs(0, _uvs, 0, _uvs.Length, MeshFlags);
            _mesh.SetTriangles(_triangles, 0, _triangles.Length, 0, false);
            _mesh.RecalculateBounds();
        }

        /// <summary>시뮬 결과(_points.Height)를 상단 정점에 반영.</summary>
        private void UpdateMeshVertices()
        {
            if (_mesh == null || _points == null) return;

            int n = _points.Length;
            float halfW = _width * 0.5f;
            float dx = _width / (n - 1);

            for (int i = 0; i < n; i++)
            {
                float x = -halfW + i * dx;
                _vertices[i] = new Vector3(x, _points[i].Height, 0f);
                // 하단 행은 토폴로지 구축 시 고정값이므로 재할당 스킵 (pointCount 미변경 시 유지)
            }

            _mesh.SetVertices(_vertices, 0, _vertices.Length, MeshFlags);
            // Bounds 는 웨이브 진폭이 작을 때 매 프레임 재계산 불필요.
            // 프리셋 여유를 포함한 bounds 를 한 번만 설정해 모바일 부담 최소화.
            _mesh.bounds = new Bounds(
                new Vector3(0f, -_depth * 0.5f, 0f),
                new Vector3(_width, _depth + 2f, 0.1f));
        }

        private void SetupCollider()
        {
            if (_collider == null) return;
            _collider.isTrigger = true;
            _collider.size = new Vector2(_width, _depth);
            _collider.offset = new Vector2(0f, -_depth * 0.5f);
        }

        /// <summary>MeshRenderer 의 Sorting Layer/Order 를 직렬화 값과 동기화.</summary>
        private void ApplySortingToRenderer()
        {
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null) return;

            // sortingLayerID 가 유효한 레이어 해시인지 확인 (삭제된 레이어 대응)
            if (!SortingLayer.IsValid(_sortingLayerID))
            {
                _sortingLayerID = 0; // Default
            }
            _meshRenderer.sortingLayerID = _sortingLayerID;
            _meshRenderer.sortingOrder = _sortingOrder;
        }

        #endregion

        #region 시뮬레이션

        /// <summary>고정 스텝(1/60s) 어큐뮬레이터 기반 시뮬레이션 실행.</summary>
        private void StepSimulation(float deltaTime)
        {
            if (_points == null || _points.Length < 2) return;
            if (deltaTime <= 0f) return;

            _simAccumulator += Mathf.Min(deltaTime, 0.1f); // 과도한 카트리지 방지
            int safety = 0;
            while (_simAccumulator >= SimStepSeconds && safety < 8)
            {
                _simAccumulator -= SimStepSeconds;
                SingleStep();
                safety++;
            }
            if (safety >= 8) _simAccumulator = 0f; // 잔여 덤프
        }

        /// <summary>한 번의 고정 스텝. Hooke's Law + damping + 이웃 전파(2-pass).</summary>
        private void SingleStep()
        {
            int n = _points.Length;

            // 1) 스프링 + 감쇠 (in-place)
            for (int i = 0; i < n; i++)
            {
                float x = _points[i].Height - _points[i].TargetHeight;
                float force = -_springConstant * x - _damping * _points[i].Velocity;
                _points[i].Velocity += force;
                _points[i].Height += _points[i].Velocity;
            }

            // 2) 이웃 전파: 높이 차 기반 Δv 계산 (두 번 순회)
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (i > 0)
                    {
                        _leftDeltas[i] = _spread * (_points[i].Height - _points[i - 1].Height);
                        _points[i - 1].Velocity += _leftDeltas[i];
                    }
                    if (i < n - 1)
                    {
                        _rightDeltas[i] = _spread * (_points[i].Height - _points[i + 1].Height);
                        _points[i + 1].Velocity += _rightDeltas[i];
                    }
                }
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) _points[i - 1].Height += _leftDeltas[i];
                    if (i < n - 1) _points[i + 1].Height += _rightDeltas[i];
                }
            }
        }

        #endregion

        #region 상호작용

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!Application.isPlaying) return;

            Rigidbody2D rb = other.attachedRigidbody;
            if (rb == null) return;

            Vector3 contactWorld = other.bounds.center;
            float localX = transform.InverseTransformPoint(contactWorld).x;

            // 진입 속도(Y, 음수 = 낙하) 와 질량으로 impulse 계산
            float velY = rb.linearVelocity.y;
            float impulse = velY * _velocityMultiplier - rb.mass * _massMultiplier;
            impulse = Mathf.Clamp(impulse, -_maxImpulse, _maxImpulse);

            Splash(localX, impulse);

            if (_buoyancyEnabled) _submergedBodies.Add(rb);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb == null) return;
            _submergedBodies.Remove(rb);
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying || !_buoyancyEnabled) return;
            if (_submergedBodies.Count == 0) return;

            float dt = Time.fixedDeltaTime;

            // Destroy 된 Rigidbody2D 참조 제거 (프레임당 1회)
            _submergedBodies.RemoveWhere(r => r == null);

            foreach (Rigidbody2D rb in _submergedBodies)
            {
                if (rb == null || !rb.simulated) continue;
                ApplyBuoyancy(rb, dt);
            }
        }

        private void ApplyBuoyancy(Rigidbody2D rb, float dt)
        {
            // 바디 위치를 로컬로 변환해 표면 높이와 비교
            Vector3 localPos = transform.InverseTransformPoint(rb.position);
            float surfaceLocalY = SampleSurfaceHeight(localPos.x);
            float submergedDepth = surfaceLocalY - localPos.y;

            if (submergedDepth <= 0f) return;

            // 부력: 잠김 깊이 × 질량 × 힘 계수 (위 방향)
            float buoyMag = _buoyancyForce * submergedDepth * rb.mass;
            rb.AddForce(new Vector2(0f, buoyMag), ForceMode2D.Force);

            // 수중 드래그 (선형·각속도)
            float linearFactor = Mathf.Max(0f, 1f - _linearDrag * dt);
            float angularFactor = Mathf.Max(0f, 1f - _angularDrag * dt);
            rb.linearVelocity *= linearFactor;
            rb.angularVelocity *= angularFactor;
        }

        /// <summary>
        /// 로컬 X 좌표의 표면 높이(로컬 Y)를 이웃 포인트 선형 보간으로 반환.
        /// 부력 계산 외에도 외부 스크립트에서 수면 높이 샘플링에 사용 가능.
        /// </summary>
        public float SampleSurfaceHeight(float localX)
        {
            if (_points == null || _points.Length < 2) return 0f;

            int n = _points.Length;
            float halfW = _width * 0.5f;
            float t = Mathf.Clamp01((localX + halfW) / Mathf.Max(0.0001f, _width));
            float fIdx = t * (n - 1);
            int i0 = Mathf.FloorToInt(fIdx);
            int i1 = Mathf.Min(i0 + 1, n - 1);
            float frac = fIdx - i0;
            return Mathf.Lerp(_points[i0].Height, _points[i1].Height, frac);
        }

        #endregion

        #region 내부 유틸

        private int LocalXToIndex(float localX)
        {
            int n = _points.Length;
            float halfW = _width * 0.5f;
            float t = Mathf.Clamp01((localX + halfW) / Mathf.Max(0.0001f, _width));
            return Mathf.Clamp(Mathf.RoundToInt(t * (n - 1)), 0, n - 1);
        }

        #endregion

        #region 에디터 프리뷰

#if UNITY_EDITOR
        // 에디터 프리뷰 활성화 여부 (커스텀 에디터에서 토글)
        [System.NonSerialized] public bool _editorPreview = false;

        private double _lastEditorTickTime = -1.0;

        private void EditorTick()
        {
            if (Application.isPlaying) return;
            if (!_editorPreview) { _lastEditorTickTime = -1.0; return; }
            if (this == null) return;

            double now = EditorApplication.timeSinceStartup;
            if (_lastEditorTickTime < 0) _lastEditorTickTime = now;
            float dt = (float)(now - _lastEditorTickTime);
            _lastEditorTickTime = now;

            StepSimulation(dt);
            UpdateMeshVertices();
            SceneView.RepaintAll();
        }

        /// <summary>에디터에서 표면 포인트 배열을 읽기 전용으로 노출 (기즈모용).</summary>
        public WaterPoint[] EditorGetPoints() => _points;
#endif

        #endregion
    }
}
