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
        public static readonly string SHADER_NAME = "CAT/Effects/ColorReplace";

        // 정적인 머티리얼을 캐싱해서 드로우콜 낮추기
        private static Dictionary<int, Material> materialCache = new Dictionary<int, Material>();
        // PropertyBlock도 캐싱하여 같은 설정의 오브젝트들이 공유 (배치 렌더링 최적화)
        private static Dictionary<int, MaterialPropertyBlock> propertyBlockCache = new Dictionary<int, MaterialPropertyBlock>();

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
        private Texture cachedTexture = null; // 텍스처도 해시에 포함하기 위해 캐싱
        private MaterialPropertyBlock propertyBlock = null; // 배치 렌더링을 위한 PropertyBlock

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

            Shader shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"Cannot find shader: {SHADER_NAME} on {gameObject.name}");
                return;
            }

            SetupMaterial(spriteRenderer.sharedMaterial, shader, spriteRenderer.sprite.texture);
            spriteRenderer.sharedMaterial = colorReplaceMaterial;
            
            // PropertyBlock 캐싱 및 적용 (배치 렌더링 최적화)
            SetupPropertyBlock();
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        // UI용 셰이더 초기화
        private void InitializeUIComponent()
        {
            if (uiGraphic == null) return;

            Shader shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"Cannot find shader: {SHADER_NAME} on {gameObject.name}");
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
            // 텍스처 캐싱
            cachedTexture = texture;
            
            // 머티리얼 캐싱에 대한 해쉬 계산하기 (설정값과 텍스처 기반)
            CalculateMaterialHash();

            // 캐쉬에 머티리얼이 있는지 체크후 업데이트
            if (materialCache.TryGetValue(materialCacheHash, out Material cachedMaterial))
            {
                colorReplaceMaterial = cachedMaterial;
                UpdateMaterial();
                return;
            }

            // 신규 머티리얼 생성하기
            if (currentMaterial != null && currentMaterial.shader.name != SHADER_NAME)
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
            // 같은 설정값과 텍스처를 가진 오브젝트들은 같은 머티리얼을 공유
            // 이를 통해 배치 렌더링이 가능해져 드로우콜을 줄일 수 있음
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + _color.GetHashCode();
                hash = hash * 23 + _hsvRangeMin.GetHashCode();
                hash = hash * 23 + _hsvRangeMax.GetHashCode();
                hash = hash * 23 + _hsvAdjust.GetHashCode();
                hash = hash * 23 + (isUIComponent ? 1 : 0);
                // 텍스처도 해시에 포함 (같은 설정이지만 다른 텍스처면 다른 머티리얼 필요)
                hash = hash * 23 + (cachedTexture != null ? cachedTexture.GetInstanceID() : 0);
                materialCacheHash = hash;
            }
        }

        private void UpdateMaterialProperty<T>(string propertyName, T value)
        {
            if (colorReplaceMaterial == null) return;

            if (value is Color colorValue)
            {
                colorReplaceMaterial.SetColor(propertyName, colorValue);
                if (propertyBlock != null && !isUIComponent)
                    propertyBlock.SetColor(propertyName, colorValue);
            }
            else if (value is float floatValue)
            {
                colorReplaceMaterial.SetFloat(propertyName, floatValue);
                if (propertyBlock != null && !isUIComponent)
                    propertyBlock.SetFloat(propertyName, floatValue);
            }
            else if (value is Vector4 vector4Value)
            {
                colorReplaceMaterial.SetVector(propertyName, vector4Value);
                if (propertyBlock != null && !isUIComponent)
                    propertyBlock.SetVector(propertyName, vector4Value);
            }
            
            // PropertyBlock 업데이트 후 렌더러에 재적용
            if (propertyBlock != null && !isUIComponent && targetRenderer != null)
            {
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void UpdateMaterial()
        {
            if (colorReplaceMaterial == null) return;

            colorReplaceMaterial.SetColor("_Color", _color);
            colorReplaceMaterial.SetFloat("_HSVRangeMin", _hsvRangeMin);
            colorReplaceMaterial.SetFloat("_HSVRangeMax", _hsvRangeMax);
            colorReplaceMaterial.SetVector("_HSVAAdjust", _hsvAdjust);
            
            UpdatePropertyBlock();
        }
        
        private void SetupPropertyBlock()
        {
            if (isUIComponent) return;
            
            // 같은 설정의 오브젝트들이 같은 PropertyBlock을 공유하도록 캐싱
            if (propertyBlockCache.TryGetValue(materialCacheHash, out MaterialPropertyBlock cachedBlock))
            {
                propertyBlock = cachedBlock;
            }
            else
            {
                propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetColor("_Color", _color);
                propertyBlock.SetFloat("_HSVRangeMin", _hsvRangeMin);
                propertyBlock.SetFloat("_HSVRangeMax", _hsvRangeMax);
                propertyBlock.SetVector("_HSVAAdjust", _hsvAdjust);
                propertyBlockCache[materialCacheHash] = propertyBlock;
            }
        }
        
        private void UpdatePropertyBlock()
        {
            if (propertyBlock == null || isUIComponent) return;
            
            // PropertyBlock에 현재 설정값 적용 (배치 렌더링 최적화)
            propertyBlock.SetColor("_Color", _color);
            propertyBlock.SetFloat("_HSVRangeMin", _hsvRangeMin);
            propertyBlock.SetFloat("_HSVRangeMax", _hsvRangeMax);
            propertyBlock.SetVector("_HSVAAdjust", _hsvAdjust);
            
            if (targetRenderer != null)
            {
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
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
                
                // If hash changed, we need to use a different material and property block
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
                            // PropertyBlock도 재설정
                            SetupPropertyBlock();
                            targetRenderer.SetPropertyBlock(propertyBlock);
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

            // 공유 머티리얼이므로 개별 오브젝트가 파괴될 때 머티리얼을 제거하지 않음
            // ClearMaterialCache()를 통해 일괄 정리하거나 씬 전환 시 자동 정리됨
            // colorReplaceMaterial은 다른 오브젝트에서도 사용 중일 수 있으므로 여기서는 정리하지 않음
        }

        // 머티리얼 캐시 정리 - 씬 변경시 호출
        public static void ClearMaterialCache()
        {
            materialCache.Clear();
            propertyBlockCache.Clear();
        }
    }
}