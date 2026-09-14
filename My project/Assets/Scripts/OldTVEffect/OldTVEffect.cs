using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>
    /// 구형 TV 효과. SpriteRenderer 또는 uGUI Image/RawImage 에 부착한다.
    /// - 설정값이 동일한 컴포넌트끼리 머티리얼을 공유한다 (참조 카운트, 동일 설정 = 동일 배치).
    /// - UI 는 IMaterialModifier 로 렌더링 머티리얼만 교체하므로 Graphic.material 직렬화 필드를 건드리지 않는다.
    /// - Sprite 는 sharedMaterial 을 교체하되 원본을 직렬화 백업하고, 씬/프리팹 저장 중에는 원본으로 되돌린다.
    /// - 프로퍼티는 값이 바뀔 때만 갱신, 저사양 모드는 셰이더 키워드로 실제 GPU 비용 절감.
    /// </summary>
    [AddComponentMenu("CAT/Effects/OldTVEffect")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class OldTVEffect : MonoBehaviour, IMaterialModifier
    {
        public enum PerformanceMode
        {
            /// <summary>QualitySettings 레벨이 lowQualityMaxLevel 이하면 Low</summary>
            Auto,
            High,
            Low
        }

        public enum ScanLineSpace
        {
            /// <summary>텍스처 UV 기준 (오브젝트 크기에 따라 줄 간격이 달라짐)</summary>
            Texture,
            /// <summary>화면 픽셀 기준 (TV 화면처럼 일정한 줄 간격)</summary>
            Screen
        }

        private const string SpriteShaderName = "CAT/Effects/OldTVEffect_Sprite";
        private const string UIShaderName = "CAT/Effects/OldTVEffect_UI";
        private const string ColorBleedKeyword = "_COLORBLEED_ON";
        private const string ScreenSpaceScanLinesKeyword = "_SCREENSPACE_SCANLINES";
        private const string UIAlphaClipKeyword = "UNITY_UI_ALPHACLIP";

        [Header("References")]
        [Tooltip("빌드 시 셰이더 스트립을 막기 위한 직접 참조. 비어 있으면 에디터에서 자동 할당된다.")]
        [SerializeField] private Shader spriteShader;
        [SerializeField] private Shader uiShader;

        [Tooltip("노이즈 텍스처 (R 채널만 사용). 비어 있으면 기본 노이즈가 생성된다.\n" +
                 "임포트 설정에서 sRGB 를 해제해야 그레인이 밝기 대칭으로 나온다.")]
        public Texture2D noiseTexture;

        [Header("Effect Parameters")]
        [Range(0f, 1f)] public float noiseIntensity = 0.5f;
        [Range(0.1f, 10f)] public float noiseScale = 3f;
        [Range(0f, 1f)] public float scanLineIntensity = 0.5f;
        [Min(10f)] public float scanLineCount = 100f;
        [Range(0.1f, 5f)] public float scanLineThickness = 1f;
        public ScanLineSpace scanLineSpace = ScanLineSpace.Texture;
        [Range(0f, 0.1f)] public float verticalJitter = 0.01f;
        [Range(0f, 0.1f)] public float horizontalJitter = 0.01f;
        [Range(0f, 0.5f)] public float colorBleed = 0.1f;
        [Range(0f, 0.1f)] public float colorBleedOffset = 0.02f;
        [Range(0f, 5f)] public float rollSpeed = 1f;
        [Range(0f, 1f)] public float saturation = 1f;

        [Header("Performance")]
        public PerformanceMode performanceMode = PerformanceMode.High;
        [Tooltip("Auto 모드에서 이 QualitySettings 레벨 이하를 저사양으로 취급")]
        [Min(0)] public int lowQualityMaxLevel = 0;

        // Sprite 원본 머티리얼 백업. 어떤 저장 경로로 렌더러 참조가 null 이 되어도 복원 가능하도록 직렬화한다.
        [SerializeField, HideInInspector] private Material spriteOriginalMaterial;

        // ---- 공유 머티리얼 캐시 ----

        /// <summary>머티리얼 공유 키 (전체 설정값 + UI 스텐실 상태)</summary>
        private struct MaterialKey : IEquatable<MaterialKey>
        {
            public int shaderId;
            public int noiseTexId;
            public bool colorBleedOn;
            public bool screenSpaceScanLines;
            public float noiseIntensity, noiseScale, scanLineIntensity, scanLineCount, scanLineThicknessInv;
            public float verticalJitter, horizontalJitter, colorBleed, colorBleedOffset, rollSpeed, saturation;
            public Vector4 uvRect;
            // UI 전용: Mask 가 StencilMaterial 로 넘긴 스텐실 상태를 그대로 승계
            public int stencilId, stencilComp, stencilOp, stencilReadMask, stencilWriteMask, colorMask;
            public bool alphaClip;

            public bool Equals(MaterialKey o) =>
                shaderId == o.shaderId && noiseTexId == o.noiseTexId &&
                colorBleedOn == o.colorBleedOn && screenSpaceScanLines == o.screenSpaceScanLines &&
                noiseIntensity == o.noiseIntensity && noiseScale == o.noiseScale &&
                scanLineIntensity == o.scanLineIntensity && scanLineCount == o.scanLineCount &&
                scanLineThicknessInv == o.scanLineThicknessInv && verticalJitter == o.verticalJitter &&
                horizontalJitter == o.horizontalJitter && colorBleed == o.colorBleed &&
                colorBleedOffset == o.colorBleedOffset && rollSpeed == o.rollSpeed && saturation == o.saturation &&
                uvRect.Equals(o.uvRect) &&
                stencilId == o.stencilId && stencilComp == o.stencilComp && stencilOp == o.stencilOp &&
                stencilReadMask == o.stencilReadMask && stencilWriteMask == o.stencilWriteMask &&
                colorMask == o.colorMask && alphaClip == o.alphaClip;

            public override bool Equals(object obj) => obj is MaterialKey k && Equals(k);

            public override int GetHashCode()
            {
                var h = new HashCode();
                h.Add(shaderId); h.Add(noiseTexId); h.Add(colorBleedOn); h.Add(screenSpaceScanLines);
                h.Add(noiseIntensity); h.Add(noiseScale); h.Add(scanLineIntensity); h.Add(scanLineCount);
                h.Add(scanLineThicknessInv); h.Add(verticalJitter); h.Add(horizontalJitter); h.Add(colorBleed);
                h.Add(colorBleedOffset); h.Add(rollSpeed); h.Add(saturation);
                h.Add(uvRect.x); h.Add(uvRect.y); h.Add(uvRect.z); h.Add(uvRect.w);
                h.Add(stencilId); h.Add(stencilComp); h.Add(stencilOp); h.Add(stencilReadMask);
                h.Add(stencilWriteMask); h.Add(colorMask); h.Add(alphaClip);
                return h.ToHashCode();
            }
        }

        private class SharedMaterial
        {
            public Material material;
            public int refCount;
        }

        private static readonly Dictionary<MaterialKey, SharedMaterial> s_sharedMaterials = new Dictionary<MaterialKey, SharedMaterial>();
        private static Texture2D s_defaultNoiseTexture;

        // ---- 인스턴스 상태 ----

        private SpriteRenderer _spriteRenderer;
        private Graphic _graphic;
        private Material _currentMaterial;
        private MaterialKey _currentKey;
        private bool _hasCurrentKey;
        private MaterialKey _uiBaseKey; // 스텐실 제외 UI 키 (GetModifiedMaterial 에서 스텐실 병합)
        private Sprite _lastSprite;
        private Texture _lastTexture;
        private Rect _lastRawUVRect;
        private int _lastQualityLevel = -1;
        private bool _attached;
        private bool _suspended;
        private bool _dirty = true;

        private static readonly int NoiseTexProp = Shader.PropertyToID("_NoiseTex");
        private static readonly int NoiseIntensityProp = Shader.PropertyToID("_NoiseIntensity");
        private static readonly int NoiseScaleProp = Shader.PropertyToID("_NoiseScale");
        private static readonly int ScanLineIntensityProp = Shader.PropertyToID("_ScanLineIntensity");
        private static readonly int ScanLineCountProp = Shader.PropertyToID("_ScanLineCount");
        private static readonly int ScanLineThicknessInvProp = Shader.PropertyToID("_ScanLineThicknessInv");
        private static readonly int VerticalJitterProp = Shader.PropertyToID("_VerticalJitter");
        private static readonly int HorizontalJitterProp = Shader.PropertyToID("_HorizontalJitter");
        private static readonly int ColorBleedProp = Shader.PropertyToID("_ColorBleed");
        private static readonly int ColorBleedOffsetProp = Shader.PropertyToID("_ColorBleedOffset");
        private static readonly int RollSpeedProp = Shader.PropertyToID("_RollSpeed");
        private static readonly int SaturationProp = Shader.PropertyToID("_Saturation");
        private static readonly int UVRectProp = Shader.PropertyToID("_UVRect");
        private static readonly int StencilProp = Shader.PropertyToID("_Stencil");
        private static readonly int StencilCompProp = Shader.PropertyToID("_StencilComp");
        private static readonly int StencilOpProp = Shader.PropertyToID("_StencilOp");
        private static readonly int StencilReadMaskProp = Shader.PropertyToID("_StencilReadMask");
        private static readonly int StencilWriteMaskProp = Shader.PropertyToID("_StencilWriteMask");
        private static readonly int ColorMaskProp = Shader.PropertyToID("_ColorMask");

        /// <summary>현재 실제로 저사양 모드로 동작 중인지</summary>
        public bool IsLowPerformanceActive
        {
            get
            {
                switch (performanceMode)
                {
                    case PerformanceMode.High: return false;
                    case PerformanceMode.Low: return true;
                    default: return QualitySettings.GetQualityLevel() <= lowQualityMaxLevel;
                }
            }
        }

        /// <summary>현재 렌더링에 사용 중인 공유 머티리얼 (디버그/검증용)</summary>
        public Material CurrentMaterial => _currentMaterial;

        /// <summary>public 필드를 코드로 변경한 뒤 호출하면 다음 프레임에 반영된다.</summary>
        public void MarkDirty()
        {
            _dirty = true;
        }

        // ---- Unity 콜백 ----

        private void OnEnable()
        {
            ResolveShaders();
            Attach();
#if UNITY_EDITOR
            s_instances.Add(this);
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            s_instances.Remove(this);
#endif
            Detach();
        }

        private void OnDestroy()
        {
            Detach();
        }

        private void OnValidate()
        {
            _dirty = true;
#if UNITY_EDITOR
            ResolveShaders();
#endif
        }

        private void Reset()
        {
            ResolveShaders();
            _dirty = true;
        }

        private void Update()
        {
            if (!_attached)
                return;

            // 대상 텍스처/스프라이트 변경 → UV 클램프 범위 갱신
            if (_spriteRenderer != null)
            {
                if (_spriteRenderer.sprite != _lastSprite) _dirty = true;
            }
            else if (_graphic is Image image)
            {
                if (image.sprite != _lastSprite) _dirty = true;
            }
            else if (_graphic is RawImage raw)
            {
                if (raw.texture != _lastTexture || raw.uvRect != _lastRawUVRect) _dirty = true;
            }

            // 품질 레벨 변경 → Auto 모드 재평가
            if (performanceMode == PerformanceMode.Auto)
            {
                int level = QualitySettings.GetQualityLevel();
                if (level != _lastQualityLevel)
                {
                    _lastQualityLevel = level;
                    _dirty = true;
                }
            }

            if (_dirty)
                ApplySettings();
        }

        /// <summary>IMaterialModifier: UI 렌더링 머티리얼을 공유 머티리얼로 교체 (Graphic.material 은 그대로)</summary>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!_attached || _graphic == null || baseMaterial == null)
                return baseMaterial;

            // Mask(StencilMaterial) 가 앞 단계에서 넣은 스텐실 상태를 승계
            MaterialKey key = _uiBaseKey;
            if (baseMaterial.HasProperty(StencilProp))
            {
                key.stencilId = (int)baseMaterial.GetFloat(StencilProp);
                key.stencilComp = (int)baseMaterial.GetFloat(StencilCompProp);
                key.stencilOp = (int)baseMaterial.GetFloat(StencilOpProp);
                key.stencilReadMask = (int)baseMaterial.GetFloat(StencilReadMaskProp);
                key.stencilWriteMask = (int)baseMaterial.GetFloat(StencilWriteMaskProp);
                key.colorMask = (int)baseMaterial.GetFloat(ColorMaskProp);
            }
            else
            {
                key.stencilComp = (int)CompareFunction.Always;
                key.stencilReadMask = 255;
                key.stencilWriteMask = 255;
                key.colorMask = 15;
            }
            key.alphaClip = baseMaterial.IsKeywordEnabled(UIAlphaClipKeyword);

            return AcquireMaterial(key, uiShader, ResolveNoiseTexture());
        }

        // ---- 부착/해제 ----

        private void ResolveShaders()
        {
            // 런타임 Shader.Find 는 빌드에서 스트립될 수 있으므로 직렬화 참조를 우선한다.
            bool assigned = false;
            if (spriteShader == null)
            {
                spriteShader = Shader.Find(SpriteShaderName);
                assigned |= spriteShader != null;
            }
            if (uiShader == null)
            {
                uiShader = Shader.Find(UIShaderName);
                assigned |= uiShader != null;
            }

#if UNITY_EDITOR
            // 에디터에서 자동 할당된 참조가 프리팹/씬에 저장되도록 표시
            if (assigned && !Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void Attach()
        {
            if (_attached)
                return;

            _spriteRenderer = GetComponent<SpriteRenderer>();
            _graphic = GetComponent<Graphic>();

            if (_spriteRenderer != null)
            {
                if (spriteShader == null) { LogMissingShader(); return; }
                CaptureSpriteOriginalMaterial();
            }
            else if (_graphic is Image || _graphic is RawImage)
            {
                if (uiShader == null) { LogMissingShader(); return; }
            }
            else
            {
                Debug.LogWarning("OldTVEffect: SpriteRenderer, Image 또는 RawImage 컴포넌트가 필요합니다.", this);
                return;
            }

            _attached = true;
            _suspended = false;
            ApplySettings();
        }

        private void Detach()
        {
            if (!_attached)
                return;

            _attached = false;
            _suspended = false;

            if (_spriteRenderer != null)
            {
                // 렌더러가 여전히 우리 머티리얼(또는 null)을 가리키면 원본 복원
                Material cur = _spriteRenderer.sharedMaterial;
                if (cur == null || cur == _currentMaterial || IsOwnMaterial(cur))
                    _spriteRenderer.sharedMaterial = spriteOriginalMaterial;
            }

            ReleaseCurrentMaterial();

            if (_graphic != null)
                _graphic.SetMaterialDirty();

            _lastSprite = null;
            _lastTexture = null;
            _lastQualityLevel = -1;
            _dirty = true;
        }

        /// <summary>
        /// Sprite 원본 머티리얼 백업. 복제(Ctrl+D/Instantiate)로 이미 공유 머티리얼이 물려 있거나
        /// 저장으로 null 이 된 경우에는 직렬화된 백업 → 파이프라인 기본 2D 머티리얼 순으로 대체한다.
        /// </summary>
        private void CaptureSpriteOriginalMaterial()
        {
            Material cur = _spriteRenderer.sharedMaterial;
            if (cur != null && !IsOwnMaterial(cur))
            {
                spriteOriginalMaterial = cur;
                return;
            }

            if (spriteOriginalMaterial != null && !IsOwnMaterial(spriteOriginalMaterial))
                return;

            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            spriteOriginalMaterial = rp != null ? rp.default2DMaterial : null;
        }

        /// <summary>이 컴포넌트가 생성한 공유 머티리얼인지 (DontSave + 우리 셰이더)</summary>
        private bool IsOwnMaterial(Material m)
        {
            if (m == null || (m.hideFlags & HideFlags.DontSave) == 0)
                return false;
            return m.shader == spriteShader || m.shader == uiShader;
        }

        private void LogMissingShader()
        {
            Debug.LogError("OldTVEffect: 셰이더 참조가 없습니다. 인스펙터에서 셰이더를 지정하세요. " +
                           "(런타임 Shader.Find 는 빌드에서 스트립되어 실패할 수 있음)", this);
        }

        // ---- 설정 적용 ----

        private void ApplySettings()
        {
            _dirty = false;
            if (!_attached || _suspended)
                return;

            bool low = IsLowPerformanceActive;

            // 저사양: 컬러 블리드 변형 제거(텍스처 3탭 → 1탭), 지터·노이즈 축소
            float intensityScale = low ? 0.7f : 1f;
            float jitterScale = low ? 0.5f : 1f;
            Texture noiseTex = ResolveNoiseTexture();

            var key = new MaterialKey
            {
                shaderId = (_spriteRenderer != null ? spriteShader : uiShader).GetInstanceID(),
                noiseTexId = noiseTex.GetInstanceID(),
                colorBleedOn = !low && colorBleed > 0f && colorBleedOffset > 0f,
                screenSpaceScanLines = scanLineSpace == ScanLineSpace.Screen,
                noiseIntensity = noiseIntensity * intensityScale,
                noiseScale = noiseScale,
                scanLineIntensity = scanLineIntensity,
                scanLineCount = low ? Mathf.Min(scanLineCount, 60f) : scanLineCount,
                scanLineThicknessInv = 1f / Mathf.Max(scanLineThickness, 0.01f),
                verticalJitter = verticalJitter * jitterScale,
                horizontalJitter = horizontalJitter * jitterScale,
                colorBleed = colorBleed,
                colorBleedOffset = colorBleedOffset,
                rollSpeed = rollSpeed,
                saturation = saturation,
                uvRect = ComputeUVRect()
            };

            if (_spriteRenderer != null)
            {
                Material mat = AcquireMaterial(key, spriteShader, noiseTex);
                if (_spriteRenderer.sharedMaterial != mat)
                    _spriteRenderer.sharedMaterial = mat;
            }
            else
            {
                // 실제 머티리얼 획득은 GetModifiedMaterial 에서 스텐실 상태를 병합한 뒤 수행
                _uiBaseKey = key;
                _graphic.SetMaterialDirty();
            }
        }

        private Texture ResolveNoiseTexture()
        {
            return noiseTexture != null ? noiseTexture : GetDefaultNoiseTexture();
        }

        /// <summary>키에 해당하는 공유 머티리얼을 얻고 참조 카운트를 갱신한다. 이전 머티리얼은 해제.</summary>
        private Material AcquireMaterial(MaterialKey key, Shader shader, Texture noiseTex)
        {
            if (_hasCurrentKey && _currentKey.Equals(key) && _currentMaterial != null)
                return _currentMaterial;

            ReleaseCurrentMaterial();

            if (!s_sharedMaterials.TryGetValue(key, out SharedMaterial shared))
            {
                shared = new SharedMaterial();
                s_sharedMaterials[key] = shared;
            }

            // 외부에서 파괴된 경우 엔트리(참조 카운트)는 유지하고 머티리얼만 재생성
            if (shared.material == null)
                shared.material = CreateMaterial(key, shader, noiseTex);

            shared.refCount++;
            _currentKey = key;
            _hasCurrentKey = true;
            _currentMaterial = shared.material;
            return _currentMaterial;
        }

        private static Material CreateMaterial(in MaterialKey key, Shader shader, Texture noiseTex)
        {
            var mat = new Material(shader)
            {
                name = shader.name + " (Shared)",
                hideFlags = HideFlags.HideAndDontSave
            };

            SetKeyword(mat, ColorBleedKeyword, key.colorBleedOn);
            SetKeyword(mat, ScreenSpaceScanLinesKeyword, key.screenSpaceScanLines);

            mat.SetTexture(NoiseTexProp, noiseTex);
            mat.SetFloat(NoiseIntensityProp, key.noiseIntensity);
            mat.SetFloat(NoiseScaleProp, key.noiseScale);
            mat.SetFloat(ScanLineIntensityProp, key.scanLineIntensity);
            mat.SetFloat(ScanLineCountProp, key.scanLineCount);
            mat.SetFloat(ScanLineThicknessInvProp, key.scanLineThicknessInv);
            mat.SetFloat(VerticalJitterProp, key.verticalJitter);
            mat.SetFloat(HorizontalJitterProp, key.horizontalJitter);
            mat.SetFloat(ColorBleedProp, key.colorBleed);
            mat.SetFloat(ColorBleedOffsetProp, key.colorBleedOffset);
            mat.SetFloat(RollSpeedProp, key.rollSpeed);
            mat.SetFloat(SaturationProp, key.saturation);
            mat.SetVector(UVRectProp, key.uvRect);

            if (mat.HasProperty(StencilProp))
            {
                mat.SetFloat(StencilProp, key.stencilId);
                mat.SetFloat(StencilCompProp, key.stencilComp);
                mat.SetFloat(StencilOpProp, key.stencilOp);
                mat.SetFloat(StencilReadMaskProp, key.stencilReadMask);
                mat.SetFloat(StencilWriteMaskProp, key.stencilWriteMask);
                mat.SetFloat(ColorMaskProp, key.colorMask);
                SetKeyword(mat, UIAlphaClipKeyword, key.alphaClip);
            }

            return mat;
        }

        private void ReleaseCurrentMaterial()
        {
            if (!_hasCurrentKey)
                return;

            if (s_sharedMaterials.TryGetValue(_currentKey, out SharedMaterial shared))
            {
                shared.refCount--;
                if (shared.refCount <= 0)
                {
                    s_sharedMaterials.Remove(_currentKey);
                    if (shared.material != null)
                    {
                        if (Application.isPlaying) Destroy(shared.material);
                        else DestroyImmediate(shared.material);
                    }
                }
            }

            _hasCurrentKey = false;
            _currentMaterial = null;
        }

        private static void SetKeyword(Material mat, string keyword, bool enabled)
        {
            if (enabled) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        /// <summary>대상이 차지하는 텍스처 UV 범위. 아틀라스 이웃이 지터/블리드로 비치는 것을 막는다.</summary>
        private Vector4 ComputeUVRect()
        {
            Sprite sprite = null;
            if (_spriteRenderer != null)
            {
                sprite = _spriteRenderer.sprite;
            }
            else if (_graphic is Image image)
            {
                sprite = image.sprite;
            }
            else if (_graphic is RawImage raw)
            {
                // RawImage 는 uvRect 타일링을 유지하도록 그 범위로 클램프
                _lastTexture = raw.texture;
                _lastRawUVRect = raw.uvRect;
                Rect u = raw.uvRect;
                return new Vector4(u.xMin, u.yMin, u.xMax, u.yMax);
            }

            _lastSprite = sprite;

            // Tight 패킹은 textureRect 접근이 예외를 던지므로 전체 범위 사용
            if (sprite == null || sprite.texture == null ||
                (sprite.packed && sprite.packingMode == SpritePackingMode.Tight))
                return new Vector4(0f, 0f, 1f, 1f);

            Rect r = sprite.textureRect;
            float w = sprite.texture.width;
            float h = sprite.texture.height;
            return new Vector4(r.xMin / w, r.yMin / h, r.xMax / w, r.yMax / h);
        }

        private static Texture2D GetDefaultNoiseTexture()
        {
            if (s_defaultNoiseTexture != null)
                return s_defaultNoiseTexture;

            // 셰이더는 .r 채널만 사용하므로 단일 채널 포맷 우선. Linear 로 생성해야 그레인이 밝기 대칭.
            const int size = 256;
            TextureFormat format = SystemInfo.SupportsTextureFormat(TextureFormat.R8)
                ? TextureFormat.R8
                : TextureFormat.RGBA32;

            var texture = new Texture2D(size, size, format, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "OldTVEffect_DefaultNoise"
            };

            var pixels = new Color32[size * size];
            var random = new System.Random(12345);
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)random.Next(256);
                pixels[i] = new Color32(v, v, v, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            s_defaultNoiseTexture = texture;
            return texture;
        }

#if UNITY_EDITOR
        /// <summary>에디터 셰이더 전역 시간 오프셋 프로퍼티 (60초 테스트 드라이버가 설정)</summary>
        public const string EditorTimeProperty = "_CAT_TVEditorTime";

        /// <summary>에디터 미리보기 틱: 인스펙터에서 바뀐 값을 즉시 머티리얼에 반영</summary>
        public void EditorPreviewTick()
        {
            if (_attached && _dirty)
                ApplySettings();
        }

        // ---- 에디터 저장 대응 (Sprite 경로) ----
        // DontSave 머티리얼이 SpriteRenderer 에 물린 상태로 저장되면 렌더러 참조가 null 로 기록된다.
        // 저장 직전 렌더러만 원본으로 되돌리고(머티리얼은 유지) 저장 후 다시 물린다.
        // UI 는 IMaterialModifier 라 직렬화 필드를 건드리지 않으므로 해당 없음.

        private static readonly HashSet<OldTVEffect> s_instances = new HashSet<OldTVEffect>();
        private static readonly List<OldTVEffect> s_instanceBuffer = new List<OldTVEffect>();

        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorSaveHooks()
        {
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (_, __) => SuspendAll();
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += _ => ResumeAll();
            UnityEditor.SceneManagement.PrefabStage.prefabSaving += _ => SuspendAll();
            UnityEditor.SceneManagement.PrefabStage.prefabSaved += _ => ResumeAll();
        }

        /// <summary>AssetDatabase.SaveAssets / 프리팹 Apply 등 sceneSaving 을 거치지 않는 저장 경로 대응</summary>
        private class SaveHook : UnityEditor.AssetModificationProcessor
        {
            private static string[] OnWillSaveAssets(string[] paths)
            {
                SuspendAll();
                return paths;
            }
        }

        private static void SuspendAll()
        {
            s_instanceBuffer.Clear();
            s_instanceBuffer.AddRange(s_instances);
            foreach (var fx in s_instanceBuffer)
                if (fx != null) fx.Suspend();

            // 저장 실패/취소로 완료 콜백이 오지 않아도 반드시 복귀
            UnityEditor.EditorApplication.delayCall -= ResumeAll;
            UnityEditor.EditorApplication.delayCall += ResumeAll;
        }

        private static void ResumeAll()
        {
            UnityEditor.EditorApplication.delayCall -= ResumeAll;
            s_instanceBuffer.Clear();
            s_instanceBuffer.AddRange(s_instances);
            foreach (var fx in s_instanceBuffer)
                if (fx != null) fx.Resume();
        }

        private void Suspend()
        {
            if (!_attached || _suspended || _spriteRenderer == null)
                return;
            _suspended = true;
            if (_spriteRenderer.sharedMaterial == _currentMaterial)
                _spriteRenderer.sharedMaterial = spriteOriginalMaterial;
        }

        private void Resume()
        {
            if (!_attached || !_suspended)
                return;
            _suspended = false;
            if (_spriteRenderer != null && _currentMaterial != null)
                _spriteRenderer.sharedMaterial = _currentMaterial;
            if (_dirty)
                ApplySettings();
        }
#endif
    }
}
