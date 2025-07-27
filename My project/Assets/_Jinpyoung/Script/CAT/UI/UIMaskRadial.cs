using UnityEngine;
using UnityEngine.UI;

namespace CAT.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public class UIMaskRadial : MonoBehaviour, IMaterialModifier
    {
        [Header("Radial Mask Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float innerRadius = 0.3f;
        
        [Range(0f, 1f)]
        [SerializeField] private float outerRadius = 0.8f;
        
        private Graphic graphic;
        private Material materialInstance;
        
        // 내부 반지름 프로퍼티
        public float InnerRadius
        {
            get => innerRadius;
            set
            {
                innerRadius = Mathf.Clamp01(value);
                if (innerRadius > outerRadius)
                {
                    innerRadius = outerRadius;
                }
                UpdateMaterial();
            }
        }
        
        // 외부 반지름 프로퍼티
        public float OuterRadius
        {
            get => outerRadius;
            set
            {
                outerRadius = Mathf.Clamp01(value);
                if (outerRadius < innerRadius)
                {
                    outerRadius = innerRadius;
                }
                UpdateMaterial();
            }
        }
        
        private void Awake()
        {
            graphic = GetComponent<Graphic>();
        }
        
        private void OnEnable()
        {
            UpdateMaterial();
            if (graphic != null)
            {
                graphic.SetMaterialDirty();
            }
        }
        
        private void OnDisable()
        {
            if (materialInstance != null)
            {
                DestroyImmediate(materialInstance);
                materialInstance = null;
            }
            
            if (graphic != null)
            {
                graphic.SetMaterialDirty();
            }
        }
        
        private void OnDestroy()
        {
            if (materialInstance != null)
            {
                DestroyImmediate(materialInstance);
                materialInstance = null;
            }
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // 에디터에서 값이 변경될 때 호출
            if (innerRadius > outerRadius)
            {
                innerRadius = outerRadius;
            }
            
            UpdateMaterial();
            
            if (graphic != null)
            {
                graphic.SetMaterialDirty();
            }
        }
#endif
        
        private void UpdateMaterial()
        {
            if (materialInstance == null)
            {
                // 셰이더 로드
                Shader shader = Shader.Find("CAT/UI/UIMaskRadial");
                if (shader == null)
                {
                    Debug.LogError("UIMaskRadial shader not found! Make sure the shader is in your project.");
                    return;
                }
                
                materialInstance = new Material(shader);
            }
            
            // 셰이더 파라미터 업데이트
            if (materialInstance != null)
            {
                materialInstance.SetFloat("_InnerRadius", innerRadius);
                materialInstance.SetFloat("_OuterRadius", outerRadius);
            }
        }
        
        // IMaterialModifier 인터페이스 구현
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!enabled)
            {
                return baseMaterial;
            }
            
            if (materialInstance == null)
            {
                UpdateMaterial();
            }
            
            return materialInstance;
        }
        
        // 헬퍼 메서드들
        
        /// <summary>
        /// 두 반지름을 동시에 설정합니다.
        /// </summary>
        public void SetRadii(float inner, float outer)
        {
            innerRadius = Mathf.Clamp01(inner);
            outerRadius = Mathf.Clamp01(outer);
            
            // 내부 반지름이 외부 반지름보다 크지 않도록 보정
            if (innerRadius > outerRadius)
            {
                float temp = innerRadius;
                innerRadius = outerRadius;
                outerRadius = temp;
            }
            
            UpdateMaterial();
            
            if (graphic != null)
            {
                graphic.SetMaterialDirty();
            }
        }
        
        /// <summary>
        /// 도넛의 두께를 설정합니다. (외부 반지름은 유지)
        /// </summary>
        public void SetThickness(float thickness)
        {
            thickness = Mathf.Clamp01(thickness);
            innerRadius = Mathf.Max(0, outerRadius - thickness);
            UpdateMaterial();
            
            if (graphic != null)
            {
                graphic.SetMaterialDirty();
            }
        }
        
        /// <summary>
        /// 도넛의 두께를 가져옵니다.
        /// </summary>
        public float GetThickness()
        {
            return outerRadius - innerRadius;
        }
    }
}