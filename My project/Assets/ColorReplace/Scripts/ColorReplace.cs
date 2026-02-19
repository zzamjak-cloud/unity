using UnityEngine;
using System.Collections.Generic;

namespace CAT.Effects
{
    /// <summary>
    /// HSV 색상 변환 컴포넌트
    /// - 프리셋 사용 시: ColorReplaceMaterialCache로 Material 공유
    /// - 프리셋 미사용 시: 기존 해시 기반 캐싱 (하위 호환성)
    /// </summary>
    [AddComponentMenu("CAT/Effects/ColorReplace")]
    public class ColorReplace : MonoBehaviour, IColorReplaceSettings
    {
        public static readonly string SHADER_NAME = "CAT/Effects/ColorReplace";

        // 셰이더 캐싱
        private static Shader cachedShader;

        // SpriteRenderer용: 텍스처 기반 Material 캐싱 (PropertyBlock으로 개별 값 설정)
        private static readonly Dictionary<int, Material> spriteMaterialCache = new Dictionary<int, Material>();

        // UI용: 텍스처 + HSV 설정 기반 Material 캐싱 (같은 설정만 공유)
        private static readonly Dictionary<int, Material> uiMaterialCache = new Dictionary<int, Material>();

        // Shader Property ID 캐싱
        private static readonly int PropHSVRangeMin = Shader.PropertyToID("_HSVRangeMin");
        private static readonly int PropHSVRangeMax = Shader.PropertyToID("_HSVRangeMax");
        private static readonly int PropHSVAdjust = Shader.PropertyToID("_HSVAAdjust");
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");

        // ─────────────────────────────────────────────
        // 프리셋 시스템
        // ─────────────────────────────────────────────

        [Header("Preset")]
        [SerializeField] private ColorReplacePreset _preset;

        /// <summary>현재 적용된 프리셋</summary>
        public ColorReplacePreset Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                if (_preset != null && initialized)
                {
                    ApplyPreset(_preset);
                }
            }
        }

        // ─────────────────────────────────────────────
        // HSV 설정
        // ─────────────────────────────────────────────

        [Header("HSV Range")]
        [SerializeField, Range(0f, 1f)] private float _hsvRangeMin = 0f;
        public float HSVRangeMin
        {
            get => _hsvRangeMin;
            set
            {
                _hsvRangeMin = Mathf.Clamp01(value);
                ApplyProperties();
            }
        }

        [SerializeField, Range(0f, 1f)] private float _hsvRangeMax = 1f;
        public float HSVRangeMax
        {
            get => _hsvRangeMax;
            set
            {
                _hsvRangeMax = Mathf.Clamp01(value);
                ApplyProperties();
            }
        }

        [Header("HSV Adjust")]
        [SerializeField] private Vector4 _hsvAdjust = Vector4.zero;
        public Vector4 HSVAdjust
        {
            get => _hsvAdjust;
            set
            {
                _hsvAdjust = value;
                ApplyProperties();
            }
        }

        private Material currentMaterial;       // 현재 사용 중인 Material
        private Material originalMaterial;      // 원본 Material (복원용)
        private SpriteRenderer spriteRenderer;
        private UnityEngine.UI.Graphic uiGraphic;
        private MaterialPropertyBlock propertyBlock;
        private bool isUIComponent;
        private bool initialized;
        private int currentCacheKey;            // 현재 캐시 키 (프리셋 미사용 시)
        private int textureId;                  // 텍스처 ID
        private bool usingPresetCache;          // 프리셋 캐시 사용 여부

        // ─────────────────────────────────────────────
        // IColorReplaceSettings 구현
        // ─────────────────────────────────────────────

        float IColorReplaceSettings.HSVRangeMin => _hsvRangeMin;
        float IColorReplaceSettings.HSVRangeMax => _hsvRangeMax;
        Vector4 IColorReplaceSettings.HSVAdjust => _hsvAdjust;

        /// <summary>
        /// Material 공유를 위한 해시 계산
        /// </summary>
        public int GetMaterialHash()
        {
            unchecked
            {
                const uint FNV_PRIME = 16777619;
                const uint FNV_OFFSET = 2166136261;
                uint hash = FNV_OFFSET;

                int minBits = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvRangeMin), 0);
                int maxBits = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvRangeMax), 0);

                hash = (hash ^ (uint)minBits) * FNV_PRIME;
                hash = (hash ^ (uint)maxBits) * FNV_PRIME;

                int adjustX = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvAdjust.x), 0);
                int adjustY = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvAdjust.y), 0);
                int adjustZ = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvAdjust.z), 0);
                int adjustW = System.BitConverter.ToInt32(System.BitConverter.GetBytes(_hsvAdjust.w), 0);

                hash = (hash ^ (uint)adjustX) * FNV_PRIME;
                hash = (hash ^ (uint)adjustY) * FNV_PRIME;
                hash = (hash ^ (uint)adjustZ) * FNV_PRIME;
                hash = (hash ^ (uint)adjustW) * FNV_PRIME;

                return (int)hash;
            }
        }

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!initialized)
            {
                Initialize();
            }
            ApplyProperties();
        }

        private void Initialize()
        {
            if (initialized) return;

            if (!Application.isPlaying && !gameObject.scene.IsValid())
            {
                return;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                isUIComponent = false;
                InitializeSpriteRenderer();
            }
            else
            {
                uiGraphic = GetComponent<UnityEngine.UI.Graphic>();
                if (uiGraphic != null)
                {
                    isUIComponent = true;
                    InitializeUIGraphic();
                }
                else
                {
                    Debug.LogWarning($"[ColorReplace] {gameObject.name}: SpriteRenderer 또는 UI.Graphic 컴포넌트가 필요합니다.");
                    return;
                }
            }

            initialized = (currentMaterial != null);
        }

        /// <summary>
        /// SpriteRenderer 초기화
        /// 프리셋 사용 시: MaterialCache로 공유
        /// 프리셋 미사용 시: 텍스처 기반 Material + PropertyBlock
        /// </summary>
        private void InitializeSpriteRenderer()
        {
            if (spriteRenderer.sprite == null) return;

            Shader shader = GetCachedShader();
            if (shader == null) return;

            Texture texture = spriteRenderer.sprite.texture;
            textureId = texture != null ? texture.GetInstanceID() : 0;

            if (_preset != null)
            {
                // 프리셋 사용: MaterialCache로 공유
                currentMaterial = ColorReplaceMaterialCache.Instance.GetOrCreate(shader, textureId, _preset);
                if (currentMaterial != null && texture != null)
                {
                    currentMaterial.SetTexture(PropMainTex, texture);
                }
                usingPresetCache = true;
            }
            else
            {
                // 프리셋 미사용: 텍스처 기반 Material 캐싱 + PropertyBlock
                if (!spriteMaterialCache.TryGetValue(textureId, out currentMaterial))
                {
                    currentMaterial = new Material(shader)
                    {
                        name = $"{SHADER_NAME} (Sprite Shared)",
                        hideFlags = HideFlags.DontSave
                    };
                    if (texture != null)
                    {
                        currentMaterial.SetTexture(PropMainTex, texture);
                    }
                    spriteMaterialCache[textureId] = currentMaterial;
                }
                usingPresetCache = false;
            }

            originalMaterial = spriteRenderer.sharedMaterial;
            spriteRenderer.sharedMaterial = currentMaterial;

            // PropertyBlock으로 개별 HSV 값 설정 (배칭 유지, 프리셋 미사용 시)
            if (!usingPresetCache)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        /// <summary>
        /// UI Graphic 초기화
        /// 프리셋 사용 시: MaterialCache로 공유
        /// 프리셋 미사용 시: 텍스처 + HSV 설정 기반 캐싱
        /// </summary>
        private void InitializeUIGraphic()
        {
            Shader shader = GetCachedShader();
            if (shader == null) return;

            Texture texture = GetUITexture();
            textureId = texture != null ? texture.GetInstanceID() : 0;

            if (_preset != null)
            {
                // 프리셋 사용: MaterialCache로 공유
                currentMaterial = ColorReplaceMaterialCache.Instance.GetOrCreate(shader, textureId, _preset);
                if (currentMaterial != null && texture != null)
                {
                    currentMaterial.SetTexture(PropMainTex, texture);
                }
                usingPresetCache = true;
            }
            else
            {
                // 프리셋 미사용: 기존 해시 기반 캐싱
                currentCacheKey = CalculateUICacheKey();

                if (!uiMaterialCache.TryGetValue(currentCacheKey, out currentMaterial))
                {
                    currentMaterial = new Material(shader)
                    {
                        name = $"{SHADER_NAME} (UI Shared)",
                        hideFlags = HideFlags.DontSave
                    };
                    if (texture != null)
                    {
                        currentMaterial.SetTexture(PropMainTex, texture);
                    }
                    currentMaterial.SetFloat(PropHSVRangeMin, _hsvRangeMin);
                    currentMaterial.SetFloat(PropHSVRangeMax, _hsvRangeMax);
                    currentMaterial.SetVector(PropHSVAdjust, _hsvAdjust);

                    uiMaterialCache[currentCacheKey] = currentMaterial;
                }
                usingPresetCache = false;
            }

            originalMaterial = uiGraphic.material;
            uiGraphic.material = currentMaterial;
        }

        private Texture GetUITexture()
        {
            if (uiGraphic is UnityEngine.UI.Image image && image.sprite != null)
            {
                return image.sprite.texture;
            }
            else if (uiGraphic is UnityEngine.UI.RawImage rawImage)
            {
                return rawImage.texture;
            }
            return null;
        }

        /// <summary>
        /// UI Material 캐시 키 계산 (텍스처 ID + HSV 설정값 해시)
        /// </summary>
        private int CalculateUICacheKey()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + textureId;
                hash = hash * 31 + _hsvRangeMin.GetHashCode();
                hash = hash * 31 + _hsvRangeMax.GetHashCode();
                hash = hash * 31 + _hsvAdjust.GetHashCode();
                return hash;
            }
        }

        private static Shader GetCachedShader()
        {
            if (cachedShader == null)
            {
                cachedShader = Shader.Find(SHADER_NAME);
                if (cachedShader == null)
                {
                    Debug.LogError($"[ColorReplace] 셰이더를 찾을 수 없습니다: {SHADER_NAME}");
                }
            }
            return cachedShader;
        }

        // ─────────────────────────────────────────────
        // 프리셋 API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 프리셋 적용
        /// </summary>
        public void ApplyPreset(ColorReplacePreset preset)
        {
            if (preset == null) return;

            _preset = preset;
            preset.ApplyTo(this);

            // 프리셋 사용 시 Material 재초기화
            if (initialized)
            {
                ReInitializeMaterial();
            }
        }

        /// <summary>
        /// Material 재초기화 (프리셋 변경 시)
        /// </summary>
        private void ReInitializeMaterial()
        {
            Shader shader = GetCachedShader();
            if (shader == null) return;

            if (_preset != null)
            {
                // 프리셋 캐시에서 Material 가져오기
                currentMaterial = ColorReplaceMaterialCache.Instance.GetOrCreate(shader, textureId, _preset);

                Texture texture = isUIComponent ? GetUITexture() : spriteRenderer?.sprite?.texture;
                if (currentMaterial != null && texture != null)
                {
                    currentMaterial.SetTexture(PropMainTex, texture);
                }

                usingPresetCache = true;
            }
            else
            {
                // 프리셋 해제 시 기존 로직으로 복귀
                if (isUIComponent)
                {
                    currentCacheKey = CalculateUICacheKey();
                    if (!uiMaterialCache.TryGetValue(currentCacheKey, out currentMaterial))
                    {
                        currentMaterial = new Material(shader)
                        {
                            name = $"{SHADER_NAME} (UI Shared)",
                            hideFlags = HideFlags.DontSave
                        };
                        Texture texture = GetUITexture();
                        if (texture != null)
                        {
                            currentMaterial.SetTexture(PropMainTex, texture);
                        }
                        currentMaterial.SetFloat(PropHSVRangeMin, _hsvRangeMin);
                        currentMaterial.SetFloat(PropHSVRangeMax, _hsvRangeMax);
                        currentMaterial.SetVector(PropHSVAdjust, _hsvAdjust);
                        uiMaterialCache[currentCacheKey] = currentMaterial;
                    }
                }
                else
                {
                    if (!spriteMaterialCache.TryGetValue(textureId, out currentMaterial))
                    {
                        currentMaterial = new Material(shader)
                        {
                            name = $"{SHADER_NAME} (Sprite Shared)",
                            hideFlags = HideFlags.DontSave
                        };
                        Texture texture = spriteRenderer?.sprite?.texture;
                        if (texture != null)
                        {
                            currentMaterial.SetTexture(PropMainTex, texture);
                        }
                        spriteMaterialCache[textureId] = currentMaterial;
                    }

                    if (propertyBlock == null)
                    {
                        propertyBlock = new MaterialPropertyBlock();
                    }
                }
                usingPresetCache = false;
            }

            // Material 적용
            if (isUIComponent && uiGraphic != null)
            {
                uiGraphic.material = currentMaterial;
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.sharedMaterial = currentMaterial;
            }

            ApplyProperties();
        }

        /// <summary>
        /// HSV 프로퍼티 적용
        /// </summary>
        private void ApplyProperties()
        {
            if (!initialized) return;

            // 프리셋 사용 중이면 Material 값은 프리셋에서 관리
            if (usingPresetCache) return;

            if (isUIComponent)
            {
                ApplyUIProperties();
            }
            else
            {
                ApplySpriteProperties();
            }
        }

        /// <summary>
        /// SpriteRenderer용 프로퍼티 적용 (PropertyBlock 사용)
        /// </summary>
        private void ApplySpriteProperties()
        {
            if (propertyBlock == null || spriteRenderer == null) return;

            propertyBlock.SetFloat(PropHSVRangeMin, _hsvRangeMin);
            propertyBlock.SetFloat(PropHSVRangeMax, _hsvRangeMax);
            propertyBlock.SetVector(PropHSVAdjust, _hsvAdjust);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// UI용 프로퍼티 적용 (설정이 변경되면 새 Material 또는 캐시된 Material 사용)
        /// </summary>
        private void ApplyUIProperties()
        {
            if (uiGraphic == null) return;

            int newCacheKey = CalculateUICacheKey();

            // 설정이 변경되었는지 확인
            if (newCacheKey != currentCacheKey)
            {
                currentCacheKey = newCacheKey;

                // 캐시에서 같은 설정의 Material 찾기
                if (!uiMaterialCache.TryGetValue(currentCacheKey, out currentMaterial))
                {
                    // 없으면 새로 생성
                    Shader shader = GetCachedShader();
                    if (shader == null) return;

                    currentMaterial = new Material(shader)
                    {
                        name = $"{SHADER_NAME} (UI Shared)",
                        hideFlags = HideFlags.DontSave
                    };
                    Texture texture = GetUITexture();
                    if (texture != null)
                    {
                        currentMaterial.SetTexture(PropMainTex, texture);
                    }
                    currentMaterial.SetFloat(PropHSVRangeMin, _hsvRangeMin);
                    currentMaterial.SetFloat(PropHSVRangeMax, _hsvRangeMax);
                    currentMaterial.SetVector(PropHSVAdjust, _hsvAdjust);

                    uiMaterialCache[currentCacheKey] = currentMaterial;
                }

                uiGraphic.material = currentMaterial;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!gameObject.scene.IsValid()) return;

            if (currentMaterial == null)
            {
                initialized = false;
            }

            if (!initialized)
            {
                Initialize();
            }

            ApplyProperties();
        }
#endif

        private void OnDisable()
        {
            // SpriteRenderer PropertyBlock 초기화
            if (!isUIComponent && spriteRenderer != null && propertyBlock != null)
            {
                propertyBlock.Clear();
                spriteRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void OnDestroy()
        {
            // 원본 Material 복원
            if (isUIComponent)
            {
                if (uiGraphic != null && originalMaterial != null)
                {
                    uiGraphic.material = originalMaterial;
                }
            }
            else
            {
                if (spriteRenderer != null && originalMaterial != null)
                {
                    spriteRenderer.sharedMaterial = originalMaterial;
                }
            }

            currentMaterial = null;
            propertyBlock = null;
        }

        /// <summary>
        /// Material 캐시 정리 (씬 전환 시 호출 권장)
        /// </summary>
        public static void ClearMaterialCache()
        {
            foreach (var mat in spriteMaterialCache.Values)
            {
                if (mat != null)
                {
                    if (Application.isPlaying)
                        Destroy(mat);
                    else
                        DestroyImmediate(mat);
                }
            }
            spriteMaterialCache.Clear();

            foreach (var mat in uiMaterialCache.Values)
            {
                if (mat != null)
                {
                    if (Application.isPlaying)
                        Destroy(mat);
                    else
                        DestroyImmediate(mat);
                }
            }
            uiMaterialCache.Clear();

            // 프리셋 캐시도 정리
            ColorReplaceMaterialCache.Instance.Clear();
        }

        /// <summary>
        /// HSV 범위를 색상 기반으로 자동 설정
        /// </summary>
        public void SetHSVRangeFromColor(Color color, float tolerance = 0.05f)
        {
            Color.RGBToHSV(color, out float hue, out _, out _);

            float min = hue - tolerance;
            float max = hue + tolerance;

            if (min < 0f) min += 1f;
            if (max > 1f) max -= 1f;

            _hsvRangeMin = min;
            _hsvRangeMax = max;
            ApplyProperties();
        }

        /// <summary>
        /// 캐시 통계 반환
        /// </summary>
        public static ColorReplaceMaterialCache.CacheStats GetCacheStats()
        {
            return ColorReplaceMaterialCache.Instance.GetStats();
        }
    }
}
