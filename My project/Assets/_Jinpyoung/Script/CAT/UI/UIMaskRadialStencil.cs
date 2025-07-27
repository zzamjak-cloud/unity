using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

        private Material maskMaterial;
        private Material maskTargetMaterial;
        private Dictionary<Graphic, Material> originalMaterials = new Dictionary<Graphic, Material>();

        void OnEnable()
        {
            UpdateMaskMaterial();
            UpdateMaskGraphicVisibility();
            if (autoApplyToChildren)
                ApplyMaskToChildren();
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
                var shader = Shader.Find("CAT/UI/UIMaskRadialStencil");
                if (shader == null)
                {
                    Debug.LogError("CAT/UI/UIMaskRadialStencil shader not found!");
                    return;
                }
                maskMaterial = new Material(shader);
            }
            maskMaterial.SetFloat("_InnerRadius", innerRadius);
            maskMaterial.SetFloat("_OuterRadius", outerRadius);

            // showMaskGraphic이 false면 알파를 0으로
            maskMaterial.SetFloat("_Alpha", showMaskGraphic ? 1f : 0f);

            image.material = maskMaterial;
        }

        void ApplyMaskToChildren()
        {
            if (maskTargetMaterial == null)
            {
                var shader = Shader.Find("CAT/UI/UIMaskRadialStencilTarget");
                if (shader == null)
                {
                    Debug.LogError("CAT/UI/UIMaskRadialStencilTarget shader not found!");
                    return;
                }
                maskTargetMaterial = new Material(shader);
            }

            originalMaterials.Clear();
            foreach (var childGraphic in GetComponentsInChildren<Graphic>(includeInactive: true))
            {
                if (childGraphic == GetComponent<Graphic>()) continue; // 자기 자신 제외
                if (!originalMaterials.ContainsKey(childGraphic))
                    originalMaterials[childGraphic] = childGraphic.material;
                childGraphic.material = maskTargetMaterial;
            }
        }

        void RestoreChildrenMaterials()
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
    }
}