using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

namespace CAT.BookFlip
{
    /// <summary>
    /// 책넘기기 효과를 제공하는 메인 컨트롤러
    /// 기존 Book.cs를 개선하여 다양한 페이지 타입 지원
    /// </summary>
    [ExecuteInEditMode]
    public class BookFlip : MonoBehaviour
    {
        public enum FlipMode
        {
            RightToLeft,
            LeftToRight
        }

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

        // 캐싱된 Transform들
        private RectTransform _clippingPlaneRT;
        private RectTransform _nextPageClipRT;
        private RectTransform _shadowRT;
        private RectTransform _shadowLTRRT;
        private RectTransform _leftRT;
        private RectTransform _leftNextRT;
        private RectTransform _rightRT;
        private RectTransform _rightNextRT;

        // Prefab/GameObject 페이지 인스턴스 캐싱
        private GameObject _leftPageInstance;
        private GameObject _leftNextPageInstance;
        private GameObject _rightPageInstance;
        private GameObject _rightNextPageInstance;

        // 현재 페이지 인덱스 (오른쪽 페이지 기준)
        [SerializeField] private int _currentPage = 0;

        // 곡선 계산 관련 변수들
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

        // 프로퍼티
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = Mathf.Clamp(value, 0, _pages.Length);
                    UpdateSprites();
                    OnPageChanged?.Invoke(_currentPage);
                }
            }
        }

        public int TotalPageCount => _pages.Length;
        public bool Interactable { get => _interactable; set => _interactable = value; }
        public Vector3 EndBottomLeft => _ebl;
        public Vector3 EndBottomRight => _ebr;
        public float Height => _bookPanel.rect.height;

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
        }

        /// <summary>
        /// 컴포넌트 캐싱
        /// </summary>
        private void CacheComponents()
        {
            if (_clippingPlane != null) _clippingPlaneRT = _clippingPlane.GetComponent<RectTransform>();
            if (_nextPageClip != null) _nextPageClipRT = _nextPageClip.GetComponent<RectTransform>();
            if (_shadow != null) _shadowRT = _shadow.GetComponent<RectTransform>();
            if (_shadowLTR != null) _shadowLTRRT = _shadowLTR.GetComponent<RectTransform>();
            if (_left != null) _leftRT = _left.GetComponent<RectTransform>();
            if (_leftNext != null) _leftNextRT = _leftNext.GetComponent<RectTransform>();
            if (_right != null) _rightRT = _right.GetComponent<RectTransform>();
            if (_rightNext != null) _rightNextRT = _rightNext.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 페이지 초기화
        /// </summary>
        private void InitializePages()
        {
            _left.gameObject.SetActive(false);
            _right.gameObject.SetActive(false);
            UpdateSprites();
        }

        /// <summary>
        /// UI 요소 설정
        /// </summary>
        private void SetupUIElements()
        {
            float pageWidth = _bookPanel.rect.width / 2.0f;
            float pageHeight = _bookPanel.rect.height;

            _nextPageClipRT.sizeDelta = new Vector2(pageWidth, pageHeight + pageHeight * 2);
            _clippingPlaneRT.sizeDelta = new Vector2(pageWidth * 2 + pageHeight, pageHeight + pageHeight * 2);

            // hypotenuse (대각선) 페이지 길이
            float hyp = Mathf.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
            float shadowPageHeight = pageWidth / 2 + hyp;

            _shadowRT.sizeDelta = new Vector2(pageWidth, shadowPageHeight);
            _shadowRT.pivot = new Vector2(1, (pageWidth / 2) / shadowPageHeight);

            _shadowLTRRT.sizeDelta = new Vector2(pageWidth, shadowPageHeight);
            _shadowLTRRT.pivot = new Vector2(0, (pageWidth / 2) / shadowPageHeight);
        }

        /// <summary>
        /// Container 하이어라키 구조 설정
        /// </summary>
        private void SetupContainerHierarchy()
        {
            // PageContainer와 HotSpotContainer가 모두 있는 경우에만 설정
            if (_pageContainer != null && _pageContainer != _bookPanel && _hotSpotContainer != null)
            {
                // 모든 페이지 요소를 PageContainer의 자식으로 이동
                if (_clippingPlane != null && _clippingPlane.transform.parent != _pageContainer)
                    _clippingPlane.transform.SetParent(_pageContainer, true);
                if (_nextPageClip != null && _nextPageClip.transform.parent != _pageContainer)
                    _nextPageClip.transform.SetParent(_pageContainer, true);
                if (_shadow != null && _shadow.transform.parent != _pageContainer)
                    _shadow.transform.SetParent(_pageContainer, true);
                if (_shadowLTR != null && _shadowLTR.transform.parent != _pageContainer)
                    _shadowLTR.transform.SetParent(_pageContainer, true);
                if (_left != null && _left.transform.parent != _pageContainer)
                    _left.transform.SetParent(_pageContainer, true);
                if (_leftNext != null && _leftNext.transform.parent != _pageContainer)
                    _leftNext.transform.SetParent(_pageContainer, true);
                if (_right != null && _right.transform.parent != _pageContainer)
                    _right.transform.SetParent(_pageContainer, true);
                if (_rightNext != null && _rightNext.transform.parent != _pageContainer)
                    _rightNext.transform.SetParent(_pageContainer, true);

                // PageContainer를 BookPanel의 자식으로
                if (_pageContainer.parent != _bookPanel)
                    _pageContainer.SetParent(_bookPanel, true);

                // HotSpotContainer를 BookPanel의 자식이면서 PageContainer 위로
                if (_hotSpotContainer.parent != _bookPanel)
                    _hotSpotContainer.SetParent(_bookPanel, true);

                // 순서: PageContainer가 먼저, HotSpotContainer가 나중(최상위)
                _pageContainer.SetSiblingIndex(0);
                _hotSpotContainer.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 핫스팟을 항상 최상위로 설정
        /// </summary>
        private void SetupHotSpots()
        {
            // HotSpotContainer가 있으면 사용, 없으면 BookPanel 직접 사용
            RectTransform hotSpotParent = _hotSpotContainer != null ? _hotSpotContainer : _bookPanel;

            if (_leftHotSpot != null)
            {
                _leftHotSpot.SetParent(hotSpotParent, true);
            }

            if (_rightHotSpot != null)
            {
                _rightHotSpot.SetParent(hotSpotParent, true);
            }

            // HotSpotContainer 자체를 BookPanel의 최상위로 설정
            if (_hotSpotContainer != null)
            {
                _hotSpotContainer.SetParent(_bookPanel, true);
                _hotSpotContainer.SetAsLastSibling();
            }
            else
            {
                // Container가 없으면 개별 핫스팟을 최상위로
                if (_leftHotSpot != null)
                    _leftHotSpot.SetAsLastSibling();
                if (_rightHotSpot != null)
                    _rightHotSpot.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 곡선의 중요 포인트들 계산
        /// </summary>
        private void CalcCurlCriticalPoints()
        {
            _sb = new Vector3(0, -_bookPanel.rect.height / 2);
            _ebr = new Vector3(_bookPanel.rect.width / 2, -_bookPanel.rect.height / 2);
            _ebl = new Vector3(-_bookPanel.rect.width / 2, -_bookPanel.rect.height / 2);
            _st = new Vector3(0, _bookPanel.rect.height / 2);
            _radius1 = Vector2.Distance(_sb, _ebr);

            float pageWidth = _bookPanel.rect.width / 2.0f;
            float pageHeight = _bookPanel.rect.height;
            _radius2 = Mathf.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
        }

        /// <summary>
        /// 마우스 스크린 좌표를 BookPanel 로컬 좌표로 변환
        /// </summary>
        private Vector3 TransformPoint(Vector3 mouseScreenPos)
        {
            if (_canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                Vector3 mouseWorldPos = _canvas.worldCamera.ScreenToWorldPoint(
                    new Vector3(mouseScreenPos.x, mouseScreenPos.y, _canvas.planeDistance));
                Vector2 localPos = _bookPanel.InverseTransformPoint(mouseWorldPos);
                return localPos;
            }
            else if (_canvas.renderMode == RenderMode.WorldSpace)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Vector3 globalEBR = transform.TransformPoint(_ebr);
                Vector3 globalEBL = transform.TransformPoint(_ebl);
                Vector3 globalSt = transform.TransformPoint(_st);
                Plane p = new Plane(globalEBR, globalEBL, globalSt);

                if (p.Raycast(ray, out float distance))
                {
                    Vector2 localPos = _bookPanel.InverseTransformPoint(ray.GetPoint(distance));
                    return localPos;
                }
                return Vector3.zero;
            }
            else
            {
                // Screen Space Overlay
                Vector2 localPos = _bookPanel.InverseTransformPoint(mouseScreenPos);
                return localPos;
            }
        }

        private void Update()
        {
            if (_pageDragging && _interactable)
            {
                UpdateBook();
            }

            // 핫스팟을 매 프레임 최상위로 유지 (페이지 넘김 중 부모 변경에도 대응)
            EnsureHotSpotsOnTop();
        }

        /// <summary>
        /// 핫스팟이 항상 최상위에 있도록 보장
        /// </summary>
        private void EnsureHotSpotsOnTop()
        {
            // PageContainer와 HotSpotContainer 순서 강제
            if (_pageContainer != null && _pageContainer != _bookPanel && _hotSpotContainer != null)
            {
                // PageContainer가 BookPanel의 자식인지 확인
                if (_pageContainer.parent != _bookPanel)
                    _pageContainer.SetParent(_bookPanel, true);

                // HotSpotContainer가 BookPanel의 자식인지 확인
                if (_hotSpotContainer.parent != _bookPanel)
                    _hotSpotContainer.SetParent(_bookPanel, true);

                // PageContainer는 인덱스 0, HotSpotContainer는 마지막
                int pageContainerIndex = _pageContainer.GetSiblingIndex();
                int hotSpotContainerIndex = _hotSpotContainer.GetSiblingIndex();

                // HotSpotContainer가 PageContainer보다 앞에 있으면 재배치
                if (hotSpotContainerIndex < pageContainerIndex)
                {
                    _pageContainer.SetSiblingIndex(0);
                    _hotSpotContainer.SetAsLastSibling();
                }
                else if (hotSpotContainerIndex != _bookPanel.childCount - 1)
                {
                    // HotSpotContainer가 최상위가 아니면 최상위로
                    _hotSpotContainer.SetAsLastSibling();
                }

                // Container 내부의 핫스팟 부모 확인
                if (_leftHotSpot != null && _leftHotSpot.parent != _hotSpotContainer)
                    _leftHotSpot.SetParent(_hotSpotContainer, true);
                if (_rightHotSpot != null && _rightHotSpot.parent != _hotSpotContainer)
                    _rightHotSpot.SetParent(_hotSpotContainer, true);
            }
            // HotSpotContainer만 있는 경우
            else if (_hotSpotContainer != null)
            {
                // Container가 BookPanel의 자식이 아니거나 최상위가 아니면 재배치
                if (_hotSpotContainer.parent != _bookPanel)
                    _hotSpotContainer.SetParent(_bookPanel, true);

                int containerIndex = _hotSpotContainer.GetSiblingIndex();
                int lastIndex = _bookPanel.childCount - 1;
                if (containerIndex != lastIndex)
                    _hotSpotContainer.SetAsLastSibling();

                // Container 내부의 핫스팟 부모 확인
                if (_leftHotSpot != null && _leftHotSpot.parent != _hotSpotContainer)
                    _leftHotSpot.SetParent(_hotSpotContainer, true);
                if (_rightHotSpot != null && _rightHotSpot.parent != _hotSpotContainer)
                    _rightHotSpot.SetParent(_hotSpotContainer, true);
            }
            // Container 없이 개별 핫스팟만 있는 경우
            else
            {
                RectTransform targetParent = _pageContainer != _bookPanel ? _pageContainer : _bookPanel;

                if (_leftHotSpot != null)
                {
                    if (_leftHotSpot.parent != targetParent)
                        _leftHotSpot.SetParent(targetParent, true);

                    int leftIndex = _leftHotSpot.GetSiblingIndex();
                    int lastIndex = targetParent.childCount - 1;
                    if (leftIndex != lastIndex && leftIndex != lastIndex - 1)
                        _leftHotSpot.SetAsLastSibling();
                }

                if (_rightHotSpot != null)
                {
                    if (_rightHotSpot.parent != targetParent)
                        _rightHotSpot.SetParent(targetParent, true);

                    int rightIndex = _rightHotSpot.GetSiblingIndex();
                    int lastIndex = targetParent.childCount - 1;
                    if (rightIndex != lastIndex && rightIndex != lastIndex - 1)
                        _rightHotSpot.SetAsLastSibling();
                }
            }
        }

        /// <summary>
        /// 책 업데이트 (드래그 중)
        /// </summary>
        private void UpdateBook()
        {
            _f = Vector3.Lerp(_f, TransformPoint(Input.mousePosition), Time.deltaTime * 10);

            if (_mode == FlipMode.RightToLeft)
                UpdateBookRTLToPoint(_f);
            else
                UpdateBookLTRToPoint(_f);
        }

        /// <summary>
        /// 왼쪽에서 오른쪽으로 넘김 업데이트
        /// </summary>
        public void UpdateBookLTRToPoint(Vector3 followLocation)
        {
            _mode = FlipMode.LeftToRight;
            _f = followLocation;

            _shadowLTR.transform.SetParent(_clippingPlane.transform, true);
            _shadowLTR.transform.localPosition = Vector3.zero;
            _shadowLTR.transform.localEulerAngles = Vector3.zero;

            _left.transform.SetParent(_clippingPlane.transform, true);
            _right.transform.SetParent(_pageContainer.transform, true);
            _right.transform.localEulerAngles = Vector3.zero;
            _leftNext.transform.SetParent(_pageContainer.transform, true);

            _c = CalcCPosition(followLocation);
            float clipAngle = CalcClipAngle(_c, _ebl, out Vector3 t1);
            clipAngle = (clipAngle + 180) % 180;

            _clippingPlane.transform.localEulerAngles = new Vector3(0, 0, clipAngle - 90);
            _clippingPlane.transform.position = _bookPanel.TransformPoint(t1);

            _left.transform.position = _bookPanel.TransformPoint(_c);
            float cT1Angle = Mathf.Atan2(t1.y - _c.y, t1.x - _c.x) * Mathf.Rad2Deg;
            _left.transform.localEulerAngles = new Vector3(0, 0, cT1Angle - 90 - clipAngle);

            _nextPageClip.transform.localEulerAngles = new Vector3(0, 0, clipAngle - 90);
            _nextPageClip.transform.position = _bookPanel.TransformPoint(t1);

            _leftNext.transform.SetParent(_nextPageClip.transform, true);
            _right.transform.SetParent(_clippingPlane.transform, true);
            _right.transform.SetAsFirstSibling();

            _shadowLTR.rectTransform.SetParent(_left.rectTransform, true);
        }

        /// <summary>
        /// 오른쪽에서 왼쪽으로 넘김 업데이트
        /// </summary>
        public void UpdateBookRTLToPoint(Vector3 followLocation)
        {
            _mode = FlipMode.RightToLeft;
            _f = followLocation;

            _shadow.transform.SetParent(_clippingPlane.transform, true);
            _shadow.transform.localPosition = Vector3.zero;
            _shadow.transform.localEulerAngles = Vector3.zero;

            _right.transform.SetParent(_clippingPlane.transform, true);
            _left.transform.SetParent(_pageContainer.transform, true);
            _left.transform.localEulerAngles = Vector3.zero;
            _rightNext.transform.SetParent(_pageContainer.transform, true);

            _c = CalcCPosition(followLocation);
            float clipAngle = CalcClipAngle(_c, _ebr, out Vector3 t1);

            if (clipAngle > -90) clipAngle += 180;

            _clippingPlaneRT.pivot = new Vector2(1, 0.35f);
            _clippingPlane.transform.localEulerAngles = new Vector3(0, 0, clipAngle + 90);
            _clippingPlane.transform.position = _bookPanel.TransformPoint(t1);

            _right.transform.position = _bookPanel.TransformPoint(_c);
            float cT1Angle = Mathf.Atan2(t1.y - _c.y, t1.x - _c.x) * Mathf.Rad2Deg;
            _right.transform.localEulerAngles = new Vector3(0, 0, cT1Angle - (clipAngle + 90));

            _nextPageClip.transform.localEulerAngles = new Vector3(0, 0, clipAngle + 90);
            _nextPageClip.transform.position = _bookPanel.TransformPoint(t1);

            _rightNext.transform.SetParent(_nextPageClip.transform, true);
            _left.transform.SetParent(_clippingPlane.transform, true);
            _left.transform.SetAsFirstSibling();

            _shadow.rectTransform.SetParent(_right.rectTransform, true);
        }

        /// <summary>
        /// 클리핑 각도 계산
        /// </summary>
        private float CalcClipAngle(Vector3 c, Vector3 bookCorner, out Vector3 t1)
        {
            Vector3 t0 = (c + bookCorner) / 2;
            float t0CornerAngle = Mathf.Atan2(bookCorner.y - t0.y, bookCorner.x - t0.x);

            float t1X = t0.x - (bookCorner.y - t0.y) * Mathf.Tan(t0CornerAngle);
            t1X = NormalizeT1X(t1X, bookCorner, _sb);
            t1 = new Vector3(t1X, _sb.y, 0);

            float t0T1Angle = Mathf.Atan2(t1.y - t0.y, t1.x - t0.x) * Mathf.Rad2Deg;
            return t0T1Angle;
        }

        /// <summary>
        /// T1 X 좌표 정규화
        /// </summary>
        private float NormalizeT1X(float t1, Vector3 corner, Vector3 sb)
        {
            if (t1 > sb.x && sb.x > corner.x)
                return sb.x;
            if (t1 < sb.x && sb.x < corner.x)
                return sb.x;
            return t1;
        }

        /// <summary>
        /// C 위치 계산
        /// </summary>
        private Vector3 CalcCPosition(Vector3 followLocation)
        {
            _f = followLocation;
            float fSbAngle = Mathf.Atan2(_f.y - _sb.y, _f.x - _sb.x);
            Vector3 r1 = new Vector3(
                _radius1 * Mathf.Cos(fSbAngle),
                _radius1 * Mathf.Sin(fSbAngle),
                0) + _sb;

            float fSbDistance = Vector2.Distance(_f, _sb);
            Vector3 c = fSbDistance < _radius1 ? _f : r1;

            float fStAngle = Mathf.Atan2(c.y - _st.y, c.x - _st.x);
            Vector3 r2 = new Vector3(
                _radius2 * Mathf.Cos(fStAngle),
                _radius2 * Mathf.Sin(fStAngle),
                0) + _st;

            float cStDistance = Vector2.Distance(c, _st);
            if (cStDistance > _radius2)
                c = r2;

            return c;
        }

        /// <summary>
        /// 오른쪽 페이지를 특정 지점으로 드래그
        /// </summary>
        public void DragRightPageToPoint(Vector3 point)
        {
            if (_currentPage >= _pages.Length) return;

            _pageDragging = true;
            _mode = FlipMode.RightToLeft;
            _f = point;

            OnFlipStart?.Invoke();
            DisablePageInteraction();

            _nextPageClipRT.pivot = new Vector2(0, 0.12f);
            _clippingPlaneRT.pivot = new Vector2(1, 0.35f);

            // Left 페이지 설정
            _left.gameObject.SetActive(true);
            _leftRT.pivot = new Vector2(0, 0);
            _left.transform.position = _rightNext.transform.position;
            _left.transform.eulerAngles = Vector3.zero;
            SetupPageDisplay(_left, _leftRT, _currentPage, ref _leftPageInstance);
            _left.transform.SetAsFirstSibling();

            // Right 페이지 설정
            _right.gameObject.SetActive(true);
            _right.transform.position = _rightNext.transform.position;
            _right.transform.eulerAngles = Vector3.zero;
            SetupPageDisplay(_right, _rightRT, _currentPage + 1, ref _rightPageInstance);

            // RightNext 페이지 설정
            SetupPageDisplay(_rightNext, _rightNextRT, _currentPage + 2, ref _rightNextPageInstance);
            _leftNext.transform.SetAsFirstSibling();

            if (_enableShadowEffect)
                _shadow.gameObject.SetActive(true);

            UpdateBookRTLToPoint(_f);
        }

        /// <summary>
        /// 왼쪽 페이지를 특정 지점으로 드래그
        /// </summary>
        public void DragLeftPageToPoint(Vector3 point)
        {
            if (_currentPage <= 0) return;

            _pageDragging = true;
            _mode = FlipMode.LeftToRight;
            _f = point;

            OnFlipStart?.Invoke();
            DisablePageInteraction();

            _nextPageClipRT.pivot = new Vector2(1, 0.12f);
            _clippingPlaneRT.pivot = new Vector2(0, 0.35f);

            // Right 페이지 설정
            _right.gameObject.SetActive(true);
            _right.transform.position = _leftNext.transform.position;
            _right.transform.eulerAngles = Vector3.zero;
            SetupPageDisplay(_right, _rightRT, _currentPage - 1, ref _rightPageInstance);
            _right.transform.SetAsFirstSibling();

            // Left 페이지 설정
            _left.gameObject.SetActive(true);
            _leftRT.pivot = new Vector2(1, 0);
            _left.transform.position = _leftNext.transform.position;
            _left.transform.eulerAngles = Vector3.zero;
            SetupPageDisplay(_left, _leftRT, _currentPage - 2, ref _leftPageInstance);

            // LeftNext 페이지 설정
            SetupPageDisplay(_leftNext, _leftNextRT, _currentPage - 3, ref _leftNextPageInstance);
            _rightNext.transform.SetAsFirstSibling();

            if (_enableShadowEffect)
                _shadowLTR.gameObject.SetActive(true);

            UpdateBookLTRToPoint(_f);
        }

        /// <summary>
        /// 마우스 드래그 - 오른쪽 페이지
        /// </summary>
        public void OnMouseDragRightPage()
        {
            if (_interactable)
                DragRightPageToPoint(TransformPoint(Input.mousePosition));
        }

        /// <summary>
        /// 마우스 드래그 - 왼쪽 페이지
        /// </summary>
        public void OnMouseDragLeftPage()
        {
            if (_interactable)
                DragLeftPageToPoint(TransformPoint(Input.mousePosition));
        }

        /// <summary>
        /// 마우스 릴리즈
        /// </summary>
        public void OnMouseRelease()
        {
            if (_interactable)
                ReleasePage();
        }

        /// <summary>
        /// 페이지 릴리즈 (드래그 종료)
        /// </summary>
        public void ReleasePage()
        {
            if (!_pageDragging) return;

            _pageDragging = false;

            float distanceToLeft = Vector2.Distance(_c, _ebl);
            float distanceToRight = Vector2.Distance(_c, _ebr);

            if (distanceToRight < distanceToLeft && _mode == FlipMode.RightToLeft)
                TweenBack();
            else if (distanceToRight > distanceToLeft && _mode == FlipMode.LeftToRight)
                TweenBack();
            else
                TweenForward();
        }

        /// <summary>
        /// 스프라이트 업데이트
        /// </summary>
        private void UpdateSprites()
        {
            SetupPageDisplay(_leftNext, _leftNextRT, _currentPage - 1, ref _leftNextPageInstance);
            SetupPageDisplay(_rightNext, _rightNextRT, _currentPage, ref _rightNextPageInstance);
        }

        /// <summary>
        /// 페이지 인스턴스 정리 헬퍼 메서드
        /// </summary>
        private void CleanupPageInstance(ref GameObject pageInstance)
        {
            if (pageInstance != null)
            {
                Destroy(pageInstance);
                pageInstance = null;
            }
        }

        /// <summary>
        /// 페이지 디스플레이 설정 (Sprite/Prefab/GameObject 모두 지원)
        /// </summary>
        private void SetupPageDisplay(Image targetImage, RectTransform targetRT, int pageIndex, ref GameObject pageInstance)
        {
            // 기존 인스턴스 정리
            CleanupPageInstance(ref pageInstance);

            // targetRT 하위의 모든 페이지 인스턴스 정리 (중복 방지)
            CleanupChildPageInstances(targetRT);

            // 범위 체크
            if (pageIndex < 0 || pageIndex >= _pages.Length)
            {
                targetImage.sprite = _background;
                targetImage.enabled = true;
                return;
            }

            BookFlipPage page = _pages[pageIndex];
            if (page == null || !page.IsValid())
            {
                targetImage.sprite = _background;
                targetImage.enabled = true;
                return;
            }

            // 페이지 타입에 따라 처리
            switch (page.Type)
            {
                case BookFlipPage.PageType.Sprite:
                    // Sprite 타입: Image의 sprite만 변경
                    targetImage.sprite = page.Sprite;
                    targetImage.enabled = true;
                    break;

                case BookFlipPage.PageType.Prefab:
                case BookFlipPage.PageType.GameObject:
                    // Prefab/GameObject 타입: 실제 GameObject 인스턴스 생성
                    targetImage.enabled = false; // Image 숨김

                    // BookFlipPage를 통해 인스턴스 생성
                    Image pageImage = page.GetOrCreateImage(targetRT, $"Page_{pageIndex}");
                    if (pageImage != null)
                    {
                        pageInstance = pageImage.gameObject;

                        // RectTransform 설정
                        RectTransform pageRT = pageInstance.GetComponent<RectTransform>();
                        if (pageRT != null)
                        {
                            pageRT.anchorMin = Vector2.zero;
                            pageRT.anchorMax = Vector2.one;
                            pageRT.sizeDelta = Vector2.zero;
                            pageRT.anchoredPosition = Vector2.zero;
                            pageRT.localScale = Vector3.one;
                        }

                        // 인터랙션 초기 비활성화
                        page.SetInteractable(false);
                    }
                    break;
            }
        }

        /// <summary>
        /// targetRT 하위의 모든 페이지 인스턴스 정리
        /// </summary>
        private void CleanupChildPageInstances(RectTransform targetRT)
        {
            if (targetRT == null) return;

            // "Page_" 로 시작하는 자식 오브젝트 모두 파괴
            for (int i = targetRT.childCount - 1; i >= 0; i--)
            {
                Transform child = targetRT.GetChild(i);
                if (child.name.StartsWith("Page_"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// 페이지 인덱스에 해당하는 스프라이트 가져오기 (Sprite 타입 전용)
        /// </summary>
        private Sprite GetPageSprite(int index)
        {
            if (index < 0 || index >= _pages.Length)
                return _background;

            if (_pages[index] == null || !_pages[index].IsValid())
                return _background;

            // Sprite 타입만 직접 스프라이트 반환
            if (_pages[index].Type == BookFlipPage.PageType.Sprite)
                return _pages[index].Sprite;

            // Prefab/GameObject 타입은 null 반환 (SetupPageDisplay에서 처리)
            return null;
        }

        /// <summary>
        /// 앞으로 넘기기
        /// </summary>
        public void TweenForward()
        {
            Vector3 target = _mode == FlipMode.RightToLeft ? _ebl : _ebr;

            if (_currentCoroutine != null)
                StopCoroutine(_currentCoroutine);

            _currentCoroutine = StartCoroutine(TweenTo(target, 0.15f, () => { Flip(); }));
        }

        /// <summary>
        /// 페이지 넘김 완료
        /// </summary>
        private void Flip()
        {
            if (_mode == FlipMode.RightToLeft)
                _currentPage += 2;
            else
                _currentPage -= 2;

            // Left/Right 페이지 인스턴스 정리
            CleanupPageInstance(ref _leftPageInstance);
            CleanupPageInstance(ref _rightPageInstance);

            _leftNext.transform.SetParent(_pageContainer.transform, true);
            _left.transform.SetParent(_pageContainer.transform, true);
            _left.gameObject.SetActive(false);

            _right.gameObject.SetActive(false);
            _right.transform.SetParent(_pageContainer.transform, true);
            _rightNext.transform.SetParent(_pageContainer.transform, true);

            UpdateSprites();

            _shadow.gameObject.SetActive(false);
            _shadowLTR.gameObject.SetActive(false);

            EnablePageInteraction();

            // 핫스팟을 최상위로 다시 설정
            SetupHotSpots();

            OnFlip?.Invoke();
            OnPageChanged?.Invoke(_currentPage);
            OnFlipEnd?.Invoke();
        }

        /// <summary>
        /// 뒤로 되돌리기
        /// </summary>
        public void TweenBack()
        {
            if (_currentCoroutine != null)
                StopCoroutine(_currentCoroutine);

            if (_mode == FlipMode.RightToLeft)
            {
                _currentCoroutine = StartCoroutine(TweenTo(_ebr, 0.15f, () =>
                {
                    // Left/Right 페이지 인스턴스 정리
                    CleanupPageInstance(ref _leftPageInstance);
                    CleanupPageInstance(ref _rightPageInstance);

                    UpdateSprites();
                    _rightNext.transform.SetParent(_pageContainer.transform);
                    _right.transform.SetParent(_pageContainer.transform);
                    _left.gameObject.SetActive(false);
                    _right.gameObject.SetActive(false);
                    _pageDragging = false;

                    EnablePageInteraction();

                    // 핫스팟을 최상위로 다시 설정
                    SetupHotSpots();

                    OnFlipEnd?.Invoke();
                }));
            }
            else
            {
                _currentCoroutine = StartCoroutine(TweenTo(_ebl, 0.15f, () =>
                {
                    // Left/Right 페이지 인스턴스 정리
                    CleanupPageInstance(ref _leftPageInstance);
                    CleanupPageInstance(ref _rightPageInstance);

                    UpdateSprites();
                    _leftNext.transform.SetParent(_pageContainer.transform);
                    _left.transform.SetParent(_pageContainer.transform);
                    _left.gameObject.SetActive(false);
                    _right.gameObject.SetActive(false);
                    _pageDragging = false;

                    EnablePageInteraction();

                    // 핫스팟을 최상위로 다시 설정
                    SetupHotSpots();

                    OnFlipEnd?.Invoke();
                }));
            }
        }

        /// <summary>
        /// 특정 지점으로 트윈
        /// </summary>
        private IEnumerator TweenTo(Vector3 to, float duration, System.Action onFinish)
        {
            int steps = (int)(duration / 0.025f);
            Vector3 displacement = (to - _f) / steps;

            for (int i = 0; i < steps - 1; i++)
            {
                if (_mode == FlipMode.RightToLeft)
                    UpdateBookRTLToPoint(_f + displacement);
                else
                    UpdateBookLTRToPoint(_f + displacement);

                yield return new WaitForSeconds(0.025f);
            }

            onFinish?.Invoke();
        }

        /// <summary>
        /// 페이지 인터랙션 비활성화
        /// </summary>
        private void DisablePageInteraction()
        {
            // TODO: BookFlipPage의 SetInteractable 활용
            for (int i = 0; i < _pages.Length; i++)
            {
                if (_pages[i] != null)
                    _pages[i].SetInteractable(false);
            }
        }

        /// <summary>
        /// 페이지 인터랙션 활성화
        /// </summary>
        private void EnablePageInteraction()
        {
            // 현재 보이는 페이지만 활성화
            if (_currentPage - 1 >= 0 && _currentPage - 1 < _pages.Length && _pages[_currentPage - 1] != null)
                _pages[_currentPage - 1].SetInteractable(true);

            if (_currentPage >= 0 && _currentPage < _pages.Length && _pages[_currentPage] != null)
                _pages[_currentPage].SetInteractable(true);
        }

        /// <summary>
        /// 다음 페이지로 이동
        /// </summary>
        public void NextPage()
        {
            if (_currentPage < _pages.Length - 1)
            {
                DragRightPageToPoint(_ebr);
                TweenForward();
            }
        }

        /// <summary>
        /// 이전 페이지로 이동
        /// </summary>
        public void PreviousPage()
        {
            if (_currentPage > 0)
            {
                DragLeftPageToPoint(_ebl);
                TweenForward();
            }
        }

        /// <summary>
        /// 특정 페이지로 이동
        /// </summary>
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

        private void OnDestroy()
        {
            // 현재 사용 중인 페이지 인스턴스들 정리
            if (_leftPageInstance != null) Destroy(_leftPageInstance);
            if (_leftNextPageInstance != null) Destroy(_leftNextPageInstance);
            if (_rightPageInstance != null) Destroy(_rightPageInstance);
            if (_rightNextPageInstance != null) Destroy(_rightNextPageInstance);

            // 런타임에 생성된 페이지들 정리
            if (_pages != null)
            {
                for (int i = 0; i < _pages.Length; i++)
                {
                    _pages[i]?.Destroy();
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 에디터에서 변경사항 반영
            if (_bookPanel != null)
            {
                CalcCurlCriticalPoints();
            }
        }
#endif
    }
}
