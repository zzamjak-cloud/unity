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
    [ExecuteAlways]
    public class Windable : MonoBehaviour
    {
        public static readonly string SHADER_NAME = "CAT/Effects/Windable";

        // 셰이더 프로퍼티 ID 캐싱 (문자열 기반 호출 방지)
        private static readonly int PropCustomTime = Shader.PropertyToID("_CustomTime");
        private static readonly int PropRotateUV = Shader.PropertyToID("_RotateUV");
        private static readonly int PropNoiseTex = Shader.PropertyToID("_NoiseTex");
        private static readonly int PropWindSpeed = Shader.PropertyToID("_WindSpeed");
        private static readonly int PropWindStrength = Shader.PropertyToID("_WindStrength");
        private static readonly int PropWindFrequency = Shader.PropertyToID("_WindFrequency");
        private static readonly int PropWindDirection = Shader.PropertyToID("_WindDirection");
        private static readonly int PropClipRect = Shader.PropertyToID("_ClipRect");
        private static readonly int PropWindScale = Shader.PropertyToID("_WindScale");
        private static readonly int PropImageOffsetX = Shader.PropertyToID("_ImageOffsetX");
        private static readonly int PropImageOffsetY = Shader.PropertyToID("_ImageOffsetY");
        private static readonly int PropImageScale = Shader.PropertyToID("_ImageScale");
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");
        private static readonly int PropSpriteUVRect = Shader.PropertyToID("_SpriteUVRect");
        private static readonly int PropSpritePivot = Shader.PropertyToID("_SpritePivot");
        private static readonly int PropNormalizedWindDir = Shader.PropertyToID("_NormalizedWindDir");

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
        private Material _lastActiveMaterial;

        /// <summary>
        /// 실제 렌더링에 사용 중인 Material 반환.
        /// CanvasRenderer에 설정된 최종 머티리얼을 직접 참조하여,
        /// materialForRendering 접근 시 발생하는 체인 재평가(StencilMaterial 캐시 문제)를 회피.
        /// </summary>
        private Material ActiveMaterial
        {
            get
            {
                if (_windableType == WindableType.UI && _graphic != null)
                {
                    // 1) SoftMaskLight: _graphic.material을 직접 교체
                    Material graphicMat = _graphic.material;
                    if (graphicMat != null && graphicMat != _material)
                        return graphicMat;
                    // 2) Unity Mask / SoftMaskable: CanvasRenderer에 이미 설정된 최종 머티리얼 참조
                    var cr = _graphic.canvasRenderer;
                    if (cr != null)
                    {
                        Material canvasMat = cr.GetMaterial(0);
                        if (canvasMat != null && canvasMat != _material)
                            return canvasMat;
                    }
                }
                return _material;
            }
        }

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

        private void OnDestroy()
        {
            CleanupMaterial();
        }

        private void Update()
        {
            // 에디터 비플레이 모드: 애니메이션 없이 정적 프로퍼티만 동기화
            // (애니메이션은 EditorAdvance()에서 처리)
            if (!Application.isPlaying)
            {
                SyncStaticProperties();
                return;
            }

            float time = Time.time;
            // 기본 머티리얼에 항상 설정 (IMaterialModifier 체인 재구축 시 소스)
            if (_material != null)
                _material.SetFloat(PropCustomTime, time);
            // 렌더링 머티리얼이 다르면 (Mask/SoftMaskable/SoftMaskLight) 거기에도 설정
            Material active = ActiveMaterial;
            if (active != null && active != _material)
            {
                // CanvasRenderer 머티리얼 인스턴스가 변경된 경우 전체 프로퍼티 동기화
                // (SoftMaskable/StencilMaterial 체인 재구축 시 새 머티리얼이 생성되므로)
                if (active != _lastActiveMaterial)
                {
                    _lastActiveMaterial = active;
                    ApplyPropertiesToTarget(active, time);
                }
                else
                {
                    active.SetFloat(PropCustomTime, time);
                }
            }
        }

        /// <summary>
        /// 에디터 비플레이 모드에서 정적 프로퍼티만 동기화 (애니메이션 없음)
        /// </summary>
        private void SyncStaticProperties()
        {
            if (_material == null) return;
            Material active = ActiveMaterial;
            if (active != null && active != _material && active != _lastActiveMaterial)
            {
                _lastActiveMaterial = active;
                ApplyPropertiesToTarget(active, 0);
            }
        }

        /// <summary>
        /// 에디터 전용: 외부에서 deltaTime 기반 경과 시간을 전달하여 바람 애니메이션을 진행시킨다.
        /// EditorApplication.update 콜백에서 호출되며, [ExecuteAlways] Update()에 의존하지 않는다.
        /// </summary>
        public void EditorAdvance(float elapsedTime)
        {
            if (_material == null || _graphic == null) return;

            // 기본 머티리얼에 프로퍼티 설정
            ApplyPropertiesToTarget(_material, elapsedTime);

            // 렌더링 머티리얼이 다르면 거기에도 설정
            Material active = ActiveMaterial;
            if (active != null && active != _material)
            {
                if (active != _lastActiveMaterial)
                {
                    _lastActiveMaterial = active;
                }
                ApplyPropertiesToTarget(active, elapsedTime);
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

                _material = new Material(shader)
                {
                    hideFlags = HideFlags.DontSave
                };

                // 타입에 따라 머티리얼 할당
                if (_windableType == WindableType.Sprite && _spriteRenderer != null)
                {
                    _spriteRenderer.material = _material;
                }
                else if (_windableType == WindableType.UI && _graphic != null)
                {
                    _graphic.material = _material;

                    // 에디터 비재생 모드에서 SoftMaskLight가 이미 자식을 처리한 경우,
                    // _graphic.material 교체로 마스킹이 해제되므로 재처리 요청
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        var sml = GetComponentInParent<SoftMaskLight.SoftMaskLight>();
                        if (sml != null) sml.InvalidateChild(_graphic);
                    }
#endif
                }
            }

            // 머티리얼이 새로 생성되었으므로 _lastActiveMaterial 초기화
            _lastActiveMaterial = null;

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

            // 기본 머티리얼에 프로퍼티 설정 (IMaterialModifier 체인 재구축 시 소스)
            ApplyPropertiesToTarget(_material, customTime);

            // 렌더링 머티리얼이 다르면 거기에도 설정
            Material active = ActiveMaterial;
            if (active != null && active != _material)
                ApplyPropertiesToTarget(active, customTime);
        }

        private void ApplyPropertiesToTarget(Material target, float customTime)
        {
            if (target == null) return;

            if (_windableType == WindableType.Sprite)
                UpdateSpriteProperties(target, customTime);
            else if (_windableType == WindableType.UI)
                UpdateUIProperties(target, customTime);

            SetCommonMaterialProperties(target, customTime);
        }

        /// <summary>
        /// Sprite용 프로퍼티 업데이트
        /// </summary>
        private void UpdateSpriteProperties(Material target, float customTime)
        {
            if (_spriteRenderer?.sprite == null) return;

            _MainTex = _spriteRenderer.sprite.texture;
            if (_MainTex == null) return;

            target.SetTexture(PropMainTex, _MainTex);

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
            target.SetVector(PropSpriteUVRect, uvRect);

            // 스프라이트의 피벗을 UV 공간 기준으로 계산
            float pivotX = (r.x + sprite.pivot.x) / t.width;
            float pivotY = (r.y + sprite.pivot.y) / t.height;
            Vector2 spritePivot = new Vector2(pivotX, pivotY);
            target.SetVector(PropSpritePivot, spritePivot);
        }

        /// <summary>
        /// UI용 프로퍼티 업데이트
        /// </summary>
        private void UpdateUIProperties(Material target, float customTime)
        {
            if (_graphic == null) return;

            _MainTex = _graphic.mainTexture;
            if (_MainTex == null) return;

            target.SetTexture(PropMainTex, _MainTex);

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
                target.SetVector(PropSpriteUVRect, uvRect);

                float pivotX = (r.x + sprite.pivot.x) / t.width;
                float pivotY = (r.y + sprite.pivot.y) / t.height;
                spritePivot = new Vector2(pivotX, pivotY);
            }
            else
            {
                target.SetVector(PropSpriteUVRect, new Vector4(0, 0, 1, 1));
            }

            target.SetVector(PropSpritePivot, spritePivot);
        }

        /// <summary>
        /// 공통 머티리얼 프로퍼티 설정
        /// </summary>
        private void SetCommonMaterialProperties(Material target, float customTime)
        {
            target.SetFloat(PropCustomTime, customTime);
            target.SetFloat(PropRotateUV, _RotateUV);
            target.SetTexture(PropNoiseTex, _NoiseTex);
            target.SetFloat(PropWindSpeed, _WindSpeed);
            target.SetFloat(PropWindStrength, _WindStrength);
            target.SetFloat(PropWindFrequency, _WindFrequency);
            target.SetVector(PropWindDirection, _WindDirection);
            target.SetVector(PropClipRect, _ClipRect);
            target.SetFloat(PropWindScale, _WindScale);
            target.SetFloat(PropImageOffsetX, _ImageOffsetX);
            target.SetFloat(PropImageOffsetY, _ImageOffsetY);
            target.SetFloat(PropImageScale, _ImageScale);

            // 바람 방향 정규화를 C#에서 사전 계산하여 셰이더 매 픽셀 normalize 연산 제거
            Vector2 windDir = new Vector2(_WindDirection.x, _WindDirection.y);
            float mag = windDir.magnitude;
            Vector2 normalizedDir = mag > 0.0001f ? windDir / mag : Vector2.right;
            target.SetVector(PropNormalizedWindDir, new Vector4(normalizedDir.x, normalizedDir.y, 0, 0));
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