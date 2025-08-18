using UnityEngine;
using System.Collections.Generic;
using System;

// 주요 기능 :
// 자식 스프라이트 그룹의 알파값을 일괄 조정하는 컴포넌트(부모의 알파값과 곱해짐)

// 성능 이슈 :
// 런타임과 에디터 모두에서 groupAlpha가 변경된 경우나 명시적으로 dirtyFlag가 설정된 경우에만 알파값을 업데이트
// OnTransformChildrenChanged 콜백을 사용하여 자식 구조가 변경되었을 때 업데이트 플래그를 설정
// 데이터가 실제로 변경된 경우에만 직렬화 작업을 수행

namespace CAT.Effects
{
    [ExecuteInEditMode]
    [AddComponentMenu("CAT/Effects/SpriteGroup")]
    [DisallowMultipleComponent]
    public class SpriteGroup : MonoBehaviour
    {
        [Range(0, 1)]
        public float groupAlpha = 1.0f;
        private float lastGroupAlpha = 1.0f;
        
        [SerializeField, HideInInspector]
        private bool dirtyFlag = false;
        
        [Serializable]
        private class SpriteAlphaData
        {
            public SpriteRenderer spriteRenderer;
            public float originalAlpha = 1.0f;
        }
        
        [SerializeField, HideInInspector]
        private List<SpriteAlphaData> spriteData = new List<SpriteAlphaData>();
        
        private Dictionary<SpriteRenderer, float> originalAlphas = new Dictionary<SpriteRenderer, float>();
        private Dictionary<SpriteRenderer, Color> lastColors = new Dictionary<SpriteRenderer, Color>();
        
        private SpriteRenderer[] childRenderers;
        
        void OnEnable()
        {
            LoadAlphaDataToDictionary();
            RefreshRendererList(false);
            lastGroupAlpha = groupAlpha;
        }
        
        private void LoadAlphaDataToDictionary()
        {
            originalAlphas.Clear();
            foreach (var data in spriteData)
            {
                if (data.spriteRenderer != null)
                {
                    originalAlphas[data.spriteRenderer] = data.originalAlpha;
                }
            }
        }
        
        private void SaveAlphaDataToList()
        {
            // This method now checks for actual changes before marking the object dirty,
            // which is more efficient for serialization.
            bool dataHasChanged = false;
            bool[] used = new bool[spriteData.Count];
            
            foreach (var renderer in originalAlphas.Keys)
            {
                bool found = false;
                for (int i = 0; i < spriteData.Count; i++)
                {
                    if (spriteData[i].spriteRenderer == renderer)
                    {
                        if (Math.Abs(spriteData[i].originalAlpha - originalAlphas[renderer]) > 0.001f)
                        {
                            spriteData[i].originalAlpha = originalAlphas[renderer];
                            dataHasChanged = true;
                        }
                        used[i] = true;
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    spriteData.Add(new SpriteAlphaData 
                    { 
                        spriteRenderer = renderer, 
                        originalAlpha = originalAlphas[renderer] 
                    });
                    dataHasChanged = true;
                }
            }
            
            for (int i = used.Length - 1; i >= 0; i--)
            {
                if (!used[i] || spriteData[i].spriteRenderer == null)
                {
                    spriteData.RemoveAt(i);
                    dataHasChanged = true;
                }
            }
            
            #if UNITY_EDITOR
            if (dataHasChanged && !Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
            #endif
        }
        
        void Update()
        {
            // In the Editor, continuously check for external color changes on children
            // that don't trigger OnValidate (e.g., changing a child's color directly).
            if (!Application.isPlaying)
            {
                CheckForColorChanges();
            }

            // Unified update logic for both Editor and Runtime.
            // This executes every frame if the groupAlpha value is changing (e.g., via animation)
            // or if the hierarchy has changed (dirtyFlag).
            if (Mathf.Abs(groupAlpha - lastGroupAlpha) > 0.001f || dirtyFlag)
            {
                // If the dirty flag is set, it's safest to re-scan for children.
                if (dirtyFlag)
                {
                    RefreshRendererList(false);
                }
                
                ApplyGroupAlpha();
                lastGroupAlpha = groupAlpha;
                dirtyFlag = false; // Reset the flag after handling it.
            }
        }
        
        void OnValidate()
        {
            // When a value is changed in the Inspector, ensure data is consistent and flag for update.
            LoadAlphaDataToDictionary();
            dirtyFlag = true;
        }
        
        private void RefreshRendererList(bool resetAlphas)
        {
            childRenderers = GetComponentsInChildren<SpriteRenderer>(true); // Include inactive children
            
            var currentRenderersInDict = new HashSet<SpriteRenderer>(originalAlphas.Keys);
            
            foreach (SpriteRenderer renderer in childRenderers)
            {
                if (renderer == null) continue;
                
                currentRenderersInDict.Remove(renderer);
                
                if (resetAlphas || !originalAlphas.ContainsKey(renderer))
                {
                    // If resetting, or if it's a new renderer, calculate its original alpha based on the current state.
                    float originalAlpha = groupAlpha > 0.001f ? 
                        renderer.color.a / groupAlpha : renderer.color.a;
                    originalAlphas[renderer] = Mathf.Clamp01(originalAlpha);
                }
                
                lastColors[renderer] = renderer.color;
            }
            
            // Remove renderers that are no longer children
            foreach (var oldRenderer in currentRenderersInDict)
            {
                if (oldRenderer != null)
                {
                    originalAlphas.Remove(oldRenderer);
                    lastColors.Remove(oldRenderer);
                }
            }
            
            SaveAlphaDataToList();
        }
        
        private void CheckForColorChanges()
        {
            if (childRenderers == null) return;
            
            bool dataChanged = false;
            
            foreach (SpriteRenderer renderer in childRenderers)
            {
                if (renderer != null && lastColors.TryGetValue(renderer, out Color lastColor))
                {
                    if (renderer.color != lastColor)
                    {
                        // If the alpha value was changed externally, recalculate the originalAlpha
                        if (Mathf.Abs(renderer.color.a - lastColor.a) > 0.001f)
                        {
                            float newOriginalAlpha = groupAlpha > 0.001f ? 
                                renderer.color.a / groupAlpha : renderer.color.a;
                            
                            if (Mathf.Abs(originalAlphas[renderer] - newOriginalAlpha) > 0.001f)
                            {
                                originalAlphas[renderer] = Mathf.Clamp01(newOriginalAlpha);
                                dataChanged = true;
                            }
                        }
                        
                        lastColors[renderer] = renderer.color;
                    }
                }
            }
            
            if (dataChanged)
            {
                SaveAlphaDataToList();
            }
        }
        
        private void ApplyGroupAlpha()
        {
            if (childRenderers == null) return;
            
            foreach (SpriteRenderer renderer in childRenderers)
            {
                if (renderer != null)
                {
                    if (!originalAlphas.ContainsKey(renderer))
                    {
                        // This case is a fallback, RefreshRendererList should handle it.
                        originalAlphas[renderer] = 1.0f;
                        lastColors[renderer] = renderer.color;
                    }
                    
                    Color color = renderer.color;
                    color.a = originalAlphas[renderer] * groupAlpha;
                    renderer.color = color;
                    lastColors[renderer] = color;
                }
            }
        }
        
        // 트랜스폼 변경 감지
        void OnTransformChildrenChanged()
        {
            // Set the dirty flag so Update() can refresh the renderer list.
            dirtyFlag = true;
        }
        
        // 공개 메서드
        public void Refresh()
        {
            RefreshRendererList(false);
            dirtyFlag = true;
        }
        
        public void ResetAllAlphas()
        {
            RefreshRendererList(true);
            dirtyFlag = true;
        }
        
        public void SetChildOriginalAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer != null)
            {
                originalAlphas[renderer] = Mathf.Clamp01(alpha);
                SaveAlphaDataToList();
                dirtyFlag = true;
            }
        }
        
        public float GetChildOriginalAlpha(SpriteRenderer renderer)
        {
            if (renderer != null && originalAlphas.TryGetValue(renderer, out float alpha))
            {
                return alpha;
            }
            return 1.0f;
        }
    }
}