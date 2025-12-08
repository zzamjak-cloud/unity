using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CAT.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class UIMaskRadialStencil : MonoBehaviour
    {
        [Range(0f, 1f)]
        public float innerRadius = 0.3f;
        [Range(0f, 1f)]
        public float outerRadius = 0.8f;

        [Header("Auto Apply To Children")]
        public bool autoApplyToChildren = true;

        [Header("Mask Options")]
        public bool showMaskGraphic = true;

        [Header("Shader References (Optional)")]
        [Tooltip("If shader is not found automatically, assign it manually here")]
        public Shader maskShader;
        [Tooltip("If shader is not found automatically, assign it manually here")]
        public Shader maskTargetShader;

        private Material maskMaterial;
        private Material maskTargetMaterial;
        private Dictionary<Graphic, Material> originalMaterials = new Dictionary<Graphic, Material>();
        
#if UNITY_EDITOR
        private int lastChildCount = 0;
        private Transform lastTransform;
#endif

        void OnEnable()
        {
            UpdateMaskMaterial();
            UpdateMaskGraphicVisibility();
            if (autoApplyToChildren)
                ApplyMaskToChildren();
                
#if UNITY_EDITOR
            // Editor 환경에서 자식 오브젝트 변경 감지를 위한 초기화
            lastChildCount = transform.childCount;
            lastTransform = transform;
#endif
        }

        void OnDisable()
        {
            if (maskMaterial != null)
            {
                DestroyImmediate(maskMaterial);
                maskMaterial = null;
            }
            if (autoApplyToChildren)
                RestoreChildrenMaterials();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UpdateMaskMaterial();
            UpdateMaskGraphicVisibility();
            if (autoApplyToChildren)
                ApplyMaskToChildren();
        }
#endif

        void UpdateMaskMaterial()
        {
            var image = GetComponent<Image>();
            if (image == null) return;

            if (maskMaterial == null)
            {
                Shader shader = maskShader; // 먼저 할당된 셰이더 사용
                
                if (shader == null)
                {
                    shader = Shader.Find("CAT/UI/UIMaskRadialStencil");
                    if (shader == null)
                    {
                        // 셰이더를 다시 찾아보기 위해 잠시 대기
                        Debug.LogWarning("CAT/UI/UIMaskRadialStencil shader not found! Trying to reload...");
                        shader = Shader.Find("CAT/UI/UIMaskRadialStencil");
                        if (shader == null)
                        {
                            Debug.LogError("CAT/UI/UIMaskRadialStencil shader not found after retry! Please assign the shader manually in the inspector.");
                            return;
                        }
                    }
                }
                maskMaterial = new Material(shader);
            }
            maskMaterial.SetFloat("_InnerRadius", innerRadius);
            maskMaterial.SetFloat("_OuterRadius", outerRadius);

            // showMaskGraphic이 false면 알파를 0으로
            maskMaterial.SetFloat("_Alpha", showMaskGraphic ? 1f : 0f);

            image.material = maskMaterial;
        }

        public void ApplyMaskToChildren()
        {
            originalMaterials.Clear();
            foreach (var childGraphic in GetComponentsInChildren<Graphic>(includeInactive: true))
            {
                if (childGraphic == GetComponent<Graphic>()) continue; // 자기 자신 제외
                if (!originalMaterials.ContainsKey(childGraphic))
                    originalMaterials[childGraphic] = childGraphic.material;

                // 머티리얼 인스턴스 생성 및 color 적용
                Shader shader = maskTargetShader; // 먼저 할당된 셰이더 사용
                
                if (shader == null)
                {
                    shader = Shader.Find("CAT/UI/UIMaskRadialStencilTarget");
                    if (shader == null)
                    {
                        // 셰이더를 다시 찾아보기 위해 잠시 대기
                        Debug.LogWarning("CAT/UI/UIMaskRadialStencilTarget shader not found! Trying to reload...");
                        shader = Shader.Find("CAT/UI/UIMaskRadialStencilTarget");
                        if (shader == null)
                        {
                            Debug.LogError("CAT/UI/UIMaskRadialStencilTarget shader not found after retry! Please assign the shader manually in the inspector.");
                            continue;
                        }
                    }
                }
                var mat = new Material(shader);
                mat.SetColor("_Color", childGraphic.color);
                childGraphic.material = mat;
            }
        }

        public void RestoreChildrenMaterials()
        {
            foreach (var kvp in originalMaterials)
            {
                if (kvp.Key != null)
                    kvp.Key.material = kvp.Value;
            }
            originalMaterials.Clear();
        }

        private void UpdateMaskGraphicVisibility()
        {
            // 아무 것도 하지 않거나, 필요 없다면 이 함수 자체를 삭제해도 됩니다.
        }

#if UNITY_EDITOR
        public void CheckForChildChanges()
        {
            if (!autoApplyToChildren) return;
            
            // 자식 오브젝트 개수 변경 감지
            int currentChildCount = transform.childCount;
            if (currentChildCount != lastChildCount)
            {
                lastChildCount = currentChildCount;
                // 새로운 자식 오브젝트가 추가되었으므로 마스킹 재적용
                ApplyMaskToChildren();
                return;
            }

            // 자식 오브젝트의 Graphic 컴포넌트 변경 감지
            var currentGraphics = GetComponentsInChildren<Graphic>(includeInactive: true);
            bool hasNewGraphics = false;
            
            foreach (var graphic in currentGraphics)
            {
                if (graphic == GetComponent<Graphic>()) continue; // 자기 자신 제외
                
                // 새로운 Graphic 컴포넌트가 추가되었거나 마스킹이 적용되지 않은 경우
                if (!originalMaterials.ContainsKey(graphic) || 
                    (graphic.material != null && !graphic.material.shader.name.Contains("UIMaskRadialStencilTarget")))
                {
                    hasNewGraphics = true;
                    break;
                }
            }
            
            if (hasNewGraphics)
            {
                ApplyMaskToChildren();
            }
        }
#endif

        void LateUpdate()
        {
            UpdateMaskMaterial();

            // 자식들의 color가 바뀌었으면 머티리얼에도 반영
            foreach (var childGraphic in GetComponentsInChildren<Graphic>(includeInactive: true))
            {
                if (childGraphic == GetComponent<Graphic>()) continue;
                var mat = childGraphic.material;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    mat.SetColor("_Color", childGraphic.color);
                }
            }
        }
    }
}