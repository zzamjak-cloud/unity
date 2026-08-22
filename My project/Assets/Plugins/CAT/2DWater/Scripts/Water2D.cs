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
    /// - World-space MeshRenderer 방식. 컴포넌트 추가 시 CAT/Effects/2D Water 셰이더 기반
    ///   머티리얼 에셋이 자동 생성·할당된다 (인스펙터에서 다른 머티리얼로 교체 가능).
    /// - 물속 색상·질감 수치는 머티리얼(셰이더) 프로퍼티로 조절한다.
    ///
    /// [지속 출렁임]
    /// - Ambient Wave: 충돌 없이도 표면이 계속 출렁인다.
    ///   진행 파형(해석적 변위) + 랜덤 임펄스(실제 파동 주입) 2단 구성.
    ///
    /// [물리 기능 = opt-in]
    /// - Interaction Enabled / Buoyancy Enabled 가 모두 OFF 면 BoxCollider2D 를 비활성해
    ///   트리거 콜백·물리 브로드페이즈 비용을 제거한다. 기본값은 둘 다 OFF (디스플레이 전용).
    /// - 스프링 시뮬은 충돌·Splash·랜덤 임펄스로만 깨어나고, 표면이 정지하면 자동으로 잠든다.
    ///   잠든 동안에는 스텝 연산과 정점 업로드를 모두 생략한다.
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

        [SerializeField, Tooltip("충돌 상호작용 활성화. OFF 면 BoxCollider2D 가 비활성되어 트리거 콜백·물리 브로드페이즈 비용이 0 이 된다.\n디스플레이용 물이면 OFF 로 두세요.")]
        private bool _interactionEnabled = false;

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

        [SerializeField, Tooltip("물 머티리얼. 비어 있으면 CAT/Effects/2D Water 셰이더 머티리얼이 자동 생성·할당된다.")]
        private Material _material;

        [SerializeField, Tooltip("Splash 발생 시 호출: (world position, 적용된 force)")]
        private Water2DSplashEvent _onSplash = new Water2DSplashEvent();

        [SerializeField, Tooltip("물 표면 라인 렌더링 활성화")]
        private bool _surfaceLineEnabled = true;

        [SerializeField, Min(0f), Tooltip("물 표면 라인 두께(로컬 단위)")]
        private float _surfaceLineThickness = 0.06f;

        [SerializeField, Tooltip("물 표면 라인 색상")]
        private Color _surfaceLineColor = Color.white;

        // ── 지속 출렁임 (Ambient Wave) ──────────────────────────────────

        [SerializeField, Tooltip("충돌 없이도 표면이 계속 출렁이는 지속 파동 활성화 (횡스크롤 물 표현용).\n스프링 시뮬을 쓰지 않는 해석적 변위이므로 물리 기능과 무관하게 동작한다.")]
        private bool _ambientEnabled = true;

        [SerializeField, Range(0f, 3f), Tooltip("지속 출렁임 전체 강도 배율. 파형 진폭과 랜덤 임펄스 세기에 함께 곱해진다.")]
        private float _ambientIntensity = 1f;

        [SerializeField, Min(0f), Tooltip("진행 파형의 기본 진폭(로컬 단위). 표면이 위아래로 흔들리는 크기.")]
        private float _waveAmplitude = 0.08f;

        [SerializeField, Min(0.01f), Tooltip("진행 파형의 파장(로컬 단위). 작을수록 잔물결, 클수록 완만한 너울.")]
        private float _waveLength = 3f;

        [SerializeField, Tooltip("파형 진행 속도(로컬 단위/초). 음수면 반대 방향으로 흐른다. 시간 빈도 = 속도/파장(Hz).")]
        private float _waveSpeed = 0.6f;

        [SerializeField, Range(1, 4), Tooltip("중첩할 파형 개수. 여러 겹을 겹쳐 반복감을 줄인다.")]
        private int _waveOctaves = 2;

        [SerializeField, Range(0.1f, 1f), Tooltip("다음 옥타브의 진폭 비율. 옥타브마다 파장은 절반이 된다.")]
        private float _waveOctaveFalloff = 0.5f;

        [SerializeField, Range(0.5f, 3f), Tooltip("다음 옥타브의 진행 속도 비율. 1 이 아니면 파형이 서로 어긋나 반복 주기가 길어진다.")]
        private float _waveOctaveSpeedRatio = 1.6f;

        [SerializeField, Range(0f, 1f), Tooltip("랜덤성. 진폭 대비 Perlin 노이즈 비율. 0 이면 완전 규칙적인 사인파.")]
        private float _waveRandomness = 0.35f;

        [SerializeField, Min(0.01f), Tooltip("노이즈 공간 밀도. 클수록 잘게 흔들린다.")]
        private float _waveNoiseScale = 0.5f;

        [SerializeField, Tooltip("노이즈 시간 변화 속도. 클수록 랜덤 성분이 빠르게 변한다.")]
        private float _waveNoiseSpeed = 0.35f;

        [SerializeField, Tooltip("랜덤 시드. 같은 씬의 여러 물 오브젝트가 서로 다른 위상을 갖게 한다.")]
        private int _ambientSeed = 0;

        [SerializeField, Tooltip("주기적으로 랜덤 위치에 impulse 를 주입해 실제 파동을 전파시킨다. 자연스러움은 올라가지만\n스프링 시뮬이 계속 깨어 있게 되므로 CPU 비용이 발생한다. 디스플레이 전용이면 OFF 권장.")]
        private bool _randomImpulseEnabled = false;

        [SerializeField, Min(0.02f), Tooltip("랜덤 임펄스 최소 간격(초). 빈도 하한.")]
        private float _impulseIntervalMin = 0.6f;

        [SerializeField, Min(0.02f), Tooltip("랜덤 임펄스 최대 간격(초). 빈도 상한.")]
        private float _impulseIntervalMax = 2f;

        [SerializeField, Tooltip("랜덤 임펄스 세기 최소값. 음수는 아래로 찍히는 파동.\n기본 설정(퍼짐 0.6, Spring 0.025)에서 세기 0.05 ≈ 표면 변위 0.08.")]
        private float _impulseForceMin = -0.05f;

        [SerializeField, Tooltip("랜덤 임펄스 세기 최대값. 양수는 위로 솟는 파동.\n퍼짐 폭이 커지면 같은 세기로도 변위가 커진다 (0.3→약 2배, 1.2→약 5배).")]
        private float _impulseForceMax = 0.05f;

        [SerializeField, Min(0f), Tooltip("임펄스가 퍼지는 로컬 폭. 0 이면 한 포인트만 때려 날카로워진다.")]
        private float _impulseSpread = 0.6f;

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
        private LineRenderer _surfaceLineRenderer;
        private Material _surfaceLineMaterial;

        // 재할당 플래그
        private int _allocatedPointCount = -1;
        private Vector3[] _surfaceLinePositions = System.Array.Empty<Vector3>();

        // 고정 스텝 시뮬레이션 어큐뮬레이터
        private float _simAccumulator;
        private const float SimStepSeconds = 1f / 60f;

        // 스프링 시뮬 슬립: 충돌·Splash·랜덤 임펄스로만 깨어나고, 정지하면 다시 잠든다.
        private bool _springAwake;
        private bool _meshFlushPending;      // 잠들기 직전 마지막 1회 메시 반영 필요
        private const float SleepEpsilon = 0.0004f;

        // 표면 라인 설정 재적용 필요 여부 (매 프레임 프로퍼티 재설정 방지)
        private bool _surfaceLineConfigDirty = true;

        // 물 속에 잠긴 Rigidbody2D 추적 (부력 적용 대상)
        private readonly System.Collections.Generic.HashSet<Rigidbody2D> _submergedBodies
            = new System.Collections.Generic.HashSet<Rigidbody2D>();

        // 메시 업데이트 플래그 (모바일 최적화)
        private const MeshUpdateFlags MeshFlags =
            MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;

        // OnValidate 에서 에디터 시 재빌드가 필요함을 지연 표시
        private bool _rebuildRequested;

        // 지속 출렁임 상태
        private float[] _ambientOffsets = System.Array.Empty<float>();
        private float _ambientTime;
        private float _impulseTimer;
        private bool _ambientOffsetsDirtyOnce;   // 비활성 전환 시 오프셋 1회 초기화 필요
        private uint _randomState = 1u;          // 자체 xorshift (전역 Random 상태 오염 방지)
        private readonly float[] _octavePhases = new float[MaxOctaves];
        private const int MaxOctaves = 4;
        private const float Tau = Mathf.PI * 2f;

        // 머티리얼
        private Material _runtimeMaterial;   // 자동 생성분 (에셋이 아닌 인스턴스)
        private bool _shaderMissingWarned;
        public const string WaterShaderName = "CAT/Effects/2D Water";

        // 셰이더 프로퍼티 ID 캐시 (모바일 최적화: 문자열 해싱 반복 방지)
        private static readonly int PropTextureEnabled = Shader.PropertyToID("_TextureEnabled");
        private static readonly int PropCausticsEnabled = Shader.PropertyToID("_CausticsEnabled");
        private static readonly int PropDistortEnabled = Shader.PropertyToID("_DistortEnabled");
        private static readonly int PropFoamEnabled = Shader.PropertyToID("_FoamEnabled");

        private const string KeywordTexture = "_CAT_TEXTURE";
        private const string KeywordCaustics = "_CAT_CAUSTICS";
        private const string KeywordDistort = "_CAT_DISTORT";
        private const string KeywordFoam = "_CAT_FOAM";

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

        /// <summary>물 머티리얼. 설정 시 MeshRenderer 에 즉시 반영된다.</summary>
        public Material WaterMaterial
        {
            get => _material;
            set
            {
                _material = value;
                if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
                if (_meshRenderer != null && _meshRenderer.sharedMaterial != value)
                {
                    _meshRenderer.sharedMaterial = value;
                }
            }
        }

        /// <summary>충돌 상호작용 on/off. OFF 면 BoxCollider2D 가 비활성된다.</summary>
        public bool InteractionEnabled
        {
            get => _interactionEnabled;
            set { _interactionEnabled = value; SetupCollider(); }
        }

        /// <summary>부력 on/off. OFF 면 FixedUpdate 처리와 콜라이더가 필요 없으면 비활성된다.</summary>
        public bool BuoyancyEnabled
        {
            get => _buoyancyEnabled;
            set
            {
                _buoyancyEnabled = value;
                if (!value) _submergedBodies.Clear();
                SetupCollider();
            }
        }

        /// <summary>표면 라인 on/off.</summary>
        public bool SurfaceLineEnabled
        {
            get => _surfaceLineEnabled;
            set
            {
                if (_surfaceLineEnabled == value) return;
                _surfaceLineEnabled = value;
                _surfaceLineConfigDirty = true;
                _meshFlushPending = true;
            }
        }

        /// <summary>스프링 시뮬이 현재 동작 중인지. false 면 프레임 비용이 사실상 0.</summary>
        public bool IsSpringAwake => _springAwake;

        /// <summary>지속 출렁임 on/off. 끄면 표면 변위가 다음 프레임에 0 으로 정리된다.</summary>
        public bool AmbientEnabled
        {
            get => _ambientEnabled;
            set => _ambientEnabled = value;
        }

        /// <summary>지속 출렁임 전체 강도 배율. 바람·상황 연출에 따라 런타임에서 보간해도 안전하다.</summary>
        public float AmbientIntensity
        {
            get => _ambientIntensity;
            set => _ambientIntensity = Mathf.Max(0f, value);
        }

        /// <summary>진행 파형 진폭(로컬 단위).</summary>
        public float WaveAmplitude
        {
            get => _waveAmplitude;
            set => _waveAmplitude = Mathf.Max(0f, value);
        }

        /// <summary>진행 파형 속도(로컬 단위/초). 음수는 반대 방향.</summary>
        public float WaveSpeed
        {
            get => _waveSpeed;
            set => _waveSpeed = value;
        }

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
            WakeSpring();

            if (_onSplash != null)
            {
                Vector3 worldPos = transform.TransformPoint(new Vector3(localX, SurfaceHeightAt(idx), 0f));
                _onSplash.Invoke(new Vector2(worldPos.x, worldPos.y), force);
            }
        }

        /// <summary>
        /// 지정 로컬 X 주변에 폭을 가진 파동을 주입한다. 단일 포인트 대비 부드러운 형태.
        /// </summary>
        /// <param name="localX">-width/2 ~ +width/2 범위의 로컬 X 좌표</param>
        /// <param name="force">중심 포인트에 가산되는 impulse (주변은 코사인 감쇠)</param>
        /// <param name="spread">영향 범위의 로컬 폭. 0 이면 단일 포인트.</param>
        public void SplashArea(float localX, float force, float spread)
        {
            if (_points == null || _points.Length == 0) return;

            InjectImpulse(localX, force, spread);

            if (_onSplash != null)
            {
                int idx = LocalXToIndex(localX);
                Vector3 worldPos = transform.TransformPoint(new Vector3(localX, SurfaceHeightAt(idx), 0f));
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
            _springAwake = false;
            _simAccumulator = 0f;
            _meshFlushPending = false;
            UpdateMeshVertices();
        }

        /// <summary>에디터에서 pointCount/width/depth 변경 후 즉시 메시를 다시 만든다.</summary>
        public void RebuildMeshIfDirty()
        {
            _rebuildRequested = true;
            CacheComponents();
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

            if (_surfaceLineMaterial != null)
            {
                if (Application.isPlaying) Destroy(_surfaceLineMaterial);
                else DestroyImmediate(_surfaceLineMaterial);
                _surfaceLineMaterial = null;
            }

            if (_runtimeMaterial != null)
            {
                if (_material == _runtimeMaterial) _material = null;
                if (Application.isPlaying) Destroy(_runtimeMaterial);
                else DestroyImmediate(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        private void OnValidate()
        {
            if (_width < 0.01f) _width = 0.01f;
            if (_depth < 0.01f) _depth = 0.01f;
            _pointCount = Mathf.Clamp(_pointCount, 8, 128);
            if (_surfaceLineThickness < 0f) _surfaceLineThickness = 0f;
            _surfaceLineConfigDirty = true;

            // 지속 출렁임 값 정리
            if (_waveLength < 0.01f) _waveLength = 0.01f;
            if (_waveNoiseScale < 0.01f) _waveNoiseScale = 0.01f;
            _waveOctaves = Mathf.Clamp(_waveOctaves, 1, MaxOctaves);
            _impulseIntervalMin = Mathf.Max(0.02f, _impulseIntervalMin);
            _impulseIntervalMax = Mathf.Max(_impulseIntervalMin, _impulseIntervalMax);
            if (_impulseForceMax < _impulseForceMin) _impulseForceMax = _impulseForceMin;
            if (_impulseSpread < 0f) _impulseSpread = 0f;
            InitAmbientRandom();

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
                EnsureMaterial();
                _meshFlushPending = true; // 유휴 상태에서도 변경 사항 1회 반영
                _rebuildRequested = false;
            }

            if (Application.isPlaying) TickAndRender(Time.deltaTime);
        }

        /// <summary>
        /// 시뮬 1프레임 + 필요할 때만 메시 반영.
        /// 지속 출렁임 OFF · 스프링 슬립 상태에서는 정점 업로드조차 하지 않는다.
        /// </summary>
        private void TickAndRender(float deltaTime)
        {
            StepSimulation(deltaTime);

            bool surfaceMoving = _ambientEnabled || _springAwake;
            if (surfaceMoving || _meshFlushPending)
            {
                UpdateMeshVertices();
                _meshFlushPending = surfaceMoving;
            }
        }

        #endregion

        #region 초기화 / 메시 구성

        private void CacheComponents()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            if (_surfaceLineRenderer == null) _surfaceLineRenderer = GetComponent<LineRenderer>();
            if (_surfaceLineRenderer == null) _surfaceLineRenderer = gameObject.AddComponent<LineRenderer>();

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

            if (_surfaceLineMaterial == null)
            {
                Shader lineShader = Shader.Find("Sprites/Default");
                if (lineShader != null)
                {
                    _surfaceLineMaterial = new Material(lineShader)
                    {
                        name = "Water2D Surface Line",
                        hideFlags = HideFlags.DontSave
                    };
                }
            }

            ConfigureSurfaceLineRenderer(true);
            EnsureMaterial();
            ApplyRendererPerfSettings();
        }

        /// <summary>
        /// 물 머티리얼을 보장한다. 인스펙터 지정 → MeshRenderer 기존 → 셰이더 기반 자동 생성 순.
        /// 자동 생성분은 HideFlags.DontSave 인스턴스이며 OnDestroy 에서 해제한다.
        /// </summary>
        private void EnsureMaterial()
        {
            if (_meshRenderer == null) return;

            if (_material == null) _material = _meshRenderer.sharedMaterial;

#if UNITY_EDITOR
            // 에디터(비플레이)에서는 항상 머티리얼 에셋을 쓴다.
            // 자동 생성된 비영속 인스턴스는 씬을 매번 dirty 로 만들고 수치가 저장되지 않으므로 에셋으로 승격.
            if (!Application.isPlaying && NeedsEditorMaterialAsset())
            {
                // 에셋이 이미 있으면 즉시 승격 (LoadAssetAtPath 는 이 타이밍에도 안전).
                Material existingAsset = LoadDefaultMaterialAsset();
                if (existingAsset != null) PromoteToMaterialAsset(existingAsset);
                else RequestEditorDefaultMaterial(); // 신규 생성만 안전한 타이밍으로 미룸

                if (_material == null) return;
            }
#endif

            if (_material == null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) return; // 에셋 할당은 delayCall 에서 처리
#endif
                Shader shader = Shader.Find(WaterShaderName);
                if (shader == null)
                {
                    if (!_shaderMissingWarned)
                    {
                        _shaderMissingWarned = true;
                        Debug.LogWarning($"[Water2D] 셰이더 '{WaterShaderName}' 를 찾을 수 없습니다. 머티리얼을 직접 지정하세요.", this);
                    }
                    return;
                }

                _runtimeMaterial = new Material(shader)
                {
                    name = "CAT Water2D (Runtime)",
                    hideFlags = HideFlags.DontSave
                };
                ApplyDefaultWaterKeywords(_runtimeMaterial);
                _material = _runtimeMaterial;
            }

            if (_meshRenderer.sharedMaterial != _material) _meshRenderer.sharedMaterial = _material;
        }

        /// <summary>새 머티리얼의 기본 표현 기능(코스틱·왜곡·거품)을 켠다.</summary>
        private static void ApplyDefaultWaterKeywords(Material m)
        {
            if (m == null) return;

            m.SetFloat(PropCausticsEnabled, 1f);
            m.SetFloat(PropDistortEnabled, 1f);
            m.SetFloat(PropFoamEnabled, 1f);
            m.SetFloat(PropTextureEnabled, 0f);

            m.EnableKeyword(KeywordCaustics);
            m.EnableKeyword(KeywordDistort);
            m.EnableKeyword(KeywordFoam);
            m.DisableKeyword(KeywordTexture);
        }

        /// <summary>2D 물에 불필요한 렌더러 기능을 끈다 (모바일 렌더링 비용 절감).</summary>
        private void ApplyRendererPerfSettings()
        {
            if (_meshRenderer == null) return;
            if (_meshRenderer.shadowCastingMode == ShadowCastingMode.Off && !_meshRenderer.receiveShadows) return;

            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _meshRenderer.allowOcclusionWhenDynamic = false;
        }

        private void EnsureAllocated()
        {
            int n = Mathf.Max(2, _pointCount);
            if (_points != null && _allocatedPointCount == n) return;

            _surfaceLineConfigDirty = true; // 포인트 수 변경 → positionCount 재설정 필요
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
            _surfaceLinePositions = new Vector3[n];
            _ambientOffsets = new float[n];
            _allocatedPointCount = n;

            InitAmbientRandom();
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
                _vertices[i] = new Vector3(x, SurfaceHeightAt(i), 0f);
                // 하단 행은 토폴로지 구축 시 고정값이므로 재할당 스킵 (pointCount 미변경 시 유지)
            }

            _mesh.SetVertices(_vertices, 0, _vertices.Length, MeshFlags);
            // Bounds 는 웨이브 진폭이 작을 때 매 프레임 재계산 불필요.
            // 프리셋 여유를 포함한 bounds 를 한 번만 설정해 모바일 부담 최소화.
            _mesh.bounds = new Bounds(
                new Vector3(0f, -_depth * 0.5f, 0f),
                new Vector3(_width, _depth + 2f, 0.1f));

            UpdateSurfaceLinePositions();
        }

        private void SetupCollider()
        {
            if (_collider == null) return;
            _collider.isTrigger = true;
            _collider.size = new Vector2(_width, _depth);
            _collider.offset = new Vector2(0f, -_depth * 0.5f);

            // 물리 기능이 모두 꺼져 있으면 콜라이더 자체를 비활성 (트리거·브로드페이즈 비용 제거)
            bool physicsNeeded = _interactionEnabled || _buoyancyEnabled;
            if (_collider.enabled != physicsNeeded) _collider.enabled = physicsNeeded;
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
            ApplySortingToSurfaceLine();
            _surfaceLineConfigDirty = true;
        }

        /// <summary>
        /// LineRenderer 프로퍼티 재설정. 네이티브 setter 12개 + 머티리얼 할당이라
        /// 매 프레임 호출하면 낭비이므로 dirty 플래그가 설정된 경우에만 수행한다.
        /// </summary>
        private void ConfigureSurfaceLineRenderer(bool force = false)
        {
            if (_surfaceLineRenderer == null) return;
            if (!force && !_surfaceLineConfigDirty) return;
            _surfaceLineConfigDirty = false;

            _surfaceLineRenderer.useWorldSpace = false;
            _surfaceLineRenderer.loop = false;
            _surfaceLineRenderer.textureMode = LineTextureMode.Stretch;
            _surfaceLineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _surfaceLineRenderer.receiveShadows = false;
            _surfaceLineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _surfaceLineRenderer.generateLightingData = false;
            _surfaceLineRenderer.alignment = LineAlignment.TransformZ;

            float clampedThickness = Mathf.Max(0f, _surfaceLineThickness);
            _surfaceLineRenderer.startWidth = clampedThickness;
            _surfaceLineRenderer.endWidth = clampedThickness;
            _surfaceLineRenderer.startColor = _surfaceLineColor;
            _surfaceLineRenderer.endColor = _surfaceLineColor;
            _surfaceLineRenderer.positionCount = _points != null ? _points.Length : 0;
            _surfaceLineRenderer.enabled = _surfaceLineEnabled;

            if (_surfaceLineMaterial != null)
            {
                _surfaceLineRenderer.sharedMaterial = _surfaceLineMaterial;
            }

            ApplySortingToSurfaceLine();
        }

        private void ApplySortingToSurfaceLine()
        {
            if (_surfaceLineRenderer == null) return;
            _surfaceLineRenderer.sortingLayerID = _sortingLayerID;
            _surfaceLineRenderer.sortingOrder = _sortingOrder + 1;
        }

        private void UpdateSurfaceLinePositions()
        {
            if (_surfaceLineRenderer == null || _points == null) return;

            // 설정 변경이 있었던 프레임에만 프로퍼티 재적용
            ConfigureSurfaceLineRenderer();
            if (!_surfaceLineEnabled) return;

            int n = _points.Length;
            if (_surfaceLinePositions == null || _surfaceLinePositions.Length != n) return;

            float halfW = _width * 0.5f;
            float dx = _width / (n - 1);

            for (int i = 0; i < n; i++)
            {
                float x = -halfW + i * dx;
                _surfaceLinePositions[i] = new Vector3(x, SurfaceHeightAt(i), 0f);
            }

            _surfaceLineRenderer.SetPositions(_surfaceLinePositions);
        }

        #endregion

        #region 시뮬레이션

        /// <summary>고정 스텝(1/60s) 어큐뮬레이터 기반 시뮬레이션 실행.</summary>
        private void StepSimulation(float deltaTime)
        {
            if (_points == null || _points.Length < 2) return;
            if (deltaTime <= 0f) return;

            float dt = Mathf.Min(deltaTime, 0.1f);

            // 지속 출렁임은 해석적(analytic) 이므로 고정 스텝 밖에서 프레임 dt 로 진행
            TickAmbient(dt);

            // 스프링 시뮬은 이벤트(충돌/Splash/랜덤 임펄스)로 깨어난 동안만 돈다.
            if (!_springAwake)
            {
                _simAccumulator = 0f;
                return;
            }

            _simAccumulator += dt; // 과도한 카트리지 방지
            int safety = 0;
            while (_simAccumulator >= SimStepSeconds && safety < 8)
            {
                _simAccumulator -= SimStepSeconds;
                SingleStep();
                safety++;
            }
            if (safety >= 8) _simAccumulator = 0f; // 잔여 덤프

            UpdateSpringSleepState();
        }

        /// <summary>스프링 시뮬을 깨운다. 충돌·Splash·랜덤 임펄스 주입 시 호출.</summary>
        private void WakeSpring()
        {
            _springAwake = true;
            _meshFlushPending = true;
        }

        /// <summary>모든 포인트가 평형·정지 상태면 스프링 시뮬을 잠재운다.</summary>
        private void UpdateSpringSleepState()
        {
            int n = _points.Length;
            for (int i = 0; i < n; i++)
            {
                if (Mathf.Abs(_points[i].Height - _points[i].TargetHeight) > SleepEpsilon) return;
                if (Mathf.Abs(_points[i].Velocity) > SleepEpsilon) return;
            }

            // 잔여 미세값을 정확히 0 으로 정리하고 슬립 (다음 프레임부터 스텝 비용 0)
            for (int i = 0; i < n; i++)
            {
                _points[i].Height = _points[i].TargetHeight;
                _points[i].Velocity = 0f;
            }
            _springAwake = false;
            _simAccumulator = 0f;
            _meshFlushPending = true;
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

        #region 지속 출렁임 (Ambient Wave)

        /// <summary>
        /// 지속 출렁임 1프레임 갱신.
        /// - 진행 파형: 스프링 시뮬과 별개로 표면 정점에 직접 가산되는 해석적 변위.
        ///   스프링/감쇠에 먹히지 않으므로 인스펙터 진폭이 그대로 화면에 나온다.
        /// - 랜덤 임펄스: 실제 파동으로 주입되어 이웃으로 전파된다.
        /// </summary>
        private void TickAmbient(float dt)
        {
            if (!_ambientEnabled)
            {
                // 껐을 때 남아 있는 변위를 1회만 정리
                if (_ambientOffsetsDirtyOnce)
                {
                    System.Array.Clear(_ambientOffsets, 0, _ambientOffsets.Length);
                    _ambientOffsetsDirtyOnce = false;
                    _meshFlushPending = true; // 파형 변위 제거를 1회 반영
                }
                return;
            }

            _ambientTime += dt;
            RecalculateAmbientOffsets();
            _ambientOffsetsDirtyOnce = true;

            if (!_randomImpulseEnabled) return;

            _impulseTimer -= dt;
            if (_impulseTimer > 0f) return;

            ScheduleNextImpulse();

            float halfW = _width * 0.5f;
            float localX = Mathf.Lerp(-halfW, halfW, NextRandom01());
            float force = Mathf.Lerp(_impulseForceMin, _impulseForceMax, NextRandom01()) * _ambientIntensity;
            InjectImpulse(localX, force, _impulseSpread);
        }

        /// <summary>모든 포인트의 진행 파형 변위를 계산해 캐시한다.</summary>
        private void RecalculateAmbientOffsets()
        {
            int n = _points.Length;
            if (_ambientOffsets == null || _ambientOffsets.Length != n) return;

            float halfW = _width * 0.5f;
            float dx = _width / (n - 1);
            float baseAmp = _waveAmplitude * _ambientIntensity;
            int octaves = Mathf.Clamp(_waveOctaves, 1, MaxOctaves);
            float noiseAmp = baseAmp * _waveRandomness;
            float noiseT = _ambientTime * _waveNoiseSpeed;

            for (int i = 0; i < n; i++)
            {
                float x = -halfW + i * dx;
                float sum = 0f;
                float amp = baseAmp;
                float wl = Mathf.Max(0.01f, _waveLength);
                float spd = _waveSpeed;

                for (int o = 0; o < octaves; o++)
                {
                    // 위상 = 공간항 + 시간항(진행) + 옥타브별 랜덤 오프셋
                    float phase = Tau * (x / wl) + Tau * (spd / wl) * _ambientTime + _octavePhases[o];
                    sum += amp * Mathf.Sin(phase);

                    amp *= _waveOctaveFalloff;
                    wl *= 0.5f;
                    spd *= _waveOctaveSpeedRatio;
                }

                if (noiseAmp > 0f)
                {
                    // PerlinNoise 는 0~1 → -1~1 로 재매핑
                    float noise = Mathf.PerlinNoise(x * _waveNoiseScale + _octavePhases[0], noiseT) * 2f - 1f;
                    sum += noiseAmp * noise;
                }

                _ambientOffsets[i] = sum;
            }
        }

        /// <summary>지정 로컬 X 주변에 코사인 감쇠로 impulse 를 분산 주입한다.</summary>
        private void InjectImpulse(float localX, float force, float spread)
        {
            if (_points == null || _points.Length == 0) return;

            WakeSpring();

            int n = _points.Length;
            if (spread <= 0f || n < 2)
            {
                _points[LocalXToIndex(localX)].Velocity += force;
                return;
            }

            float dx = _width / (n - 1);
            int radius = Mathf.Max(1, Mathf.RoundToInt(spread / Mathf.Max(0.0001f, dx)));
            int center = LocalXToIndex(localX);

            for (int offset = -radius; offset <= radius; offset++)
            {
                int idx = center + offset;
                if (idx < 0 || idx >= n) continue;

                // 0.5*(1+cos(pi*t)) : 중심 1, 경계 0
                float t = Mathf.Abs(offset) / (float)radius;
                float falloff = 0.5f * (1f + Mathf.Cos(Mathf.PI * t));
                _points[idx].Velocity += force * falloff;
            }
        }

        private void ScheduleNextImpulse()
        {
            float min = Mathf.Max(0.02f, Mathf.Min(_impulseIntervalMin, _impulseIntervalMax));
            float max = Mathf.Max(min, _impulseIntervalMax);
            _impulseTimer = Mathf.Lerp(min, max, NextRandom01());
        }

        /// <summary>시드에서 옥타브 위상과 난수 상태를 파생시킨다. 씬 내 여러 물이 서로 다른 모양이 되도록 인스턴스 ID 도 섞는다.</summary>
        private void InitAmbientRandom()
        {
            unchecked
            {
                uint seed = (uint)(_ambientSeed * 747796405 + GetInstanceID() * 2891336453);
                _randomState = seed == 0u ? 1u : seed;
            }

            for (int o = 0; o < MaxOctaves; o++)
            {
                _octavePhases[o] = NextRandom01() * Tau;
            }

            ScheduleNextImpulse();
        }

        /// <summary>xorshift32 기반 0~1 난수. UnityEngine.Random 전역 상태를 건드리지 않는다.</summary>
        private float NextRandom01()
        {
            unchecked
            {
                _randomState ^= _randomState << 13;
                _randomState ^= _randomState >> 17;
                _randomState ^= _randomState << 5;
                return (_randomState & 0xFFFFFFu) / (float)0x1000000u;
            }
        }

        /// <summary>로컬 X 위치의 진행 파형 변위를 선형 보간으로 반환.</summary>
        private float SampleAmbientOffset(float localX)
        {
            if (_ambientOffsets == null || _ambientOffsets.Length < 2) return 0f;

            int n = _ambientOffsets.Length;
            float halfW = _width * 0.5f;
            float t = Mathf.Clamp01((localX + halfW) / Mathf.Max(0.0001f, _width));
            float fIdx = t * (n - 1);
            int i0 = Mathf.FloorToInt(fIdx);
            int i1 = Mathf.Min(i0 + 1, n - 1);
            return Mathf.Lerp(_ambientOffsets[i0], _ambientOffsets[i1], fIdx - i0);
        }

        /// <summary>인덱스의 최종 표면 높이 (스프링 시뮬 + 지속 출렁임).</summary>
        private float SurfaceHeightAt(int index)
        {
            float ambient = (_ambientOffsets != null && index < _ambientOffsets.Length)
                ? _ambientOffsets[index]
                : 0f;
            return _points[index].Height + ambient;
        }

        #endregion

        #region 상호작용

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!Application.isPlaying) return;
            if (!_interactionEnabled && !_buoyancyEnabled) return;

            Rigidbody2D rb = other.attachedRigidbody;
            if (rb == null) return;

            if (_interactionEnabled)
            {
                Vector3 contactWorld = other.bounds.center;
                float localX = transform.InverseTransformPoint(contactWorld).x;

                // 진입 속도(Y, 음수 = 낙하) 와 질량으로 impulse 계산
                float velY = rb.linearVelocity.y;
                float impulse = velY * _velocityMultiplier - rb.mass * _massMultiplier;
                impulse = Mathf.Clamp(impulse, -_maxImpulse, _maxImpulse);

                Splash(localX, impulse);
            }

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
            return Mathf.Lerp(_points[i0].Height, _points[i1].Height, frac) + SampleAmbientOffset(localX);
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

            TickAndRender(dt);
            SceneView.RepaintAll();
        }

        /// <summary>에디터에서 표면 포인트 배열을 읽기 전용으로 노출 (기즈모용).</summary>
        public WaterPoint[] EditorGetPoints() => _points;

        /// <summary>컴포넌트 추가 시 호출. 기본 물 머티리얼 에셋을 만들어 즉시 할당한다.</summary>
        private void Reset()
        {
            CacheComponents();

            if (_material == null || _material == _runtimeMaterial)
            {
                Material asset = LoadOrCreateDefaultMaterialAsset();
                if (asset != null) WaterMaterial = asset;
            }

            RebuildMeshIfDirty();
        }

        [System.NonSerialized] private bool _defaultMaterialRequested;

        /// <summary>에디터에서 머티리얼 에셋 할당이 필요한 상태인지. (없음 또는 자동 생성된 비영속 인스턴스)</summary>
        private bool NeedsEditorMaterialAsset()
        {
            if (_material == null) return true;
            if (EditorUtility.IsPersistent(_material)) return false;

            // 사용자가 직접 만든 인스턴스는 건드리지 않는다 — 우리 셰이더의 자동 생성분만 승격 대상.
            return _material.shader != null && _material.shader.name == WaterShaderName;
        }

        /// <summary>다음 에디터 틱에 기본 머티리얼 에셋을 로드·생성해 할당한다. (AssetDatabase 안전 타이밍)</summary>
        private void RequestEditorDefaultMaterial()
        {
            if (_defaultMaterialRequested) return;
            _defaultMaterialRequested = true;

            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                _defaultMaterialRequested = false;
                if (!NeedsEditorMaterialAsset()) return;

                Material asset = LoadOrCreateDefaultMaterialAsset();
                if (asset != null) PromoteToMaterialAsset(asset);
            };
        }

        /// <summary>머티리얼 에셋으로 교체하고, 자동 생성된 비영속 인스턴스는 정리한다.</summary>
        private void PromoteToMaterialAsset(Material asset)
        {
            if (asset == null || _material == asset) return;

            Material stale = _material;
            WaterMaterial = asset;
            EditorUtility.SetDirty(this);

            if (stale != null && !EditorUtility.IsPersistent(stale))
            {
                if (_runtimeMaterial == stale) _runtimeMaterial = null;
                DestroyImmediate(stale);
            }
        }

        /// <summary>플러그인 폴더의 Materials 하위 경로. 플러그인을 이동해도 스크립트 위치를 기준으로 따라간다.</summary>
        private string GetMaterialFolder()
        {
            MonoScript script = MonoScript.FromMonoBehaviour(this);
            string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : null;

            if (!string.IsNullOrEmpty(scriptPath))
            {
                // .../2DWater/Scripts/Water2D.cs → .../2DWater/Materials
                string scriptsDir = System.IO.Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');
                string pluginRoot = System.IO.Path.GetDirectoryName(scriptsDir)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(pluginRoot)) return pluginRoot + "/Materials";
            }

            return "Assets/Plugins/CAT/2DWater/Materials";
        }

        /// <summary>공용 기본 물 머티리얼 에셋을 로드한다 (없으면 null, 생성하지 않음).</summary>
        public Material LoadDefaultMaterialAsset()
        {
            return AssetDatabase.LoadAssetAtPath<Material>(GetMaterialFolder() + "/Water2D_Default.mat");
        }

        /// <summary>공용 기본 물 머티리얼 에셋을 로드하거나 없으면 생성한다.</summary>
        public Material LoadOrCreateDefaultMaterialAsset()
        {
            string folder = GetMaterialFolder();
            string path = folder + "/Water2D_Default.mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find(WaterShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Water2D] 셰이더 '{WaterShaderName}' 를 찾을 수 없어 머티리얼을 생성하지 못했습니다.", this);
                return null;
            }

            EnsureFolder(folder);
            Material created = new Material(shader) { name = "Water2D_Default" };
            ApplyDefaultWaterKeywords(created);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>이 오브젝트 전용 머티리얼 에셋을 복제 생성해 할당한다 (공용 기본 머티리얼 공유 해제).</summary>
        public Material CreateDedicatedMaterialAsset()
        {
            Shader shader = Shader.Find(WaterShaderName);
            if (shader == null) return null;

            string folder = GetMaterialFolder();
            EnsureFolder(folder);

            bool copyFromCurrent = _material != null && _material.shader == shader;
            Material created = copyFromCurrent ? new Material(_material) : new Material(shader);
            if (!copyFromCurrent) ApplyDefaultWaterKeywords(created);

            string safeName = SanitizeFileName(gameObject.name);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Water2D_{safeName}.mat");
            created.name = System.IO.Path.GetFileNameWithoutExtension(path);

            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();

            WaterMaterial = created;
            EditorUtility.SetDirty(this);
            return created;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Water";

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            char[] buffer = raw.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (System.Array.IndexOf(invalid, buffer[i]) >= 0) buffer[i] = '_';
            }
            return new string(buffer);
        }
#endif

        #endregion
    }
}
