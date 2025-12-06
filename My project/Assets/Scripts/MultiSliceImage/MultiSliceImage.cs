using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace CAT.UI
{
    public enum ImageType
    {
        Sliced,   // 가장자리 1픽셀을 타일링하여 확장 영역을 채움 (현재 기본 동작)
        Tiled  // 조건부 처리 (목표 크기 >= 원본 크기: 전체 이미지를 타일링, 목표 크기 < 원본 크기: 슬라이싱 (Stretch))
    }

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
    /// - Image Type: Sliced (가장자리 픽셀 타일링) / Tiled (조건부 타일링)
    ///
    /// 성능 최적화:
    /// - 배열 캐싱: 매 프레임 배열 할당 대신 캐시 재사용 (GC 압박 감소)
    /// - 타일링 최적화: 1픽셀 단위 대신 최대 32픽셀 단위로 타일링 (버텍스 수 대폭 감소)
    /// - while 루프 제거: 계산 기반 for 루프로 대체 (CPU 부하 감소)
    /// - List 캐싱: stops 리스트 재사용 (GC 할당 최소화)
    /// - UV 데이터 캐싱: DataUtility.GetOuterUV() 호출 최소화, 텍셀 크기 역수 캐싱
    /// - 구조체 재사용: Vector2, Vector3, Rect 할당 최소화 (tempPos, tempRect 재사용)
    /// - 나눗셈 최적화: 곱셈으로 변환 (역수 캐싱)
    /// - 메서드 분리: OnPopulateMesh 분리로 가독성 및 유지보수성 향상
    ///
    /// 모바일 환경에서 안정적인 성능 제공
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class MultiSliceImage : MaskableGraphic
    {
        private const int MAX_CUTS = 4;                 // 최대 컷 수 제한
        private const int MAX_TILE_SIZE = 32;           // 타일 최대 크기 (픽셀) - 성능 최적화: 1픽셀보다 큰 타일로 버텍스 수 감소
        private const int FIXED_REGION_MODULO = 0;      // 짝수 인덱스 = 고정 영역
        private const int FLEXIBLE_REGION_MODULO = 1;   // 홀수 인덱스 = 확장 영역
        private const int GRID_SECTIONS = 3;            // 3x3 그리드
        private const int GRID_CENTER = 1;              // 그리드 중앙 인덱스

        [SerializeField] private Sprite m_Sprite;
        [SerializeField] private bool m_PreserveAspect = false;
        [SerializeField] private ImageType m_ImageType = ImageType.Sliced;

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

        // 성능 최적화: UV 데이터 캐싱
        private Vector4 cachedOuterUV;
        private float cachedUvWidth;
        private float cachedUvHeight;
        private Vector2 cachedUvMin;
        private float cachedTexelWidth;
        private float cachedTexelHeight;
        private float cachedSpriteW;
        private float cachedSpriteH;
        private bool uvDataDirty = true;

        // 성능 최적화: 구조체 재사용 (GC 할당 최소화)
        private Vector2 tempPos = Vector2.zero;
        private Rect tempRect = Rect.zero;

        public Sprite sprite
        {
            get { return m_Sprite; }
            set
            {
                if (m_Sprite != value)
                {
                    m_Sprite = value;
                    stopsDirty = true;
                    uvDataDirty = true;
                    cachedSprite = null;
                    SetVerticesDirty();
                    SetMaterialDirty();
                }
            }
        }

        public override Texture mainTexture => m_Sprite == null ? s_WhiteTexture : m_Sprite.texture;

        // 종횡비 유지 여부
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

        // 이미지 렌더링 타입
        public ImageType imageType
        {
            get { return m_ImageType; }
            set
            {
                if (m_ImageType != value)
                {
                    m_ImageType = value;
                    SetVerticesDirty();
                }
            }
        }

        // 스프라이트의 원본 크기를 RectTransform에 적용합니다.
        public override void SetNativeSize()
        {
            if (m_Sprite == null) return;
            if (rectTransform == null) return;

            rectTransform.anchorMax = rectTransform.anchorMin;
            rectTransform.sizeDelta = m_Sprite.rect.size;
            SetVerticesDirty();
        }

        // 에디터에서 값이 변경될 때 컷 수 제한 적용
        protected override void OnValidate()
        {
            base.OnValidate();

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

        // 렌더링 데이터를 준비하고 캐싱합니다
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (m_Sprite == null) return;

            Rect rect = PrepareRenderRect();
            RenderData renderData = PrepareRenderData(rect);
            
            RenderCells(vh, rect, renderData);
        }

        // 렌더링할 Rect를 준비합니다 (Preserve Aspect 처리 포함)
        private Rect PrepareRenderRect()
        {
            Rect rect = GetPixelAdjustedRect();

            if (m_PreserveAspect)
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

            return rect;
        }

        // 렌더링 데이터를 준비하고 캐싱합니다
        private RenderData PrepareRenderData(Rect rect)
        {
            // 컷 리스트 변경 감지
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

            // UV 데이터 캐싱
            if (uvDataDirty || needsUpdate)
            {
                cachedOuterUV = UnityEngine.Sprites.DataUtility.GetOuterUV(m_Sprite);
                cachedUvWidth = cachedOuterUV.z - cachedOuterUV.x;
                cachedUvHeight = cachedOuterUV.w - cachedOuterUV.y;
                cachedUvMin.x = cachedOuterUV.x;
                cachedUvMin.y = cachedOuterUV.y;
                
                cachedSpriteW = m_Sprite.rect.width;
                cachedSpriteH = m_Sprite.rect.height;
                
                // 나눗셈을 곱셈으로 최적화 (역수 캐싱)
                float invSpriteW = 1f / cachedSpriteW;
                float invSpriteH = 1f / cachedSpriteH;
                cachedTexelWidth = cachedUvWidth * invSpriteW;
                cachedTexelHeight = cachedUvHeight * invSpriteH;
                
                uvDataDirty = false;
            }

            // 캐시된 stops 사용 (변경 시에만 재계산)
            List<float> vStops = GetSortedStops(verticalCuts, cachedVStops, needsUpdate);
            List<float> hStops = GetSortedStops(horizontalCuts, cachedHStops, needsUpdate);

            float[] vSizesSrc, vSizesDst;
            float[] hSizesSrc, hSizesDst;

            bool useTiled = (m_ImageType == ImageType.Tiled);
            CalculateSizes(vStops, rect.width, cachedSpriteW, out vSizesSrc, out vSizesDst, ref cachedVSizesSrc, ref cachedVSizesDst, useTiled);
            CalculateSizes(hStops, rect.height, cachedSpriteH, out hSizesSrc, out hSizesDst, ref cachedHSizesSrc, ref cachedHSizesDst, useTiled);

            return new RenderData
            {
                vStops = vStops,
                hStops = hStops,
                vSizesSrc = vSizesSrc,
                vSizesDst = vSizesDst,
                hSizesSrc = hSizesSrc,
                hSizesDst = hSizesDst
            };
        }

        // 렌더링 데이터 구조체
        private struct RenderData
        {
            public List<float> vStops;
            public List<float> hStops;
            public float[] vSizesSrc;
            public float[] vSizesDst;
            public float[] hSizesSrc;
            public float[] hSizesDst;
        }

        // 모든 셀을 렌더링합니다
        private void RenderCells(VertexHelper vh, Rect rect, RenderData data)
        {
            tempPos.x = rect.x;
            tempPos.y = rect.y;

            for (int y = 0; y < data.hSizesDst.Length; y++)
            {
                float rowHeight = data.hSizesDst[y];
                float srcRowHeight = data.hSizesSrc[y];
                bool isFlexibleRow = (y % 2 == FLEXIBLE_REGION_MODULO);

                tempPos.x = rect.x;

                for (int x = 0; x < data.vSizesDst.Length; x++)
                {
                    float colWidth = data.vSizesDst[x];
                    float srcColWidth = data.vSizesSrc[x];
                    bool isFlexibleCol = (x % 2 == FLEXIBLE_REGION_MODULO);

                    // UV 좌표 계산
                    float baseUvLeft = cachedUvMin.x + data.vStops[x] * cachedUvWidth;
                    float baseUvRight = cachedUvMin.x + data.vStops[x + 1] * cachedUvWidth;
                    float baseUvBottom = cachedUvMin.y + data.hStops[y] * cachedUvHeight;
                    float baseUvTop = cachedUvMin.y + data.hStops[y + 1] * cachedUvHeight;
                    float baseUvColWidth = baseUvRight - baseUvLeft;
                    float baseUvRowHeight = baseUvTop - baseUvBottom;

                    // 셀 렌더링
                    if (isFlexibleCol || isFlexibleRow)
                    {
                        RenderFlexibleCell(vh, tempPos, colWidth, rowHeight, srcColWidth, srcRowHeight,
                                          baseUvLeft, baseUvRight, baseUvBottom, baseUvTop,
                                          isFlexibleCol, isFlexibleRow);
                    }
                    else
                    {
                        // 고정 영역은 그대로
                        tempRect.x = tempPos.x;
                        tempRect.y = tempPos.y;
                        tempRect.width = colWidth;
                        tempRect.height = rowHeight;
                        Rect uvRect = new Rect(baseUvLeft, baseUvBottom, baseUvColWidth, baseUvRowHeight);
                        AddQuad(vh, tempRect, uvRect);
                    }

                    tempPos.x += colWidth;
                }
                tempPos.y += rowHeight;
            }
        }

        // 확장 가능한 셀을 렌더링합니다
        private void RenderFlexibleCell(VertexHelper vh, Vector2 pos, float colWidth, float rowHeight,
                                       float srcColWidth, float srcRowHeight,
                                       float baseUvLeft, float baseUvRight, float baseUvBottom, float baseUvTop,
                                       bool isFlexibleCol, bool isFlexibleRow)
        {
            if (m_ImageType == ImageType.Tiled)
            {
                // Tiled 모드: 타일링만 사용 (stretch 없음)
                bool shouldTileX = false;
                bool shouldTileY = false;

                if (isFlexibleCol && srcColWidth > 0 && colWidth >= srcColWidth)
                {
                    shouldTileX = true;
                }

                if (isFlexibleRow && srcRowHeight > 0 && rowHeight >= srcRowHeight)
                {
                    shouldTileY = true;
                }

                if (shouldTileX || shouldTileY)
                {
                    // 타일링: 모든 공간을 타일로 채움 (마지막 타일은 필요한 만큼 늘림)
                    RenderTiled(vh, pos, colWidth, rowHeight, srcColWidth, srcRowHeight,
                               baseUvLeft, baseUvRight, baseUvBottom, baseUvTop,
                               shouldTileX, shouldTileY);
                }
                else
                {
                    // Stretch: 원본 크기보다 작으면 stretch 사용
                    RenderStretched(vh, pos, colWidth, rowHeight,
                                   baseUvLeft, baseUvRight, baseUvBottom, baseUvTop);
                }
            }
            else
            {
                // Sliced 모드: 가장자리 1픽셀 타일링
                RenderSlicedCell(vh, pos, colWidth, rowHeight, srcColWidth, srcRowHeight,
                               baseUvLeft, baseUvRight, baseUvBottom, baseUvTop,
                               isFlexibleCol, isFlexibleRow);
            }
        }

        // Sliced 모드 셀을 렌더링합니다 (3x3 그리드)
        private void RenderSlicedCell(VertexHelper vh, Vector2 currentPos, float colWidth, float rowHeight,
                                     float srcColWidth, float srcRowHeight,
                                     float baseUvLeft, float baseUvRight, float baseUvBottom, float baseUvTop,
                                     bool isFlexibleCol, bool isFlexibleRow)
        {
            // 확장량 계산 (양쪽으로 늘어나는 크기)
            float expandColSize = isFlexibleCol ? (colWidth - srcColWidth) * 0.5f : 0f;
            float expandRowSize = isFlexibleRow ? (rowHeight - srcRowHeight) * 0.5f : 0f;

            // 가로 방향: 왼쪽 1픽셀, 원본, 오른쪽 1픽셀
            float leftEdgeUvX = baseUvLeft;
            float rightEdgeUvX = baseUvRight - cachedTexelWidth;

            // 세로 방향: 아래 1픽셀, 원본, 위 1픽셀
            float bottomEdgeUvY = baseUvBottom;
            float topEdgeUvY = baseUvTop - cachedTexelHeight;

            float baseUvColWidth = baseUvRight - baseUvLeft;
            float baseUvRowHeight = baseUvTop - baseUvBottom;

            // 3x3 그리드로 처리 (왼쪽/중앙/오른쪽 x 아래/중앙/위)
            for (int sectionY = 0; sectionY < GRID_SECTIONS; sectionY++)
            {
                for (int sectionX = 0; sectionX < GRID_SECTIONS; sectionX++)
                {
                    // 고정 영역인 경우 중앙만 그림
                    if (!isFlexibleCol && sectionX != GRID_CENTER) continue;
                    if (!isFlexibleRow && sectionY != GRID_CENTER) continue;

                    // Position 계산
                    float sectionPosX, sectionWidth;
                    CalculateSectionPositionX(isFlexibleCol, sectionX, currentPos.x, expandColSize, srcColWidth, colWidth, out sectionPosX, out sectionWidth);

                    float sectionPosY, sectionHeight;
                    CalculateSectionPositionY(isFlexibleRow, sectionY, currentPos.y, expandRowSize, srcRowHeight, rowHeight, out sectionPosY, out sectionHeight);

                    // UV 계산
                    float uvX, uvW;
                    bool tilableX = isFlexibleCol && sectionX != GRID_CENTER;
                    CalculateSectionUVX(isFlexibleCol, sectionX, baseUvLeft, baseUvColWidth, leftEdgeUvX, rightEdgeUvX, out uvX, out uvW);

                    float uvY, uvH;
                    bool tilableY = isFlexibleRow && sectionY != GRID_CENTER;
                    CalculateSectionUVY(isFlexibleRow, sectionY, baseUvBottom, baseUvRowHeight, bottomEdgeUvY, topEdgeUvY, out uvY, out uvH);

                    // 타일링 또는 원본 렌더링
                    if (tilableX || tilableY)
                    {
                        RenderTiledSection(vh, sectionPosX, sectionPosY, sectionWidth, sectionHeight, uvX, uvY, uvW, uvH, tilableX, tilableY);
                    }
                    else
                    {
                        tempRect.x = sectionPosX;
                        tempRect.y = sectionPosY;
                        tempRect.width = sectionWidth;
                        tempRect.height = sectionHeight;
                        Rect uvRect = new Rect(uvX, uvY, uvW, uvH);
                        AddQuad(vh, tempRect, uvRect);
                    }
                }
            }
        }

        // 섹션의 X 위치와 너비를 계산합니다
        private void CalculateSectionPositionX(bool isFlexibleCol, int sectionX, float currentPosX, float expandColSize, float srcColWidth, float colWidth, out float sectionPosX, out float sectionWidth)
        {
            if (!isFlexibleCol)
            {
                sectionPosX = currentPosX;
                sectionWidth = colWidth;
            }
            else
            {
                if (sectionX == 0) // 왼쪽 확장 영역
                {
                    sectionPosX = currentPosX;
                    sectionWidth = expandColSize;
                }
                else if (sectionX == GRID_CENTER) // 중앙 원본
                {
                    sectionPosX = currentPosX + expandColSize;
                    sectionWidth = srcColWidth;
                }
                else // 오른쪽 확장 영역
                {
                    sectionPosX = currentPosX + expandColSize + srcColWidth;
                    sectionWidth = expandColSize;
                }
            }
        }

        // 섹션의 Y 위치와 높이를 계산합니다
        private void CalculateSectionPositionY(bool isFlexibleRow, int sectionY, float currentPosY, float expandRowSize, float srcRowHeight, float rowHeight, out float sectionPosY, out float sectionHeight)
        {
            if (!isFlexibleRow)
            {
                sectionPosY = currentPosY;
                sectionHeight = rowHeight;
            }
            else
            {
                if (sectionY == 0) // 아래 확장 영역
                {
                    sectionPosY = currentPosY;
                    sectionHeight = expandRowSize;
                }
                else if (sectionY == GRID_CENTER) // 중앙 원본
                {
                    sectionPosY = currentPosY + expandRowSize;
                    sectionHeight = srcRowHeight;
                }
                else // 위 확장 영역
                {
                    sectionPosY = currentPosY + expandRowSize + srcRowHeight;
                    sectionHeight = expandRowSize;
                }
            }
        }

        // 섹션의 UV X 좌표를 계산합니다
        private void CalculateSectionUVX(bool isFlexibleCol, int sectionX, float baseUvLeft, float baseUvColWidth, float leftEdgeUvX, float rightEdgeUvX, out float uvX, out float uvW)
        {
            if (!isFlexibleCol || sectionX == GRID_CENTER) // 원본
            {
                uvX = baseUvLeft;
                uvW = baseUvColWidth;
            }
            else if (sectionX == 0) // 왼쪽 1픽셀
            {
                uvX = leftEdgeUvX;
                uvW = cachedTexelWidth;
            }
            else // 오른쪽 1픽셀
            {
                uvX = rightEdgeUvX;
                uvW = cachedTexelWidth;
            }
        }

        // 섹션의 UV Y 좌표를 계산합니다
        private void CalculateSectionUVY(bool isFlexibleRow, int sectionY, float baseUvBottom, float baseUvRowHeight, float bottomEdgeUvY, float topEdgeUvY, out float uvY, out float uvH)
        {
            if (!isFlexibleRow || sectionY == GRID_CENTER) // 원본
            {
                uvY = baseUvBottom;
                uvH = baseUvRowHeight;
            }
            else if (sectionY == 0) // 아래 1픽셀
            {
                uvY = bottomEdgeUvY;
                uvH = cachedTexelHeight;
            }
            else // 위 1픽셀
            {
                uvY = topEdgeUvY;
                uvH = cachedTexelHeight;
            }
        }

        // 타일링 섹션을 렌더링합니다
        private void RenderTiledSection(VertexHelper vh, float sectionPosX, float sectionPosY, float sectionWidth, float sectionHeight,
                                        float uvX, float uvY, float uvW, float uvH, bool tilableX, bool tilableY)
        {
            // 타일 크기 계산 (최대 MAX_TILE_SIZE 픽셀)
            float tileW = tilableX ? Mathf.Min(MAX_TILE_SIZE, sectionWidth) : sectionWidth;
            float tileH = tilableY ? Mathf.Min(MAX_TILE_SIZE, sectionHeight) : sectionHeight;

            // 타일 개수 계산
            int tilesX = tilableX ? Mathf.CeilToInt(sectionWidth / tileW) : 1;
            int tilesY = tilableY ? Mathf.CeilToInt(sectionHeight / tileH) : 1;

            // 계산 기반 타일링
            for (int ty = 0; ty < tilesY; ty++)
            {
                float currentTileY = sectionPosY + ty * tileH;
                float actualTileH = Mathf.Min(tileH, sectionPosY + sectionHeight - currentTileY);

                for (int tx = 0; tx < tilesX; tx++)
                {
                    float currentTileX = sectionPosX + tx * tileW;
                    float actualTileW = Mathf.Min(tileW, sectionPosX + sectionWidth - currentTileX);

                    tempRect.x = currentTileX;
                    tempRect.y = currentTileY;
                    tempRect.width = actualTileW;
                    tempRect.height = actualTileH;
                    Rect uvRect = new Rect(uvX, uvY, uvW, uvH);
                    AddQuad(vh, tempRect, uvRect);
                }
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

        private void CalculateSizes(List<float> stops, float totalDstSize, float totalSrcSize, out float[] srcSizes, out float[] dstSizes, ref float[] cacheSrc, ref float[] cacheDst, bool useTiled = false)
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
                if (i % 2 == FIXED_REGION_MODULO)
                {
                    totalFixedSrc += size;  // 짝수 인덱스: 고정 영역 (0, 2, 4, ...)
                }
                else
                {
                    totalFlexSrc += size;   // 홀수 인덱스: 확장 영역 (1, 3, 5, ...)
                }
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
            if (useTiled)
            {
                // Tiled 모드: 조건부 타일링/슬라이싱
                // 주의: 타일링은 렌더링 단계에서 처리되므로, 여기서는 목표 크기를 그대로 사용
                // 렌더링 시 목표 크기 >= 원본 크기이면 타일링, 아니면 stretch
                for (int i = 0; i < count; i++)
                {
                    if (i % 2 == FIXED_REGION_MODULO)
                    {
                        // 짝수 인덱스: 고정 영역 (원본 크기 유지)
                        dstSizes[i] = srcSizes[i];
                    }
                    else
                    {
                        // 홀수 인덱스: 확장 영역 (비율에 따라 분배)
                        // 타일링 여부는 렌더링 단계에서 판단하므로, 여기서는 목표 크기를 그대로 사용
                        float ratio = srcSizes[i] / totalFlexSrc;
                        dstSizes[i] = availableFlexSpace * ratio;
                    }
                }
            }
            else
            {
                // Sliced 모드: 기존 로직 (확장 영역 비율 분배)
                for (int i = 0; i < count; i++)
                {
                    if (i % 2 == FIXED_REGION_MODULO)
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
        }

        // Tiled 모드: 확장 영역의 원본 크기만큼씩 반복하여 타일링 (타일링만 사용, stretch 없음)
        private void RenderTiled(
            VertexHelper vh,
            Vector2 startPos,
            float totalWidth,
            float totalHeight,
            float srcWidth,
            float srcHeight,
            float uvLeft, float uvRight,
            float uvBottom, float uvTop,
            bool tileX, bool tileY)
        {
            float uvWidth = uvRight - uvLeft;
            float uvHeight = uvTop - uvBottom;

            // 타일 크기 (픽셀 단위) - 확장 영역의 원본 크기 사용
            float baseTileW = tileX ? srcWidth : totalWidth;
            float baseTileH = tileY ? srcHeight : totalHeight;

            // 타일 개수 계산 (모든 공간을 채우기 위해 ceil 사용)
            int tilesX = tileX ? Mathf.CeilToInt(totalWidth / baseTileW) : 1;
            int tilesY = tileY ? Mathf.CeilToInt(totalHeight / baseTileH) : 1;

            // 역수 캐싱 (나눗셈 최적화)
            float invBaseTileW = 1f / baseTileW;
            float invBaseTileH = 1f / baseTileH;

            // 타일링 (마지막 타일은 필요한 만큼 늘려서 채움)
            for (int ty = 0; ty < tilesY; ty++)
            {
                float currentY = startPos.y + ty * baseTileH;
                float actualH = Mathf.Min(baseTileH, startPos.y + totalHeight - currentY);

                for (int tx = 0; tx < tilesX; tx++)
                {
                    float currentX = startPos.x + tx * baseTileW;
                    float actualW = Mathf.Min(baseTileW, startPos.x + totalWidth - currentX);

                    tempRect.x = currentX;
                    tempRect.y = currentY;
                    tempRect.width = actualW;
                    tempRect.height = actualH;

                    // UV는 실제 렌더링 크기에 맞춰 조정 (마지막 타일이 늘어날 수 있음)
                    // 나눗셈을 곱셈으로 최적화
                    float uvW = uvWidth * (actualW * invBaseTileW);
                    float uvH = uvHeight * (actualH * invBaseTileH);
                    Rect uvRect = new Rect(uvLeft, uvBottom, uvW, uvH);

                    AddQuad(vh, tempRect, uvRect);
                }
            }
        }

        // Stretched 모드: 단순 stretch 렌더링
        private void RenderStretched(
            VertexHelper vh,
            Vector2 pos,
            float width,
            float height,
            float uvLeft, float uvRight,
            float uvBottom, float uvTop)
        {
            tempRect.x = pos.x;
            tempRect.y = pos.y;
            tempRect.width = width;
            tempRect.height = height;
            Rect uvRect = new Rect(uvLeft, uvBottom, uvRight - uvLeft, uvTop - uvBottom);
            AddQuad(vh, tempRect, uvRect);
        }

        // 쿼드를 추가합니다 (최적화: 구조체 재사용)
        private void AddQuad(VertexHelper vh, Rect posRect, Rect uvRect)
        {
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = color;

            // 좌하단
            v.position.x = posRect.x;
            v.position.y = posRect.y;
            v.position.z = 0f;
            v.uv0.x = uvRect.x;
            v.uv0.y = uvRect.y;
            vh.AddVert(v);

            // 좌상단
            v.position.x = posRect.x;
            v.position.y = posRect.y + posRect.height;
            v.uv0.x = uvRect.x;
            v.uv0.y = uvRect.y + uvRect.height;
            vh.AddVert(v);

            // 우상단
            v.position.x = posRect.x + posRect.width;
            v.position.y = posRect.y + posRect.height;
            v.uv0.x = uvRect.x + uvRect.width;
            v.uv0.y = uvRect.y + uvRect.height;
            vh.AddVert(v);

            // 우하단
            v.position.x = posRect.x + posRect.width;
            v.position.y = posRect.y;
            v.uv0.x = uvRect.x + uvRect.width;
            v.uv0.y = uvRect.y;
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