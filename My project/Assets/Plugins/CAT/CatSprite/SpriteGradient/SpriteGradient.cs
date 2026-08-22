using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace CAT.Effect
{
    /// <summary>
    /// SpriteRenderer의 텍스처에 2개의 컬러를 Gradient로 Multiply하는 효과.
    /// Vertical/Horizontal 방향과 두 컬러의 비중을 정하는 Lerp 값을 지원한다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("CAT/Effect/SpriteGradient")]
    public class SpriteGradient : MonoBehaviour
    {
        /// <summary>머티리얼 생성/검증에 사용하는 셰이더 이름.</summary>
        public const string ShaderName = "CAT/Effect/SpriteGradient";

        public enum GradientDirection
        {
            Vertical = 0,
            Horizontal = 1
        }

        [Header("Gradient 설정")]
        [SerializeField] private Color color1 = Color.white;
        [SerializeField] private Color color2 = Color.black;

        [Header("Gradient 방향")]
        [SerializeField] private GradientDirection gradientDirection = GradientDirection.Vertical;

        [Header("Lerp 설정")]
        // 두 컬러의 비중. 0.5 = 5:5, 0.2 = Color1 80% / Color2 20%. 그라디언트의 50:50 지점을 이동시킨다.
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Color2의 비중. 0.5면 5:5, 0.2면 Color1 80% / Color2 20% 비율로 그라디언트 중앙이 이동한다.")]
        private float lerpValue = 0.5f;

        [Header("성능 설정")]
        // 애니메이션 클립은 프로퍼티가 아닌 필드에 직접 기록하므로 setter의 변경 감지가 동작하지 않는다.
        [SerializeField] private bool alwaysUpdate = false;
        [SerializeField] private float updateThreshold = 0.001f;

        #region Public Properties

        public Color Color1
        {
            get => color1;
            set
            {
                if (color1 == value)
                    return;

                color1 = value;
                isDirty = true;
            }
        }

        public Color Color2
        {
            get => color2;
            set
            {
                if (color2 == value)
                    return;

                color2 = value;
                isDirty = true;
            }
        }

        public GradientDirection Direction
        {
            get => gradientDirection;
            set
            {
                if (gradientDirection == value)
                    return;

                gradientDirection = value;
                isDirty = true;
            }
        }

        /// <summary>Color2의 비중(0~1). 0.5면 5:5, 0.2면 Color1 80% / Color2 20%.</summary>
        public float LerpValue
        {
            get => lerpValue;
            set
            {
                float newValue = Mathf.Clamp01(value);
                if (Mathf.Abs(lerpValue - newValue) <= updateThreshold)
                    return;

                lerpValue = newValue;
                isDirty = true;
            }
        }

        /// <summary>클립 애니메이션 사용 여부. 꺼져 있으면 프로퍼티 변경만 반영된다.</summary>
        public bool AlwaysUpdate
        {
            get => alwaysUpdate;
            set => alwaysUpdate = value;
        }

        #endregion

        // 캐시된 컴포넌트
        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;

        // 스프라이트 단위 캐시 (아틀라스 UV 영역)
        private Sprite cachedSprite;
        private Vector4 spriteUVRect = new Vector4(0f, 0f, 1f, 1f);

        // 변경 감지
        private bool isDirty = true;
        private bool forceWrite = true;
        private Color lastColor1;
        private Color lastColor2;
        private GradientDirection lastGradientDirection;
        private float lastLerpValue = -1f;

        // 씬/프리팹에 머티리얼이 지정되지 않은 경우에만 쓰이는 런타임 임시 머티리얼
        private static Material runtimeFallbackMaterial;
        private static bool runtimeShaderMissingLogged;

        // 셰이더 프로퍼티 ID (성능 최적화)
        private static readonly int Color1Property = Shader.PropertyToID("_Color1");
        private static readonly int Color2Property = Shader.PropertyToID("_Color2");
        private static readonly int GradientDirectionProperty = Shader.PropertyToID("_GradientDirection");
        private static readonly int LerpValueProperty = Shader.PropertyToID("_LerpValue");
        private static readonly int SpriteRectProperty = Shader.PropertyToID("_SpriteRect");

        /// <summary>현재 SpriteRenderer. 인스펙터 확장에서 사용한다.</summary>
        public SpriteRenderer Renderer
        {
            get
            {
                InitializeComponents();
                return spriteRenderer;
            }
        }

        /// <summary>지정한 머티리얼이 SpriteGradient 셰이더를 사용하는지 여부.</summary>
        public static bool HasGradientShader(Material material)
        {
            return material != null && material.shader != null && material.shader.name == ShaderName;
        }

        private void Awake()
        {
            InitializeComponents();
            SetupMaterial();
        }

        private void OnEnable()
        {
            InitializeComponents();
            SetupMaterial();
            SpriteGradientUpdater.Register(this);
            ForceUpdate();
        }

        private void OnDisable()
        {
            SpriteGradientUpdater.Unregister(this);
        }

        private void OnDestroy()
        {
            SpriteGradientUpdater.Unregister(this);

#if UNITY_EDITOR
            // 사용자가 편집 중 컴포넌트를 제거한 경우에만 원복한다.
            // 도메인 리로드/씬 언로드/플레이 모드 종료 시에는 씬을 건드리지 않는다.
            if (Application.isPlaying || spriteRenderer == null)
                return;

            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            spriteRenderer.SetPropertyBlock(null);

            if (HasGradientShader(spriteRenderer.sharedMaterial))
            {
                Material defaultSprite = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                if (defaultSprite != null)
                    spriteRenderer.sharedMaterial = defaultSprite;
            }

            EditorUtility.SetDirty(spriteRenderer);
            EditorSceneManager.MarkSceneDirty(scene);
#endif
        }

        private void InitializeComponents()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// 런타임에서 머티리얼이 비어 있을 때만 Shader.Find로 임시 머티리얼을 만들어 채운다.
        /// 에디터 편집 중에는 에셋을 자동 생성하지 않는다. (인스펙터의 "머티리얼 생성" 버튼 사용)
        /// </summary>
        private void SetupMaterial()
        {
            if (spriteRenderer == null)
                return;

            if (HasGradientShader(spriteRenderer.sharedMaterial))
                return;

            if (!Application.isPlaying)
                return;

            if (runtimeFallbackMaterial == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    if (!runtimeShaderMissingLogged)
                    {
                        runtimeShaderMissingLogged = true;
                        Debug.LogError(
                            $"[SpriteGradient] 셰이더 '{ShaderName}'를 찾을 수 없습니다. " +
                            "빌드에 포함되도록 Project Settings > Graphics > Always Included Shaders 에 등록하거나, " +
                            "해당 셰이더를 쓰는 머티리얼을 SpriteRenderer에 직접 할당하세요.", this);
                    }
                    return;
                }

                runtimeFallbackMaterial = new Material(shader) { name = "SpriteGradient (Runtime)" };
            }

            spriteRenderer.sharedMaterial = runtimeFallbackMaterial;
        }

        private void OnValidate()
        {
            lerpValue = Mathf.Clamp01(lerpValue);
            updateThreshold = Mathf.Max(0f, updateThreshold);
            isDirty = true;
            forceWrite = true;

#if UNITY_EDITOR
            // OnValidate는 임포트/직렬화 도중에도 호출되므로 렌더러 조작을 다음 에디터 틱으로 미룬다.
            if (Application.isPlaying)
                return;

            EditorApplication.delayCall += DelayedEditorRefresh;
#endif
        }

#if UNITY_EDITOR
        private void DelayedEditorRefresh()
        {
            EditorApplication.delayCall -= DelayedEditorRefresh;

            if (this == null)
                return;

            // 프리팹 에셋 임포트 중에는 렌더러를 건드리지 않는다. (임포트 재귀/불필요한 더티 방지)
            if (EditorUtility.IsPersistent(this))
                return;

            InitializeComponents();
            UpdateMaterialProperties();
        }
#endif

        /// <summary>SpriteGradientUpdater가 프레임당 한 번 호출한다.</summary>
        internal void Tick()
        {
            if (!Application.isPlaying)
            {
                UpdateMaterialProperties();
                return;
            }

            if (alwaysUpdate || isDirty)
            {
                UpdateMaterialProperties();
                isDirty = false;
            }
        }

        /// <summary>스프라이트가 바뀌면 아틀라스 UV 영역을 다시 계산한다.</summary>
        private void RefreshSpriteData()
        {
            Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
            if (ReferenceEquals(sprite, cachedSprite))
                return;

            cachedSprite = sprite;
            forceWrite = true;
            spriteUVRect = sprite != null
                ? CalculateSpriteUVRect(sprite)
                : new Vector4(0f, 0f, 1f, 1f);
        }

        /// <summary>
        /// 스프라이트가 아틀라스에서 차지하는 UV 영역을 구한다.
        /// Sprite.textureRect는 Tight 패킹에서 예외를 던지므로 실제 메시 UV의 바운딩 박스를 사용한다.
        /// </summary>
        private static Vector4 CalculateSpriteUVRect(Sprite sprite)
        {
            Vector2[] uvs = sprite.uv;
            if (uvs == null || uvs.Length == 0)
                return new Vector4(0f, 0f, 1f, 1f);

            Vector2 min = uvs[0];
            Vector2 max = uvs[0];

            for (int i = 1; i < uvs.Length; i++)
            {
                min = Vector2.Min(min, uvs[i]);
                max = Vector2.Max(max, uvs[i]);
            }

            Vector2 size = max - min;
            if (size.x <= Mathf.Epsilon || size.y <= Mathf.Epsilon)
                return new Vector4(0f, 0f, 1f, 1f);

            return new Vector4(min.x, min.y, size.x, size.y);
        }

        private void UpdateMaterialProperties()
        {
            if (spriteRenderer == null || propertyBlock == null)
                return;

            if (!HasGradientShader(spriteRenderer.sharedMaterial))
                return;

            RefreshSpriteData();

            // 값이 실제로 변경되었는지 확인 (성능 최적화)
            bool hasChanged = forceWrite;
            forceWrite = false;

            if (lastColor1 != color1)
                hasChanged = true;

            if (lastColor2 != color2)
                hasChanged = true;

            if (lastGradientDirection != gradientDirection)
                hasChanged = true;

            if (Mathf.Abs(lastLerpValue - lerpValue) > updateThreshold)
                hasChanged = true;

            if (!hasChanged)
                return;

            lastColor1 = color1;
            lastColor2 = color2;
            lastGradientDirection = gradientDirection;
            lastLerpValue = lerpValue;

            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(Color1Property, color1);
            propertyBlock.SetColor(Color2Property, color2);
            propertyBlock.SetFloat(GradientDirectionProperty, (float)gradientDirection);
            propertyBlock.SetFloat(LerpValueProperty, lerpValue);
            propertyBlock.SetVector(SpriteRectProperty, spriteUVRect);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>다음 갱신에서 무조건 프로퍼티를 다시 기록한다.</summary>
        public void ForceUpdate()
        {
            InitializeComponents();
            cachedSprite = null;
            isDirty = true;
            forceWrite = true;
            UpdateMaterialProperties();
        }

        [ContextMenu("Refresh Material")]
        public void RefreshMaterial()
        {
            InitializeComponents();
            propertyBlock.Clear();
            SetupMaterial();
            ForceUpdate();
        }

        /// <summary>
        /// 런타임에서 값 변경을 위한 헬퍼 메서드들
        /// </summary>
        public void SetColor1(Color color)
        {
            Color1 = color;
        }

        public void SetColor2(Color color)
        {
            Color2 = color;
        }

        public void SetGradientDirection(GradientDirection direction)
        {
            Direction = direction;
        }

        public void SetLerpValue(float value)
        {
            LerpValue = value;
        }

        public void SetAnimationMode(bool useAnimation)
        {
            alwaysUpdate = useAnimation;
        }

        /// <summary>
        /// 컴포넌트 리셋
        /// </summary>
        private void Reset()
        {
            color1 = Color.white;
            color2 = Color.black;
            gradientDirection = GradientDirection.Vertical;
            lerpValue = 0.5f;
            alwaysUpdate = false;
            updateThreshold = 0.001f;

            InitializeComponents();
            SetupMaterial();
            ForceUpdate();
        }
    }
}
