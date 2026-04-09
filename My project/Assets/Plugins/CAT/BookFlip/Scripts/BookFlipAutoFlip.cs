using UnityEngine;
using System.Collections;

namespace CAT.BookFlip
{
    /// <summary>
    /// 자동 페이지 넘김 기능을 제공하는 컴포넌트
    /// 기존 AutoFlip.cs를 개선하여 모바일 최적화 및 DOTween 활용 옵션 제공
    /// </summary>
    [RequireComponent(typeof(BookFlip))]
    public class BookFlipAutoFlip : MonoBehaviour
    {
        [Header("자동 넘김 설정")]
        [SerializeField] private BookFlip.FlipMode _mode = BookFlip.FlipMode.RightToLeft;
        [SerializeField] private float _pageFlipTime = 1f;
        [SerializeField] private float _timeBetweenPages = 1f;
        [SerializeField] private float _delayBeforeStarting = 0f;
        [SerializeField] private bool _autoStartFlip = true;

        [Header("애니메이션 설정")]
        [SerializeField] private int _animationFramesCount = 40;
        [SerializeField] private AnimationCurve _flipCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("참조")]
        [SerializeField] private BookFlip _controlledBook;

        private bool _isFlipping = false;
        private Coroutine _autoFlipCoroutine;

        // 캐싱된 값들 (모바일 최적화)
        private float _frameTime;
        private WaitForSeconds _frameWait;

        public bool IsFlipping => _isFlipping;
        public BookFlip ControlledBook => _controlledBook;

        private void Start()
        {
            if (_controlledBook == null)
                _controlledBook = GetComponent<BookFlip>();

            if (_controlledBook == null)
            {
                Debug.LogError("[BookFlipAutoFlip] BookFlip 컴포넌트를 찾을 수 없습니다.");
                enabled = false;
                return;
            }

            // OnFlip 이벤트에 리스너 등록
            _controlledBook.OnFlip.AddListener(PageFlipped);

            // 프레임 타임 캐싱
            _frameTime = _pageFlipTime / _animationFramesCount;
            _frameWait = new WaitForSeconds(_frameTime);

            if (_autoStartFlip)
                StartFlipping();
        }

        /// <summary>
        /// 페이지 넘김 완료 시 호출되는 콜백
        /// </summary>
        private void PageFlipped()
        {
            _isFlipping = false;
        }

        /// <summary>
        /// 자동 넘김 시작
        /// </summary>
        public void StartFlipping()
        {
            if (_autoFlipCoroutine != null)
                StopCoroutine(_autoFlipCoroutine);

            _autoFlipCoroutine = StartCoroutine(FlipToEndCoroutine());
        }

        /// <summary>
        /// 자동 넘김 중지
        /// </summary>
        public void StopFlipping()
        {
            if (_autoFlipCoroutine != null)
            {
                StopCoroutine(_autoFlipCoroutine);
                _autoFlipCoroutine = null;
            }
            _isFlipping = false;
        }

        /// <summary>
        /// 오른쪽 페이지 넘기기 (수동)
        /// </summary>
        public void FlipRightPage()
        {
            if (_isFlipping) return;
            if (_controlledBook.CurrentPage >= _controlledBook.TotalPageCount) return;

            _isFlipping = true;
            StartCoroutine(FlipRTLCoroutine());
        }

        /// <summary>
        /// 왼쪽 페이지 넘기기 (수동)
        /// </summary>
        public void FlipLeftPage()
        {
            if (_isFlipping) return;
            if (_controlledBook.CurrentPage <= 0) return;

            _isFlipping = true;
            StartCoroutine(FlipLTRCoroutine());
        }

        /// <summary>
        /// 끝까지 자동 넘김 코루틴
        /// </summary>
        private IEnumerator FlipToEndCoroutine()
        {
            yield return new WaitForSeconds(_delayBeforeStarting);

            switch (_mode)
            {
                case BookFlip.FlipMode.RightToLeft:
                    while (_controlledBook.CurrentPage < _controlledBook.TotalPageCount)
                    {
                        yield return FlipRTLCoroutine();
                        yield return new WaitForSeconds(_timeBetweenPages);
                    }
                    break;

                case BookFlip.FlipMode.LeftToRight:
                    while (_controlledBook.CurrentPage > 0)
                    {
                        yield return FlipLTRCoroutine();
                        yield return new WaitForSeconds(_timeBetweenPages);
                    }
                    break;
            }
        }

        /// <summary>
        /// 오른쪽에서 왼쪽으로 넘김 코루틴 (개선된 버전)
        /// </summary>
        private IEnumerator FlipRTLCoroutine()
        {
            // 캐싱된 값들
            float xc = (_controlledBook.EndBottomRight.x + _controlledBook.EndBottomLeft.x) / 2;
            float xl = ((_controlledBook.EndBottomRight.x - _controlledBook.EndBottomLeft.x) / 2) * 0.9f;
            float h = Mathf.Abs(_controlledBook.EndBottomRight.y) * 0.9f;
            float dx = (xl * 2) / _animationFramesCount;

            float x = xc + xl;
            float y = CalculateParabola(x, xc, xl, h);

            _controlledBook.DragRightPageToPoint(new Vector3(x, y, 0));

            // 애니메이션 커브를 사용한 부드러운 넘김
            for (int i = 0; i < _animationFramesCount; i++)
            {
                float t = (float)i / _animationFramesCount;
                float curveValue = _flipCurve.Evaluate(t);

                x = xc + xl - (dx * _animationFramesCount * curveValue);
                y = CalculateParabola(x, xc, xl, h);

                _controlledBook.UpdateBookRTLToPoint(new Vector3(x, y, 0));
                yield return _frameWait;
            }

            _controlledBook.ReleasePage();
        }

        /// <summary>
        /// 왼쪽에서 오른쪽으로 넘김 코루틴 (개선된 버전)
        /// </summary>
        private IEnumerator FlipLTRCoroutine()
        {
            // 캐싱된 값들
            float xc = (_controlledBook.EndBottomRight.x + _controlledBook.EndBottomLeft.x) / 2;
            float xl = ((_controlledBook.EndBottomRight.x - _controlledBook.EndBottomLeft.x) / 2) * 0.9f;
            float h = Mathf.Abs(_controlledBook.EndBottomRight.y) * 0.9f;
            float dx = (xl * 2) / _animationFramesCount;

            float x = xc - xl;
            float y = CalculateParabola(x, xc, xl, h);

            _controlledBook.DragLeftPageToPoint(new Vector3(x, y, 0));

            // 애니메이션 커브를 사용한 부드러운 넘김
            for (int i = 0; i < _animationFramesCount; i++)
            {
                float t = (float)i / _animationFramesCount;
                float curveValue = _flipCurve.Evaluate(t);

                x = xc - xl + (dx * _animationFramesCount * curveValue);
                y = CalculateParabola(x, xc, xl, h);

                _controlledBook.UpdateBookLTRToPoint(new Vector3(x, y, 0));
                yield return _frameWait;
            }

            _controlledBook.ReleasePage();
        }

        /// <summary>
        /// 포물선 y 좌표 계산
        /// y = -(h/(xl)^2) * (x-xc)^2
        /// </summary>
        private float CalculateParabola(float x, float xc, float xl, float h)
        {
            float dx = x - xc;
            return -(h / (xl * xl)) * dx * dx;
        }

        /// <summary>
        /// 특정 페이지로 자동 넘김
        /// </summary>
        public void FlipToPage(int targetPage)
        {
            if (targetPage < 0 || targetPage >= _controlledBook.TotalPageCount)
            {
                Debug.LogWarning($"[BookFlipAutoFlip] 유효하지 않은 페이지 인덱스: {targetPage}");
                return;
            }

            if (_autoFlipCoroutine != null)
                StopCoroutine(_autoFlipCoroutine);

            _autoFlipCoroutine = StartCoroutine(FlipToPageCoroutine(targetPage));
        }

        /// <summary>
        /// 특정 페이지로 넘기는 코루틴
        /// </summary>
        private IEnumerator FlipToPageCoroutine(int targetPage)
        {
            int currentPage = _controlledBook.CurrentPage;

            if (targetPage > currentPage)
            {
                // 앞으로 넘기기
                while (_controlledBook.CurrentPage < targetPage && _controlledBook.CurrentPage < _controlledBook.TotalPageCount)
                {
                    yield return FlipRTLCoroutine();
                    yield return new WaitForSeconds(_timeBetweenPages);
                }
            }
            else if (targetPage < currentPage)
            {
                // 뒤로 넘기기
                while (_controlledBook.CurrentPage > targetPage && _controlledBook.CurrentPage > 0)
                {
                    yield return FlipLTRCoroutine();
                    yield return new WaitForSeconds(_timeBetweenPages);
                }
            }
        }

        private void OnDestroy()
        {
            if (_controlledBook != null)
            {
                _controlledBook.OnFlip.RemoveListener(PageFlipped);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 프레임 수 제한
            _animationFramesCount = Mathf.Clamp(_animationFramesCount, 10, 100);

            // 시간 값 제한
            _pageFlipTime = Mathf.Max(0.1f, _pageFlipTime);
            _timeBetweenPages = Mathf.Max(0f, _timeBetweenPages);
            _delayBeforeStarting = Mathf.Max(0f, _delayBeforeStarting);

            // 프레임 타임 재계산
            if (_animationFramesCount > 0)
            {
                _frameTime = _pageFlipTime / _animationFramesCount;
            }
        }
#endif
    }
}
