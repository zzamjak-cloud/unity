using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace CAT.UI
{
    /// <summary>
    /// 16-Slice 이미지 컴포넌트 (최대 4개의 Vertical/Horizontal Cuts 지원)
    ///
    /// Unity Image 컴포넌트 호환 기능:
    /// - Sprite: 렌더링할 스프라이트
    /// - Color: MaskableGraphic의 color 프로퍼티 사용 (Tint Color)
    /// - Material: MaskableGraphic의 material 프로퍼티 사용
    /// - Raycast Target: MaskableGraphic의 raycastTarget 프로퍼티 사용 (RectTransform 전체 영역)
    /// - Raycast Padding: Graphic의 raycastPadding 프로퍼티 사용
    /// - Maskable: MaskableGraphic 상속으로 자동 지원
    /// - Preserve Aspect: 종횡비 유지 옵션 (렌더링만 조정, Raycast는 전체 Rect 사용)
    ///
    /// 성능 최적화:
    /// - 배열 캐싱: 매 프레임 배열 할당 대신 캐시 재사용 (GC 압박 감소)
    /// - 타일링 최적화: 1픽셀 단위 대신 최대 32픽셀 단위로 타일링 (버텍스 수 대폭 감소)
    /// - while 루프 제거: 계산 기반 for 루프로 대체 (CPU 부하 감소)
    /// - List 캐싱: stops 리스트 재사용 (GC 할당 최소화)
    ///
    /// 모바일 환경에서 안정적인 성능 제공
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class MultiSliceImage : MaskableGraphic
    {
        private const int MAX_CUTS = 4; // 최대 컷 수 제한
        private const int MAX_TILE_SIZE = 32; // 타일 최대 크기 (픽셀) - 성능 최적화: 1픽셀보다 큰 타일로 버텍스 수 감소

        [SerializeField] private Sprite m_Sprite;
        [SerializeField] private bool m_PreserveAspect = false;

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

        // 성능 최적화: 배열 캐싱 (GC 할당 최소화)
        private float[] cachedVSizesSrc;
        private float[] cachedVSizesDst;
        private float[] cachedHSizesSrc;
        private float[] cachedHSizesDst;

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
        /// 종횡비 유지 여부
        /// true인 경우 RectTransform 크기에 맞춰 종횡비를 유지하며 이미지를 렌더링합니다.
        /// </summary>
        public bool preserveAspect
        {
            get { return m_PreserveAspect; }
            set
            {
                if (m_PreserveAspect != value)
                {
                    m_PreserveAspect = value;
                    SetVerticesDirty();
                }
            }
        }

        /// <summary>
        /// 스프라이트의 원본 크기를 RectTransform에 적용합니다.
        /// Unity Image 컴포넌트의 SetNativeSize()와 동일한 기능입니다.
        /// </summary>
        public override void SetNativeSize()
        {
            if (m_Sprite == null) return;
            if (rectTransform == null) return;

            rectTransform.anchorMax = rectTransform.anchorMin;
            rectTransform.sizeDelta = m_Sprite.rect.size;
            SetVerticesDirty();
        }

        protected override void OnValidate()
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

            // Preserve Aspect 처리
            if (m_PreserveAspect && m_Sprite != null)
            {
                float aspectW = m_Sprite.rect.width;
                float aspectH = m_Sprite.rect.height;
                float spriteRatio = aspectW / aspectH;
                float rectRatio = rect.width / rect.height;

                if (spriteRatio > rectRatio)
                {
                    // 스프라이트가 더 넓음 -> 세로를 줄임
                    float height = rect.width / spriteRatio;
                    rect.y += (rect.height - height) * 0.5f;
                    rect.height = height;
                }
                else
                {
                    // 스프라이트가 더 높음 -> 가로를 줄임
                    float width = rect.height * spriteRatio;
                    rect.x += (rect.width - width) * 0.5f;
                    rect.width = width;
                }
            }
            
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

            CalculateSizes(vStops, rect.width, spriteW, out vSizesSrc, out vSizesDst, ref cachedVSizesSrc, ref cachedVSizesDst);
            CalculateSizes(hStops, rect.height, spriteH, out hSizesSrc, out hSizesDst, ref cachedHSizesSrc, ref cachedHSizesDst);

            Vector2 currentPos = new Vector2(rect.x, rect.y);

            for (int y = 0; y < hSizesDst.Length; y++)
            {
                float rowHeight = hSizesDst[y];
                float srcRowHeight = hSizesSrc[y];
                bool isFlexibleRow = (y % 2 == 1); // 홀수 인덱스: 확장 영역

                currentPos.x = rect.x;

                for (int x = 0; x < vSizesDst.Length; x++)
                {
                    float colWidth = vSizesDst[x];
                    float srcColWidth = vSizesSrc[x];
                    bool isFlexibleCol = (x % 2 == 1); // 홀수 인덱스: 확장 영역

                    // UV 좌표는 원본 크기 기준
                    float baseUvLeft = uvMin.x + vStops[x] * uvWidth;
                    float baseUvRight = uvMin.x + vStops[x + 1] * uvWidth;
                    float baseUvBottom = uvMin.y + hStops[y] * uvHeight;
                    float baseUvTop = uvMin.y + hStops[y + 1] * uvHeight;
                    float baseUvColWidth = baseUvRight - baseUvLeft;
                    float baseUvRowHeight = baseUvTop - baseUvBottom;

                    // 확장 영역은 원본 유지 + 양쪽 가장자리 1픽셀을 타일링 (9-Slice Tiled 방식)
                    if (isFlexibleCol || isFlexibleRow)
                    {
                        // 텍스처 1픽셀 크기 계산
                        float texelWidth = uvWidth / spriteW;
                        float texelHeight = uvHeight / spriteH;

                        // 확장량 계산 (양쪽으로 늘어나는 크기)
                        float expandColSize = isFlexibleCol ? (colWidth - srcColWidth) * 0.5f : 0f;
                        float expandRowSize = isFlexibleRow ? (rowHeight - srcRowHeight) * 0.5f : 0f;

                        // 가로 방향: 왼쪽 1픽셀, 원본, 오른쪽 1픽셀
                        float leftEdgeUvX = baseUvLeft;
                        float rightEdgeUvX = baseUvRight - texelWidth;

                        // 세로 방향: 아래 1픽셀, 원본, 위 1픽셀
                        float bottomEdgeUvY = baseUvBottom;
                        float topEdgeUvY = baseUvTop - texelHeight;

                        // 3x3 그리드로 처리 (왼쪽/중앙/오른쪽 x 아래/중앙/위)
                        for (int sectionY = 0; sectionY < 3; sectionY++)
                        {
                            for (int sectionX = 0; sectionX < 3; sectionX++)
                            {
                                // 고정 영역인 경우 중앙만 그림
                                if (!isFlexibleCol && sectionX != 1) continue;
                                if (!isFlexibleRow && sectionY != 1) continue;

                                // Position 계산
                                float sectionPosX, sectionWidth;
                                if (!isFlexibleCol)
                                {
                                    sectionPosX = currentPos.x;
                                    sectionWidth = colWidth;
                                }
                                else
                                {
                                    if (sectionX == 0) // 왼쪽 확장 영역
                                    {
                                        sectionPosX = currentPos.x;
                                        sectionWidth = expandColSize;
                                    }
                                    else if (sectionX == 1) // 중앙 원본
                                    {
                                        sectionPosX = currentPos.x + expandColSize;
                                        sectionWidth = srcColWidth;
                                    }
                                    else // 오른쪽 확장 영역
                                    {
                                        sectionPosX = currentPos.x + expandColSize + srcColWidth;
                                        sectionWidth = expandColSize;
                                    }
                                }

                                float sectionPosY, sectionHeight;
                                if (!isFlexibleRow)
                                {
                                    sectionPosY = currentPos.y;
                                    sectionHeight = rowHeight;
                                }
                                else
                                {
                                    if (sectionY == 0) // 아래 확장 영역
                                    {
                                        sectionPosY = currentPos.y;
                                        sectionHeight = expandRowSize;
                                    }
                                    else if (sectionY == 1) // 중앙 원본
                                    {
                                        sectionPosY = currentPos.y + expandRowSize;
                                        sectionHeight = srcRowHeight;
                                    }
                                    else // 위 확장 영역
                                    {
                                        sectionPosY = currentPos.y + expandRowSize + srcRowHeight;
                                        sectionHeight = expandRowSize;
                                    }
                                }

                                // UV 계산
                                float uvX, uvW;
                                bool tilableX = isFlexibleCol && sectionX != 1;
                                if (!isFlexibleCol || sectionX == 1) // 원본
                                {
                                    uvX = baseUvLeft;
                                    uvW = baseUvColWidth;
                                }
                                else if (sectionX == 0) // 왼쪽 1픽셀
                                {
                                    uvX = leftEdgeUvX;
                                    uvW = texelWidth;
                                }
                                else // 오른쪽 1픽셀
                                {
                                    uvX = rightEdgeUvX;
                                    uvW = texelWidth;
                                }

                                float uvY, uvH;
                                bool tilableY = isFlexibleRow && sectionY != 1;
                                if (!isFlexibleRow || sectionY == 1) // 원본
                                {
                                    uvY = baseUvBottom;
                                    uvH = baseUvRowHeight;
                                }
                                else if (sectionY == 0) // 아래 1픽셀
                                {
                                    uvY = bottomEdgeUvY;
                                    uvH = texelHeight;
                                }
                                else // 위 1픽셀
                                {
                                    uvY = topEdgeUvY;
                                    uvH = texelHeight;
                                }

                                // 타일링 (확장 영역만) - 최적화된 버전
                                if (tilableX || tilableY)
                                {
                                    // 타일 크기 계산 (최대 MAX_TILE_SIZE 픽셀)
                                    float tileW = tilableX ? Mathf.Min(MAX_TILE_SIZE, sectionWidth) : sectionWidth;
                                    float tileH = tilableY ? Mathf.Min(MAX_TILE_SIZE, sectionHeight) : sectionHeight;

                                    // 타일 개수 계산
                                    int tilesX = tilableX ? Mathf.CeilToInt(sectionWidth / tileW) : 1;
                                    int tilesY = tilableY ? Mathf.CeilToInt(sectionHeight / tileH) : 1;

                                    // 계산 기반 타일링 (while 루프 대신)
                                    for (int ty = 0; ty < tilesY; ty++)
                                    {
                                        float currentTileY = sectionPosY + ty * tileH;
                                        float actualTileH = Mathf.Min(tileH, sectionPosY + sectionHeight - currentTileY);

                                        for (int tx = 0; tx < tilesX; tx++)
                                        {
                                            float currentTileX = sectionPosX + tx * tileW;
                                            float actualTileW = Mathf.Min(tileW, sectionPosX + sectionWidth - currentTileX);

                                            Rect cellRect = new Rect(currentTileX, currentTileY, actualTileW, actualTileH);
                                            Rect uvRect = new Rect(uvX, uvY, uvW, uvH);

                                            AddQuad(vh, cellRect, uvRect);
                                        }
                                    }
                                }
                                else // 원본 영역
                                {
                                    Rect cellRect = new Rect(sectionPosX, sectionPosY, sectionWidth, sectionHeight);
                                    Rect uvRect = new Rect(uvX, uvY, uvW, uvH);

                                    AddQuad(vh, cellRect, uvRect);
                                }
                            }
                        }
                    }
                    else
                    {
                        // 고정 영역은 그대로
                        Rect cellRect = new Rect(currentPos.x, currentPos.y, colWidth, rowHeight);
                        Rect uvRect = new Rect(baseUvLeft, baseUvBottom, baseUvColWidth, baseUvRowHeight);

                        AddQuad(vh, cellRect, uvRect);
                    }

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

        private void CalculateSizes(List<float> stops, float totalDstSize, float totalSrcSize, out float[] srcSizes, out float[] dstSizes, ref float[] cacheSrc, ref float[] cacheDst)
        {
            int count = stops.Count - 1;

            // 배열 캐싱: 크기가 맞으면 재사용, 아니면 새로 할당
            if (cacheSrc == null || cacheSrc.Length != count)
            {
                cacheSrc = new float[count];
            }
            if (cacheDst == null || cacheDst.Length != count)
            {
                cacheDst = new float[count];
            }

            srcSizes = cacheSrc;
            dstSizes = cacheDst;

            float totalFixedSrc = 0f;
            float totalFlexSrc = 0f;

            // 각 영역의 원본 크기 계산 및 고정/확장 영역 분류
            for (int i = 0; i < count; i++)
            {
                float size = (stops[i + 1] - stops[i]) * totalSrcSize;
                srcSizes[i] = size;
                if (i % 2 == 0) totalFixedSrc += size;  // 짝수 인덱스: 고정 영역 (0, 2, 4, ...)
                else totalFlexSrc += size;               // 홀수 인덱스: 확장 영역 (1, 3, 5, ...)
            }

            // 고정 영역은 원본 크기 유지, 확장 영역만 조정
            float availableFlexSpace = totalDstSize - totalFixedSrc;
            
            // 확장 영역이 없으면 모든 영역을 비율적으로 축소
            if (totalFlexSrc <= 0)
            {
                float scale = totalDstSize / totalSrcSize;
                for (int i = 0; i < count; i++)
                {
                    dstSizes[i] = srcSizes[i] * scale;
                }
                return;
            }

            // 목표 크기가 고정 영역보다 작으면 모든 영역을 비율적으로 축소
            if (totalDstSize < totalFixedSrc)
            {
                float scale = totalDstSize / totalSrcSize;
                for (int i = 0; i < count; i++)
                {
                    dstSizes[i] = srcSizes[i] * scale;
                }
                return;
            }

            // 고정 영역은 원본 크기 유지, 확장 영역은 비율에 따라 분배
            for (int i = 0; i < count; i++)
            {
                if (i % 2 == 0)
                {
                    // 짝수 인덱스: 고정 영역 (원본 크기 유지)
                    dstSizes[i] = srcSizes[i];
                }
                else
                {
                    // 홀수 인덱스: 확장 영역 (비율에 따라 분배)
                    float ratio = srcSizes[i] / totalFlexSrc;
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
}