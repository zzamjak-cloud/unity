using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.Effects
{
    [AddComponentMenu("CAT/Effects/ColorReplace")]
    public class ColorReplace : MonoBehaviour
    {
        public static readonly string SPRITE_SHADER_NAME = "CAT/Effects/ColorReplaceSprite";
        public static readonly string UI_SHADER_NAME = "CAT/Effects/ColorReplaceUI";

        // 정적인 머티리얼을 캐싱해서 드로우콜 낮추기
        private static Dictionary<int, Material> materialCache = new Dictionary<int, Material>();

        [SerializeField] private Color _color = Color.black;
        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                UpdateMaterialProperty("_Color", value);
            }
        }

        [SerializeField, Range(0f, 1f)] private float _hsvRangeMin = 0f;
        public float HSVRangeMin
        {
            get => _hsvRangeMin;
            set
            {
                _hsvRangeMin = Mathf.Clamp01(value);
                UpdateMaterialProperty("_HSVRangeMin", _hsvRangeMin);
            }
        }

        [SerializeField, Range(0f, 1f)] private float _hsvRangeMax = 1f;
        public float HSVRangeMax
        {
            get => _hsvRangeMax;
            set
            {
                _hsvRangeMax = Mathf.Clamp01(value);
                UpdateMaterialProperty("_HSVRangeMax", _hsvRangeMax);
            }
        }

        [SerializeField] private Vector4 _hsvAdjust = Vector4.zero;
        public Vector4 HSVAdjust
        {
            get => _hsvAdjust;
            set
            {
                _hsvAdjust = value;
                UpdateMaterialProperty("_HSVAAdjust", _hsvAdjust);
            }
        }

        private Material colorReplaceMaterial;
        private Material originalMaterial;
        private Renderer targetRenderer;
        private UnityEngine.UI.Graphic uiGraphic;
        private bool isUIComponent = false;
        private bool initialized = false;
        private int materialCacheHash = 0;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;

            // 런타임 상황인지 체크
            if (!Application.isPlaying && !gameObject.scene.IsValid())
            {
                return;
            }

            // 렌더러 타입을 결정
            targetRenderer = GetComponent<SpriteRenderer>();
            if (targetRenderer == null)
            {
                uiGraphic = GetComponent<UnityEngine.UI.Graphic>();
                if (uiGraphic != null)
                {
                    isUIComponent = true;
                }
                else
                {
                    // 렌더러를 찾지 못했을 경우
                    Debug.LogWarning($"ColorReplace component on {gameObject.name} requires either SpriteRenderer or UI.Graphic component.");
                    return;
                }
            }

            // 적합한 셰이더 적용
            if (isUIComponent)
            {
                InitializeUIComponent();
            }
            else
            {
                InitializeSpriteComponent();
            }

            initialized = true;
        }

        // 스프라이트용 셰이더 초기화
        private void InitializeSpriteComponent()
        {
            SpriteRenderer spriteRenderer = targetRenderer as SpriteRenderer;
            if (spriteRenderer == null || spriteRenderer.sprite == null) return;

            Shader shader = Shader.Find(SPRITE_SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"Cannot find shader: {SPRITE_SHADER_NAME} on {gameObject.name}");
                return;
            }

            SetupMaterial(spriteRenderer.sharedMaterial, shader, spriteRenderer.sprite.texture);
            spriteRenderer.sharedMaterial = colorReplaceMaterial;
        }

        // UI용 셰이더 초기화
        private void InitializeUIComponent()
        {
            if (uiGraphic == null) return;

            Shader shader = Shader.Find(UI_SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"Cannot find shader: {UI_SHADER_NAME} on {gameObject.name}");
                return;
            }

            // UI 컴포넌트에서 메인텍스쳐 가져오기
            Texture mainTexture = null;
            if (uiGraphic is UnityEngine.UI.Image image && image.sprite != null)
            {
                mainTexture = image.sprite.texture;
            }
            else if (uiGraphic is UnityEngine.UI.RawImage rawImage)
            {
                mainTexture = rawImage.texture;
            }

            SetupMaterial(uiGraphic.material, shader, mainTexture);
            uiGraphic.material = colorReplaceMaterial;
        }

        private void SetupMaterial(Material currentMaterial, Shader shader, Texture texture)
        {
            // 머티리얼 캐싱에 대한 해쉬 계산하기
            CalculateMaterialHash();

            // 캐쉬에 머티리얼이 있는지 체크후 업데이트
            if (materialCache.TryGetValue(materialCacheHash, out Material cachedMaterial))
            {
                colorReplaceMaterial = cachedMaterial;
                UpdateMaterial();
                return;
            }

            // 신규 머티리얼 생성하기
            if (currentMaterial != null && currentMaterial.shader.name != SPRITE_SHADER_NAME && currentMaterial.shader.name != UI_SHADER_NAME)
            {
                originalMaterial = currentMaterial;
                colorReplaceMaterial = new Material(shader);
                if (texture != null)
                {
                    colorReplaceMaterial.SetTexture("_MainTex", texture);
                }
            }
            else if (currentMaterial != null)
            {
                colorReplaceMaterial = currentMaterial;
            }
            else
            {
                colorReplaceMaterial = new Material(shader);
                if (texture != null)
                {
                    colorReplaceMaterial.SetTexture("_MainTex", texture);
                }
            }

            // 현재 값으로 머티리얼 업데이트
            UpdateMaterial();

            // 캐시에 추가하기
            materialCache[materialCacheHash] = colorReplaceMaterial;
        }

        private void CalculateMaterialHash()
        {
            // 현재 세팅을 기준으로 해쉬를 생성, 비슷한 머티리얼을 캐싱하기 위함
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + _color.GetHashCode();
                hash = hash * 23 + _hsvRangeMin.GetHashCode();
                hash = hash * 23 + _hsvRangeMax.GetHashCode();
                hash = hash * 23 + _hsvAdjust.GetHashCode();
                hash = hash * 23 + (isUIComponent ? 1 : 0);
                materialCacheHash = hash;
            }
        }

        private void UpdateMaterialProperty<T>(string propertyName, T value)
        {
            if (colorReplaceMaterial == null) return;

            if (value is Color colorValue)
                colorReplaceMaterial.SetColor(propertyName, colorValue);
            else if (value is float floatValue)
                colorReplaceMaterial.SetFloat(propertyName, floatValue);
            else if (value is Vector4 vector4Value)
                colorReplaceMaterial.SetVector(propertyName, vector4Value);
        }

        private void UpdateMaterial()
        {
            if (colorReplaceMaterial == null) return;

            colorReplaceMaterial.SetColor("_Color", _color);
            colorReplaceMaterial.SetFloat("_HSVRangeMin", _hsvRangeMin);
            colorReplaceMaterial.SetFloat("_HSVRangeMax", _hsvRangeMax);
            colorReplaceMaterial.SetVector("_HSVAAdjust", _hsvAdjust);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Don't initialize in prefab mode
            if (!gameObject.scene.IsValid()) return;
            
            if (!initialized)
                Initialize();
            
            // If we're in play mode, calculate new hash and check cache
            if (Application.isPlaying)
            {
                int oldHash = materialCacheHash;
                CalculateMaterialHash();
                
                // If hash changed, we need to use a different material
                if (oldHash != materialCacheHash)
                {
                    if (materialCache.TryGetValue(materialCacheHash, out Material cachedMaterial))
                    {
                        colorReplaceMaterial = cachedMaterial;
                        if (isUIComponent && uiGraphic != null)
                        {
                            uiGraphic.material = colorReplaceMaterial;
                        }
                        else if (targetRenderer != null)
                        {
                            targetRenderer.sharedMaterial = colorReplaceMaterial;
                        }
                    }
                    else
                    {
                        // Reinitialize with new values
                        if (isUIComponent)
                        {
                            InitializeUIComponent();
                        }
                        else
                        {
                            InitializeSpriteComponent();
                        }
                    }
                }
            }
            
            UpdateMaterial();
        }
#endif

        private void OnDestroy()
        {
            if (isUIComponent)
            {
                if (uiGraphic != null && originalMaterial != null)
                {
                    uiGraphic.material = originalMaterial;
                }
            }
            else
            {
                if (targetRenderer != null && originalMaterial != null)
                {
                    targetRenderer.sharedMaterial = originalMaterial;
                }
            }

            // 다른 오브젝트에서 사용되는한 캐싱된 머티리얼은 제거되지 않음
            if (colorReplaceMaterial != null && colorReplaceMaterial != originalMaterial
                && !materialCache.ContainsValue(colorReplaceMaterial))
            {
                if (Application.isPlaying)
                    Destroy(colorReplaceMaterial);
                else
                    DestroyImmediate(colorReplaceMaterial);
            }
        }

        // 머티리얼 캐시 정리 - 씬 변경시 호출
        public static void ClearMaterialCache()
        {
            materialCache.Clear();
        }
    }
}