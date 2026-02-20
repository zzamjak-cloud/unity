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
        private static readonly int PropBurnBias = Shader.PropertyToID("_BurnBias");
        private static readonly int PropBlendStrength = Shader.PropertyToID("_BlendStrength");
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");
        private static readonly int PropSpriteUVRect = Shader.PropertyToID("_SpriteUVRect");

        public enum LoopType
        {
            Replay,
            Yoyo
        }

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

        private Graphic _graphic;
        private Material _material;
        private Material _originalMaterial;
        private float _rawProgress;
        private bool _forward = true;
        private float _intervalRemaining;
        private bool _meshDirtyNeeded;

        /// <summary>에디터 전용: true일 때 에디터에서도 루프 애니메이션 재생 (60초간)</summary>
        [SerializeField, HideInInspector] private bool _editorTestRunning;
        /// <summary>에디터 전용: 테스트 재생 시작 시간 (60초 타이머용)</summary>
        [SerializeField, HideInInspector] private double _editorTestStartTime;

        /// <summary>진행을 0으로 되돌리고 Material에 반영. 에디터 테스트 중지 시 즉시 초기화용.</summary>
        public void ResetProgressToStart()
        {
            _rawProgress = 0f;
            _forward = true;
            _intervalRemaining = 0f;
            if (_material != null)
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

        /// <summary>Material 재초기화</summary>
        private void ReInitializeMaterial()
        {
            if (_graphic == null) return;

            Shader shader = GetCachedShader();
            if (shader == null) return;

            Texture tex = GetUITexture();
            if (_material == null)
            {
                _material = new Material(shader)
                {
                    name = $"{SHADER_NAME} (Instance)",
                    hideFlags = HideFlags.DontSave
                };
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
            if (_material == null || _graphic == null) return;

            // Material 설정 후 첫 프레임에 mesh 재구축 (Image가 Material 변경 감지 후 mesh 재생성하므로)
            if (_meshDirtyNeeded)
            {
                _meshDirtyNeeded = false;
                _graphic.SetVerticesDirty();
            }

            // 버텍스 전달 실패 대비: fallback _SpriteUVRect 항상 설정 (Windable 방식)
            SetSpriteUVRect();

            // 에디터 전용: _editorTestRunning이 아니면 옵션값만 갱신, true면 60초간 루프 재생
            if (!Application.isPlaying)
            {
                if (_editorTestRunning)
                {
                    #if UNITY_EDITOR
                    // 60초 경과 확인
                    double elapsed = UnityEditor.EditorApplication.timeSinceStartup - _editorTestStartTime;
                    if (elapsed >= 60.0)
                    {
                        _editorTestRunning = false;
                        ResetProgressToStart();
                        return;
                    }
                    #endif
                }
                else
                {
                    ApplyProgressToMaterial();
                    return;
                }
            }

            if (_intervalRemaining > 0f)
            {
                _intervalRemaining -= Time.deltaTime;
                if (_intervalRemaining <= 0f)
                    _intervalRemaining = 0f;
                ApplyProgressToMaterial();
                return;
            }

            float step = Time.deltaTime / _duration;
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

        private void ApplyProgressToMaterial()
        {
            float t = Mathf.Clamp01(_rawProgress);
            float progress = _movementCurve != null && _movementCurve.keys.Length > 0
                ? _movementCurve.Evaluate(t)
                : t;

            _material.SetFloat(PropProgress, progress);
            _material.SetFloat(PropWidthStart, _widthStart);
            _material.SetFloat(PropWidthEnd, _widthEnd);
            _material.SetFloat(PropIntensity, _intensity);
            _material.SetFloat(PropCurvatureStart, _curvatureStart);
            _material.SetFloat(PropCurvatureEnd, _curvatureEnd);
            _material.SetFloat(PropAngle, _angle);
            _material.SetFloat(PropProgressOffset, _progressOffset);
            _material.SetColor(PropShineColor, _shineColor);
            _material.SetFloat(PropSoftness, _softness);
            _material.SetFloat(PropBurnBias, _burnBias);
            _material.SetFloat(PropBlendStrength, _blendStrength);
            SetSpriteUVRect();
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
            if (_material == null || _graphic == null) return;
            _material.SetVector(PropSpriteUVRect, GetSpriteUVRectVector());
            // Windable 방식: SetMaterialDirty 호출하여 Material 변경 알림
            _graphic.SetMaterialDirty();
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
