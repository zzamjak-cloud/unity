using UnityEngine;
using UnityEngine.UI;


namespace CAT.UI
{
    /// <summary>
    /// 대칭 이미지를 위한 미러 슬라이스 이미지 컴포넌트
    /// 절반 스프라이트만 사용하여 나머지 절반을 자동으로 반전시켜 생성합니다.
    /// 9-Slice border 정보를 활용하여 중앙 부분을 확장할 수 있습니다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class MirrorSliceImage : MaskableGraphic
    {
        public enum MirrorMode
        {
            None,       // 반전 없음
            Vertical,   // 좌측 -> 우측 복제 (수직 대칭)
            Horizontal  // 위 -> 아래 복제 (수평 대칭)
        }

        public enum GradientDirection
        {
            Horizontal, // 좌 → 우
            Vertical    // 아래 → 위
        }

        [SerializeField] private Sprite m_Sprite;
        [SerializeField] private MirrorMode mirrorMode = MirrorMode.Vertical;

        [SerializeField] private bool m_IsGradient = false;
        [SerializeField] private GradientDirection m_GradientDirection = GradientDirection.Horizontal;
        [SerializeField] private Color m_GradientColorA = Color.white;
        [SerializeField] private Color m_GradientColorB = Color.white;
        [SerializeField] [Range(0f, 1f)] private float m_GradientLerp = 0.5f;

        // 성능 최적화: 캐시된 값들
        private bool cacheDirty = true;
        private Rect cachedRect;
        private Sprite cachedSprite;
        private MirrorMode cachedMirrorMode;
        private Vector4 cachedOuterUV;
        private Vector4 cachedInnerUV;
        private Vector4 cachedBorder;
        private float cachedSpriteW;
        private float cachedSpriteH;
        private float cachedUvWidth;
        private float cachedUvHeight;
        private Vector2 cachedUvMin;
        private float cachedBorderLeftUV;
        private float cachedBorderRightUV;
        private float cachedBorderBottomUV;
        private float cachedBorderTopUV;

        public Sprite sprite
        {
            get { return m_Sprite; }
            set
            {
                if (m_Sprite != value)
                {
                    m_Sprite = value;
                    cacheDirty = true;
                    cachedSprite = null;
                    SetVerticesDirty();
                    SetMaterialDirty();
                }
            }
        }

        public override Texture mainTexture => m_Sprite == null ? s_WhiteTexture : m_Sprite.texture;

        public MirrorMode Mirror
        {
            get { return mirrorMode; }
            set
            {
                if (mirrorMode != value)
                {
                    mirrorMode = value;
                    cacheDirty = true;
                    SetVerticesDirty();
                }
            }
        }

        public bool isGradient
        {
            get { return m_IsGradient; }
            set
            {
                if (m_IsGradient != value)
                {
                    m_IsGradient = value;
                    SetVerticesDirty();
                }
            }
        }

        public GradientDirection gradientDirection
        {
            get { return m_GradientDirection; }
            set
            {
                if (m_GradientDirection != value)
                {
                    m_GradientDirection = value;
                    SetVerticesDirty();
                }
            }
        }

        public Color gradientColorA
        {
            get { return m_GradientColorA; }
            set
            {
                if (m_GradientColorA != value)
                {
                    m_GradientColorA = value;
                    SetVerticesDirty();
                }
            }
        }

        public Color gradientColorB
        {
            get { return m_GradientColorB; }
            set
            {
                if (m_GradientColorB != value)
                {
                    m_GradientColorB = value;
                    SetVerticesDirty();
                }
            }
        }

        public float gradientLerp
        {
            get { return m_GradientLerp; }
            set
            {
                float clampedValue = Mathf.Clamp01(value);
                if (!Mathf.Approximately(m_GradientLerp, clampedValue))
                {
                    m_GradientLerp = clampedValue;
                    SetVerticesDirty();
                }
            }
        }

        private new void OnValidate()
        {
            base.OnValidate();
            
            // Editor에서 값이 변경될 때 즉시 반영
            if (m_Sprite != null)
            {
                cacheDirty = true;
                cachedSprite = null;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// 스프라이트의 원본 크기를 반전 모드에 따라 RectTransform에 적용합니다.
        /// </summary>
        public override void SetNativeSize()
        {
            if (m_Sprite == null) return;

            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null) return;

            rectTransform.anchorMax = rectTransform.anchorMin;
            Vector2 spriteSize = m_Sprite.rect.size;
            
            if (mirrorMode == MirrorMode.None)
            {
                rectTransform.sizeDelta = spriteSize;
            }
            else if (mirrorMode == MirrorMode.Vertical)
            {
                rectTransform.sizeDelta = new Vector2(spriteSize.x * 2f, spriteSize.y);
            }
            else // Horizontal
            {
                rectTransform.sizeDelta = new Vector2(spriteSize.x, spriteSize.y * 2f);
            }
            
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (m_Sprite == null) return;

            Rect rect = GetPixelAdjustedRect();
            
            // 변경 감지: 스프라이트, 크기, 또는 모드가 변경되었는지 확인
            bool needsUpdate = cacheDirty || cachedSprite != m_Sprite || cachedRect != rect || cachedMirrorMode != mirrorMode;
            
            if (needsUpdate)
            {
                cachedSprite = m_Sprite;
                cachedRect = rect;
                cachedMirrorMode = mirrorMode;
                
                cachedOuterUV = UnityEngine.Sprites.DataUtility.GetOuterUV(m_Sprite);
                cachedInnerUV = UnityEngine.Sprites.DataUtility.GetInnerUV(m_Sprite);
                cachedBorder = m_Sprite.border;
                
                cachedSpriteW = m_Sprite.rect.width;
                cachedSpriteH = m_Sprite.rect.height;
                
                // UV 좌표 범위 (아틀라스 내에서의 범위)
                cachedUvWidth = cachedOuterUV.z - cachedOuterUV.x;
                cachedUvHeight = cachedOuterUV.w - cachedOuterUV.y;
                cachedUvMin = new Vector2(cachedOuterUV.x, cachedOuterUV.y);
                
                // Border를 픽셀 단위에서 UV 단위로 변환
                cachedBorderLeftUV = cachedBorder.x / cachedSpriteW * cachedUvWidth;
                cachedBorderRightUV = cachedBorder.z / cachedSpriteW * cachedUvWidth;
                cachedBorderBottomUV = cachedBorder.y / cachedSpriteH * cachedUvHeight;
                cachedBorderTopUV = cachedBorder.w / cachedSpriteH * cachedUvHeight;
                
                cacheDirty = false;
            }

            // 캐시된 값 사용
            Vector4 outerUV = cachedOuterUV;
            Vector4 innerUV = cachedInnerUV;
            Vector4 border = cachedBorder;
            float spriteW = cachedSpriteW;
            float spriteH = cachedSpriteH;
            float uvWidth = cachedUvWidth;
            float uvHeight = cachedUvHeight;
            Vector2 uvMin = cachedUvMin;
            float borderLeftUV = cachedBorderLeftUV;
            float borderRightUV = cachedBorderRightUV;
            float borderBottomUV = cachedBorderBottomUV;
            float borderTopUV = cachedBorderTopUV;

            if (mirrorMode == MirrorMode.None)
            {
                // 반전 없음: 전체를 하나의 9-Slice로
                float[] horizontalSizes = CalculateSliceSizes(
                    border.x,
                    spriteW - border.x - border.z,
                    border.z,
                    rect.width
                );

                float[] verticalSizes = CalculateSliceSizes(
                    border.y,
                    spriteH - border.y - border.w,
                    border.w,
                    rect.height
                );

                float currentY = rect.y;
                for (int row = 0; row < 3; row++)
                {
                    float rowHeight = verticalSizes[row];
                    if (rowHeight <= 0) continue;

                    float currentX = rect.x;
                    float vUVBottom, vUVTop;

                    if (row == 0) // Bottom
                    {
                        vUVBottom = uvMin.y;
                        vUVTop = uvMin.y + borderBottomUV;
                    }
                    else if (row == 1) // Center
                    {
                        vUVBottom = uvMin.y + borderBottomUV;
                        vUVTop = uvMin.y + uvHeight - borderTopUV;
                    }
                    else // Top
                    {
                        vUVBottom = uvMin.y + uvHeight - borderTopUV;
                        vUVTop = uvMin.y + uvHeight;
                    }

                    for (int col = 0; col < 3; col++)
                    {
                        float colWidth = horizontalSizes[col];
                        if (colWidth <= 0)
                        {
                            currentX += colWidth;
                            continue;
                        }

                        float uUVLeft, uUVRight;
                        if (col == 0) // Left border
                        {
                            uUVLeft = uvMin.x;
                            uUVRight = uvMin.x + borderLeftUV;
                        }
                        else if (col == 1) // Center
                        {
                            uUVLeft = uvMin.x + borderLeftUV;
                            uUVRight = uvMin.x + uvWidth - borderRightUV;
                        }
                        else // Right border
                        {
                            uUVLeft = uvMin.x + uvWidth - borderRightUV;
                            uUVRight = uvMin.x + uvWidth;
                        }

                        Rect posRect = new Rect(currentX, currentY, colWidth, rowHeight);
                        Rect uvRect = new Rect(uUVLeft, vUVBottom, uUVRight - uUVLeft, vUVTop - vUVBottom);
                        AddQuad(vh, posRect, uvRect);
                        currentX += colWidth;
                    }

                    currentY += rowHeight;
                }
            }
            else if (mirrorMode == MirrorMode.Vertical)
            {
                // 좌측 -> 우측 복제 (기존 구현)
                float halfSpriteWidth = spriteW;
                float totalWidth = rect.width;
                float totalHeight = rect.height;

                // 왼쪽 절반의 9-Slice 계산: Left, Center, Right
                float[] leftSizes = CalculateSliceSizes(
                    border.x, 
                    halfSpriteWidth - border.x - border.z, 
                    border.z,
                    totalWidth * 0.5f
                );

                // 오른쪽 절반의 9-Slice 계산: Left, Center, Right (왼쪽과 동일하지만 반전)
                float[] rightSizes = CalculateSliceSizes(
                    border.z,
                    halfSpriteWidth - border.x - border.z, 
                    border.x,
                    totalWidth * 0.5f
                );

                // 9-Slice 계산: Bottom, Center, Top
                float[] verticalSizes = CalculateSliceSizes(
                    border.y,
                    spriteH - border.y - border.w,
                    border.w,
                    totalHeight
                );

                float currentY = rect.y;

                // 세로 방향으로 3개 행 (Bottom, Center, Top)
                for (int row = 0; row < 3; row++)
                {
                    float rowHeight = verticalSizes[row];
                    if (rowHeight <= 0) continue;

                    float currentX = rect.x;
                    float vUVBottom, vUVTop;

                    // 세로 UV 계산
                    if (row == 0) // Bottom
                    {
                        vUVBottom = uvMin.y;
                        vUVTop = uvMin.y + borderBottomUV;
                    }
                    else if (row == 1) // Center
                    {
                        vUVBottom = uvMin.y + borderBottomUV;
                        vUVTop = uvMin.y + uvHeight - borderTopUV;
                    }
                    else // Top
                    {
                        vUVBottom = uvMin.y + uvHeight - borderTopUV;
                        vUVTop = uvMin.y + uvHeight;
                    }

                    // 왼쪽 절반: Left, Center, Right
                    for (int col = 0; col < 3; col++)
                    {
                        float colWidth = leftSizes[col];
                        if (colWidth <= 0)
                        {
                            currentX += colWidth;
                            continue;
                        }

                        float uUVLeft, uUVRight;

                        if (col == 0) // Left border
                        {
                            uUVLeft = uvMin.x;
                            uUVRight = uvMin.x + borderLeftUV;
                        }
                        else if (col == 1) // Center
                        {
                            uUVLeft = uvMin.x + borderLeftUV;
                            uUVRight = uvMin.x + uvWidth - borderRightUV;
                        }
                        else // Right border (중앙선)
                        {
                            uUVLeft = uvMin.x + uvWidth - borderRightUV;
                            uUVRight = uvMin.x + uvWidth;
                        }

                        Rect posRect = new Rect(currentX, currentY, colWidth, rowHeight);
                        Rect uvRect = new Rect(uUVLeft, vUVBottom, uUVRight - uUVLeft, vUVTop - vUVBottom);

                        AddQuad(vh, posRect, uvRect);
                        currentX += colWidth;
                    }

                    // 오른쪽 절반: Left, Center, Right (왼쪽 절반을 수평 반전)
                    for (int col = 0; col < 3; col++)
                    {
                        float colWidth = rightSizes[col];
                        if (colWidth <= 0)
                        {
                            currentX += colWidth;
                            continue;
                        }

                        float uUVLeft, uUVRight;

                        // 왼쪽 절반의 해당 부분의 UV를 가져옴 (역순으로)
                        if (col == 0) // Left border (왼쪽 절반의 Right border를 반전)
                        {
                            uUVLeft = uvMin.x + uvWidth - borderRightUV;
                            uUVRight = uvMin.x + uvWidth;
                        }
                        else if (col == 1) // Center (왼쪽 절반의 Center를 반전)
                        {
                            uUVLeft = uvMin.x + borderLeftUV;
                            uUVRight = uvMin.x + uvWidth - borderRightUV;
                        }
                        else // Right border (왼쪽 절반의 Left border를 반전)
                        {
                            uUVLeft = uvMin.x;
                            uUVRight = uvMin.x + borderLeftUV;
                        }

                        Rect posRect = new Rect(currentX, currentY, colWidth, rowHeight);
                        Rect uvRect = new Rect(uUVLeft, vUVBottom, uUVRight - uUVLeft, vUVTop - vUVBottom);

                        // 오른쪽 절반은 버텍스 순서를 바꿔서 반전 효과
                        AddQuadMirroredHorizontal(vh, posRect, uvRect);
                        currentX += colWidth;
                    }

                    currentY += rowHeight;
                }
            }
            else // Horizontal: 위 -> 아래 복제
            {
                // 위쪽 절반 스프라이트의 크기 (원본 스프라이트가 위쪽 절반)
                float halfSpriteHeight = spriteH;
                float totalWidth = rect.width;
                float totalHeight = rect.height;

                // 위쪽 절반의 9-Slice 계산: Bottom, Center, Top
                float[] topSizes = CalculateSliceSizes(
                    border.y,
                    halfSpriteHeight - border.y - border.w,
                    border.w,
                    totalHeight * 0.5f
                );

                // 아래쪽 절반의 9-Slice 계산: Bottom, Center, Top (위쪽과 동일한 크기)
                float[] bottomSizes = CalculateSliceSizes(
                    border.y,
                    halfSpriteHeight - border.y - border.w,
                    border.w,
                    totalHeight * 0.5f
                );

                // 9-Slice 계산: Left, Center, Right
                float[] horizontalSizes = CalculateSliceSizes(
                    border.x,
                    spriteW - border.x - border.z,
                    border.z,
                    totalWidth
                );

                // 위쪽 절반: Bottom, Center, Top (원본)
                // Unity UI 좌표계: Y는 아래에서 위로 증가, rect.y는 아래쪽
                // 위쪽 절반을 그리려면: rect.y + totalHeight * 0.5f부터 시작해서 위로
                float currentY = rect.y + totalHeight * 0.5f;

                for (int row = 0; row < 3; row++)
                {
                    float rowHeight = topSizes[row];
                    if (rowHeight <= 0) continue;

                    float currentX = rect.x;
                    float vUVBottom, vUVTop;

                    // 세로 UV 계산 (원본 그대로, 위쪽 절반)
                    if (row == 0) // Bottom border (위쪽 절반의 하단, 중앙선)
                    {
                        vUVBottom = uvMin.y;
                        vUVTop = uvMin.y + borderBottomUV;
                    }
                    else if (row == 1) // Center
                    {
                        vUVBottom = uvMin.y + borderBottomUV;
                        vUVTop = uvMin.y + uvHeight - borderTopUV;
                    }
                    else // Top border (위쪽 절반의 상단)
                    {
                        vUVBottom = uvMin.y + uvHeight - borderTopUV;
                        vUVTop = uvMin.y + uvHeight;
                    }

                    // 가로 방향으로 3개 열 (Left, Center, Right)
                    for (int col = 0; col < 3; col++)
                    {
                        float colWidth = horizontalSizes[col];
                        if (colWidth <= 0)
                        {
                            currentX += colWidth;
                            continue;
                        }

                        float uUVLeft, uUVRight;

                        if (col == 0) // Left border
                        {
                            uUVLeft = uvMin.x;
                            uUVRight = uvMin.x + borderLeftUV;
                        }
                        else if (col == 1) // Center
                        {
                            uUVLeft = uvMin.x + borderLeftUV;
                            uUVRight = uvMin.x + uvWidth - borderRightUV;
                        }
                        else // Right border
                        {
                            uUVLeft = uvMin.x + uvWidth - borderRightUV;
                            uUVRight = uvMin.x + uvWidth;
                        }

                        Rect posRect = new Rect(currentX, currentY, colWidth, rowHeight);
                        Rect uvRect = new Rect(uUVLeft, vUVBottom, uUVRight - uUVLeft, vUVTop - vUVBottom);

                        AddQuad(vh, posRect, uvRect);
                        currentX += colWidth;
                    }

                    currentY += rowHeight; // 위로 올라가면서 그리기
                }

                // 아래쪽 절반: Bottom, Center, Top (위쪽 절반을 수직 반전)
                currentY = rect.y; // 아래쪽 절반은 아래에서 시작
                for (int row = 0; row < 3; row++)
                {
                    float rowHeight = bottomSizes[row];
                    if (rowHeight <= 0) continue;

                    float currentX = rect.x;
                    float vUVBottom, vUVTop;

                    // 위쪽 절반의 해당 부분의 UV를 가져옴 (역순으로)
                    // row 0 (아래쪽 절반의 Bottom) -> 위쪽 절반의 Top border
                    // row 1 (아래쪽 절반의 Center) -> 위쪽 절반의 Center
                    // row 2 (아래쪽 절반의 Top) -> 위쪽 절반의 Bottom border
                    if (row == 0) // Bottom border (위쪽 절반의 Top border를 반전)
                    {
                        vUVBottom = uvMin.y + uvHeight - borderTopUV;
                        vUVTop = uvMin.y + uvHeight;
                    }
                    else if (row == 1) // Center (위쪽 절반의 Center를 반전)
                    {
                        vUVBottom = uvMin.y + borderBottomUV;
                        vUVTop = uvMin.y + uvHeight - borderTopUV;
                    }
                    else // Top border (위쪽 절반의 Bottom border를 반전)
                    {
                        vUVBottom = uvMin.y;
                        vUVTop = uvMin.y + borderBottomUV;
                    }

                    // 가로 방향으로 3개 열 (Left, Center, Right)
                    for (int col = 0; col < 3; col++)
                    {
                        float colWidth = horizontalSizes[col];
                        if (colWidth <= 0)
                        {
                            currentX += colWidth;
                            continue;
                        }

                        float uUVLeft, uUVRight;

                        if (col == 0) // Left border
                        {
                            uUVLeft = uvMin.x;
                            uUVRight = uvMin.x + borderLeftUV;
                        }
                        else if (col == 1) // Center
                        {
                            uUVLeft = uvMin.x + borderLeftUV;
                            uUVRight = uvMin.x + uvWidth - borderRightUV;
                        }
                        else // Right border
                        {
                            uUVLeft = uvMin.x + uvWidth - borderRightUV;
                            uUVRight = uvMin.x + uvWidth;
                        }

                        Rect posRect = new Rect(currentX, currentY, colWidth, rowHeight);
                        Rect uvRect = new Rect(uUVLeft, vUVBottom, uUVRight - uUVLeft, vUVTop - vUVBottom);

                        // 아래쪽 절반은 버텍스 순서를 바꿔서 반전 효과
                        AddQuadMirroredVertical(vh, posRect, uvRect);
                        currentX += colWidth;
                    }

                    currentY += rowHeight;
                }
            }
        }

        /// <summary>
        /// Vertex position에 따라 Gradient Color 계산
        /// Mirror Mode와는 독립적으로, GradientDirection에 따라서만 작동
        /// </summary>
        private Color CalculateGradientColor(Vector2 position, Rect bounds)
        {
            if (!m_IsGradient)
            {
                return color; // 기본 Tint Color
            }

            // Mirror Mode와 상관없이 GradientDirection만 확인
            float t;

            if (m_GradientDirection == GradientDirection.Horizontal)
            {
                // 좌 → 우: x 좌표 기반 (Mirror Mode와 무관)
                t = (position.x - bounds.xMin) / bounds.width;
            }
            else // GradientDirection.Vertical
            {
                // 아래 → 위: y 좌표 기반 (Mirror Mode와 무관)
                t = (position.y - bounds.yMin) / bounds.height;
            }

            // t 값 범위 보정
            t = Mathf.Clamp01(t);

            // Lerp 값 적용 (선형 offset 방식)
            // m_GradientLerp: 0 = Color A 영역 넓음, 0.5 = 균등, 1 = Color B 영역 넓음
            //
            // 원리: t 값을 선형적으로 재매핑하여 Color A 또는 Color B 영역을 확장
            // - Lerp < 0.5: t를 아래로 밀어서 Color A가 더 넓은 영역을 차지하게 함
            // - Lerp > 0.5: t를 위로 밀어서 Color B가 더 넓은 영역을 차지하게 함
            float offset = (m_GradientLerp - 0.5f) * 2f; // -1 ~ +1 범위
            t = Mathf.Clamp01(t + offset * 0.4f); // Lerp=0이면 t 감소(Color A 확장), Lerp=1이면 t 증가(Color B 확장)

            // Color A와 B 사이 보간
            Color gradientColor = Color.Lerp(m_GradientColorA, m_GradientColorB, t);

            // 기본 Tint Color와 Multiply
            return new Color(
                color.r * gradientColor.r,
                color.g * gradientColor.g,
                color.b * gradientColor.b,
                color.a * gradientColor.a
            );
        }

        /// <summary>
        /// 9-Slice 크기 계산 (Left, Center, Right)
        /// </summary>
        private float[] CalculateSliceSizes(float leftBorder, float centerSize, float rightBorder, float totalDstSize)
        {
            float[] sizes = new float[3];
            float totalSrcSize = leftBorder + centerSize + rightBorder;

            // 목표 크기가 원본보다 작으면 스케일 다운
            float scale = 1f;
            if (totalDstSize < totalSrcSize)
            {
                scale = totalDstSize / totalSrcSize;
                sizes[0] = leftBorder * scale;
                sizes[1] = centerSize * scale;
                sizes[2] = rightBorder * scale;
            }
            else
            {
                // Left와 Right border는 고정, Center만 확장
                sizes[0] = leftBorder;
                sizes[1] = totalDstSize - leftBorder - rightBorder;
                sizes[2] = rightBorder;
            }

            return sizes;
        }

        private void AddQuad(VertexHelper vh, Rect posRect, Rect uvRect)
        {
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;

            // 좌하단
            v.position = new Vector3(posRect.x, posRect.y);
            v.uv0 = new Vector2(uvRect.x, uvRect.y);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            // 좌상단
            v.position = new Vector3(posRect.x, posRect.y + posRect.height);
            v.uv0 = new Vector2(uvRect.x, uvRect.y + uvRect.height);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            // 우상단
            v.position = new Vector3(posRect.x + posRect.width, posRect.y + posRect.height);
            v.uv0 = new Vector2(uvRect.x + uvRect.width, uvRect.y + uvRect.height);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            // 우하단
            v.position = new Vector3(posRect.x + posRect.width, posRect.y);
            v.uv0 = new Vector2(uvRect.x + uvRect.width, uvRect.y);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        /// <summary>
        /// 수평 반전된 Quad 추가 (UV 좌표를 좌우로 바꿔서 반전 효과)
        /// </summary>
        private void AddQuadMirroredHorizontal(VertexHelper vh, Rect posRect, Rect uvRect)
        {
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;

            // 왼쪽 하단 (UV는 오른쪽 하단)
            v.position = new Vector3(posRect.x, posRect.y);
            v.uv0 = new Vector2(uvRect.x + uvRect.width, uvRect.y);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            // 왼쪽 상단 (UV는 오른쪽 상단)
            v.position = new Vector3(posRect.x, posRect.y + posRect.height);
            v.uv0 = new Vector2(uvRect.x + uvRect.width, uvRect.y + uvRect.height);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            // 오른쪽 상단 (UV는 왼쪽 상단)
            v.position = new Vector3(posRect.x + posRect.width, posRect.y + posRect.height);
            v.uv0 = new Vector2(uvRect.x, uvRect.y + uvRect.height);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            // 오른쪽 하단 (UV는 왼쪽 하단)
            v.position = new Vector3(posRect.x + posRect.width, posRect.y);
            v.uv0 = new Vector2(uvRect.x, uvRect.y);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        /// <summary>
        /// 수직 반전된 Quad 추가 (UV 좌표를 상하로 바꿔서 반전 효과)
        /// </summary>
        private void AddQuadMirroredVertical(VertexHelper vh, Rect posRect, Rect uvRect)
        {
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;

            // 왼쪽 하단 (UV는 왼쪽 상단)
            v.position = new Vector3(posRect.x, posRect.y);
            v.uv0 = new Vector2(uvRect.x, uvRect.y + uvRect.height);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            // 왼쪽 상단 (UV는 왼쪽 하단)
            v.position = new Vector3(posRect.x, posRect.y + posRect.height);
            v.uv0 = new Vector2(uvRect.x, uvRect.y);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            // 오른쪽 상단 (UV는 오른쪽 하단)
            v.position = new Vector3(posRect.x + posRect.width, posRect.y + posRect.height);
            v.uv0 = new Vector2(uvRect.x + uvRect.width, uvRect.y);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            // 오른쪽 하단 (UV는 오른쪽 상단)
            v.position = new Vector3(posRect.x + posRect.width, posRect.y);
            v.uv0 = new Vector2(uvRect.x + uvRect.width, uvRect.y + uvRect.height);
            v.color = CalculateGradientColor(v.position, cachedRect);
            vh.AddVert(v);

            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}

