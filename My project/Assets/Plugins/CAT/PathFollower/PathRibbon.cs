using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace CAT.Utility
{
    /// <summary>
    /// PathFollower 경로를 따라 Tiling 스프라이트 리본 메시를 생성한다.
    /// 자식 오브젝트에 SpriteRenderer(DrawMode=Tiled) 또는 Image(Type=Tiled)를 배치하면
    /// 해당 컴포넌트의 Sprite/Material/Color/Size를 읽어 리본 메시를 구부려 렌더링한다.
    /// Loop 경로에서는 컨베이어 벨트처럼 UV가 흐른다.
    ///
    /// [모드 자동 감지]
    /// - Canvas 부모 존재 → UI 모드 (MaskableGraphic 경로)
    /// - 없음 → Sprite 모드 (MeshRenderer 경로)
    ///
    /// [설계 주의]
    /// - Graphic 베이스의 raycastTarget, color는 무시되며, 자식 렌더러의 Color가 정점 컬러로 사용된다.
    /// - 스프라이트의 텍스처는 Wrap Mode = Repeat 이어야 타일링이 끊어지지 않는다.
    /// - 비Loop 경로에서는 마지막 타일이 잘릴 수 있고 UV 스크롤은 비활성화된다.
    /// - 모든 길이/두께/법선 계산은 경로 공간(부모 로컬 공간) 기준 → 카메라/캔버스 월드 스케일과
    ///   무관하게 타일 개수와 리본 두께가 일정하다 (Screen Space - Camera 캔버스 대응).
    /// - 리본 오브젝트 자신의 회전/스케일은 렌더링 결과에 영향을 주지 않는다 (경로 고정 원칙).
    ///   예: 자신을 Y축 180° 회전해도 flipY 가 뒤집히지 않음. 리본을 거울 반전하려면
    ///   부모 Transform 을 회전/반전하거나 flipX/flipY 를 사용할 것.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PathFollower))]
    public class PathRibbon : MaskableGraphic
    {
        #region 직렬화 필드

        [Tooltip("컨베이어 벨트 스크롤 속도 (units/sec, UI 모드에서는 px/sec). 음수 = 역방향. Loop 경로에서만 동작")]
        public float scrollSpeed = 0f;

        [Tooltip("경로 길이 1유닛당 샘플 정점 개수 (자동 모드). UI 모드에서는 100px = 1유닛으로 환산")]
        [Min(0.1f)] public float samplesPerUnit = 10f;

        [Tooltip("샘플 개수를 수동으로 지정")]
        public bool overrideSamples = false;

        [Tooltip("수동 샘플 개수 (overrideSamples=true 일 때 사용)")]
        [Range(4, 512)] public int manualSamples = 32;

        [Tooltip("UI 모드: 서브 Canvas 를 자동 추가하여 상위 Canvas rebuild 를 격리한다. UV 스크롤/모핑 사용 시 권장.")]
        public bool autoCreateSubCanvas = true;

        [Tooltip("가로 반전 (경로 방향 UV 뒤집기). Sprite 모드에서는 자식 SpriteRenderer.flipX 와 XOR 로 결합됨")]
        public bool flipX = false;

        [Tooltip("세로 반전 (리본 두께 방향 UV 뒤집기). Sprite 모드에서는 자식 SpriteRenderer.flipY 와 XOR 로 결합됨")]
        public bool flipY = false;

        #endregion

        #region 내부 상태

        private PathFollower _follower;
        private Canvas _parentCanvas;
        private bool _isUIMode;

        // 자식 렌더러 (둘 중 하나만 유효)
        private SpriteRenderer _childSpriteRenderer;
        private Image _childImage;

        // Sprite 모드 전용
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private MaterialPropertyBlock _mpb;
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");

        // Sprite 모드 폴백 material (URP 2D Sprite 셰이더는 MeshRenderer 에서 렌더 불가)
        // 모든 PathRibbon 이 공유 (텍스처는 MaterialPropertyBlock 으로 개별 주입)
        private static Material s_ribbonFallbackMaterial;
        private const string FallbackShaderResourceName = "PathRibbonUnlit";

        // UI 모드: 자동 생성한 서브 Canvas (null 이면 미생성 또는 사용자가 직접 추가)
        private Canvas _autoSubCanvas;

        // UI 모드 자동 샘플 계산용: 로컬 단위(픽셀) → 유닛 환산 계수 (PPU 100 관례)
        private const float UISampleUnitPixels = 100f;

        // Mesh 업데이트 플래그 (인덱스 검증 + bounds 자동 계산 생략 → 모바일 최적화)
        // 인덱스는 구조적으로 유효함이 보장되고, bounds 는 RecalculateBounds 로 수동 호출.
        private const MeshUpdateFlags MeshFlags =
            MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;

        // 자식 렌더러 원래 상태(복구용)
        private bool _childSREnabledOriginal = true;
        private bool _childImgEnabledOriginal = true;
        private bool _childStateStored = false;

        // 메시 데이터 (사전 할당, GC 최소화)
        private Vector3[] _vertices = System.Array.Empty<Vector3>();
        private Vector2[] _uvs = System.Array.Empty<Vector2>();
        private Color32[] _colors32 = System.Array.Empty<Color32>();
        private int[] _triangles = System.Array.Empty<int>();
        /// <summary>각 샘플의 스크롤 전 기본 U 좌표 (UV 스크롤 갱신용)</summary>
        private float[] _baseU = System.Array.Empty<float>();

        // 변경 감지 캐시
        private int _lastPathVersion = -1;
        private int _lastChildSpriteID = 0;
        private int _lastChildMaterialID = 0;
        private Vector2 _lastChildSize = Vector2.zero;
        private Color _lastChildColor = Color.white;
        private Matrix4x4 _lastRibbonWorldMatrix;
        private bool _lastLoopEnabled;
        private bool _lastOverrideSamples;
        private int _lastManualSamples;
        private float _lastSamplesPerUnit;
        private bool _lastFlipX;
        private bool _lastFlipY;
        private bool _lastChildFlipX;
        private bool _lastChildFlipY;

        // 가장 최근 리빌드 시 적용된 실효 flip (ApplyScrollUV 에서 V 좌표 결정에 사용)
        private bool _effectiveFlipX;
        private bool _effectiveFlipY;

        // 리빌드 결과
        private int _sampleCount;
        private float _effectiveTileLength = 1f;
        private float _totalLength;

        // UV 스크롤
        private float _uOffset;

        #endregion

        #region 공개 프로퍼티

        /// <summary>Canvas 자식으로 동작 중인지 여부</summary>
        public bool IsUIMode => _isUIMode;

        /// <summary>현재 생성된 샘플(정점 쌍) 개수</summary>
        public int ActualSampleCount => _sampleCount;

        /// <summary>Loop 보정 후 실제 사용되는 타일 길이 (경로 공간 단위)</summary>
        public float EffectiveTileLength => _effectiveTileLength;

        /// <summary>경로 전체 길이 추정값 (경로 공간 단위)</summary>
        public float TotalPathLength => _totalLength;

        #endregion

        #region Unity 생명주기

        protected override void Awake()
        {
            base.Awake();
            CacheReferences();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheReferences();
            MarkDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_meshRenderer != null) _meshRenderer.enabled = false;
            // 자식 렌더러 원래 상태 복구 (PathRibbon 이 꺼지면 사용자가 직접 자식을 볼 수 있어야 함)
            RestoreChildOriginalState();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
                _mesh = null;
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            CacheReferences();
            MarkDirty();
        }

        protected override void Reset()
        {
            base.Reset();
            // 리본은 기본적으로 UI 이벤트 수신 대상이 아님
            raycastTarget = false;
        }
#endif

        private void LateUpdate()
        {
            if (_follower == null) return;

            // 자식 렌더러를 PathRibbon 활성화 이후에 추가한 경우 대응: 유효한 자식이 없으면 재탐색.
            // (기존에는 OnValidate 가 발생해야만 뒤늦게 감지되어, 인스펙터 값 변경 시점에
            //  자식 비활성화 + material 교체가 한꺼번에 일어나는 혼란이 있었음)
            if (!HasValidChild() && TryFindChildRenderer())
            {
                CacheReferences();
                MarkDirty();
            }

            bool needsRebuild = DetectDataChanges();

            if (_follower.PathVersion != _lastPathVersion)
            {
                _lastPathVersion = _follower.PathVersion;
                needsRebuild = true;
            }

            Matrix4x4 cur = transform.localToWorldMatrix;
            if (cur != _lastRibbonWorldMatrix)
            {
                _lastRibbonWorldMatrix = cur;
                needsRebuild = true;
            }

            if (_follower.IsLoopEnabled != _lastLoopEnabled)
            {
                _lastLoopEnabled = _follower.IsLoopEnabled;
                needsRebuild = true;
            }

            if (overrideSamples != _lastOverrideSamples
                || manualSamples != _lastManualSamples
                || !Mathf.Approximately(samplesPerUnit, _lastSamplesPerUnit))
            {
                _lastOverrideSamples = overrideSamples;
                _lastManualSamples = manualSamples;
                _lastSamplesPerUnit = samplesPerUnit;
                needsRebuild = true;
            }

            if (flipX != _lastFlipX || flipY != _lastFlipY)
            {
                _lastFlipX = flipX;
                _lastFlipY = flipY;
                needsRebuild = true;
            }

            // 자식 SpriteRenderer.flipX / flipY 변경 감지 (Sprite 모드)
            if (!_isUIMode && _childSpriteRenderer != null)
            {
                bool cfx = _childSpriteRenderer.flipX;
                bool cfy = _childSpriteRenderer.flipY;
                if (cfx != _lastChildFlipX || cfy != _lastChildFlipY)
                {
                    _lastChildFlipX = cfx;
                    _lastChildFlipY = cfy;
                    needsRebuild = true;
                }
            }

            if (needsRebuild) RebuildMesh();

            // UV 스크롤 (플레이 모드 + Loop 경로 한정)
            if (Application.isPlaying
                && _follower.IsLoopEnabled
                && Mathf.Abs(scrollSpeed) > 1e-6f
                && _effectiveTileLength > 1e-6f
                && _sampleCount >= 2)
            {
                _uOffset += (scrollSpeed * Time.deltaTime) / _effectiveTileLength;
                _uOffset -= Mathf.Floor(_uOffset);
                ApplyScrollUV();
            }
        }

        #endregion

        #region 참조 캐싱 / 모드 감지

        private void CacheReferences()
        {
            _follower = GetComponent<PathFollower>();
            _parentCanvas = GetComponentInParent<Canvas>();
            _isUIMode = _parentCanvas != null;

            // 자식 1단계에서 Tiled 렌더러 탐색 (깊이 탐색은 하지 않음)
            _childSpriteRenderer = null;
            _childImage = null;
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (_isUIMode)
                {
                    var img = child.GetComponent<Image>();
                    if (img != null) { _childImage = img; break; }
                }
                else
                {
                    var sr = child.GetComponent<SpriteRenderer>();
                    if (sr != null) { _childSpriteRenderer = sr; break; }
                }
            }

            if (_isUIMode) SetupUIMode();
            else SetupSpriteMode();
        }

        private void SetupUIMode()
        {
            if (_meshRenderer != null) _meshRenderer.enabled = false;
            if (_meshFilter != null) _meshFilter.sharedMesh = null;

            // 자식 Image 숨김 (중복 렌더링 방지). 원래 상태는 기억했다가 OnDisable 시 복구.
            StoreChildOriginalState();
            if (_childImage != null) _childImage.enabled = false;

            // 서브 Canvas 자동 생성: 상위 Canvas rebuild 를 이 리본에서 격리
            EnsureSubCanvas();
        }

        /// <summary>
        /// UI 모드에서 서브 Canvas 를 자동 추가한다 (autoCreateSubCanvas=true 일 때).
        /// 상위 Canvas 의 매 프레임 mesh rebuild 폭탄을 막는다.
        /// 이미 Canvas 가 있으면 아무것도 하지 않는다.
        /// </summary>
        private void EnsureSubCanvas()
        {
            if (!autoCreateSubCanvas) return;
            if (_parentCanvas == null) return;

            var existing = GetComponent<Canvas>();
            if (existing != null)
            {
                // 사용자/시스템이 이미 Canvas 를 두었으면 그대로 사용 (우리 소유로 기록하지 않음)
                _autoSubCanvas = null;
                return;
            }

            _autoSubCanvas = gameObject.AddComponent<Canvas>();
            _autoSubCanvas.overrideSorting = true;
            _autoSubCanvas.sortingLayerID = _parentCanvas.sortingLayerID;
            _autoSubCanvas.sortingOrder = _parentCanvas.sortingOrder;
            // 자동 생성된 Canvas 는 에셋/씬 저장 대상이 아님을 명시할 필요는 없음(사용자 의도 반영 위해 저장 허용)
        }

        private void SetupSpriteMode()
        {
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();

            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshRenderer.enabled = true;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "PathRibbon", hideFlags = HideFlags.DontSave };
                _mesh.MarkDynamic();
            }
            _meshFilter.sharedMesh = _mesh;

            // 자식 SpriteRenderer 숨김 (중복 렌더링 방지). 원래 상태는 기억했다가 OnDisable 시 복구.
            StoreChildOriginalState();
            if (_childSpriteRenderer != null) _childSpriteRenderer.enabled = false;

            if (_childSpriteRenderer != null)
            {
                _meshRenderer.sharedMaterial = ResolveMeshMaterial(_childSpriteRenderer.sharedMaterial);
                _meshRenderer.sortingLayerID = _childSpriteRenderer.sortingLayerID;
                _meshRenderer.sortingOrder = _childSpriteRenderer.sortingOrder;

                // 스프라이트 재질의 _MainTex 는 SpriteRenderer 가 내부 주입한다.
                // MeshRenderer 는 해당 경로가 없으므로 MaterialPropertyBlock 으로 텍스처를 주입.
                ApplySpriteTexturePropertyBlock();
            }

            // UI Graphic 쪽은 Canvas가 없으면 어차피 렌더되지 않지만, 명시적으로 숨김 처리
            if (canvasRenderer != null) canvasRenderer.cull = true;
        }

        /// <summary>자식 렌더러의 enabled 원래 상태 저장 (PathRibbon 비활성 시 복구용)</summary>
        private void StoreChildOriginalState()
        {
            if (_childStateStored) return;
            if (_childSpriteRenderer != null) _childSREnabledOriginal = _childSpriteRenderer.enabled;
            if (_childImage != null) _childImgEnabledOriginal = _childImage.enabled;
            _childStateStored = true;
        }

        /// <summary>자식 렌더러의 enabled 상태 복구</summary>
        private void RestoreChildOriginalState()
        {
            if (!_childStateStored) return;
            if (_childSpriteRenderer != null) _childSpriteRenderer.enabled = _childSREnabledOriginal;
            if (_childImage != null) _childImage.enabled = _childImgEnabledOriginal;
            _childStateStored = false;
        }

        /// <summary>자식 Sprite 의 텍스처를 MaterialPropertyBlock 으로 MeshRenderer 에 주입 (Sprite 모드 전용)</summary>
        private void ApplySpriteTexturePropertyBlock()
        {
            if (_meshRenderer == null || _childSpriteRenderer == null) return;

            Texture tex = _childSpriteRenderer.sprite != null ? _childSpriteRenderer.sprite.texture : null;
            if (tex == null)
            {
                // 텍스처 없으면 PropertyBlock 비움 → 재질 기본값(하얀색) 그대로 표시
                _meshRenderer.SetPropertyBlock(null);
                return;
            }

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _meshRenderer.GetPropertyBlock(_mpb);
            _mpb.SetTexture(PropMainTex, tex);
            _meshRenderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 스프라이트 전용 셰이더인지 판단한다.
        /// URP 2D Sprite 셰이더는 SpriteRenderer 가 draw 시점에 주입하는
        /// unity_SpriteProps / unity_SpriteColor 내장값에 의존하므로,
        /// MeshRenderer 로 그리면 정점이 붕괴(×0)되고 알파가 0이 되어 보이지 않는다.
        /// </summary>
        public static bool IsSpriteOnlyShader(Material mat)
        {
            if (mat == null || mat.shader == null) return false;
            string shaderName = mat.shader.name;
            return shaderName == "Universal Render Pipeline/2D/Sprite-Unlit-Default"
                || shaderName == "Universal Render Pipeline/2D/Sprite-Lit-Default";
        }

        /// <summary>
        /// MeshRenderer 에 사용할 material 을 결정한다 (Sprite 모드 전용).
        /// 자식 material 이 MeshRenderer 비호환(스프라이트 전용 셰이더)이거나 null 이면
        /// 공유 폴백 material(CAT/PathFollower/Ribbon-Unlit)을 반환한다.
        /// 변경 감지 시점에만 호출되므로 문자열 비교 비용은 무시 가능.
        /// </summary>
        private static Material ResolveMeshMaterial(Material childMat)
        {
            if (childMat != null && !IsSpriteOnlyShader(childMat)) return childMat;

            if (s_ribbonFallbackMaterial == null)
            {
                Shader shader = Resources.Load<Shader>(FallbackShaderResourceName);
                if (shader == null)
                {
                    Debug.LogError("[PathRibbon] Resources 에서 PathRibbonUnlit.shader 를 찾을 수 없습니다. 리본이 표시되지 않을 수 있습니다.");
                    return childMat;
                }
                s_ribbonFallbackMaterial = new Material(shader)
                {
                    name = "PathRibbon-Unlit (Shared)",
                    hideFlags = HideFlags.DontSave
                };
            }
            return s_ribbonFallbackMaterial;
        }

        /// <summary>자식 1단계에 유효한 렌더러(Image/SpriteRenderer)가 있는지 검사만 한다 (부작용 없음).</summary>
        private bool TryFindChildRenderer()
        {
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (_isUIMode)
                {
                    if (child.TryGetComponent<Image>(out _)) return true;
                }
                else
                {
                    if (child.TryGetComponent<SpriteRenderer>(out _)) return true;
                }
            }
            return false;
        }

        #endregion

        #region 변경 감지

        private bool DetectDataChanges()
        {
            if (_isUIMode)
            {
                if (_childImage == null) return false;

                int spriteID = _childImage.sprite != null ? _childImage.sprite.GetInstanceID() : 0;
                int matID = _childImage.material != null ? _childImage.material.GetInstanceID() : 0;
                Vector2 size = _childImage.rectTransform.rect.size;
                Color col = _childImage.color;

                bool changed = spriteID != _lastChildSpriteID
                            || matID != _lastChildMaterialID
                            || size != _lastChildSize
                            || col != _lastChildColor;

                if (changed)
                {
                    _lastChildSpriteID = spriteID;
                    _lastChildMaterialID = matID;
                    _lastChildSize = size;
                    _lastChildColor = col;
                    SetMaterialDirty();
                }
                // 자식 Image 가 외부에서 enabled=true 로 복구된 경우 다시 숨김
                if (_childImage != null && _childImage.enabled)
                    _childImage.enabled = false;
                return changed;
            }
            else
            {
                if (_childSpriteRenderer == null) return false;

                int spriteID = _childSpriteRenderer.sprite != null ? _childSpriteRenderer.sprite.GetInstanceID() : 0;
                int matID = _childSpriteRenderer.sharedMaterial != null ? _childSpriteRenderer.sharedMaterial.GetInstanceID() : 0;
                Vector2 size = _childSpriteRenderer.size;
                Color col = _childSpriteRenderer.color;

                bool changed = spriteID != _lastChildSpriteID
                            || matID != _lastChildMaterialID
                            || size != _lastChildSize
                            || col != _lastChildColor;

                if (changed)
                {
                    _lastChildSpriteID = spriteID;
                    _lastChildMaterialID = matID;
                    _lastChildSize = size;
                    _lastChildColor = col;

                    // 재질/정렬 Sprite 모드에서 MeshRenderer 에 반영
                    if (_meshRenderer != null)
                    {
                        _meshRenderer.sharedMaterial = ResolveMeshMaterial(_childSpriteRenderer.sharedMaterial);
                        _meshRenderer.sortingLayerID = _childSpriteRenderer.sortingLayerID;
                        _meshRenderer.sortingOrder = _childSpriteRenderer.sortingOrder;
                    }
                    // Sprite 텍스처를 MeshRenderer 에 재주입
                    ApplySpriteTexturePropertyBlock();
                }
                // 자식 SpriteRenderer 가 외부에서 enabled=true 로 복구된 경우 다시 숨김
                if (_childSpriteRenderer != null && _childSpriteRenderer.enabled)
                    _childSpriteRenderer.enabled = false;
                return changed;
            }
        }

        #endregion

        #region 공개 API

        /// <summary>다음 프레임에 강제 리빌드가 수행되도록 표시한다.</summary>
        public void MarkDirty()
        {
            _lastPathVersion = -1;
            _lastRibbonWorldMatrix = default;
            _lastChildSize = new Vector2(float.NaN, float.NaN);
            if (_isUIMode) SetVerticesDirty();
        }

        /// <summary>즉시 메시를 재생성한다.</summary>
        public void RebuildMesh()
        {
            if (_follower == null) _follower = GetComponent<PathFollower>();
            if (_follower == null) { _sampleCount = 0; return; }
            if (!HasValidChild()) { _sampleCount = 0; ClearOutputMesh(); return; }

            Vector2 tileAndWidth = GetTileAndWidth();
            float tileLength = Mathf.Max(0.001f, tileAndWidth.x);
            float ribbonWidth = Mathf.Max(0.001f, tileAndWidth.y);
            Color color = GetChildColor();
            bool isLoop = _follower.IsLoopEnabled;

            // 자식 SpriteRenderer 의 flipX/flipY 와 PathRibbon 자체 flipX/flipY 를 XOR 로 결합
            bool childFx = !_isUIMode && _childSpriteRenderer != null && _childSpriteRenderer.flipX;
            bool childFy = !_isUIMode && _childSpriteRenderer != null && _childSpriteRenderer.flipY;
            _effectiveFlipX = flipX ^ childFx;
            _effectiveFlipY = flipY ^ childFy;

            // 1. 예비 샘플링으로 경로 총 길이(로컬) 추정 → 자동 샘플 개수 계산
            //    UI 모드의 로컬 단위는 픽셀이므로 100px = 1유닛으로 환산하여 샘플 밀도 유지
            float prelimLength = EstimatePathLength(32);
            float unitLength = _isUIMode ? prelimLength / UISampleUnitPixels : prelimLength;
            int baseSampleCount = overrideSamples
                ? Mathf.Max(4, manualSamples)
                : Mathf.Clamp(Mathf.CeilToInt(unitLength * samplesPerUnit), 4, 4096);

            // Loop 경로는 이음매에서 UV 연속성을 위해 마지막 정점을 한 번 더 추가
            int vertexSampleCount = isLoop ? baseSampleCount + 1 : baseSampleCount;

            EnsureArraySize(vertexSampleCount);

            // 2. 정점 및 누적 호 길이 계산 — 전부 경로 공간(부모 로컬 공간) 기준
            //    [카메라 독립성] Screen Space - Camera 캔버스는 카메라 설정에 따라 월드 스케일이
            //    변하므로 월드 공간에서 길이/두께를 재면 타일링이 카메라에 종속된다.
            //    [handedness 일관성] 경로 포인트가 저장된 공간(부모 로컬)에서 법선을 계산해야
            //    리본 자신의 회전/스케일(예: Y축 180° 회전)이 좌/우 정점과 V 방향을 뒤집지 않는다.
            //    경로 공간에서 만든 정점을 마지막에 리본 로컬로 변환하면, 렌더링 시 리본 자신의
            //    Transform 과 상쇄되어 경로 고정 원칙(자기 Transform 무영향)이 그대로 유지된다.
            Transform parent = transform.parent;
            Matrix4x4 worldToPath = parent != null ? parent.worldToLocalMatrix : Matrix4x4.identity;
            Matrix4x4 pathToLocal = parent != null
                ? transform.worldToLocalMatrix * parent.localToWorldMatrix
                : transform.worldToLocalMatrix;
            float cumLen = 0f;
            Vector3 prevPathPos = Vector3.zero;
            float halfW = ribbonWidth * 0.5f;

            for (int i = 0; i < vertexSampleCount; i++)
            {
                float t = isLoop
                    ? (float)i / baseSampleCount
                    : ((baseSampleCount > 1) ? (float)i / (baseSampleCount - 1) : 0f);

                Vector3 pathPos = worldToPath.MultiplyPoint3x4(_follower.GetPointAt(t));
                Vector3 pathTangent = worldToPath.MultiplyVector(_follower.GetDirectionAt(t));

                // 2D 법선: 경로 공간 접선을 CCW 90° 회전
                float nx = -pathTangent.y;
                float ny = pathTangent.x;
                float nMagSq = nx * nx + ny * ny;
                if (nMagSq > 1e-6f)
                {
                    float inv = 1f / Mathf.Sqrt(nMagSq);
                    nx *= inv; ny *= inv;
                }
                else { nx = 0f; ny = 1f; }

                Vector3 leftPath = new Vector3(pathPos.x + nx * halfW, pathPos.y + ny * halfW, pathPos.z);
                Vector3 rightPath = new Vector3(pathPos.x - nx * halfW, pathPos.y - ny * halfW, pathPos.z);

                _vertices[i * 2 + 0] = pathToLocal.MultiplyPoint3x4(leftPath);
                _vertices[i * 2 + 1] = pathToLocal.MultiplyPoint3x4(rightPath);

                if (i > 0)
                {
                    float dx = pathPos.x - prevPathPos.x;
                    float dy = pathPos.y - prevPathPos.y;
                    float dz = pathPos.z - prevPathPos.z;
                    cumLen += Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                }
                prevPathPos = pathPos;

                _baseU[i] = cumLen; // 일단 누적 호 길이 저장 → 뒤에서 타일 길이로 나눠 정규화
            }

            _totalLength = cumLen;

            // 3. Loop 이음매 보정
            if (isLoop && _totalLength > 1e-6f)
            {
                int tileCount = Mathf.Max(1, Mathf.RoundToInt(_totalLength / tileLength));
                _effectiveTileLength = _totalLength / tileCount;
            }
            else
            {
                _effectiveTileLength = tileLength;
            }

            // 4. UV 정규화 + 스크롤 오프셋 반영 + flip 적용
            float invTileLen = 1f / Mathf.Max(1e-6f, _effectiveTileLength);
            float uSign = _effectiveFlipX ? -1f : 1f;
            float vLeft = _effectiveFlipY ? 1f : 0f;
            float vRight = _effectiveFlipY ? 0f : 1f;
            for (int i = 0; i < vertexSampleCount; i++)
            {
                float u = _baseU[i] * invTileLen;
                _baseU[i] = u; // 스크롤 갱신용 기본 U (flip 미적용) 보관
                float uScroll = u * uSign + _uOffset;
                _uvs[i * 2 + 0] = new Vector2(uScroll, vLeft);
                _uvs[i * 2 + 1] = new Vector2(uScroll, vRight);
            }

            // 5. 정점 컬러
            Color32 c32 = color;
            int vertCount = vertexSampleCount * 2;
            for (int i = 0; i < vertCount; i++) _colors32[i] = c32;

            // 6. 삼각형 인덱스 (quad strip)
            int quadCount = vertexSampleCount - 1;
            for (int q = 0; q < quadCount; q++)
            {
                int v0 = q * 2;       // left_i
                int v1 = q * 2 + 1;   // right_i
                int v2 = v0 + 2;      // left_{i+1}
                int v3 = v0 + 3;      // right_{i+1}
                int t0 = q * 6;

                _triangles[t0 + 0] = v0;
                _triangles[t0 + 1] = v2;
                _triangles[t0 + 2] = v1;

                _triangles[t0 + 3] = v2;
                _triangles[t0 + 4] = v3;
                _triangles[t0 + 5] = v1;
            }

            _sampleCount = vertexSampleCount;

            // 7. 메시에 반영
            if (_isUIMode)
            {
                SetVerticesDirty();
            }
            else
            {
                ApplySpriteMeshData();
            }
        }

        #endregion

        #region 내부 헬퍼

        private bool HasValidChild()
        {
            return _isUIMode ? (_childImage != null) : (_childSpriteRenderer != null);
        }

        /// <summary>
        /// 자식 렌더러로부터 (타일 길이, 리본 두께) 를 얻는다.
        /// - 타일 길이: 스프라이트 네이티브 크기(= rect.width / PPU)를 사용 → SpriteRenderer.size.x 는 "타일 크기"가 아닌 "그릴 영역"이므로 사용하지 않음.
        /// - 리본 두께: SpriteRenderer.size.y (Tiled/Sliced 모드) 또는 Image RectTransform 높이.
        /// </summary>
        private Vector2 GetTileAndWidth()
        {
            if (_isUIMode)
            {
                float tileLenPx = (_childImage != null && _childImage.sprite != null)
                    ? _childImage.sprite.rect.width
                    : 100f;
                float widthPx = (_childImage != null) ? _childImage.rectTransform.rect.size.y : 100f;
                return new Vector2(tileLenPx, widthPx);
            }
            else
            {
                var sp = _childSpriteRenderer != null ? _childSpriteRenderer.sprite : null;
                float tileLen = sp != null
                    ? sp.rect.width / Mathf.Max(0.0001f, sp.pixelsPerUnit)
                    : 1f;
                // Simple 모드는 size 가 유효하지 않으므로 네이티브 높이로 폴백
                float width;
                if (_childSpriteRenderer != null && _childSpriteRenderer.drawMode != SpriteDrawMode.Simple)
                {
                    width = _childSpriteRenderer.size.y;
                }
                else
                {
                    width = sp != null ? sp.rect.height / Mathf.Max(0.0001f, sp.pixelsPerUnit) : 1f;
                }
                return new Vector2(tileLen, width);
            }
        }

        private Color GetChildColor()
        {
            return _isUIMode ? _childImage.color : _childSpriteRenderer.color;
        }

        /// <summary>경로 총 길이를 경로 공간(부모 로컬) 기준으로 추정한다 (카메라/캔버스 스케일·리본 자신 Transform 무관).</summary>
        private float EstimatePathLength(int samples)
        {
            Transform parent = transform.parent;
            Matrix4x4 worldToPath = parent != null ? parent.worldToLocalMatrix : Matrix4x4.identity;
            Vector3 prev = worldToPath.MultiplyPoint3x4(_follower.GetPointAt(0f));
            float len = 0f;
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector3 p = worldToPath.MultiplyPoint3x4(_follower.GetPointAt(t));
                len += (p - prev).magnitude;
                prev = p;
            }
            return len;
        }

        private void EnsureArraySize(int sampleCount)
        {
            int vertsNeeded = sampleCount * 2;
            if (_vertices.Length != vertsNeeded)
            {
                _vertices = new Vector3[vertsNeeded];
                _uvs = new Vector2[vertsNeeded];
                _colors32 = new Color32[vertsNeeded];
            }
            if (_baseU.Length != sampleCount)
            {
                _baseU = new float[sampleCount];
            }
            int trisNeeded = Mathf.Max(0, (sampleCount - 1) * 6);
            if (_triangles.Length != trisNeeded)
            {
                _triangles = new int[trisNeeded];
            }
        }

        private void ApplySpriteMeshData()
        {
            if (_mesh == null) return;
            _mesh.Clear();
            // MeshUpdateFlags 로 인덱스 검증 / bounds 자동 계산 생략 (bounds 는 아래서 수동 호출).
            _mesh.SetVertices(_vertices, 0, _vertices.Length, MeshFlags);
            _mesh.SetUVs(0, _uvs, 0, _uvs.Length, MeshFlags);
            _mesh.SetColors(_colors32, 0, _colors32.Length, MeshFlags);
            _mesh.SetTriangles(_triangles, 0, calculateBounds: false);
            _mesh.RecalculateBounds();
        }

        private void ApplyScrollUV()
        {
            float uSign = _effectiveFlipX ? -1f : 1f;
            float vLeft = _effectiveFlipY ? 1f : 0f;
            float vRight = _effectiveFlipY ? 0f : 1f;
            for (int i = 0; i < _sampleCount; i++)
            {
                float u = _baseU[i] * uSign + _uOffset;
                _uvs[i * 2 + 0] = new Vector2(u, vLeft);
                _uvs[i * 2 + 1] = new Vector2(u, vRight);
            }

            if (_isUIMode)
            {
                SetVerticesDirty();
            }
            else if (_mesh != null)
            {
                // UV 만 갱신: 인덱스/바운즈 건드리지 않음 → 가장 저렴한 mesh 업데이트 경로.
                _mesh.SetUVs(0, _uvs, 0, _uvs.Length, MeshFlags);
            }
        }

        private void ClearOutputMesh()
        {
            if (_isUIMode) SetVerticesDirty();
            else if (_mesh != null) _mesh.Clear();
        }

        #endregion

        #region Graphic 오버라이드 (UI 모드 전용 경로)

        public override Texture mainTexture
        {
            get
            {
                if (_isUIMode && _childImage != null && _childImage.sprite != null)
                    return _childImage.sprite.texture;
                return base.mainTexture;
            }
        }

        public override Material material
        {
            get
            {
                if (_isUIMode && _childImage != null && _childImage.material != null)
                    return _childImage.material;
                return base.material;
            }
            set => base.material = value;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_isUIMode) return;
            if (_sampleCount < 2) return;

            int vertCount = _sampleCount * 2;
            UIVertex v = UIVertex.simpleVert;
            for (int i = 0; i < vertCount; i++)
            {
                v.position = _vertices[i];
                v.uv0 = _uvs[i];
                v.color = _colors32[i];
                vh.AddVert(v);
            }

            int quadCount = _sampleCount - 1;
            for (int q = 0; q < quadCount; q++)
            {
                int v0 = q * 2;
                int v1 = q * 2 + 1;
                int v2 = v0 + 2;
                int v3 = v0 + 3;
                vh.AddTriangle(v0, v2, v1);
                vh.AddTriangle(v2, v3, v1);
            }
        }

        #endregion
    }
}
