using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

namespace CAT.BookFlip
{
    /// <summary>
    /// 책넘기기 효과를 제공하는 메인 컨트롤러.
    /// 런타임 페이지 관리(SetPages/AddPage/RemovePage/RefreshPage) 및
    /// AnimationCurve 기반 넘김 애니메이션을 지원한다.
    /// </summary>
    [ExecuteInEditMode]
    public class BookFlip : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Enum
        // ─────────────────────────────────────────────

        public enum FlipMode { RightToLeft, LeftToRight }

        // ─────────────────────────────────────────────
        // Inspector 필드
        // ─────────────────────────────────────────────

        [Header("Canvas 설정")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _bookPanel;
        [SerializeField] private RectTransform _pageContainer;

        [Header("페이지 설정")]
        [SerializeField] private Sprite _background;
        [SerializeField] private BookFlipPage[] _pages = new BookFlipPage[0];

        [Header("옵션")]
        [SerializeField] private bool _interactable = true;
        [SerializeField] private bool _enableShadowEffect = true;

        [Header("애니메이션 설정")]
        [Tooltip("페이지 넘김 총 소요 시간 (초)")]
        [SerializeField] private float _flipDuration = 0.3f;
        [Tooltip("페이지 넘김 이징 곡선 — x: 시간(0→1), y: 진행도(0→1)")]
        [SerializeField] private AnimationCurve _flipCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("UI 요소")]
        [SerializeField] private Image _clippingPlane;
        [SerializeField] private Image _nextPageClip;
        [SerializeField] private Image _shadow;
        [SerializeField] private Image _shadowLTR;
        [SerializeField] private Image _left;
        [SerializeField] private Image _leftNext;
        [SerializeField] private Image _right;
        [SerializeField] private Image _rightNext;

        [Header("핫스팟 (선택사항)")]
        [Tooltip("BookFlipHotSpot 컴포넌트를 사용하는 경우 자동으로 설정됩니다")]
        [SerializeField] private RectTransform _hotSpotContainer;
        [SerializeField] private RectTransform _leftHotSpot;
        [SerializeField] private RectTransform _rightHotSpot;

        [Header("이벤트")]
        public UnityEvent OnFlip;
        public UnityEvent<int> OnPageChanged;
        public UnityEvent OnFlipStart;
        public UnityEvent OnFlipEnd;

        // ─────────────────────────────────────────────
        // 캐싱된 RectTransform
        // ─────────────────────────────────────────────

        private RectTransform _clippingPlaneRT;
        private RectTransform _nextPageClipRT;
        private RectTransform _shadowRT;
        private RectTransform _shadowLTRRT;
        private RectTransform _leftRT;
        private RectTransform _leftNextRT;
        private RectTransform _rightRT;
        private RectTransform _rightNextRT;

        // ─────────────────────────────────────────────
        // 페이지 인스턴스 & 슬롯 추적
        // ─────────────────────────────────────────────

        // 각 표시 슬롯에 현재 띄워진 실제 GameObject 참조
        private GameObject _leftPageInstance;
        private GameObject _leftNextPageInstance;
        private GameObject _rightPageInstance;
        private GameObject _rightNextPageInstance;

        // 각 슬롯에 표시 중인 _pages 인덱스 (-1 = 비어 있음)
        // CleanupDisplaySlot에서 page.Release()를 올바르게 호출하기 위해 사용
        private int _leftDisplayIndex     = -1;
        private int _leftNextDisplayIndex = -1;
        private int _rightDisplayIndex    = -1;
        private int _rightNextDisplayIndex = -1;

        // ─────────────────────────────────────────────
        // 슬롯 RT 초기 상태 (애니메이션 후 복원용)
        // ─────────────────────────────────────────────

        // _leftNext / _rightNext 는 애니메이션 중 _nextPageClip 자식으로 이동되어 좌표가 틀어지므로
        // Start() 에서 초기값을 저장해두고 Flip / TweenBack 완료 시 복원한다
        private struct SlotRTState
        {
            public Vector2 anchorMin, anchorMax, anchoredPosition, sizeDelta;
        }
        private SlotRTState _leftNextRTState, _rightNextRTState;

        // ─────────────────────────────────────────────
        // 곡선 계산 변수
        // ─────────────────────────────────────────────

        [SerializeField] private int _currentPage = 0;

        private float _radius1, _radius2;
        private Vector3 _sb; // Spine Bottom
        private Vector3 _st; // Spine Top
        private Vector3 _c;  // Corner of the page
        private Vector3 _ebr; // Edge Bottom Right
        private Vector3 _ebl; // Edge Bottom Left
        private Vector3 _f;  // Follow point

        private bool _pageDragging = false;
        private FlipMode _mode;
        private Coroutine _currentCoroutine;

        // ─────────────────────────────────────────────
        // 프로퍼티
        // ─────────────────────────────────────────────

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = Mathf.Clamp(value, 0, Mathf.Max(0, _pages.Length - 1));
                    UpdateSprites();
                    OnPageChanged?.Invoke(_currentPage);
                }
            }
        }

        public int TotalPageCount => _pages.Length;
        public bool Interactable { get => _interactable; set => _interactable = value; }
        public Vector3 EndBottomLeft  => _ebl;
        public Vector3 EndBottomRight => _ebr;
        public float   Height         => _bookPanel.rect.height;

        // ─────────────────────────────────────────────
        // Unity 생명주기
        // ─────────────────────────────────────────────

        private void Awake()
        {
            CacheComponents();
        }

        private void Start()
        {
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();

            if (_canvas == null)
                Debug.LogError("[BookFlip] Canvas를 찾을 수 없습니다. BookFlip은 Canvas의 자식이어야 합니다.");

            // PageContainer가 없으면 BookPanel을 대신 사용
            if (_pageContainer == null)
                _pageContainer = _bookPanel;

            InitializePages();
            CalcCurlCriticalPoints();
            SetupUIElements();
            SetupContainerHierarchy();
            SetupHotSpots();
            SaveSlotRTStates(); // 슬롯 RT 초기값 저장 (애니메이션 후 복원용)
        }

        private void OnDestroy()
        {
            // 현재 슬롯에 표시 중인 인스턴스 정리
            CleanupAllDisplaySlots();

            // 모든 페이지 인스턴스 정리
            if (_pages != null)
                for (int i = 0; i < _pages.Length; i++)
                    _pages[i]?.Destroy();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_bookPanel != null)
                CalcCurlCriticalPoints();
        }
#endif

        // ─────────────────────────────────────────────
        // 초기화
        // ─────────────────────────────────────────────

        private void CacheComponents()
        {
            if (_clippingPlane != null) _clippingPlaneRT = _clippingPlane.GetComponent<RectTransform>();
            if (_nextPageClip  != null) _nextPageClipRT  = _nextPageClip.GetComponent<RectTransform>();
            if (_shadow        != null) _shadowRT        = _shadow.GetComponent<RectTransform>();
            if (_shadowLTR     != null) _shadowLTRRT     = _shadowLTR.GetComponent<RectTransform>();
            if (_left          != null) _leftRT          = _left.GetComponent<RectTransform>();
            if (_leftNext      != null) _leftNextRT      = _leftNext.GetComponent<RectTransform>();
            if (_right         != null) _rightRT         = _right.GetComponent<RectTransform>();
            if (_rightNext     != null) _rightNextRT     = _rightNext.GetComponent<RectTransform>();
        }

        private void InitializePages()
        {
            _left.gameObject.SetActive(false);
            _right.gameObject.SetActive(false);
            UpdateSprites();
        }

        private void SetupUIElements()
        {
            float pageWidth  = _bookPanel.rect.width  / 2.0f;
            float pageHeight = _bookPanel.rect.height;

            _nextPageClipRT.sizeDelta  = new Vector2(pageWidth, pageHeight + pageHeight * 2);
            _clippingPlaneRT.sizeDelta = new Vector2(pageWidth * 2 + pageHeight, pageHeight + pageHeight * 2);

            float hyp = Mathf.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
            float shadowPageHeight = pageWidth / 2 + hyp;

            _shadowRT.sizeDelta    = new Vector2(pageWidth, shadowPageHeight);
            _shadowRT.pivot        = new Vector2(1, (pageWidth / 2) / shadowPageHeight);

            _shadowLTRRT.sizeDelta = new Vector2(pageWidth, shadowPageHeight);
            _shadowLTRRT.pivot     = new Vector2(0, (pageWidth / 2) / shadowPageHeight);
        }

        private void SetupContainerHierarchy()
        {
            if (_pageContainer == null || _pageContainer == _bookPanel || _hotSpotContainer == null)
                return;

            // 모든 페이지 요소를 PageContainer 하위로 이동
            TryReparent(_clippingPlane?.transform, _pageContainer);
            TryReparent(_nextPageClip?.transform,  _pageContainer);
            TryReparent(_shadow?.transform,        _pageContainer);
            TryReparent(_shadowLTR?.transform,     _pageContainer);
            TryReparent(_left?.transform,          _pageContainer);
            TryReparent(_leftNext?.transform,      _pageContainer);
            TryReparent(_right?.transform,         _pageContainer);
            TryReparent(_rightNext?.transform,     _pageContainer);

            if (_pageContainer.parent != _bookPanel)
                _pageContainer.SetParent(_bookPanel, true);

            if (_hotSpotContainer.parent != _bookPanel)
                _hotSpotContainer.SetParent(_bookPanel, true);

            _pageContainer.SetSiblingIndex(0);
            _hotSpotContainer.SetAsLastSibling();
        }

        private static void TryReparent(Transform t, Transform newParent)
        {
            if (t != null && t.parent != newParent)
                t.SetParent(newParent, true);
        }

        private void SetupHotSpots()
        {
            RectTransform hotSpotParent = _hotSpotContainer != null ? _hotSpotContainer : _bookPanel;

            if (_leftHotSpot  != null) _leftHotSpot.SetParent(hotSpotParent,  true);
            if (_rightHotSpot != null) _rightHotSpot.SetParent(hotSpotParent, true);

            if (_hotSpotContainer != null)
            {
                _hotSpotContainer.SetParent(_bookPanel, true);
                _hotSpotContainer.SetAsLastSibling();
            }
            else
            {
                if (_leftHotSpot  != null) _leftHotSpot.SetAsLastSibling();
                if (_rightHotSpot != null) _rightHotSpot.SetAsLastSibling();
            }
        }

        private void CalcCurlCriticalPoints()
        {
            _sb   = new Vector3(0, -_bookPanel.rect.height / 2);
            _ebr  = new Vector3( _bookPanel.rect.width / 2, -_bookPanel.rect.height / 2);
            _ebl  = new Vector3(-_bookPanel.rect.width / 2, -_bookPanel.rect.height / 2);
            _st   = new Vector3(0, _bookPanel.rect.height / 2);
            _radius1 = Vector2.Distance(_sb, _ebr);

            float pageWidth  = _bookPanel.rect.width  / 2.0f;
            float pageHeight = _bookPanel.rect.height;
            _radius2 = Mathf.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
        }

        // ─────────────────────────────────────────────
        // Update
        // ─────────────────────────────────────────────

        private void Update()
        {
            if (_pageDragging && _interactable)
                UpdateBook();

            // 플립 트윈 중 매 프레임 SetSiblingIndex/SetParent 하면 페이지·핫스팟 순서가 흔들려 깜빡일 수 있음
            if (_currentCoroutine == null)
                EnsureHotSpotsOnTop();
        }

        private void EnsureHotSpotsOnTop()
        {
            if (_pageContainer != null && _pageContainer != _bookPanel && _hotSpotContainer != null)
            {
                if (_pageContainer.parent != _bookPanel)
                    _pageContainer.SetParent(_bookPanel, true);

                if (_hotSpotContainer.parent != _bookPanel)
                    _hotSpotContainer.SetParent(_bookPanel, true);

                int pageIdx    = _pageContainer.GetSiblingIndex();
                int hotSpotIdx = _hotSpotContainer.GetSiblingIndex();

                if (hotSpotIdx < pageIdx)
                {
                    // 이미 0이면 SetSiblingIndex 호출 생략(매 프레임 dirty 방지)
                    if (pageIdx != 0)
                        _pageContainer.SetSiblingIndex(0);
                    _hotSpotContainer.SetAsLastSibling();
                }
                else if (hotSpotIdx != _bookPanel.childCount - 1)
                {
                    _hotSpotContainer.SetAsLastSibling();
                }

                if (_leftHotSpot  != null && _leftHotSpot.parent  != _hotSpotContainer) _leftHotSpot.SetParent(_hotSpotContainer,  true);
                if (_rightHotSpot != null && _rightHotSpot.parent != _hotSpotContainer) _rightHotSpot.SetParent(_hotSpotContainer, true);
            }
            else if (_hotSpotContainer != null)
            {
                if (_hotSpotContainer.parent != _bookPanel)
                    _hotSpotContainer.SetParent(_bookPanel, true);

                if (_hotSpotContainer.GetSiblingIndex() != _bookPanel.childCount - 1)
                    _hotSpotContainer.SetAsLastSibling();

                if (_leftHotSpot  != null && _leftHotSpot.parent  != _hotSpotContainer) _leftHotSpot.SetParent(_hotSpotContainer,  true);
                if (_rightHotSpot != null && _rightHotSpot.parent != _hotSpotContainer) _rightHotSpot.SetParent(_hotSpotContainer, true);
            }
            else
            {
                RectTransform targetParent = (_pageContainer != null && _pageContainer != _bookPanel) ? _pageContainer : _bookPanel;
                int lastIndex = targetParent.childCount - 1;

                if (_leftHotSpot != null)
                {
                    if (_leftHotSpot.parent != targetParent) _leftHotSpot.SetParent(targetParent, true);
                    int idx = _leftHotSpot.GetSiblingIndex();
                    if (idx != lastIndex && idx != lastIndex - 1) _leftHotSpot.SetAsLastSibling();
                }

                if (_rightHotSpot != null)
                {
                    if (_rightHotSpot.parent != targetParent) _rightHotSpot.SetParent(targetParent, true);
                    int idx = _rightHotSpot.GetSiblingIndex();
                    if (idx != lastIndex && idx != lastIndex - 1) _rightHotSpot.SetAsLastSibling();
                }
            }
        }

        // ─────────────────────────────────────────────
        // Book 업데이트 (드래그 중)
        // ─────────────────────────────────────────────

        private void UpdateBook()
        {
            _f = Vector3.Lerp(_f, TransformPoint(Input.mousePosition), Time.deltaTime * 10);

            if (_mode == FlipMode.RightToLeft)
                UpdateBookRTLToPoint(_f);
            else
                UpdateBookLTRToPoint(_f);
        }

        /// <summary>
        /// 부모가 같을 때 SetParent를 반복 호출하면 Canvas/Graphic이 매 프레임 재빌드되어 스프라이트·UI가 깜빡일 수 있다.
        /// </summary>
        private static void SetParentIfDifferent(Transform t, Transform parent, bool worldPositionStays = true)
        {
            if (t == null || parent == null) return;
            if (t.parent != parent)
                t.SetParent(parent, worldPositionStays);
        }

        public void UpdateBookLTRToPoint(Vector3 followLocation)
        {
            _mode = FlipMode.LeftToRight;
            _f    = followLocation;

            SetParentIfDifferent(_shadowLTR.transform, _clippingPlane.transform, true);
            if (_shadowLTR.transform.parent == _clippingPlane.transform)
            {
                _shadowLTR.transform.localPosition    = Vector3.zero;
                _shadowLTR.transform.localEulerAngles = Vector3.zero;
            }

            SetParentIfDifferent(_left.transform, _clippingPlane.transform, true);
            SetParentIfDifferent(_right.transform, _pageContainer.transform, true);
            _right.transform.localEulerAngles = Vector3.zero;
            SetParentIfDifferent(_leftNext.transform, _pageContainer.transform, true);

            _c = CalcCPosition(followLocation);
            float clipAngle = CalcClipAngle(_c, _ebl, out Vector3 t1);
            clipAngle = (clipAngle + 180) % 180;

            _clippingPlane.transform.localEulerAngles = new Vector3(0, 0, clipAngle - 90);
            _clippingPlane.transform.position         = _bookPanel.TransformPoint(t1);

            _left.transform.position = _bookPanel.TransformPoint(_c);
            float cT1Angle = Mathf.Atan2(t1.y - _c.y, t1.x - _c.x) * Mathf.Rad2Deg;
            _left.transform.localEulerAngles = new Vector3(0, 0, cT1Angle - 90 - clipAngle);

            _nextPageClip.transform.localEulerAngles = new Vector3(0, 0, clipAngle - 90);
            _nextPageClip.transform.position         = _bookPanel.TransformPoint(t1);

            SetParentIfDifferent(_leftNext.transform, _nextPageClip.transform, true);
            SetParentIfDifferent(_right.transform, _clippingPlane.transform, true);
            if (_right.transform.GetSiblingIndex() != 0)
                _right.transform.SetAsFirstSibling();

            SetParentIfDifferent(_shadowLTR.rectTransform, _left.rectTransform, true);
        }

        public void UpdateBookRTLToPoint(Vector3 followLocation)
        {
            _mode = FlipMode.RightToLeft;
            _f    = followLocation;

            SetParentIfDifferent(_shadow.transform, _clippingPlane.transform, true);
            if (_shadow.transform.parent == _clippingPlane.transform)
            {
                _shadow.transform.localPosition    = Vector3.zero;
                _shadow.transform.localEulerAngles = Vector3.zero;
            }

            SetParentIfDifferent(_right.transform, _clippingPlane.transform, true);
            SetParentIfDifferent(_left.transform, _pageContainer.transform, true);
            _left.transform.localEulerAngles = Vector3.zero;
            SetParentIfDifferent(_rightNext.transform, _pageContainer.transform, true);

            _c = CalcCPosition(followLocation);
            float clipAngle = CalcClipAngle(_c, _ebr, out Vector3 t1);

            if (clipAngle > -90) clipAngle += 180;

            _clippingPlaneRT.pivot                    = new Vector2(1, 0.35f);
            _clippingPlane.transform.localEulerAngles = new Vector3(0, 0, clipAngle + 90);
            _clippingPlane.transform.position         = _bookPanel.TransformPoint(t1);

            _right.transform.position = _bookPanel.TransformPoint(_c);
            float cT1Angle = Mathf.Atan2(t1.y - _c.y, t1.x - _c.x) * Mathf.Rad2Deg;
            _right.transform.localEulerAngles = new Vector3(0, 0, cT1Angle - (clipAngle + 90));

            _nextPageClip.transform.localEulerAngles = new Vector3(0, 0, clipAngle + 90);
            _nextPageClip.transform.position         = _bookPanel.TransformPoint(t1);

            SetParentIfDifferent(_rightNext.transform, _nextPageClip.transform, true);
            SetParentIfDifferent(_left.transform, _clippingPlane.transform, true);
            if (_left.transform.GetSiblingIndex() != 0)
                _left.transform.SetAsFirstSibling();

            SetParentIfDifferent(_shadow.rectTransform, _right.rectTransform, true);
        }

        // ─────────────────────────────────────────────
        // 수학 유틸
        // ─────────────────────────────────────────────

        private Vector3 TransformPoint(Vector3 mouseScreenPos)
        {
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                Vector3 mouseWorldPos = _canvas.worldCamera.ScreenToWorldPoint(
                    new Vector3(mouseScreenPos.x, mouseScreenPos.y, _canvas.planeDistance));
                return _bookPanel.InverseTransformPoint(mouseWorldPos);
            }
            else if (_canvas.renderMode == RenderMode.WorldSpace)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Vector3 globalEBR = transform.TransformPoint(_ebr);
                Vector3 globalEBL = transform.TransformPoint(_ebl);
                Vector3 globalSt  = transform.TransformPoint(_st);
                Plane p = new Plane(globalEBR, globalEBL, globalSt);

                if (p.Raycast(ray, out float distance))
                    return _bookPanel.InverseTransformPoint(ray.GetPoint(distance));
                return Vector3.zero;
            }
            else
            {
                return _bookPanel.InverseTransformPoint(mouseScreenPos);
            }
        }

        private float CalcClipAngle(Vector3 c, Vector3 bookCorner, out Vector3 t1)
        {
            Vector3 t0 = (c + bookCorner) / 2;
            float t0CornerAngle = Mathf.Atan2(bookCorner.y - t0.y, bookCorner.x - t0.x);

            float t1X = t0.x - (bookCorner.y - t0.y) * Mathf.Tan(t0CornerAngle);
            t1X = NormalizeT1X(t1X, bookCorner, _sb);
            t1  = new Vector3(t1X, _sb.y, 0);

            return Mathf.Atan2(t1.y - t0.y, t1.x - t0.x) * Mathf.Rad2Deg;
        }

        private float NormalizeT1X(float t1, Vector3 corner, Vector3 sb)
        {
            if (t1 > sb.x && sb.x > corner.x) return sb.x;
            if (t1 < sb.x && sb.x < corner.x) return sb.x;
            return t1;
        }

        private Vector3 CalcCPosition(Vector3 followLocation)
        {
            _f = followLocation;
            float fSbAngle = Mathf.Atan2(_f.y - _sb.y, _f.x - _sb.x);
            Vector3 r1 = new Vector3(
                _radius1 * Mathf.Cos(fSbAngle),
                _radius1 * Mathf.Sin(fSbAngle), 0) + _sb;

            float fSbDistance = Vector2.Distance(_f, _sb);
            Vector3 c = fSbDistance < _radius1 ? _f : r1;

            float fStAngle = Mathf.Atan2(c.y - _st.y, c.x - _st.x);
            Vector3 r2 = new Vector3(
                _radius2 * Mathf.Cos(fStAngle),
                _radius2 * Mathf.Sin(fStAngle), 0) + _st;

            if (Vector2.Distance(c, _st) > _radius2) c = r2;
            return c;
        }

        // ─────────────────────────────────────────────
        // 드래그 / 릴리즈
        // ─────────────────────────────────────────────

        public void DragRightPageToPoint(Vector3 point)
        {
            if (_currentPage >= _pages.Length) return;

            _f = point;

            // 드래그 제스처당 1회만 슬롯을 재구성한다. 매 프레임 호출 시 Cleanup/Setup 반복으로 스프라이트·프리팹 모두 깜빡임이 난다.
            if (!_pageDragging)
            {
                _pageDragging = true;
                _mode = FlipMode.RightToLeft;

                OnFlipStart?.Invoke();
                DisablePageInteraction();

                // _rightNext(currentPage)와 _left(currentPage)의 인덱스 충돌 해소
                // _rightNext를 먼저 정리해야 같은 페이지를 _left에 새 인스턴스로 생성할 수 있음
                CleanupDisplaySlot(ref _rightNextPageInstance, ref _rightNextDisplayIndex);

                _nextPageClipRT.pivot  = new Vector2(0, 0.12f);
                _clippingPlaneRT.pivot = new Vector2(1, 0.35f);

                _left.gameObject.SetActive(true);
                _leftRT.pivot = new Vector2(0, 0);
                _left.transform.position     = _rightNext.transform.position;
                _left.transform.eulerAngles  = Vector3.zero;
                SetupPageDisplay(_left, _leftRT, _currentPage, ref _leftPageInstance, ref _leftDisplayIndex);
                _left.enabled = true; // Mask용 Image는 항상 활성 유지
                _left.transform.SetAsFirstSibling();

                _right.gameObject.SetActive(true);
                _right.transform.position    = _rightNext.transform.position;
                _right.transform.eulerAngles = Vector3.zero;
                SetupPageDisplay(_right, _rightRT, _currentPage + 1, ref _rightPageInstance, ref _rightDisplayIndex);
                _right.enabled = true; // Mask용 Image는 항상 활성 유지

                SetupPageDisplay(_rightNext, _rightNextRT, _currentPage + 2, ref _rightNextPageInstance, ref _rightNextDisplayIndex);
                _leftNext.transform.SetAsFirstSibling();

                if (_enableShadowEffect)
                    _shadow.gameObject.SetActive(true);
            }

            UpdateBookRTLToPoint(_f);
        }

        public void DragLeftPageToPoint(Vector3 point)
        {
            if (_currentPage <= 0) return;

            _f = point;

            if (!_pageDragging)
            {
                _pageDragging = true;
                _mode = FlipMode.LeftToRight;

                OnFlipStart?.Invoke();
                DisablePageInteraction();

                // _leftNext(currentPage-1)와 _right(currentPage-1)의 인덱스 충돌 해소
                // _leftNext를 먼저 정리해야 같은 페이지를 _right에 새 인스턴스로 생성할 수 있음
                CleanupDisplaySlot(ref _leftNextPageInstance, ref _leftNextDisplayIndex);

                _nextPageClipRT.pivot  = new Vector2(1, 0.12f);
                _clippingPlaneRT.pivot = new Vector2(0, 0.35f);

                _right.gameObject.SetActive(true);
                _right.transform.position    = _leftNext.transform.position;
                _right.transform.eulerAngles = Vector3.zero;
                SetupPageDisplay(_right, _rightRT, _currentPage - 1, ref _rightPageInstance, ref _rightDisplayIndex);
                _right.enabled = true; // Mask용 Image는 항상 활성 유지
                _right.transform.SetAsFirstSibling();

                _left.gameObject.SetActive(true);
                _leftRT.pivot = new Vector2(1, 0);
                _left.transform.position    = _leftNext.transform.position;
                _left.transform.eulerAngles = Vector3.zero;
                SetupPageDisplay(_left, _leftRT, _currentPage - 2, ref _leftPageInstance, ref _leftDisplayIndex);
                _left.enabled = true; // Mask용 Image는 항상 활성 유지

                SetupPageDisplay(_leftNext, _leftNextRT, _currentPage - 3, ref _leftNextPageInstance, ref _leftNextDisplayIndex);
                _rightNext.transform.SetAsFirstSibling();

                if (_enableShadowEffect)
                    _shadowLTR.gameObject.SetActive(true);
            }

            UpdateBookLTRToPoint(_f);
        }

        public void OnMouseDragRightPage()
        {
            if (_interactable) DragRightPageToPoint(TransformPoint(Input.mousePosition));
        }

        public void OnMouseDragLeftPage()
        {
            if (_interactable) DragLeftPageToPoint(TransformPoint(Input.mousePosition));
        }

        public void OnMouseRelease()
        {
            if (_interactable) ReleasePage();
        }

        public void ReleasePage()
        {
            if (!_pageDragging) return;

            _pageDragging = false;

            float distanceToLeft  = Vector2.Distance(_c, _ebl);
            float distanceToRight = Vector2.Distance(_c, _ebr);

            if (distanceToRight < distanceToLeft && _mode == FlipMode.RightToLeft)
                TweenBack();
            else if (distanceToRight > distanceToLeft && _mode == FlipMode.LeftToRight)
                TweenBack();
            else
                TweenForward();
        }

        // ─────────────────────────────────────────────
        // 페이지 표시 설정
        // ─────────────────────────────────────────────

        private void UpdateSprites()
        {
            SetupPageDisplay(_leftNext,  _leftNextRT,  _currentPage - 1, ref _leftNextPageInstance,  ref _leftNextDisplayIndex);
            SetupPageDisplay(_rightNext, _rightNextRT, _currentPage,     ref _rightNextPageInstance, ref _rightNextDisplayIndex);
            // 슬롯 스프라이트/자식 UI 교체 직후 이전 메시가 1프레임 잔상으로 남는 것을 방지
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// 지정 슬롯에 pageIndex 페이지를 표시한다.
        /// 기존 슬롯 내용은 CleanupDisplaySlot으로 정리한다.
        /// </summary>
        private void SetupPageDisplay(
            Image targetImage, RectTransform targetRT,
            int pageIndex,
            ref GameObject pageInstance, ref int displayIndex)
        {
            // 기존 슬롯 정리 (PersistInstance → Release로 비활성화, 아닌 경우 → 파괴)
            CleanupDisplaySlot(ref pageInstance, ref displayIndex);

            // 범위 체크
            if (pageIndex < 0 || pageIndex >= _pages.Length)
            {
                targetImage.sprite  = _background;
                targetImage.enabled = true;
                return;
            }

            BookFlipPage page = _pages[pageIndex];
            if (page == null || !page.IsValid())
            {
                targetImage.sprite  = _background;
                targetImage.enabled = true;
                return;
            }

            displayIndex = pageIndex;

            switch (page.Type)
            {
                case BookFlipPage.PageType.Sprite:
                    // ResourcesPath 모드: Sprite가 없는 경우에만 동기 로드 (Unity Resources 캐시 활용)
                    if (page.Source == BookFlipPage.SourceMode.ResourcesPath && page.Sprite == null)
                        page.LoadFromResources();

                    targetImage.sprite  = page.Sprite;
                    targetImage.enabled = true;
                    break;

                case BookFlipPage.PageType.Prefab:
                case BookFlipPage.PageType.GameObject:
                    // 두 타입 모두 GetOrCreateImage → Instantiate(씬 템플릿 또는 참조) 동일.
                    // Prefab 전용으로 LayoutRebuilder/ForceUpdateCanvases를 넣으면 오히려 한 프레임 깜빡임이 날 수 있어 GameObject와 동일 경로만 사용한다.
                    targetImage.enabled = false;

                    Image pageImage = page.GetOrCreateImage(targetRT, $"Page_{pageIndex}");

                    // pageImage가 없어도 RuntimeInstance가 생성됐을 수 있음 (루트에 Image 없는 경우)
                    pageInstance = pageImage != null
                        ? pageImage.gameObject
                        : page.RuntimeInstance;

                    page.SetInteractable(false);
                    break;
            }
        }

        // ─────────────────────────────────────────────
        // 슬롯 정리
        // ─────────────────────────────────────────────

        /// <summary>
        /// 특정 슬롯의 페이지 인스턴스를 정리한다.
        /// PersistInstance 페이지는 Release()로 비활성화, 아닌 경우 Destroy.
        /// </summary>
        private void CleanupDisplaySlot(ref GameObject pageInstance, ref int displayIndex)
        {
            if (displayIndex >= 0 && displayIndex < _pages.Length && _pages[displayIndex] != null)
            {
                // _pageContainer를 풀(pool) 부모로 사용 — 비활성화된 PersistInstance가 다른 슬롯 정리에 영향받지 않도록
                _pages[displayIndex].Release(_pageContainer);
            }
            else if (pageInstance != null)
            {
                // 페이지 인덱스 추적이 없는 경우의 폴백 (인덱스 범위 초과 등)
                pageInstance.SetActive(false);
                Destroy(pageInstance);
            }

            pageInstance = null;
            displayIndex = -1;
        }

        /// <summary>모든 슬롯을 정리한다</summary>
        private void CleanupAllDisplaySlots()
        {
            CleanupDisplaySlot(ref _leftPageInstance,     ref _leftDisplayIndex);
            CleanupDisplaySlot(ref _rightPageInstance,    ref _rightDisplayIndex);
            CleanupDisplaySlot(ref _leftNextPageInstance, ref _leftNextDisplayIndex);
            CleanupDisplaySlot(ref _rightNextPageInstance, ref _rightNextDisplayIndex);
        }

        // ─────────────────────────────────────────────
        // 슬롯 RT 복원
        // ─────────────────────────────────────────────

        /// <summary>
        /// _leftNext / _rightNext 슬롯의 RectTransform 초기값을 저장한다.
        /// 애니메이션 중 _nextPageClip 자식으로 재부모 → 좌표가 틀어지므로
        /// Flip / TweenBack 완료 시 SaveSlotRTStates 값으로 복원해야 한다.
        /// </summary>
        private void SaveSlotRTStates()
        {
            _leftNextRTState  = CaptureRTState(_leftNextRT);
            _rightNextRTState = CaptureRTState(_rightNextRT);
        }

        private SlotRTState CaptureRTState(RectTransform rt)
        {
            if (rt == null) return default;
            return new SlotRTState
            {
                anchorMin        = rt.anchorMin,
                anchorMax        = rt.anchorMax,
                anchoredPosition = rt.anchoredPosition,
                sizeDelta        = rt.sizeDelta,
            };
        }

        private void RestoreRT(RectTransform rt, SlotRTState state)
        {
            if (rt == null) return;
            rt.anchorMin        = state.anchorMin;
            rt.anchorMax        = state.anchorMax;
            rt.anchoredPosition = state.anchoredPosition;
            rt.sizeDelta        = state.sizeDelta;
        }

        // ─────────────────────────────────────────────
        // 애니메이션
        // ─────────────────────────────────────────────

        public void TweenForward()
        {
            Vector3 target = _mode == FlipMode.RightToLeft ? _ebl : _ebr;
            Vector3 from   = _f;

            if (_currentCoroutine != null)
                StopCoroutine(_currentCoroutine);

            _currentCoroutine = StartCoroutine(TweenTo(from, target, _flipDuration, Flip));
        }

        public void TweenBack()
        {
            if (_currentCoroutine != null)
                StopCoroutine(_currentCoroutine);

            Vector3 from   = _f;
            Vector3 target = _mode == FlipMode.RightToLeft ? _ebr : _ebl;

            _currentCoroutine = StartCoroutine(TweenTo(from, target, _flipDuration, () =>
            {
                // ── 1. 애니메이션 슬롯 인스턴스 정리 ──
                CleanupDisplaySlot(ref _leftPageInstance,  ref _leftDisplayIndex);
                CleanupDisplaySlot(ref _rightPageInstance, ref _rightDisplayIndex);

                _left.transform.SetParent(_pageContainer.transform, true);
                _left.gameObject.SetActive(false);
                _right.transform.SetParent(_pageContainer.transform, true);
                _right.gameObject.SetActive(false);

                // ── 2. 정적 슬롯 복귀 + RT 좌표 복원 ──
                _leftNext.transform.SetParent(_pageContainer.transform, true);
                RestoreRT(_leftNextRT, _leftNextRTState);
                _rightNext.transform.SetParent(_pageContainer.transform, true);
                RestoreRT(_rightNextRT, _rightNextRTState);

                // ── 3. 콘텐츠 재설정 ──
                UpdateSprites();

                _pageDragging = false;

                EnablePageInteraction();
                SetupHotSpots();
                OnFlipEnd?.Invoke();
            }));
        }

        /// <summary>
        /// AnimationCurve 기반 트윈.
        /// Time.deltaTime 사용 → 프레임레이트 독립적.
        /// </summary>
        private IEnumerator TweenTo(Vector3 from, Vector3 to, float duration, System.Action onFinish)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t   = _flipCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
                Vector3 pos = Vector3.LerpUnclamped(from, to, t);

                if (_mode == FlipMode.RightToLeft)
                    UpdateBookRTLToPoint(pos);
                else
                    UpdateBookLTRToPoint(pos);

                // 최종 위치 도달 시 렌더링하지 않고 즉시 완료 처리로 직행
                // (yield 하면 "클리핑 애니메이션 상태"가 1프레임 보인 뒤 정착 상태로 전환되어 잔상 발생)
                if (elapsed >= duration)
                    break;

                yield return null;
            }

            onFinish?.Invoke();
            _currentCoroutine = null;
        }

        private void Flip()
        {
            // ── 1. 애니메이션 슬롯 인스턴스 정리 ──
            CleanupDisplaySlot(ref _leftPageInstance,  ref _leftDisplayIndex);
            CleanupDisplaySlot(ref _rightPageInstance, ref _rightDisplayIndex);

            // 애니메이션 슬롯 복귀 (_clippingPlane 자식 → 회전 상태이므로 worldPositionStays=true 필수)
            _left.transform.SetParent(_pageContainer.transform, true);
            _left.gameObject.SetActive(false);
            _right.gameObject.SetActive(false);
            _right.transform.SetParent(_pageContainer.transform, true);

            // ── 2. 정적 슬롯 복귀 + RT 좌표 복원 ──
            _leftNext.transform.SetParent(_pageContainer.transform, true);
            RestoreRT(_leftNextRT, _leftNextRTState);
            _rightNext.transform.SetParent(_pageContainer.transform, true);
            RestoreRT(_rightNextRT, _rightNextRTState);

            // ── 3. 페이지 인덱스 갱신 + 콘텐츠 재설정 ──
            if (_mode == FlipMode.RightToLeft)
                _currentPage += 2;
            else
                _currentPage -= 2;

            UpdateSprites();

            _shadow.gameObject.SetActive(false);
            _shadowLTR.gameObject.SetActive(false);

            _pageDragging = false;

            EnablePageInteraction();
            SetupHotSpots();

            OnFlip?.Invoke();
            OnPageChanged?.Invoke(_currentPage);
            OnFlipEnd?.Invoke();
        }

        // ─────────────────────────────────────────────
        // 인터랙션 제어
        // ─────────────────────────────────────────────

        private void DisablePageInteraction()
        {
            if (_pages == null) return;
            for (int i = 0; i < _pages.Length; i++)
                _pages[i]?.SetInteractable(false);
        }

        private void EnablePageInteraction()
        {
            // 현재 보이는 두 페이지만 활성화
            int leftIdx  = _currentPage - 1;
            int rightIdx = _currentPage;

            if (leftIdx  >= 0 && leftIdx  < _pages.Length && _pages[leftIdx]  != null) _pages[leftIdx].SetInteractable(true);
            if (rightIdx >= 0 && rightIdx < _pages.Length && _pages[rightIdx] != null) _pages[rightIdx].SetInteractable(true);
        }

        // ─────────────────────────────────────────────
        // 공개 API — 페이지 이동
        // ─────────────────────────────────────────────

        /// <summary>다음 페이지로 이동</summary>
        public void NextPage()
        {
            if (_currentPage < _pages.Length - 1)
            {
                DragRightPageToPoint(_ebr);
                TweenForward();
            }
        }

        /// <summary>이전 페이지로 이동</summary>
        public void PreviousPage()
        {
            if (_currentPage > 0)
            {
                DragLeftPageToPoint(_ebl);
                TweenForward();
            }
        }

        /// <summary>특정 페이지로 즉시 이동 (애니메이션 없음)</summary>
        public void GoToPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _pages.Length)
            {
                Debug.LogWarning($"[BookFlip] 유효하지 않은 페이지 인덱스: {pageIndex}");
                return;
            }

            _currentPage = pageIndex;
            UpdateSprites();
            OnPageChanged?.Invoke(_currentPage);
        }

        // ─────────────────────────────────────────────
        // 공개 API — 런타임 페이지 목록 관리
        // ─────────────────────────────────────────────

        /// <summary>
        /// 페이지 목록 전체를 교체한다 (런타임에서 호출).
        /// 기존 페이지 인스턴스는 모두 정리되고, 현재 페이지는 0으로 리셋된다.
        /// </summary>
        public void SetPages(BookFlipPage[] pages)
        {
            CleanupAllDisplaySlots();

            // 기존 페이지 인스턴스 완전 파괴
            if (_pages != null)
                for (int i = 0; i < _pages.Length; i++)
                    _pages[i]?.Destroy();

            _pages = pages ?? new BookFlipPage[0];
            _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, _pages.Length - 1));

            UpdateSprites();
            OnPageChanged?.Invoke(_currentPage);
        }

        /// <summary>페이지를 끝에 추가한다</summary>
        public void AddPage(BookFlipPage page)
        {
            if (page == null) return;
            var newPages = new BookFlipPage[_pages.Length + 1];
            System.Array.Copy(_pages, newPages, _pages.Length);
            newPages[_pages.Length] = page;
            _pages = newPages;
            UpdateSprites();
        }

        /// <summary>지정 인덱스 위치에 페이지를 삽입한다</summary>
        public void InsertPage(int index, BookFlipPage page)
        {
            if (page == null) return;
            index = Mathf.Clamp(index, 0, _pages.Length);

            var newPages = new BookFlipPage[_pages.Length + 1];
            for (int i = 0; i < index; i++)               newPages[i]     = _pages[i];
            newPages[index] = page;
            for (int i = index; i < _pages.Length; i++)   newPages[i + 1] = _pages[i];

            _pages = newPages;

            // 삽입 위치 이전에 현재 페이지가 있으면 인덱스 보정
            if (_currentPage >= index) _currentPage++;

            UpdateSprites();
        }

        /// <summary>지정 인덱스 페이지를 제거한다</summary>
        public void RemovePage(int index)
        {
            if (index < 0 || index >= _pages.Length) return;

            _pages[index]?.Destroy();

            var newPages = new BookFlipPage[_pages.Length - 1];
            for (int i = 0; i < index; i++)               newPages[i]     = _pages[i];
            for (int i = index + 1; i < _pages.Length; i++) newPages[i - 1] = _pages[i];

            _pages = newPages;
            _currentPage = Mathf.Clamp(_currentPage, 0, Mathf.Max(0, _pages.Length - 1));

            UpdateSprites();
        }

        /// <summary>
        /// 특정 페이지의 인스턴스를 새로 생성하도록 표시한다.
        /// 소스(Sprite/Prefab/GameObject) 변경 후 호출하면 다음 표시 시 반영된다.
        /// 현재 화면에 표시 중인 페이지라면 즉시 갱신된다.
        /// </summary>
        public void RefreshPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _pages.Length || _pages[pageIndex] == null)
                return;

            _pages[pageIndex].RefreshInstance();

            // 현재 표시 중인 슬롯이면 즉시 갱신
            bool isVisible = (pageIndex == _leftNextDisplayIndex  ||
                              pageIndex == _rightNextDisplayIndex ||
                              pageIndex == _leftDisplayIndex      ||
                              pageIndex == _rightDisplayIndex);

            if (isVisible) UpdateSprites();
        }

        /// <summary>
        /// 모든 페이지 인스턴스를 새로 생성하도록 표시한다.
        /// 복수의 소스 변경 후 호출한다.
        /// </summary>
        public void RefreshAllPages()
        {
            if (_pages == null) return;
            for (int i = 0; i < _pages.Length; i++)
                _pages[i]?.RefreshInstance();
            UpdateSprites();
        }

        // ─────────────────────────────────────────────
        // 내부 유틸
        // ─────────────────────────────────────────────

        private Sprite GetPageSprite(int index)
        {
            if (index < 0 || index >= _pages.Length) return _background;
            if (_pages[index] == null || !_pages[index].IsValid()) return _background;
            if (_pages[index].Type == BookFlipPage.PageType.Sprite) return _pages[index].Sprite;
            return null;
        }
    }
}
