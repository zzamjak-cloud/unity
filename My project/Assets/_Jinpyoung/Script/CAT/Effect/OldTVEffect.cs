using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering; // GraphicsDeviceType 네임스페이스 추가

namespace CAT.Effects
{
    [AddComponentMenu("CAT/Effects/OldTVEffect")]
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class OldTVEffect : MonoBehaviour
    {
        private enum RendererType
        {
            Sprite,
            UI
        }

        [Header("References")]
        [Tooltip("Optional noise texture. If not set, a default noise will be used.")]
        public Texture2D noiseTexture;

        [Header("Effect Parameters")]
        [Range(0f, 1f)]
        public float noiseIntensity = 0.5f;

        [Range(0.1f, 10f)]
        public float noiseScale = 3f;

        [Range(0f, 1f)]
        public float scanLineIntensity = 0.5f;

        [Min(10f)]
        public float scanLineCount = 100f;

        [Range(0.1f, 5f)]
        public float scanLineThickness = 1f;

        [Range(0f, 0.1f)]
        public float verticalJitter = 0.01f;

        [Range(0f, 0.1f)]
        public float horizontalJitter = 0.01f;

        [Range(0f, 0.5f)]
        public float colorBleed = 0.1f;

        [Range(0f, 0.1f)]
        public float colorBleedOffset = 0.02f;

        [Range(0f, 5f)]
        public float rollSpeed = 1f;

        [Header("Performance Settings")]
        [Tooltip("Enable to reduce visual effects quality for better performance on lower-end devices")]
        public bool lowPerformanceMode = false;

        [Tooltip("Automatically detect low-end devices and enable low performance mode")]
        public bool autoDetectPerformance = true;

        // 프라이빗 변수
        private RendererType _rendererType;
        private SpriteRenderer _spriteRenderer;
        private RawImage _rawImage;
        private Material _material;
        private static Shader _spriteShader;
        private static Shader _uiShader;
        private static Texture2D _defaultNoiseTexture;

        // 프로퍼티 ID 캐싱
        private static readonly int NoiseTexProp = Shader.PropertyToID("_NoiseTex");
        private static readonly int NoiseIntensityProp = Shader.PropertyToID("_NoiseIntensity");
        private static readonly int NoiseScaleProp = Shader.PropertyToID("_NoiseScale");
        private static readonly int ScanLineIntensityProp = Shader.PropertyToID("_ScanLineIntensity");
        private static readonly int ScanLineCountProp = Shader.PropertyToID("_ScanLineCount");
        private static readonly int ScanLineThicknessProp = Shader.PropertyToID("_ScanLineThickness");
        private static readonly int VerticalJitterProp = Shader.PropertyToID("_VerticalJitter");
        private static readonly int HorizontalJitterProp = Shader.PropertyToID("_HorizontalJitter");
        private static readonly int ColorBleedProp = Shader.PropertyToID("_ColorBleed");
        private static readonly int ColorBleedOffsetProp = Shader.PropertyToID("_ColorBleedOffset");
        private static readonly int RollSpeedProp = Shader.PropertyToID("_RollSpeed");

        private void OnEnable()
        {
            // 시스템 메모리가 적거나 저사양 GPU 감지시 저사양 모드 활성화
            if (autoDetectPerformance)
            {
                bool isLowEndDevice = SystemInfo.graphicsMemorySize < 2048;
                bool isOldGLES = false;

                // OpenGLES2 체크를 다양한 유니티 버전에서 작동하도록 수정
#if UNITY_2017_1_OR_NEWER
                isOldGLES = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2;
#else
                isOldGLES = SystemInfo.graphicsDeviceVersion.StartsWith("OpenGL ES 2");
#endif

                if (isLowEndDevice || isOldGLES)
                {
                    lowPerformanceMode = true;
                }
            }

            InitializeShaders();
            InitializeRenderer();

            if (_material != null)
            {
                ApplyEffectSettings();
            }
        }

        private void InitializeShaders()
        {
            if (_spriteShader == null)
                _spriteShader = Shader.Find("CAT/Effects/OldTVEffect_Sprite");

            if (_uiShader == null)
                _uiShader = Shader.Find("CAT/Effects/OldTVEffect_UI");

            if (_defaultNoiseTexture == null)
                _defaultNoiseTexture = GenerateDefaultNoiseTexture();
        }

        private void InitializeRenderer()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _rawImage = GetComponent<RawImage>();

            if (_spriteRenderer != null && _spriteRenderer.enabled)
            {
                _rendererType = RendererType.Sprite;
                InitializeMaterial(_spriteShader);

                if (_material != null)
                    _spriteRenderer.material = _material;
            }
            else if (_rawImage != null && _rawImage.enabled)
            {
                _rendererType = RendererType.UI;
                InitializeMaterial(_uiShader);

                if (_material != null)
                    _rawImage.material = _material;
            }
        }

        private void InitializeMaterial(Shader shader)
        {
            if (shader == null)
            {
                Debug.LogError("OldTVEffect: Shader not found. Make sure the shader is in your project.");
                return;
            }

            _material = new Material(shader);
            _material.hideFlags = HideFlags.HideAndDontSave;

            // 기본 노이즈 텍스처 설정
            if (noiseTexture == null)
            {
                _material.SetTexture(NoiseTexProp, _defaultNoiseTexture);
            }
            else
            {
                _material.SetTexture(NoiseTexProp, noiseTexture);
            }
        }

        private void Update()
        {
            if (_material != null)
            {
                if (lowPerformanceMode)
                {
                    // 저사양 설정 적용
                    _material.SetTexture(NoiseTexProp, noiseTexture != null ? noiseTexture : _defaultNoiseTexture);
                    _material.SetFloat(NoiseIntensityProp, noiseIntensity * 0.7f);
                    _material.SetFloat(NoiseScaleProp, noiseScale);
                    _material.SetFloat(ScanLineIntensityProp, scanLineIntensity);
                    _material.SetFloat(ScanLineCountProp, Mathf.Min(scanLineCount, 60f));
                    _material.SetFloat(ScanLineThicknessProp, scanLineThickness);
                    _material.SetFloat(VerticalJitterProp, verticalJitter * 0.5f);
                    _material.SetFloat(HorizontalJitterProp, horizontalJitter * 0.5f);
                    _material.SetFloat(ColorBleedProp, Mathf.Min(colorBleed, 0.1f));
                    _material.SetFloat(ColorBleedOffsetProp, colorBleedOffset);
                    _material.SetFloat(RollSpeedProp, rollSpeed);
                }
                else
                {
                    // 일반 설정 적용
                    ApplyEffectSettings();
                }
            }
        }

        private void ApplyEffectSettings()
        {
            // 노이즈 텍스처 업데이트 (런타임에 변경될 수 있음)
            if (noiseTexture != null)
            {
                _material.SetTexture(NoiseTexProp, noiseTexture);
            }
            else if (_defaultNoiseTexture != null)
            {
                _material.SetTexture(NoiseTexProp, _defaultNoiseTexture);
            }

            // 모든 속성 업데이트
            _material.SetFloat(NoiseIntensityProp, noiseIntensity);
            _material.SetFloat(NoiseScaleProp, noiseScale);
            _material.SetFloat(ScanLineIntensityProp, scanLineIntensity);
            _material.SetFloat(ScanLineCountProp, scanLineCount);
            _material.SetFloat(ScanLineThicknessProp, scanLineThickness);
            _material.SetFloat(VerticalJitterProp, verticalJitter);
            _material.SetFloat(HorizontalJitterProp, horizontalJitter);
            _material.SetFloat(ColorBleedProp, colorBleed);
            _material.SetFloat(ColorBleedOffsetProp, colorBleedOffset);
            _material.SetFloat(RollSpeedProp, rollSpeed);
        }

        private void OnDisable()
        {
            // 원래 머티리얼로 복원
            if (_spriteRenderer != null && _rendererType == RendererType.Sprite)
            {
                _spriteRenderer.material = null;
            }
            else if (_rawImage != null && _rendererType == RendererType.UI)
            {
                _rawImage.material = null;
            }

            // 머티리얼 정리
            if (_material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_material);
                }
                else
                {
                    DestroyImmediate(_material);
                }
                _material = null;
            }
        }

        private void OnDestroy()
        {
            // 머티리얼 정리
            if (_material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_material);
                }
                else
                {
                    DestroyImmediate(_material);
                }
                _material = null;
            }
        }

        private Texture2D GenerateDefaultNoiseTexture()
        {
            // 기본 노이즈 텍스처 생성 (256x256 RGBA 노이즈)
            int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;

            Color[] pixels = new Color[size * size];
            System.Random random = new System.Random();

            for (int i = 0; i < pixels.Length; i++)
            {
                // 각 채널에 대해 다른 노이즈 생성
                float r = (float)random.NextDouble();
                float g = (float)random.NextDouble();
                float b = (float)random.NextDouble();
                float a = (float)random.NextDouble();

                pixels[i] = new Color(r, g, b, a);
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }

        // 에디터에서 컴포넌트가 추가될 때 호출
        private void Reset()
        {
            InitializeShaders();
            InitializeRenderer();

            if (_material != null)
            {
                ApplyEffectSettings();
            }
        }
    }
}