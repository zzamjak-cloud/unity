using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class MultiSliceImage : MaskableGraphic
{
    private const int MAX_CUTS = 4; // 최대 컷 수 제한

    [SerializeField] private Sprite m_Sprite;
    
    // 0.0 ~ 1.0 정규화된 좌표값 (최대 4개)
    public List<float> verticalCuts = new List<float>();
    public List<float> horizontalCuts = new List<float>();

    // 성능 최적화: 캐시된 stops 리스트
    private List<float> cachedVStops = new List<float>();
    private List<float> cachedHStops = new List<float>();
    private bool stopsDirty = true;
    private Rect cachedRect;
    private Sprite cachedSprite;
    private int cachedVCutsHash = 0;
    private int cachedHCutsHash = 0;

    public Sprite sprite
    {
        get { return m_Sprite; }
        set
        {
            if (m_Sprite != value)
            {
                m_Sprite = value;
                stopsDirty = true;
                cachedSprite = null;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }
    }

    public override Texture mainTexture => m_Sprite == null ? s_WhiteTexture : m_Sprite.texture;

    /// <summary>
    /// 스프라이트의 원본 크기를 RectTransform에 적용합니다.
    /// Unity Image 컴포넌트의 SetNativeSize()와 동일한 기능입니다.
    /// </summary>
    public override void SetNativeSize()
    {
        if (m_Sprite == null) return;

        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) return;

        rectTransform.anchorMax = rectTransform.anchorMin;
        rectTransform.sizeDelta = m_Sprite.rect.size;
        SetVerticesDirty();
    }

    private new void OnValidate()
    {
        base.OnValidate();
        
        // 에디터에서 값이 변경될 때 컷 수 제한 적용
        bool changed = false;
        if (verticalCuts.Count > MAX_CUTS)
        {
            verticalCuts.RemoveRange(MAX_CUTS, verticalCuts.Count - MAX_CUTS);
            changed = true;
        }
        if (horizontalCuts.Count > MAX_CUTS)
        {
            horizontalCuts.RemoveRange(MAX_CUTS, horizontalCuts.Count - MAX_CUTS);
            changed = true;
        }
        
        if (changed)
        {
            stopsDirty = true;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (m_Sprite == null) return;

        Rect rect = GetPixelAdjustedRect();
        
        // 컷 리스트 변경 감지 (Editor에서 즉시 반영을 위해)
        int currentVCutsHash = GetListHash(verticalCuts);
        int currentHCutsHash = GetListHash(horizontalCuts);
        bool cutsChanged = currentVCutsHash != cachedVCutsHash || currentHCutsHash != cachedHCutsHash;
        
        // 변경 감지: 스프라이트, 크기, 또는 컷 리스트가 변경되었는지 확인
        bool needsUpdate = stopsDirty || cachedSprite != m_Sprite || cachedRect != rect || cutsChanged;
        
        if (needsUpdate)
        {
            cachedSprite = m_Sprite;
            cachedRect = rect;
            cachedVCutsHash = currentVCutsHash;
            cachedHCutsHash = currentHCutsHash;
            stopsDirty = false;
        }

        // 캐시된 stops 사용 (변경 시에만 재계산)
        List<float> vStops = GetSortedStops(verticalCuts, cachedVStops, needsUpdate);
        List<float> hStops = GetSortedStops(horizontalCuts, cachedHStops, needsUpdate);

        Vector4 outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(m_Sprite);
        
        // UV 좌표 범위 (아틀라스 내에서의 범위)
        float uvWidth = outerUV.z - outerUV.x;
        float uvHeight = outerUV.w - outerUV.y;
        Vector2 uvMin = new Vector2(outerUV.x, outerUV.y);

        float spriteW = m_Sprite.rect.width;
        float spriteH = m_Sprite.rect.height;

        float[] vSizesSrc, vSizesDst;
        float[] hSizesSrc, hSizesDst;

        CalculateSizes(vStops, rect.width, spriteW, out vSizesSrc, out vSizesDst);
        CalculateSizes(hStops, rect.height, spriteH, out hSizesSrc, out hSizesDst);

        Vector2 currentPos = new Vector2(rect.x, rect.y);

        for (int y = 0; y < hSizesDst.Length; y++)
        {
            float rowHeight = hSizesDst[y];

            currentPos.x = rect.x;

            for (int x = 0; x < vSizesDst.Length; x++)
            {
                float colWidth = vSizesDst[x];

                float uvLeft = uvMin.x + vStops[x] * uvWidth;
                float uvRight = uvMin.x + vStops[x + 1] * uvWidth;
                float uvBottom = uvMin.y + hStops[y] * uvHeight;
                float uvTop = uvMin.y + hStops[y + 1] * uvHeight;

                Rect cellRect = new Rect(currentPos.x, currentPos.y, colWidth, rowHeight);
                Rect uvRect = new Rect(uvLeft, uvBottom, uvRight - uvLeft, uvTop - uvBottom);

                AddQuad(vh, cellRect, uvRect);
                currentPos.x += colWidth;
            }
            currentPos.y += rowHeight;
        }
    }

    // 성능 최적화: 캐시된 리스트 재사용 (GC 할당 최소화)
    private List<float> GetSortedStops(List<float> cuts, List<float> cache, bool forceUpdate)
    {
        if (!forceUpdate && cache.Count > 0)
        {
            return cache;
        }

        cache.Clear();
        cache.Add(0f);
        
        if (cuts != null && cuts.Count > 0)
        {
            // 최대 4개까지만 처리
            int count = Mathf.Min(cuts.Count, MAX_CUTS);
            for (int i = 0; i < count; i++)
            {
                cache.Add(cuts[i]);
            }
            cache.Sort(); // 오름차순 정렬
        }
        
        cache.Add(1f);
        return cache;
    }

    private void CalculateSizes(List<float> stops, float totalDstSize, float totalSrcSize, out float[] srcSizes, out float[] dstSizes)
    {
        int count = stops.Count - 1;
        srcSizes = new float[count];
        dstSizes = new float[count];

        float totalFixedSrc = 0f;
        float totalFlexSrc = 0f;

        for (int i = 0; i < count; i++)
        {
            float size = (stops[i + 1] - stops[i]) * totalSrcSize;
            srcSizes[i] = size;
            if (i % 2 == 0) totalFixedSrc += size;
            else totalFlexSrc += size;
        }

        float scale = 1.0f;
        float availableFlexSpace = 0f;

        if (totalDstSize < totalFixedSrc)
        {
            scale = totalDstSize / totalFixedSrc;
        }
        else
        {
            availableFlexSpace = totalDstSize - totalFixedSrc;
        }

        for (int i = 0; i < count; i++)
        {
            if (i % 2 == 0) dstSizes[i] = srcSizes[i] * scale;
            else
            {
                float ratio = (totalFlexSrc > 0) ? (srcSizes[i] / totalFlexSrc) : 0;
                dstSizes[i] = availableFlexSpace * ratio;
            }
        }
    }

    private void AddQuad(VertexHelper vh, Rect posRect, Rect uvRect)
    {
        int i = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = color;

        v.position = new Vector3(posRect.x, posRect.y);
        v.uv0 = new Vector2(uvRect.x, uvRect.y);
        vh.AddVert(v);

        v.position = new Vector3(posRect.x, posRect.y + posRect.height);
        v.uv0 = new Vector2(uvRect.x, uvRect.y + uvRect.height);
        vh.AddVert(v);

        v.position = new Vector3(posRect.x + posRect.width, posRect.y + posRect.height);
        v.uv0 = new Vector2(uvRect.x + uvRect.width, uvRect.y + uvRect.height);
        vh.AddVert(v);

        v.position = new Vector3(posRect.x + posRect.width, posRect.y);
        v.uv0 = new Vector2(uvRect.x + uvRect.width, uvRect.y);
        vh.AddVert(v);

        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }

    // 리스트의 해시값을 계산하여 변경 감지
    private int GetListHash(List<float> list)
    {
        if (list == null || list.Count == 0) return 0;
        
        int hash = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            hash = hash * 31 + list[i].GetHashCode();
        }
        return hash;
    }

}