using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    [System.Serializable]
    public enum WindableType
    {
        Sprite,
        UI
    }

    [DisallowMultipleComponent]
    public class Windable : MonoBehaviour
    {
        public static readonly string SHADER_NAME = "CAT/Effects/Windable";

        [Header("컴포넌트 타입")]
        [SerializeField] private WindableType _windableType = WindableType.Sprite;

        [Header("바람 효과 설정")]
        [SerializeField, HideInInspector] private Texture _MainTex;
        [SerializeField, Range(0, 360)] private float _RotateUV;
        [SerializeField] private Texture _NoiseTex;
        [SerializeField] private float _WindSpeed = 0.2f;
        [SerializeField] private float _WindStrength = 0.5f;
        [SerializeField] private float _WindFrequency = 0.2f;
        [SerializeField] private Vector4 _WindDirection = new Vector4(1, 1, 0, 0);
        [SerializeField] private float _WindScale = 1.0f;
        [SerializeField, HideInInspector] private Vector4 _ClipRect = new Vector4(-2147.0f, -2147.0f, 2147.0f, 2147.0f);
        [SerializeField] private float _ImageOffsetX = 0.3f;
        [SerializeField] private float _ImageOffsetY = 0.3f;
        [SerializeField] private float _ImageScale = 1.1f;

        // 컴포넌트 레퍼런스
        private Material _material;
        private SpriteRenderer _spriteRenderer;
        private Graphic _graphic;

        // 프로퍼티
        public WindableType WindableTypeValue => _windableType;

        private void Awake()
        {
            ValidateComponents();
        }

        private void OnEnable()
        {
            SetupMaterial();
        }

        private void OnDisable()
        {
            CleanupMaterial();
        }

        private void Update()
        {
            if (_material != null)
            {
                _material.SetFloat("_CustomTime", Time.time);
            }
        }

        /// <summary>
        /// 컴포넌트 유효성 검사 및 타입 자동 설정
        /// </summary>
        private void ValidateComponents()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _graphic = GetComponent<Graphic>();

            // 자동으로 타입 결정
            if (_spriteRenderer != null)
            {
                _windableType = WindableType.Sprite;
                // UI 컴포넌트가 함께 있다면 경고
                if (_graphic != null)
                {
                    Debug.LogWarning($"[Windable] {gameObject.name}: SpriteRenderer와 UI Graphic 컴포넌트가 모두 발견되었습니다. SpriteRenderer를 사용합니다.");
                }
            }
            else if (_graphic != null)
            {
                _windableType = WindableType.UI;
            }
            else
            {
                Debug.LogError($"[Windable] {gameObject.name}: SpriteRenderer 또는 UI Graphic 컴포넌트가 필요합니다.");
            }
        }

        /// <summary>
        /// 머티리얼 설정
        /// </summary>
        private void SetupMaterial()
        {
            if (_windableType == WindableType.Sprite && _spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            else if (_windableType == WindableType.UI && _graphic == null)
            {
                _graphic = GetComponent<Graphic>();
            }

            if (_material == null)
            {
                Shader shader = Shader.Find(SHADER_NAME);
                if (shader == null)
                {
                    Debug.LogError($"[Windable] 쉐이더를 찾을 수 없습니다: {SHADER_NAME}");
                    return;
                }

                _material = new Material(shader);
                
                // 타입에 따라 머티리얼 할당
                if (_windableType == WindableType.Sprite && _spriteRenderer != null)
                {
                    _spriteRenderer.material = _material;
                }
                else if (_windableType == WindableType.UI && _graphic != null)
                {
                    _graphic.material = _material;
                }
            }

            UpdateMaterialProperties();
        }

        /// <summary>
        /// 머티리얼 정리
        /// </summary>
        private void CleanupMaterial()
        {
            if (_windableType == WindableType.Sprite && _spriteRenderer != null)
            {
                _spriteRenderer.material = null;
            }
            else if (_windableType == WindableType.UI && _graphic != null)
            {
                _graphic.material = null;
            }

            if (_material != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(_material);
#else
                Destroy(_material);
#endif
                _material = null;
            }
        }

        /// <summary>
        /// 머티리얼 프로퍼티 업데이트 (에디터에서 호출됨)
        /// </summary>
        public void UpdateMaterialProperties(float customTime = 0)
        {
            if (_material == null)
            {
                SetupMaterial();
                if (_material == null) return;
            }

            // 타입에 따른 텍스처 설정
            if (_windableType == WindableType.Sprite)
            {
                UpdateSpriteProperties(customTime);
            }
            else if (_windableType == WindableType.UI)
            {
                UpdateUIProperties(customTime);
            }

            // 공통 프로퍼티 설정
            SetCommonMaterialProperties(customTime);
        }

        /// <summary>
        /// Sprite용 프로퍼티 업데이트
        /// </summary>
        private void UpdateSpriteProperties(float customTime)
        {
            if (_spriteRenderer?.sprite == null) return;

            _MainTex = _spriteRenderer.sprite.texture;
            if (_MainTex == null) return;

            _material.SetTexture("_MainTex", _MainTex);

            Sprite sprite = _spriteRenderer.sprite;
            Rect r = sprite.textureRect;
            Texture t = sprite.texture;

            // 아틀라스에 포함된 스프라이트의 UV 좌표 계산
            Vector4 uvRect = new Vector4(
                r.x / t.width, 
                r.y / t.height, 
                (r.x + r.width) / t.width, 
                (r.y + r.height) / t.height
            );
            _material.SetVector("_SpriteUVRect", uvRect);

            // 스프라이트의 피벗을 UV 공간 기준으로 계산
            float pivotX = (r.x + sprite.pivot.x) / t.width;
            float pivotY = (r.y + sprite.pivot.y) / t.height;
            Vector2 spritePivot = new Vector2(pivotX, pivotY);
            _material.SetVector("_SpritePivot", spritePivot);
        }

        /// <summary>
        /// UI용 프로퍼티 업데이트
        /// </summary>
        private void UpdateUIProperties(float customTime)
        {
            if (_graphic == null) return;

            _MainTex = _graphic.mainTexture;
            if (_MainTex == null) return;

            _material.SetTexture("_MainTex", _MainTex);

            Vector2 spritePivot = new Vector2(0.5f, 0.5f);

            if (_graphic is Image image && image.sprite != null)
            {
                Sprite sprite = image.sprite;
                Rect r = sprite.textureRect;
                Texture t = sprite.texture;

                Vector4 uvRect = new Vector4(
                    r.x / t.width, 
                    r.y / t.height, 
                    (r.x + r.width) / t.width, 
                    (r.y + r.height) / t.height
                );
                _material.SetVector("_SpriteUVRect", uvRect);

                float pivotX = (r.x + sprite.pivot.x) / t.width;
                float pivotY = (r.y + sprite.pivot.y) / t.height;
                spritePivot = new Vector2(pivotX, pivotY);
            }
            else
            {
                _material.SetVector("_SpriteUVRect", new Vector4(0, 0, 1, 1));
            }

            _material.SetVector("_SpritePivot", spritePivot);
            _graphic.SetMaterialDirty();
        }

        /// <summary>
        /// 공통 머티리얼 프로퍼티 설정
        /// </summary>
        private void SetCommonMaterialProperties(float customTime)
        {
            _material.SetFloat("_CustomTime", customTime);
            _material.SetFloat("_RotateUV", _RotateUV);
            _material.SetTexture("_NoiseTex", _NoiseTex);
            _material.SetFloat("_WindSpeed", _WindSpeed);
            _material.SetFloat("_WindStrength", _WindStrength);
            _material.SetFloat("_WindFrequency", _WindFrequency);
            _material.SetVector("_WindDirection", _WindDirection);
            _material.SetVector("_ClipRect", _ClipRect);
            _material.SetFloat("_WindScale", _WindScale);
            _material.SetFloat("_ImageOffsetX", _ImageOffsetX);
            _material.SetFloat("_ImageOffsetY", _ImageOffsetY);
            _material.SetFloat("_ImageScale", _ImageScale);
        }

        /// <summary>
        /// 타입을 수동으로 변경 (에디터용)
        /// </summary>
        public void ChangeWindableType(WindableType newType)
        {
            if (_windableType != newType)
            {
                CleanupMaterial();
                _windableType = newType;
                ValidateComponents();
                SetupMaterial();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 값 변경 시 호출되는 메서드
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            
            ValidateComponents();
            if (_material != null)
            {
                UpdateMaterialProperties();
            }
        }
#endif
    }
}