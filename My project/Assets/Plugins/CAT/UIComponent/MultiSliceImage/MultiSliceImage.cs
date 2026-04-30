using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace CAT.UI
{
    public enum ImageType
    {
        /// <summary>Unity 9-slice와 동일: 타일링 없이 셀당 1쿼드 스트레치. 쿼드 수 = (1+vCuts)×(1+hCuts).</summary>
        Sliced = 0,
        /// <summary>조건부: 목표 크기 >= 원본이면 타일링, 아니면 스트레치.</summary>
        Tiled = 1,
        /// <summary>
        /// 한 축은 Tiled, 다른 축은 Sliced(스트레치)로 혼합.
        /// 축 조합은 <see cref="MixedAxisMode"/>로 지정합니다.
        /// </summary>
        Mixed = 4,
        /// <summary>
        /// Stepwise: 타일의 일부만 늘려 채우지 않고,
        /// 타일 1개 크기가 확보될 때만 타일을 추가로 렌더링합니다.
        /// </summary>
        TiledFilled = 2,
        /// <summary>
        /// Tiled + Filled Mask 조합 모드.
        /// 타일링은 일반 Tiled와 동일하게 처리하고, 최종 렌더만 Fill Rect로 클리핑합니다.
        /// </summary>
        TiledFilledMask = 3
    }

    /// <summary>
    /// <see cref="ImageType.Mixed"/>에서 가로(열·column) / 세로(행·row) 중 어느 축에 Tiled를 쓸지 지정합니다.
    /// </summary>
    public enum MixedAxisMode
    {
        /// <summary>가로(열)는 Tiled, 세로(행)는 Sliced(스트레치).</summary>
        HorizontalTiled_VerticalSliced,
        /// <summary>가로(열)는 Sliced(스트레치), 세로(행)는 Tiled.</summary>
        HorizontalSliced_VerticalTiled,
    }

    public enum FillOrigin
    {
        Left,
        Right,
        Bottom,
        Top
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
    /// - Image Type: Sliced / Tiled / Mixed (축별 Tiled+Sliced) / TiledFilled / TiledFilledMask
    ///
    /// 성능 최적화:
    /// - 배열 캐싱: 매 프레임 배열 할당 대신 캐시 재사용 (GC 압박 감소)
    /// - 타일링 최적화: 1픽셀 단위 대신 최대 32픽셀 단위로 타일링
    /// - List 캐싱: stops 리스트 재사용 (GC 할당 최소화)
    /// - UV 데이터 캐싱: DataUtility.GetOuterUV() 호출 최소화, 텍셀 크기 역수 캐싱
    /// - 구조체 재사용: Vector2, Vector3, Rect 할당 최소화 (tempPos, tempRect 재사용)
    /// - 나눗셈 최적화: 곱셈으로 변환 (역수 캐싱)
    ///
    /// 모바일 정점 폭발 방지 (필수):
    /// - Sliced: 타일링 없음. 쿼드 수 = (1+vCuts)×(1+hCuts). Unity 9-slice와 동일.
    /// - Tiled / Mixed: 확장 셀당 타일링 1번, 셀당 최대 256 쿼드. 섹션당 256, 전체 65000 미만 유지.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class MultiSliceImage : MaskableGraphic
    {
        private const int MAX_CUTS = 4;                 // 최대 컷 수 제한
        private const int MAX_TILE_SIZE = 32;           // 타일 최대 크기 (픽셀)
        private const int FIXED_REGION_MODULO = 0;      // 짝수 인덱스 = 고정 영역
        private const int FLEXIBLE_REGION_MODULO = 1;   // 홀수 인덱스 = 확장 영역
        private const int GRID_SECTIONS = 3;            // 3x3 그리드
        private const int GRID_CENTER = 1;              // 그리드 중앙 인덱스

        // 모바일 정점 폭발 방지: 타일링 철저 제한
        private const int MAX_MESH_VERTICES = 65000;
        /// <summary>섹션당 타일 쿼드 상한. 초과 시 해당 구간은 1쿼드 스트레치로만 그림.</summary>
        private const int MAX_QUADS_PER_SECTION = 256;
        /// <summary>Sliced 모드에서 확장 셀 1개당 쿼드 상한.</summary>
        private const int MAX_QUADS_PER_SLICED_CELL = 256;
        /// <summary>Sliced 모드 전체 타일 쿼드 상한. 이 수를 넘기면 나머지 섹션은 전부 1쿼드 스트레치. (Tiled가 수십 쿼드인데 Sliced만 수백~천 개 나오는 것 방지)</summary>
        private const int MAX_QUADS_TOTAL_SLICED = 128;
        /// <summary>전체 메시 쿼드 상한(여유 1).</summary>
        private const int MAX_QUADS_SAFE = (MAX_MESH_VERTICES / 4) - 1;

        [SerializeField] private Sprite m_Sprite;
        [SerializeField] private bool m_PreserveAspect = false;
        [SerializeField] private ImageType m_ImageType = ImageType.Sliced;
        [SerializeField] private MixedAxisMode m_MixedAxisMode = MixedAxisMode.HorizontalTiled_VerticalSliced;
        [SerializeField] private float m_PixelsPerUnitMultiplier = 1f;
        [SerializeField] private float m_FillAmount = 1f;
        [SerializeField] private FillOrigin m_FillOrigin = FillOrigin.Left;
        // 레거시 호환용: 기존 Inspector 체크박스 데이터 마이그레이션을 위해 유지합니다.
        [SerializeField] private bool m_UseFilledMask = false;

        /// <summary>
        /// TiledFilled에서 0~1 사이로 채움 진행값입니다.
        /// (Unity Image Filled의 fillAmount처럼 보이되, 현재 구현은 표시 영역을 줄이는 방식입니다.)
        /// </summary>
        public float fillAmount
        {
            get => m_FillAmount;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Abs(m_FillAmount - v) > 0.0001f)
                {
                    m_FillAmount = v;
                    SetVerticesDirty();
                }
            }
        }

        public FillOrigin fillOrigin
        {
            get => m_FillOrigin;
            set
            {
                if (m_FillOrigin != value)
                {
                    m_FillOrigin = value;
                    SetVerticesDirty();
                }
            }
        }

        /// <summary>
        /// Unity Image와 동일한 Pixels Per Unit Multiplier.
        /// 1보다 작으면 더 크게, 1보다 크면 더 작게 렌더링됩니다.
        /// </summary>
        public float pixelsPerUnitMultiplier
        {
            get => m_PixelsPerUnitMultiplier;
            set
            {
                float clamped = Mathf.Max(0.01f, value);
                if (Mathf.Abs(m_PixelsPerUnitMultiplier - clamped) > 0.0001f)
                {
                    m_PixelsPerUnitMultiplier = clamped;
                    stopsDirty = true;
                    uvDataDirty = true;
                    SetVerticesDirty();
                }
            }
        }

        /// <summary>
        /// Tiled에서 전체 타일링으로 메쉬를 만든 뒤, fill 영역으로만 쿼드를 클리핑해서 Filled처럼 보이게 합니다.
        /// (즉, rect 자체를 줄이지 않고 "마스킹"만 추가)
        /// </summary>
        public bool useFilledMask
        {
            get => m_UseFilledMask;
            set
            {
                if (m_UseFilledMask != value)
                {
                    m_UseFilledMask = value;
                    SetVerticesDirty();
                }
            }
        }

        // OnPopulateMesh 동안 채워지는 렌더 마스크 정보
        private bool _filledMaskActive;
        private bool _filledMaskStepwiseQuad;
        private Rect _filledMaskRect;

        // 0.0 ~ 1.0 정규화된 좌표값 (최대 4개)
        public List<float> verticalCuts = new List<float>();
        public List<float> horizontalCuts = new List<float>();

        // 성능 최적화: 캐시된 stops 리스트
        private List<float> cachedVStops = new List<float>();
        private List<float> cachedHStops = new List<float>();
        private bool stopsDirty = true;
        private Rect cachedRect;
        private Sprite cachedSprite;
        private Canvas cachedCanvas;
        private int cachedVCutsHash = 0;
        private int cachedHCutsHash = 0;
        // PixelsPerUnitMultiplier 변경 감지 (Inspector 편집은 setter를 거치지 않으므로 별도 캐싱)
        private float cachedPixelsPerUnitMultiplier = 1f;

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

        /// <summary>Sliced 모드에서 현재까지 사용한 타일 쿼드 수 (전체 상한 MAX_QUADS_TOTAL_SLICED 적용용).</summary>
        private int _slicedTiledQuadsUsed;

        /// <summary>마지막 OnPopulateMesh에서 생성된 정점 수. 에디터에서 Slice 타입 정점 수 확인용.</summary>
        private int _lastPopulateVertexCount;

        /// <summary>현재 메시의 정점 수 (마지막 빌드 기준). 에디터 전용.</summary>
        public int lastPopulateVertexCount => _lastPopulateVertexCount;

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

        /// <summary><see cref="ImageType.Mixed"/>일 때만 사용: 열/행 중 Tiled를 적용할 축.</summary>
        public MixedAxisMode mixedAxisMode
        {
            get => m_MixedAxisMode;
            set
            {
                if (m_MixedAxisMode != value)
                {
                    m_MixedAxisMode = value;
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
            float ppu = MirrorSliceImageHelper.GetMultipliedPixelsPerUnit(m_Sprite, canvas) * Mathf.Max(0.01f, m_PixelsPerUnitMultiplier);
            rectTransform.sizeDelta = m_Sprite.rect.size / ppu;
            SetVerticesDirty();
        }

        // 에디터에서 값이 변경될 때 컷 수 제한 적용
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (verticalCuts.Count > MAX_CUTS)
            {
                verticalCuts.RemoveRange(MAX_CUTS, verticalCuts.Count - MAX_CUTS);
            }
            if (horizontalCuts.Count > MAX_CUTS)
            {
                horizontalCuts.RemoveRange(MAX_CUTS, horizontalCuts.Count - MAX_CUTS);
            }

            // 레거시 데이터 호환:
            // 과거에는 Tiled + m_UseFilledMask 조합으로 "Tiled Filled Mask"를 표현했습니다.
            // 새 enum 모드로 자동 마이그레이션합니다.
            if (m_ImageType == ImageType.Tiled && m_UseFilledMask)
            {
                m_ImageType = ImageType.TiledFilledMask;
                m_UseFilledMask = false;
            }

            // 0 또는 음수가 되면 division-by-zero가 발생하므로 최소값 보정
            if (m_PixelsPerUnitMultiplier < 0.01f)
            {
                m_PixelsPerUnitMultiplier = 0.01f;
            }

            // 인스펙터에서 어떤 필드든 변경되면 항상 캐시 무효화 + 메시 재빌드.
            // (이전 구현: 변경 플래그가 false면 multiplier 변경 시 캐시가 갱신되지 않아 렌더링이 정지된 채로 유지되었음)
            stopsDirty = true;
            uvDataDirty = true;
            SetVerticesDirty();
        }
#endif

        // 렌더링 데이터를 준비하고 캐싱합니다
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (m_Sprite == null)
            {
                _lastPopulateVertexCount = 0;
                return;
            }

            _filledMaskActive = false;
            _filledMaskStepwiseQuad = false;
            _filledMaskRect = Rect.zero;

            Rect rect = PrepareRenderRect();
            if (m_ImageType == ImageType.TiledFilled || m_ImageType == ImageType.TiledFilledMask)
            {
                // Filled 계열은 "사이즈 재계산" 대신,
                // 전체 메쉬를 만든 뒤 Fill 영역만 클립합니다.
                _filledMaskRect = GetFilledMaskRect(rect, m_FillAmount, m_FillOrigin);
                _filledMaskActive = m_FillAmount < 0.99999f;
                _filledMaskStepwiseQuad = (m_ImageType == ImageType.TiledFilled);
            }
            else if (m_UseFilledMask && m_FillAmount < 0.99999f)
            {
                // "타일링으로 사이즈를 잡고" render 결과를 fill 영역으로만 클립합니다.
                _filledMaskActive = true;
                _filledMaskRect = GetFilledMaskRect(rect, m_FillAmount, m_FillOrigin);
            }
            RenderData renderData = PrepareRenderData(rect);

            RenderCells(vh, rect, renderData);

            _lastPopulateVertexCount = vh.currentVertCount;
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

        private Rect ApplyFillRect(Rect rect)
        {
            float fill = Mathf.Clamp01(m_FillAmount);
            if (fill >= 0.999f) return rect;

            switch (m_FillOrigin)
            {
                case FillOrigin.Left:
                    rect.width *= fill;
                    break;
                case FillOrigin.Right:
                    float oldW = rect.width;
                    rect.width = oldW * fill;
                    rect.x += oldW - rect.width;
                    break;
                case FillOrigin.Bottom:
                    rect.height *= fill;
                    break;
                case FillOrigin.Top:
                    float oldH = rect.height;
                    rect.height = oldH * fill;
                    rect.y += oldH - rect.height;
                    break;
            }

            // 안전: 음수 방지
            if (rect.width < 0f) rect.width = 0f;
            if (rect.height < 0f) rect.height = 0f;
            return rect;
        }

        private Rect GetFilledMaskRect(Rect rect, float fillAmount, FillOrigin origin)
        {
            float fill = Mathf.Clamp01(fillAmount);
            if (fill >= 0.999f) return rect;

            switch (origin)
            {
                case FillOrigin.Left:
                    rect.width = rect.width * fill;
                    return rect;
                case FillOrigin.Right:
                    rect.x += rect.width * (1f - fill);
                    rect.width = rect.width * fill;
                    return rect;
                case FillOrigin.Bottom:
                    rect.height = rect.height * fill;
                    return rect;
                case FillOrigin.Top:
                    rect.y += rect.height * (1f - fill);
                    rect.height = rect.height * fill;
                    return rect;
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
            
            // 변경 감지: 스프라이트, 크기, Canvas, 컷 리스트, 또는 PixelsPerUnitMultiplier 변경 확인
            Canvas currentCanvas = canvas;
            bool ppuMultiplierChanged = !Mathf.Approximately(cachedPixelsPerUnitMultiplier, m_PixelsPerUnitMultiplier);
            bool needsUpdate = stopsDirty || cachedSprite != m_Sprite || cachedRect != rect || cutsChanged || cachedCanvas != currentCanvas || ppuMultiplierChanged;

            if (needsUpdate)
            {
                cachedSprite = m_Sprite;
                cachedRect = rect;
                cachedCanvas = currentCanvas;
                cachedVCutsHash = currentVCutsHash;
                cachedHCutsHash = currentHCutsHash;
                cachedPixelsPerUnitMultiplier = m_PixelsPerUnitMultiplier;
                stopsDirty = false;
                // multiplier가 변경되면 cachedSpriteW/H도 다시 계산해야 하므로 UV 데이터도 갱신
                if (ppuMultiplierChanged)
                {
                    uvDataDirty = true;
                }
            }

            // UV 데이터 캐싱
            if (uvDataDirty || needsUpdate)
            {
                cachedOuterUV = UnityEngine.Sprites.DataUtility.GetOuterUV(m_Sprite);
                cachedUvWidth = cachedOuterUV.z - cachedOuterUV.x;
                cachedUvHeight = cachedOuterUV.w - cachedOuterUV.y;
                cachedUvMin.x = cachedOuterUV.x;
                cachedUvMin.y = cachedOuterUV.y;
                
                // Pixel Per Unit 적용: 스프라이트 크기를 UI 좌표 단위로 변환
                float ppu = MirrorSliceImageHelper.GetMultipliedPixelsPerUnit(m_Sprite, cachedCanvas) * Mathf.Max(0.01f, m_PixelsPerUnitMultiplier);
                cachedSpriteW = m_Sprite.rect.width / ppu;
                cachedSpriteH = m_Sprite.rect.height / ppu;

                // cachedTexelWidth/Height: 소스 1픽셀에 해당하는 UV 길이.
                // PixelsPerUnitMultiplier와 무관하게 원본 스프라이트 픽셀 기준으로 계산해야
                // Sliced 모드의 1픽셀 엣지 UV 샘플링이 정확합니다(서브픽셀 보간 방지).
                float spritePixelW = m_Sprite.rect.width;
                float spritePixelH = m_Sprite.rect.height;
                cachedTexelWidth = spritePixelW > 0f ? cachedUvWidth / spritePixelW : 0f;
                cachedTexelHeight = spritePixelH > 0f ? cachedUvHeight / spritePixelH : 0f;
                
                uvDataDirty = false;
            }

            // 캐시된 stops 사용 (변경 시에만 재계산)
            List<float> vStops = GetSortedStops(verticalCuts, cachedVStops, needsUpdate);
            List<float> hStops = GetSortedStops(horizontalCuts, cachedHStops, needsUpdate);

            float[] vSizesSrc, vSizesDst;
            float[] hSizesSrc, hSizesDst;

            bool useTiled = m_ImageType == ImageType.Tiled
                || m_ImageType == ImageType.Mixed
                || m_ImageType == ImageType.TiledFilled
                || m_ImageType == ImageType.TiledFilledMask;
            CalculateSizes(vStops, rect.width, cachedSpriteW, out vSizesSrc, out vSizesDst, ref cachedVSizesSrc, ref cachedVSizesDst, useTiled);
            CalculateSizes(hStops, rect.height, cachedSpriteH, out hSizesSrc, out hSizesDst, ref cachedHSizesSrc, ref cachedHSizesDst, useTiled);

            // float ratio·곱셈 누적 오차로 인접 셀 경계가 sub-pixel로 어긋나면 anti-aliased 렌더링에서
            // 1픽셀 구멍이 나타날 수 있습니다. dst와 src 사이즈를 동일한 픽셀 그리드에 스냅하고
            // 마지막 셀이 정확히 끝에 도달하도록 잔차를 흡수시켜 누적 오차를 제거합니다.
            // src도 함께 스냅하는 이유: tilesX = ceil(colWidth / baseTileW) 계산에서
            // dst만 정수이고 src가 float이면 비율이 정수 경계를 넘나들 때 타일 개수가 1↔2로 토글되어
            // 드래그 중 쿼드가 사라졌다 나타나는 깜빡임이 발생합니다. 둘 다 정수면 비율이 안정됩니다.
            // (Set Native Size에서는 dst == src가 되어 tilesX = 1로 고정 → 셀당 1쿼드 안정 유지)
            SnapDstSizesToPixels(0f, cachedSpriteW, vSizesSrc);
            SnapDstSizesToPixels(0f, cachedSpriteH, hSizesSrc);
            SnapDstSizesToPixels(rect.x, rect.width, vSizesDst, vSizesSrc);
            SnapDstSizesToPixels(rect.y, rect.height, hSizesDst, hSizesSrc);

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

        // dst 사이즈 배열의 경계를 정수 픽셀에 스냅합니다.
        // 누적 오차로 인한 sub-pixel 갭을 제거하고, 마지막 셀이 rect 끝에 정확히 일치하도록 보정합니다.
        // srcSizes가 제공되면 src가 0이 아닌 셀의 dst 최소값을 1px로 보장하여
        // 드래그 시 0px과 1px 사이로 깜빡이는 양자화 토글을 방지합니다.
        private static void SnapDstSizesToPixels(float startPos, float totalSize, float[] sizes, float[] srcSizes = null)
        {
            if (sizes == null) return;
            int n = sizes.Length;
            if (n == 0) return;
            if (n == 1)
            {
                sizes[0] = totalSize;
                return;
            }

            const float CONTENT_EPS = 0.001f;
            float prev = startPos;
            float endPos = startPos + totalSize;
            for (int i = 0; i < n - 1; i++)
            {
                float boundary = prev + sizes[i];
                // rect 시작점 기준 정수 픽셀 오프셋으로 스냅
                float snapped = Mathf.Round(boundary - startPos) + startPos;

                // src가 0이 아닌데 스냅 결과 0px 셀이 되는 경우 최소 1px 보장.
                // (드래그 중 src가 0.4 ↔ 0.6 변동 시 dst가 0 ↔ 1로 토글되는 깜빡임 방지)
                bool hasContent = (srcSizes != null && i < srcSizes.Length)
                    ? srcSizes[i] > CONTENT_EPS
                    : sizes[i] > CONTENT_EPS;
                if (hasContent && snapped <= prev + 0.0001f)
                {
                    snapped = prev + 1f;
                }

                // 단조 증가 보장 + 영역 초과 방지
                if (snapped < prev) snapped = prev;
                if (snapped > endPos) snapped = endPos;
                sizes[i] = snapped - prev;
                prev = snapped;
            }
            // 마지막 셀: 잔차를 모두 흡수해 sum == totalSize 보장
            sizes[n - 1] = endPos - prev;
            if (sizes[n - 1] < 0f) sizes[n - 1] = 0f;
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

                    // Sliced: Unity 9-slice와 동일하게 셀당 1쿼드만 (타일링 없음)
                    if (m_ImageType == ImageType.Sliced)
                    {
                        tempRect.x = tempPos.x;
                        tempRect.y = tempPos.y;
                        tempRect.width = colWidth;
                        tempRect.height = rowHeight;
                        Rect uvRect = new Rect(baseUvLeft, baseUvBottom, baseUvColWidth, baseUvRowHeight);
                        // 고정 메쉬 단위 표시: TiledFilled에서 부분 클립 시 전체 숨김 (all-or-nothing)
                        AddQuad(vh, tempRect, uvRect, true, true);
                    }
                    else if (m_ImageType == ImageType.Mixed)
                    {
                        if (isFlexibleCol || isFlexibleRow)
                        {
                            RenderMixedFlexibleCell(vh, tempPos, colWidth, rowHeight, srcColWidth, srcRowHeight,
                                baseUvLeft, baseUvRight, baseUvBottom, baseUvTop,
                                isFlexibleCol, isFlexibleRow);
                        }
                        else
                        {
                            tempRect.x = tempPos.x;
                            tempRect.y = tempPos.y;
                            tempRect.width = colWidth;
                            tempRect.height = rowHeight;
                            Rect uvRect = new Rect(baseUvLeft, baseUvBottom, baseUvColWidth, baseUvRowHeight);
                            // 고정 메쉬 단위 표시
                            AddQuad(vh, tempRect, uvRect, true, true);
                        }
                    }
                    else if (isFlexibleCol || isFlexibleRow)
                    {
                        RenderFlexibleCell(vh, tempPos, colWidth, rowHeight, srcColWidth, srcRowHeight,
                                          baseUvLeft, baseUvRight, baseUvBottom, baseUvTop,
                                          isFlexibleCol, isFlexibleRow);
                    }
                    else
                    {
                        // 고정 영역: 메쉬 단위로 표시. TiledFilled에서 마스크와 부분 교차 시
                        // 타일이 한번에 꺼지듯 고정 셀도 한번에 숨김 (all-or-nothing).
                        tempRect.x = tempPos.x;
                        tempRect.y = tempPos.y;
                        tempRect.width = colWidth;
                        tempRect.height = rowHeight;
                        Rect uvRect = new Rect(baseUvLeft, baseUvBottom, baseUvColWidth, baseUvRowHeight);
                        AddQuad(vh, tempRect, uvRect, true, true);
                    }

                    tempPos.x += colWidth;
                }
                tempPos.y += rowHeight;
            }
        }

        // Mixed: 한 축만 Tiled(목표>=원본일 때), 다른 축은 Sliced와 동일하게 스트레치.
        private void RenderMixedFlexibleCell(VertexHelper vh, Vector2 pos, float colWidth, float rowHeight,
            float srcColWidth, float srcRowHeight,
            float baseUvLeft, float baseUvRight, float baseUvBottom, float baseUvTop,
            bool isFlexibleCol, bool isFlexibleRow)
        {
            bool horizontalAxisTiled = (m_MixedAxisMode == MixedAxisMode.HorizontalTiled_VerticalSliced);
            bool verticalAxisTiled = (m_MixedAxisMode == MixedAxisMode.HorizontalSliced_VerticalTiled);

            // 타일 1개 크기는 srcColWidth/srcRowHeight (이미 PixelsPerUnitMultiplier가 적용된 UI 좌표 단위).
            // colWidth/rowHeight 또한 같은 UI 좌표 단위이므로 직접 비교합니다.
            bool shouldTileX = false;
            bool shouldTileY = false;

            if (isFlexibleCol && horizontalAxisTiled && srcColWidth > 0f && colWidth >= srcColWidth)
                shouldTileX = true;
            if (isFlexibleRow && verticalAxisTiled && srcRowHeight > 0f && rowHeight >= srcRowHeight)
                shouldTileY = true;

            if (shouldTileX || shouldTileY)
            {
                RenderTiled(vh, pos, colWidth, rowHeight, srcColWidth, srcRowHeight,
                    baseUvLeft, baseUvRight, baseUvBottom, baseUvTop,
                    shouldTileX, shouldTileY,
                    false);
            }
            else
            {
                RenderStretched(vh, pos, colWidth, rowHeight,
                    baseUvLeft, baseUvRight, baseUvBottom, baseUvTop);
            }
        }

        // 확장 가능한 셀을 렌더링합니다
        private void RenderFlexibleCell(VertexHelper vh, Vector2 pos, float colWidth, float rowHeight,
                                       float srcColWidth, float srcRowHeight,
                                       float baseUvLeft, float baseUvRight, float baseUvBottom, float baseUvTop,
                                       bool isFlexibleCol, bool isFlexibleRow)
        {
            if (m_ImageType == ImageType.Tiled || m_ImageType == ImageType.TiledFilled || m_ImageType == ImageType.TiledFilledMask)
            {
                bool stepwiseTiling = (m_ImageType == ImageType.TiledFilled);

                // 타일 1개 크기는 srcColWidth/srcRowHeight (이미 PixelsPerUnitMultiplier가 적용된 UI 좌표 단위).
                // colWidth/rowHeight 또한 같은 UI 좌표 단위이므로 직접 비교합니다.
                // (multiplier로 한 번 더 나누면 multiplier<1일 때 타일링이 트리거되지 않는 버그가 발생)
                bool shouldTileX = false;
                bool shouldTileY = false;

                if (isFlexibleCol && srcColWidth > 0f && colWidth >= srcColWidth)
                {
                    shouldTileX = true;
                }

                if (isFlexibleRow && srcRowHeight > 0f && rowHeight >= srcRowHeight)
                {
                    shouldTileY = true;
                }

                if (shouldTileX || shouldTileY)
                {
                    // 타일링: 모든 공간을 타일로 채움 (마지막 타일은 필요한 만큼 늘림)
                    RenderTiled(vh, pos, colWidth, rowHeight, srcColWidth, srcRowHeight,
                               baseUvLeft, baseUvRight, baseUvBottom, baseUvTop,
                               shouldTileX, shouldTileY,
                               stepwiseTiling);
                }
                else
                {
                    // Stretch: 원본 크기보다 작으면 stretch 사용
                    if (stepwiseTiling)
                    {
                        // Stepwise 모드에서는 타일 단위로만 확장되므로,
                        // 아직 타일 1개를 그릴 만큼 공간이 없으면 렌더를 생략합니다.
                        return;
                    }

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

        // Sliced 모드 셀을 렌더링합니다 (3x3 그리드). 셀당 쿼드 상한으로 Tiled와 비슷한 정점 수 유지.
        private void RenderSlicedCell(VertexHelper vh, Vector2 currentPos, float colWidth, float rowHeight,
                                     float srcColWidth, float srcRowHeight,
                                     float baseUvLeft, float baseUvRight, float baseUvBottom, float baseUvTop,
                                     bool isFlexibleCol, bool isFlexibleRow)
        {
            int cellStartVertCount = vh.currentVertCount;

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

            // 3x3 그리드로 처리 (왼쪽/중앙/오른쪽 x 아래/중앙/위). 셀당 MAX_QUADS_PER_SLICED_CELL 이하로 제한.
            for (int sectionY = 0; sectionY < GRID_SECTIONS; sectionY++)
            {
                for (int sectionX = 0; sectionX < GRID_SECTIONS; sectionX++)
                {
                    if (!isFlexibleCol && sectionX != GRID_CENTER) continue;
                    if (!isFlexibleRow && sectionY != GRID_CENTER) continue;

                    float sectionPosX, sectionWidth;
                    CalculateSectionPositionX(isFlexibleCol, sectionX, currentPos.x, expandColSize, srcColWidth, colWidth, out sectionPosX, out sectionWidth);

                    float sectionPosY, sectionHeight;
                    CalculateSectionPositionY(isFlexibleRow, sectionY, currentPos.y, expandRowSize, srcRowHeight, rowHeight, out sectionPosY, out sectionHeight);

                    float uvX, uvW;
                    bool tilableX = isFlexibleCol && sectionX != GRID_CENTER;
                    CalculateSectionUVX(isFlexibleCol, sectionX, baseUvLeft, baseUvColWidth, leftEdgeUvX, rightEdgeUvX, out uvX, out uvW);

                    float uvY, uvH;
                    bool tilableY = isFlexibleRow && sectionY != GRID_CENTER;
                    CalculateSectionUVY(isFlexibleRow, sectionY, baseUvBottom, baseUvRowHeight, bottomEdgeUvY, topEdgeUvY, out uvY, out uvH);

                    int quadsUsedInCell = (vh.currentVertCount - cellStartVertCount) / 4;
                    int remainingCellBudget = Mathf.Max(0, MAX_QUADS_PER_SLICED_CELL - quadsUsedInCell);

                    if (tilableX || tilableY)
                    {
                        RenderTiledSection(vh, sectionPosX, sectionPosY, sectionWidth, sectionHeight, uvX, uvY, uvW, uvH, tilableX, tilableY, remainingCellBudget);
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

        // 타일링 섹션을 렌더링합니다. 정점 한계 초과 시 1쿼드 스트레치로 폴백.
        // maxQuadsForCell: Sliced 모드에서 셀당 예산(남은 쿼드). -1이면 무시.
        private void RenderTiledSection(VertexHelper vh, float sectionPosX, float sectionPosY, float sectionWidth, float sectionHeight,
                                        float uvX, float uvY, float uvW, float uvH, bool tilableX, bool tilableY, int maxQuadsForCell = -1)
        {
            // 타일 크기 계산 (최대 MAX_TILE_SIZE 픽셀)
            float tileW = tilableX ? Mathf.Min(MAX_TILE_SIZE, sectionWidth) : sectionWidth;
            float tileH = tilableY ? Mathf.Min(MAX_TILE_SIZE, sectionHeight) : sectionHeight;

            // 타일 개수 계산
            int tilesX = tilableX ? Mathf.CeilToInt(sectionWidth / tileW) : 1;
            int tilesY = tilableY ? Mathf.CeilToInt(sectionHeight / tileH) : 1;
            int plannedQuads = tilesX * tilesY;
            int remainingQuads = (MAX_MESH_VERTICES - vh.currentVertCount) / 4;

            // Sliced 전체 타일 쿼드 상한 (Tiled와 비슷한 수준으로 유지)
            int remainingSlicedBudget = maxQuadsForCell >= 0 ? Mathf.Max(0, MAX_QUADS_TOTAL_SLICED - _slicedTiledQuadsUsed) : int.MaxValue;
            bool overSlicedTotalBudget = maxQuadsForCell >= 0 && plannedQuads > remainingSlicedBudget;

            // 모바일 안전: 섹션당/전체/셀당/Sliced 전체 예산 초과 시 타일링 금지, 1쿼드 스트레치만 허용
            bool overSectionLimit = plannedQuads > MAX_QUADS_PER_SECTION;
            bool overTotalLimit = plannedQuads > remainingQuads || plannedQuads > MAX_QUADS_SAFE;
            bool overCellBudget = maxQuadsForCell >= 0 && plannedQuads > maxQuadsForCell;
            if (overSectionLimit || overTotalLimit || overCellBudget || overSlicedTotalBudget)
            {
                tempRect.x = sectionPosX;
                tempRect.y = sectionPosY;
                tempRect.width = sectionWidth;
                tempRect.height = sectionHeight;
                Rect uvRect = new Rect(uvX, uvY, uvW, uvH);
                AddQuad(vh, tempRect, uvRect);
                if (maxQuadsForCell >= 0)
                    _slicedTiledQuadsUsed += 1;
                return;
            }

            // 계산 기반 타일링 (섹션당/전체 Sliced 예산 내)
            int quadsAdded = 0;
            for (int ty = 0; ty < tilesY && quadsAdded < MAX_QUADS_PER_SECTION && (maxQuadsForCell < 0 || quadsAdded < remainingSlicedBudget); ty++)
            {
                float currentTileY = sectionPosY + ty * tileH;
                float actualTileH = Mathf.Min(tileH, sectionPosY + sectionHeight - currentTileY);

                for (int tx = 0; tx < tilesX && quadsAdded < MAX_QUADS_PER_SECTION && (maxQuadsForCell < 0 || quadsAdded < remainingSlicedBudget); tx++)
                {
                    float currentTileX = sectionPosX + tx * tileW;
                    float actualTileW = Mathf.Min(tileW, sectionPosX + sectionWidth - currentTileX);

                    tempRect.x = currentTileX;
                    tempRect.y = currentTileY;
                    tempRect.width = actualTileW;
                    tempRect.height = actualTileH;
                    Rect uvRect = new Rect(uvX, uvY, uvW, uvH);
                    AddQuad(vh, tempRect, uvRect);
                    quadsAdded++;
                }
            }

            if (maxQuadsForCell >= 0)
                _slicedTiledQuadsUsed += quadsAdded;
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

        // Tiled 모드: 확장 영역의 원본 크기만큼씩 반복하여 타일링. 정점 한계 초과 시 1쿼드 스트레치로 폴백.
        private void RenderTiled(
            VertexHelper vh,
            Vector2 startPos,
            float totalWidth,
            float totalHeight,
            float srcWidth,
            float srcHeight,
            float uvLeft, float uvRight,
            float uvBottom, float uvTop,
            bool tileX, bool tileY,
            bool stepwiseTiling)
        {
            // 타일 크기 (픽셀 단위) - 확장 영역의 원본 크기 사용
            float baseTileW = tileX ? srcWidth : totalWidth;
            float baseTileH = tileY ? srcHeight : totalHeight;

            if (baseTileW <= 0f || baseTileH <= 0f)
            {
                return;
            }

            float uvWidth = uvRight - uvLeft;
            float uvHeight = uvTop - uvBottom;
            float invBaseTileW = 1f / baseTileW;
            float invBaseTileH = 1f / baseTileH;

            const float EPS = 0.0001f;
            const float EPS_FULL = 0.0005f;

            // stepwiseTiling이 true인 경우:
            // - FillOrigin에 해당하는 축에서만 "완전한 타일"만 렌더링
            // - 다른 축은 일반 Tiled처럼 stretch 포함(ceil로 타일 계획)
            bool stepX = stepwiseTiling && tileX && (m_FillOrigin == FillOrigin.Left || m_FillOrigin == FillOrigin.Right);
            bool stepY = stepwiseTiling && tileY && (m_FillOrigin == FillOrigin.Bottom || m_FillOrigin == FillOrigin.Top);

            float tileOriginX = startPos.x;
            float tileOriginY = startPos.y;
            bool exactStepX = false; // step 축에서만 부분 타일을 제외
            bool exactStepY = false;

            int tilesX = 1;
            int tilesY = 1;

            // X axis tiles
            if (tileX)
            {
                if (stepX)
                {
                    float cellLeft = startPos.x;
                    float cellRight = startPos.x + totalWidth;

                    float maskLeft = _filledMaskRect.x;
                    float maskRight = _filledMaskRect.x + _filledMaskRect.width;

                    float visLeft = Mathf.Max(cellLeft, maskLeft);
                    float visRight = Mathf.Min(cellRight, maskRight);
                    float visibleWidth = visRight - visLeft;

                    if (visibleWidth <= EPS_FULL)
                    {
                        tilesX = 0;
                    }
                    else
                    {
                        bool fullVisibleX = visibleWidth >= totalWidth - EPS_FULL;
                        if (fullVisibleX)
                        {
                            tilesX = Mathf.CeilToInt(totalWidth / baseTileW);
                            if (m_FillOrigin == FillOrigin.Right)
                            {
                                tileOriginX = cellRight - tilesX * baseTileW;
                            }
                        }
                        else
                        {
                            tilesX = Mathf.FloorToInt((visibleWidth / baseTileW) + EPS);
                            tilesX = Mathf.Max(0, tilesX);
                            exactStepX = true;
                            tileOriginX = (m_FillOrigin == FillOrigin.Left)
                                ? cellLeft
                                : (cellRight - tilesX * baseTileW);
                        }
                    }
                }
                else
                {
                    tilesX = Mathf.CeilToInt(totalWidth / baseTileW);
                }
            }

            // Y axis tiles
            if (tileY)
            {
                if (stepY)
                {
                    float cellBottom = startPos.y;
                    float cellTop = startPos.y + totalHeight;

                    float maskBottom = _filledMaskRect.y;
                    float maskTop = _filledMaskRect.y + _filledMaskRect.height;

                    float visBottom = Mathf.Max(cellBottom, maskBottom);
                    float visTop = Mathf.Min(cellTop, maskTop);
                    float visibleHeight = visTop - visBottom;

                    if (visibleHeight <= EPS_FULL)
                    {
                        tilesY = 0;
                    }
                    else
                    {
                        bool fullVisibleY = visibleHeight >= totalHeight - EPS_FULL;
                        if (fullVisibleY)
                        {
                            tilesY = Mathf.CeilToInt(totalHeight / baseTileH);
                            if (m_FillOrigin == FillOrigin.Top)
                            {
                                tileOriginY = cellTop - tilesY * baseTileH;
                            }
                        }
                        else
                        {
                            tilesY = Mathf.FloorToInt((visibleHeight / baseTileH) + EPS);
                            tilesY = Mathf.Max(0, tilesY);
                            exactStepY = true;
                            tileOriginY = (m_FillOrigin == FillOrigin.Bottom)
                                ? cellBottom
                                : (cellTop - tilesY * baseTileH);
                        }
                    }
                }
                else
                {
                    tilesY = Mathf.CeilToInt(totalHeight / baseTileH);
                }
            }

            if (tilesX <= 0 || tilesY <= 0)
                return;

            int plannedQuads = tilesX * tilesY;
            int remainingQuads = (MAX_MESH_VERTICES - vh.currentVertCount) / 4;

            // 모바일 안전: 셀당/전체 정점 한계 초과 시 타일링 금지, 1쿼드 스트레치만 허용
            bool overSectionLimit = plannedQuads > MAX_QUADS_PER_SECTION;
            bool overTotalLimit = plannedQuads > remainingQuads || plannedQuads > MAX_QUADS_SAFE;
            if (overSectionLimit || overTotalLimit)
            {
                RenderStretched(vh, startPos, totalWidth, totalHeight, uvLeft, uvRight, uvBottom, uvTop);
                return;
            }

            // 타일링 (셀당 최대 MAX_QUADS_PER_SECTION 쿼드, 모바일 안전)
            int quadsAdded = 0;
            for (int ty = 0; ty < tilesY && quadsAdded < MAX_QUADS_PER_SECTION; ty++)
            {
                float currentY = tileOriginY + ty * baseTileH;
                float actualH = exactStepY ? baseTileH : Mathf.Min(baseTileH, startPos.y + totalHeight - currentY);

                for (int tx = 0; tx < tilesX && quadsAdded < MAX_QUADS_PER_SECTION; tx++)
                {
                    float currentX = tileOriginX + tx * baseTileW;
                    float actualW = exactStepX ? baseTileW : Mathf.Min(baseTileW, startPos.x + totalWidth - currentX);

                    tempRect.x = currentX;
                    tempRect.y = currentY;
                    tempRect.width = actualW;
                    tempRect.height = actualH;

                    float uvW = uvWidth * (actualW * invBaseTileW);
                    float uvH = uvHeight * (actualH * invBaseTileH);
                    Rect uvRect = new Rect(uvLeft, uvBottom, uvW, uvH);

                    // TiledFilled의 핵심 동작: 모든 메쉬는 단위(unit)로 렌더링.
                    // 두 축 모두 stepwise 검사 적용 → 부분 클립된 타일은 통째로 숨김.
                    // (한 축만 타일링되는 셀의 경우, 비-타일 축은 stepX/stepY 사전클리핑이 적용되지 않아
                    //  단일 타일이 행/열 전체를 차지하지만, 이 경우에도 단위 메쉬로 취급해 한번에 숨김)
                    AddQuad(vh, tempRect, uvRect, true, true);
                    quadsAdded++;
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
        // stepwiseTileX/Y: TiledFilled에서 "이 축이 타일 정렬 축"인지 여부.
        //   - true이면 해당 축에서 부분 클립된 쿼드는 렌더 거부 (완전한 타일만 표시 — 정점 그리드 유지)
        //   - false이면 해당 축은 일반 클리핑 허용 (고정 셀이나 수직축 전체를 차지하는 셀이 통째로 사라지는 버그 방지)
        // 기본값 false → 호출자가 명시적으로 타일 컨텍스트임을 표시해야 stepwise 거부가 적용됩니다.
        private void AddQuad(VertexHelper vh, Rect posRect, Rect uvRect, bool stepwiseTileX = false, bool stepwiseTileY = false)
        {
            if (_filledMaskActive)
            {
                float originalX = posRect.x;
                float originalY = posRect.y;
                float originalW = posRect.width;
                float originalH = posRect.height;

                float maskLeft = _filledMaskRect.x;
                float maskRight = _filledMaskRect.x + _filledMaskRect.width;
                float maskBottom = _filledMaskRect.y;
                float maskTop = _filledMaskRect.y + _filledMaskRect.height;

                float left = Mathf.Max(originalX, maskLeft);
                float right = Mathf.Min(originalX + originalW, maskRight);
                float bottom = Mathf.Max(originalY, maskBottom);
                float top = Mathf.Min(originalY + originalH, maskTop);

                if (right <= left || top <= bottom || originalW <= 0f || originalH <= 0f)
                    return;

                if (_filledMaskStepwiseQuad)
                {
                    const float CLIP_EPS = 0.0001f;
                    // 타일 정렬 축에서만 stepwise 거부 적용.
                    // 비-타일 축(고정 셀의 양 축, 또는 한 축만 타일링되는 경우의 다른 축)은 일반 클리핑.
                    if (stepwiseTileX)
                    {
                        bool fullyVisibleX = left <= originalX + CLIP_EPS && right >= originalX + originalW - CLIP_EPS;
                        if (!fullyVisibleX) return;
                    }
                    if (stepwiseTileY)
                    {
                        bool fullyVisibleY = bottom <= originalY + CLIP_EPS && top >= originalY + originalH - CLIP_EPS;
                        if (!fullyVisibleY) return;
                    }
                }

                // TiledFilledMask/레거시 FilledMask는 기존처럼 부분 클리핑을 허용합니다.
                // posRect 클립
                float clippedW = right - left;
                float clippedH = top - bottom;
                posRect.x = left;
                posRect.y = bottom;
                posRect.width = clippedW;
                posRect.height = clippedH;

                // uvRect 클립 (posRect 비율만큼)
                float uLeftRatio = (left - originalX) / originalW;
                float uRightRatio = (right - originalX) / originalW;
                float vBottomRatio = (bottom - originalY) / originalH;
                float vTopRatio = (top - originalY) / originalH;

                uvRect.x = uvRect.x + uvRect.width * uLeftRatio;
                uvRect.width = uvRect.width * (uRightRatio - uLeftRatio);

                uvRect.y = uvRect.y + uvRect.height * vBottomRatio;
                uvRect.height = uvRect.height * (vTopRatio - vBottomRatio);
            }

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