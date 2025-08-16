using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq; // <-- 1. 필터링(Where) 기능을 사용하기 위해 이 줄을 추가합니다.

// 주요 기능 :
// 자식 스프라이트 그룹의 알파값을 일괄 조정하는 컴포넌트(부모의 알파값과 곱해짐)

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
            // 에디터 모드일 때, 자식 스프라이트의 색상이 외부에서 직접 변경되는지 계속 확인합니다.
            if (!Application.isPlaying)
            {
                CheckForColorChanges();
            }

            // groupAlpha 값이 변경되거나 dirtyFlag가 설정되었을 때만 업데이트를 수행하는 통합 로직
            if (Mathf.Abs(groupAlpha - lastGroupAlpha) > 0.001f || dirtyFlag)
            {
                // dirtyFlag가 설정되었다면 자식 구조에 변경이 있었을 가능성이 있으므로 목록을 갱신합니다.
                if (dirtyFlag)
                {
                    RefreshRendererList(false);
                }
                
                ApplyGroupAlpha();
                lastGroupAlpha = groupAlpha;
                dirtyFlag = false; // 플래그 처리 완료 후 초기화
            }
        }
        
        void OnValidate()
        {
            // 인스펙터에서 값이 변경될 때마다 데이터를 로드하고 업데이트 플래그를 설정합니다.
            LoadAlphaDataToDictionary();
            dirtyFlag = true;
        }
        
        private void RefreshRendererList(bool resetAlphas)
        {
            // <-- 2. 오류를 해결하기 위해 수정된 부분
            // LINQ를 사용하여 모든 렌더러를 가져온 뒤, 에디터용 임시 오브젝트를 걸러냅니다.
            var allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            childRenderers = allRenderers.Where(r => r != null && (r.gameObject.hideFlags & (HideFlags.DontSaveInEditor | HideFlags.DontSave)) == 0).ToArray();
            
            var currentRenderersInDict = new HashSet<SpriteRenderer>(originalAlphas.Keys);
            
            foreach (SpriteRenderer renderer in childRenderers)
            {
                currentRenderersInDict.Remove(renderer);
                
                if (resetAlphas || !originalAlphas.ContainsKey(renderer))
                {
                    float originalAlpha = groupAlpha > 0.001f ? 
                        renderer.color.a / groupAlpha : renderer.color.a;
                    originalAlphas[renderer] = Mathf.Clamp01(originalAlpha);
                }
                
                lastColors[renderer] = renderer.color;
            }
            
            // 더 이상 자식이 아닌 렌더러는 목록에서 제거합니다.
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
                        // 알파값이 외부에서 변경되었다면, originalAlpha 값을 다시 계산합니다.
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
        
        // 자식 트랜스폼 변경 감지
        void OnTransformChildrenChanged()
        {
            // 자식 계층 구조가 변경되었음을 표시하여 Update에서 렌더러 목록을 갱신하도록 합니다.
            dirtyFlag = true;
        }
        
        // 외부 호출용 공개 메서드
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