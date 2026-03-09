using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>
    /// UGUI Image/RawImage에 휘어진 광택 밴드가 지나가는 Additive 루프 효과.
    /// 이동 Ease는 AnimationCurve로, 루프는 Replay/Yoyo, 1회 루프 후 Interval(범위 랜덤) 지원.
    /// IMeshModifier로 스프라이트 UV를 버텍스에 주입해 Sprite Atlas를 지원합니다.
    /// </summary>
    [AddComponentMenu("CAT/Effects/UIShining")]
    [RequireComponent(typeof(Graphic))]
    [ExecuteAlways]
    public class UIShining : MonoBehaviour, IMeshModifier
    {
        public static readonly string SHADER_NAME = "CAT/Effects/UIShining";

        private static Shader _cachedShader;
        private static readonly int PropProgress = Shader.PropertyToID("_Progress");
        private static readonly int PropWidthStart = Shader.PropertyToID("_WidthStart");
        private static readonly int PropWidthEnd = Shader.PropertyToID("_WidthEnd");
        private static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        private static readonly int PropCurvatureStart = Shader.PropertyToID("_CurvatureStart");
        private static readonly int PropCurvatureEnd = Shader.PropertyToID("_CurvatureEnd");
        private static readonly int PropAngle = Shader.PropertyToID("_Angle");
        private static readonly int PropProgressOffset = Shader.PropertyToID("_ProgressOffset");
        private static readonly int PropShineColor = Shader.PropertyToID("_ShineColor");
        private static readonly int PropSoftness = Shader.PropertyToID("_Softness");
        // SoftMaskLight Hidden 변형에서는 _Softness가 마스크에 사용되므로 _ShineSoftness로 분리
        private static readonly int PropShineSoftness = Shader.PropertyToID("_ShineSoftness");
        private static readonly int PropBurnBias = Shader.PropertyToID("_BurnBias");
        private static readonly int PropBlendStrength = Shader.PropertyToID("_BlendStrength");
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");
        private static readonly int PropSpriteUVRect = Shader.PropertyToID("_SpriteUVRect");

        public enum LoopType
        {
            Replay,
            Yoyo
        }

        [Header("머티리얼")]
        [SerializeField, Tooltip("빌드 시 셰이더 스트리핑 방지용 저장된 머티리얼 에셋. 설정하면 Shader.Find 대신 이 머티리얼을 복제하여 사용합니다.")]
        private Material _savedMaterial;

        [Header("타이밍")]
        [SerializeField, Min(0.01f)] private float _duration = 1.5f;
        [SerializeField] private AnimationCurve _movementCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private LoopType _loopType = LoopType.Replay;
        [SerializeField, Min(0f)] private float _intervalMin = 0.3f;
        [SerializeField, Min(0f)] private float _intervalMax = 0.6f;

        [Header("광택")]
        [SerializeField, Range(0f, 2f)] private float _progressOffset = 0.55f;
        [SerializeField, Range(0.01f, 1f)] private float _widthStart = 0.15f;
        [SerializeField, Range(0.01f, 1f)] private float _widthEnd = 0.15f;
        [SerializeField, Range(0f, 3f)] private float _intensity = 1.35f;
        [SerializeField, Range(-1f, 1f)] private float _curvatureStart = 0.3f;
        [SerializeField, Range(-1f, 1f)] private float _curvatureEnd = 0.3f;
        [SerializeField, Range(-180f, 180f)] private float _angle = 0f;
        [SerializeField] private Color _shineColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float _softness = 0f;
        [SerializeField, Range(0f, 1f)] private float _burnBias = 0.85f;
        [SerializeField, Range(0.5f, 2.5f)] private float _blendStrength = 1.45f;

        public float Duration { get => _duration; set => _duration = Mathf.Max(0.01f, value); }
        public AnimationCurve MovementCurve { get => _movementCurve; set => _movementCurve = value; }
        public LoopType Loop { get => _loopType; set => _loopType = value; }
        public float IntervalMin { get => _intervalMin; set => _intervalMin = Mathf.Max(0f, value); }
        public float IntervalMax { get => _intervalMax; set => _intervalMax = Mathf.Max(0f, value); }
        public float ProgressOffset { get => _progressOffset; set => _progressOffset = Mathf.Clamp(value, 0f, 2f); }
        public float WidthStart { get => _widthStart; set => _widthStart = Mathf.Clamp(value, 0.01f, 1f); }
        public float WidthEnd { get => _widthEnd; set => _widthEnd = Mathf.Clamp(value, 0.01f, 1f); }
        public float Intensity { get => _intensity; set => _intensity = Mathf.Clamp(value, 0f, 3f); }
        public float CurvatureStart { get => _curvatureStart; set => _curvatureStart = Mathf.Clamp(value, -1f, 1f); }
        public float CurvatureEnd { get => _curvatureEnd; set => _curvatureEnd = Mathf.Clamp(value, -1f, 1f); }
        public float Angle { get => _angle; set => _angle = Mathf.Clamp(value, -180f, 180f); }
        public Color ShineColor { get => _shineColor; set => _shineColor = value; }
        public float Softness { get => _softness; set => _softness = Mathf.Clamp01(value); }
        public float BurnBias { get => _burnBias; set => _burnBias = Mathf.Clamp01(value); }
        public float BlendStrength { get => _blendStrength; set => _blendStrength = Mathf.Clamp(value, 0.5f, 2.5f); }

        /// <summary>
        /// 에디터에서 저장된 머티리얼 에셋에 접근하기 위한 프로퍼티
        /// </summary>
        public Material SavedMaterial
        {
            get => _savedMaterial;
            set => _savedMaterial = value;
        }

        /// <summary>
        /// 주어진 머티리얼이 UIShining 셰이더를 사용하는지 확인
        /// </summary>
        public static bool IsUIShiningShader(Material material)
        {
            if (material == null || material.shader == null) return false;
            return material.shader.name == SHADER_NAME;
        }

        private Graphic _graphic;
        private Material _material;
        private Material _originalMaterial;
        private float _rawProgress;
        private bool _forward = true;
        private float _intervalRemaining;
        private bool _meshDirtyNeeded;
        private bool _propertiesDirty = true;
        private Vector4 _lastSpriteUVRect;

        // 기존 직렬화 호환용 (사용하지 않음 — 에디터 테스트는 UIShiningEditor가 직접 구동)
        [SerializeField, HideInInspector] private bool _editorTestRunning;
        [SerializeField, HideInInspector] private double _editorTestStartTime;

        /// <summary>진행을 0으로 되돌리고 Material에 반영. 에디터 테스트 중지 시 즉시 초기화용.</summary>
        public void ResetProgressToStart()
        {
            _rawProgress = 0f;
            _forward = true;
            _intervalRemaining = 0f;
            if (ActiveMaterial != null)
                ApplyProgressToMaterial();
        }

        /// <summary>
        /// 에디터 전용: 외부에서 deltaTime을 전달하여 애니메이션을 진행시킨다.
        /// EditorApplication.update 콜백에서 호출되며, [ExecuteAlways] Update()에 의존하지 않는다.
        /// </summary>
        public void EditorAdvance(float dt)
        {
            if (ActiveMaterial == null || _graphic == null) return;

            SetSpriteUVRect();
            // 에디터 테스트 중에는 항상 전체 프로퍼티 갱신 (인스펙터 변경 즉시 반영)
            _propertiesDirty = true;

            if (_intervalRemaining > 0f)
            {
                _intervalRemaining -= dt;
                if (_intervalRemaining <= 0f)
                    _intervalRemaining = 0f;
                ApplyProgressToMaterial();
                return;
            }

            float step = dt / _duration;
            if (!_forward)
                step = -step;

            _rawProgress += step;

            if (_loopType == LoopType.Replay)
            {
                if (_rawProgress >= 1f)
                {
                    _rawProgress = 0f;
                    _intervalRemaining = Random.Range(_intervalMin, _intervalMax);
                }
                else if (_rawProgress < 0f)
                    _rawProgress = 0f;
            }
            else
            {
                if (_rawProgress >= 1f)
                {
                    _rawProgress = 1f;
                    _forward = false;
                    _intervalRemaining = Random.Range(_intervalMin, _intervalMax);
                }
                else if (_rawProgress <= 0f)
                {
                    _rawProgress = 0f;
                    _forward = true;
                    _intervalRemaining = Random.Range(_intervalMin, _intervalMax);
                }
            }

            ApplyProgressToMaterial();
        }

        private static readonly List<UIVertex> s_vertexBuffer = new List<UIVertex>(64);

        public void ModifyMesh(VertexHelper vh)
        {
            if (vh == null) return;
            Graphic g = _graphic != null ? _graphic : GetComponent<Graphic>();
            if (g == null) return;

            Vector4 rect = GetSpriteUVRectVectorFor(g);

            // Sliced 이미지 대응: RectTransform 로컬 좌표 계산
            RectTransform rectTransform = g.transform as RectTransform;
            Rect localRect = rectTransform != null ? rectTransform.rect : new Rect(0, 0, 100, 100);
            float width = Mathf.Max(localRect.width, 1f);
            float height = Mathf.Max(localRect.height, 1f);
            Vector2 rectMin = localRect.min;

            s_vertexBuffer.Clear();
            vh.GetUIVertexStream(s_vertexBuffer);
            for (int i = 0; i < s_vertexBuffer.Count; i++)
            {
                UIVertex v = s_vertexBuffer[i];

                // uv2/uv3: 스프라이트 UV 영역 (Sprite Atlas 대응)
                v.uv2 = new Vector2(rect.x, rect.y);
                v.uv3 = new Vector2(rect.z, rect.w);

                // tangent: RectTransform 로컬 좌표 (0~1 정규화) - Sliced 이미지 대응
                // v.position은 로컬 좌표 (RectTransform 기준)
                float localX = Mathf.Clamp01((v.position.x - rectMin.x) / width);
                float localY = Mathf.Clamp01((v.position.y - rectMin.y) / height);
                v.tangent = new Vector4(localX, localY, 0, 0);

                // 디버깅: 첫 버텍스의 로컬 좌표 확인 (필요 시 주석 해제)
                // if (i == 0)
                //     Debug.Log($"[UIShining] localPos=({localX:F3}, {localY:F3}), pos={v.position}, rect.min={rectMin}, size=({width}, {height})");


                s_vertexBuffer[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(s_vertexBuffer);
        }

        public void ModifyMesh(Mesh mesh) { }

        /// <summary>지정 Graphic 기준 스프라이트 UV 사각형 (캐시 키/메시 주입용)</summary>
        private static Vector4 GetSpriteUVRectVectorFor(Graphic graphic)
        {
            if (graphic is Image image && image.sprite != null)
            {
                Sprite sprite = image.sprite;
                Rect r = sprite.textureRect;
                Texture t = sprite.texture;
                if (t != null)
                {
                    // Windable과 동일한 방식: textureRect를 텍스처 크기로 정규화
                    return new Vector4(
                        r.x / t.width,
                        r.y / t.height,
                        (r.x + r.width) / t.width,
                        (r.y + r.height) / t.height
                    );
                }
            }
            return new Vector4(0f, 0f, 1f, 1f);
        }

        private void Awake()
        {
            _graphic = GetComponent<Graphic>();
            if (_graphic == null)
            {
                Debug.LogWarning("[UIShining] Graphic(Image/RawImage)이 필요합니다.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            if (_graphic == null) return;
            ReInitializeMaterial();
        }

        /// <summary>
        /// 머티리얼을 재초기화 (에디터에서 _savedMaterial 변경 시 호출)
        /// </summary>
        public void ResetMaterial()
        {
            if (_graphic == null) return;
            // 기존 머티리얼 정리
            if (_graphic != null && _originalMaterial != null)
                _graphic.material = _originalMaterial;
            if (_material != null)
            {
                if (Application.isPlaying)
                    Destroy(_material);
                else
                    DestroyImmediate(_material);
            }
            _material = null;
            // 재초기화
            ReInitializeMaterial();
        }

        /// <summary>Material 재초기화</summary>
        private void ReInitializeMaterial()
        {
            if (_graphic == null) return;

            Shader shader = GetCachedShader();
            if (shader == null) return;

            Texture tex = GetUITexture();
            if (_material == null)
            {
                // 저장된 머티리얼이 있으면 복제하여 사용 (빌드 시 셰이더 스트리핑 방지)
                if (_savedMaterial != null)
                {
                    _material = new Material(_savedMaterial)
                    {
                        name = $"{SHADER_NAME} (Instance)",
                        hideFlags = HideFlags.DontSave
                    };
                }
                else
                {
                    _material = new Material(shader)
                    {
                        name = $"{SHADER_NAME} (Instance)",
                        hideFlags = HideFlags.DontSave
                    };
                }
            }
            if (tex != null)
                _material.SetTexture(PropMainTex, tex);
            _rawProgress = 0f;
            _forward = true;
            _intervalRemaining = 0f;

            _originalMaterial = _graphic.material;
            if (_material != null)
                _graphic.material = _material;

            // Material/스프라이트 변경 후 메시 재구축 유도 → ModifyMesh가 호출되어 uv2/uv3(스프라이트 UV)가 버텍스에 주입됨
            _graphic.SetVerticesDirty();
            // Material 설정 후 Image가 mesh를 다시 생성할 수 있으므로, Update에서 한 번 더 SetVerticesDirty 호출
            _meshDirtyNeeded = true;
            // Material 재초기화 시 모든 프로퍼티 재전송 필요
            _propertiesDirty = true;
        }

        private void OnDisable()
        {
            if (_graphic != null && _originalMaterial != null)
                _graphic.material = _originalMaterial;
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                if (Application.isPlaying)
                    Destroy(_material);
                else
                    DestroyImmediate(_material);
            }
            _material = null;
            if (_graphic != null && _originalMaterial != null)
                _graphic.material = _originalMaterial;
        }

        private void Update()
        {
            if (_graphic == null || ActiveMaterial == null) return;

            // Material 설정 후 첫 프레임에 mesh 재구축 (Image가 Material 변경 감지 후 mesh 재생성하므로)
            if (_meshDirtyNeeded)
            {
                _meshDirtyNeeded = false;
                _graphic.SetVerticesDirty();
            }

            // 버텍스 전달 실패 대비: fallback _SpriteUVRect 항상 설정 (Windable 방식)
            SetSpriteUVRect();

            // 에디터 모드: 프로퍼티 갱신만 수행 (애니메이션은 EditorAdvance()에서 처리)
            if (!Application.isPlaying)
            {
                ApplyProgressToMaterial();
                return;
            }

            float dt = Time.deltaTime;

            if (_intervalRemaining > 0f)
            {
                _intervalRemaining -= dt;
                if (_intervalRemaining <= 0f)
                    _intervalRemaining = 0f;
                ApplyProgressToMaterial();
                return;
            }

            float step = dt / _duration;
            if (!_forward)
                step = -step;

            _rawProgress += step;

            if (_loopType == LoopType.Replay)
            {
                if (_rawProgress >= 1f)
                {
                    _rawProgress = 0f;
                    _intervalRemaining = Random.Range(_intervalMin, _intervalMax);
                }
                else if (_rawProgress < 0f)
                {
                    _rawProgress = 0f;
                }
            }
            else
            {
                if (_rawProgress >= 1f)
                {
                    _rawProgress = 1f;
                    _forward = false;
                    _intervalRemaining = Random.Range(_intervalMin, _intervalMax);
                }
                else if (_rawProgress <= 0f)
                {
                    _rawProgress = 0f;
                    _forward = true;
                    _intervalRemaining = Random.Range(_intervalMin, _intervalMax);
                }
            }

            ApplyProgressToMaterial();
        }

        /// <summary>
        /// 실제 렌더링에 사용되는 Material 반환.
        /// CanvasRenderer에 설정된 최종 머티리얼을 직접 참조하여,
        /// materialForRendering 접근 시 발생하는 체인 재평가(StencilMaterial 캐시 문제)를 회피.
        /// </summary>
        private Material ActiveMaterial
        {
            get
            {
                if (_graphic == null) return _material;
                // Unity Mask / SoftMaskable / SoftMaskLight: CanvasRenderer의 최종 머티리얼 참조
                // (SoftMaskLight v2.1: IMaterialModifier 프록시로 graphic.m_Material 미수정)
                // materialForRendering 대신 canvasRenderer.GetMaterial을 사용하여
                // SoftMaskable.CopyPropertiesFromMaterial이 StencilMaterial 캐시에서
                // stale 값을 복사하는 문제를 방지
                var cr = _graphic.canvasRenderer;
                if (cr != null)
                {
                    Material canvasMat = cr.GetMaterial(0);
                    if (canvasMat != null && canvasMat != _material)
                        return canvasMat;
                }
                return _material;
            }
        }

        private void ApplyProgressToMaterial()
        {
            if (_material == null) return;

            float t = Mathf.Clamp01(_rawProgress);
            // .length는 GC 없이 키프레임 수를 반환 (.keys는 배열을 새로 복사하여 GC 발생)
            float progress = _movementCurve != null && _movementCurve.length > 0
                ? _movementCurve.Evaluate(t)
                : t;

            // dirty flag: 정적 프로퍼티가 변경된 경우에만 전체 갱신, 아니면 _Progress만 전송
            if (_propertiesDirty)
            {
                WriteAllProperties(_material, progress);
                Material active = ActiveMaterial;
                if (active != null && active != _material)
                    WriteAllProperties(active, progress);
                _propertiesDirty = false;
            }
            else
            {
                WriteProgressOnly(_material, progress);
                Material active = ActiveMaterial;
                if (active != null && active != _material)
                    WriteProgressOnly(active, progress);
            }
            // SetSpriteUVRect()는 Update()에서 이미 호출되므로 여기서 중복 호출하지 않음
        }

        /// <summary>_Progress만 전송 (매 프레임 변하는 값)</summary>
        private void WriteProgressOnly(Material target, float progress)
        {
            target.SetFloat(PropProgress, progress);
        }

        /// <summary>모든 셰이더 프로퍼티를 전송 (정적 프로퍼티 포함)</summary>
        private void WriteAllProperties(Material target, float progress)
        {
            target.SetFloat(PropProgress, progress);
            target.SetFloat(PropWidthStart, _widthStart);
            target.SetFloat(PropWidthEnd, _widthEnd);
            target.SetFloat(PropIntensity, _intensity);
            target.SetFloat(PropCurvatureStart, _curvatureStart);
            target.SetFloat(PropCurvatureEnd, _curvatureEnd);
            target.SetFloat(PropAngle, _angle);
            target.SetFloat(PropProgressOffset, _progressOffset);
            target.SetColor(PropShineColor, _shineColor);
            target.SetFloat(PropSoftness, _softness);
            target.SetFloat(PropShineSoftness, _softness); // SoftMaskLight Hidden 변형용
            target.SetFloat(PropBurnBias, _burnBias);
            target.SetFloat(PropBlendStrength, _blendStrength);
        }

        /// <summary>아틀라스 내 스프라이트 UV 영역을 Vector4로 반환 (머티리얼 유니폼 fallback용)</summary>
        private Vector4 GetSpriteUVRectVector()
        {
            Graphic g = _graphic != null ? _graphic : GetComponent<Graphic>();
            return g != null ? GetSpriteUVRectVectorFor(g) : new Vector4(0f, 0f, 1f, 1f);
        }

        /// <summary>아틀라스 내 스프라이트 UV 영역을 머티리얼에 전달 (Windable과 동일 방식)</summary>
        private void SetSpriteUVRect()
        {
            if (_graphic == null) return;
            Vector4 uvRect = GetSpriteUVRectVector();

            // 기본 머티리얼에 항상 설정
            if (_material != null)
                _material.SetVector(PropSpriteUVRect, uvRect);

            // 렌더링 머티리얼이 다르면 거기에도 설정
            Material active = ActiveMaterial;
            if (active != null && active != _material)
                active.SetVector(PropSpriteUVRect, uvRect);

            // UV가 변경된 경우에만 MaterialDirty 호출 (매 프레임 체인 재평가 방지)
            if (uvRect != _lastSpriteUVRect)
            {
                _lastSpriteUVRect = uvRect;
                _graphic.SetMaterialDirty();
            }
        }

        private Texture GetUITexture()
        {
            if (_graphic is Image img && img.sprite != null)
                return img.sprite.texture;
            if (_graphic is RawImage raw)
                return raw.texture;
            return null;
        }

        private static Shader GetCachedShader()
        {
            if (_cachedShader == null)
            {
                _cachedShader = Shader.Find(SHADER_NAME);
                if (_cachedShader == null)
                    Debug.LogError($"[UIShining] 셰이더를 찾을 수 없습니다: {SHADER_NAME}");
            }
            return _cachedShader;
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            // 에디터에서 값 변경 시 모든 프로퍼티 재전송 필요
            _propertiesDirty = true;
            // 에디터에서 값 변경 시 메시 재구축 및 Material Property 갱신
            if (_graphic != null && !Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && _graphic != null)
                    {
                        _graphic.SetVerticesDirty();
                        if (_material != null)
                        {
                            SetSpriteUVRect();
                            ApplyProgressToMaterial();
                        }
                    }
                };
            }
        }
        #endif
    }
}
