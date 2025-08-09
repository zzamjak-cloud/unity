using UnityEngine;

namespace CAT.Utility
{
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("CAT/2D/SpriteWindable")]
    [DisallowMultipleComponent]
    public class SpriteWindable : MonoBehaviour
    {
        public static readonly string SHADER_NAME = "CAT/Effects/Windable";

        [SerializeField, HideInInspector] private Texture _MainTex;
        [SerializeField, Range(0, 360)] private float _RotateUV;
        [SerializeField] private Texture _NoiseTex;
        [SerializeField] private float _WindSpeed = 0.2f;
        [SerializeField] private float _WindStrength = 0.50f;
        [SerializeField] private float _WindFrequency = 0.2f;
        [SerializeField] private Vector4 _WindDirection = new Vector4(1, 1, 0, 0);
        [SerializeField] private float _WindScale = 1.0f;
        [SerializeField, HideInInspector] private Vector4 _ClipRect = new Vector4(-2147.0f, -2147.0f, 2147.0f, 2147.0f);
        [SerializeField] private float _ImageOffsetX = 0.3f;
        [SerializeField] private float _ImageOffsetY = 0.3f;
        [SerializeField] private float _ImageScale = 1.1f;

        private Material _material;
        private SpriteRenderer _spriteRenderer;

        private void OnEnable()
        {
            SetupMaterial();
        }

        private void OnDisable()
        {
            if (_spriteRenderer != null)
            {
                // 공유 머티리얼을 사용하지 않으므로 인스턴스만 정리합니다.
                _spriteRenderer.material = null; 
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
        
        private void Update()
        {
            if (_material != null)
            {
                // 런타임(플레이 모드)일 때 매 프레임 시간을 쉐이더로 전달합니다.
                _material.SetFloat("_CustomTime", Time.time);
            }
        }

        private void SetupMaterial()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (_material == null)
            {
                Shader shader = Shader.Find(SHADER_NAME);
                if (shader == null)
                {
                    Debug.LogError($"Shader not found: {SHADER_NAME}");
                    return;
                }
                // SpriteRenderer를 위해 새로운 머티리얼 인스턴스를 생성합니다.
                _material = new Material(shader);
                _spriteRenderer.material = _material;
            }
            
            UpdateMaterialProperties();
        }

        public void UpdateMaterialProperties(float customTime = 0)
        {
            if (_material == null || _spriteRenderer == null)
            {
                SetupMaterial();
            }
            
            // 머티리얼이 비정상적으로 생성되었을 경우를 대비합니다.
            if (_material == null) return;

            // SpriteRenderer에서 메인 텍스처를 가져옵니다.
            if (_spriteRenderer.sprite == null) return;
            _MainTex = _spriteRenderer.sprite.texture;
            if (_MainTex == null) return;
            
            _material.SetTexture("_MainTex", _MainTex);
            _material.SetFloat("_CustomTime", customTime);

            Vector2 spritePivot = new Vector2(0.5f, 0.5f);
            
            // SpriteRenderer의 스프라이트 정보를 사용합니다.
            Sprite sprite = _spriteRenderer.sprite;
            Rect r = sprite.textureRect;
            Texture t = sprite.texture;

            // 아틀라스에 포함된 스프라이트의 UV 좌표를 계산합니다.
            Vector4 uvRect = new Vector4(r.x / t.width, r.y / t.height, (r.x + r.width) / t.width, (r.y + r.height) / t.height);
            _material.SetVector("_SpriteUVRect", uvRect);
            
            // 스프라이트의 피벗을 UV 공간 기준으로 계산합니다.
            float pivotX = (r.x + sprite.pivot.x) / t.width;
            float pivotY = (r.y + sprite.pivot.y) / t.height;
            spritePivot = new Vector2(pivotX, pivotY);
            
            _material.SetVector("_SpritePivot", spritePivot);
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
    }
}