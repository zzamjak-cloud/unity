using UnityEngine;
using DG.Tweening;
using System;

namespace MoveObject
{
    /// <summary>
    /// 개별 보상 오브젝트의 이동 로직을 담당하는 컴포넌트
    /// </summary>
    public class RewardItemEntity : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Transform _cachedTransform;
        private Sequence _moveSequence;
        private RewardData _data;
        private Action<RewardItemEntity> _onArrivalCallback;
        private Vector3 _targetWorldPosition; // 도착 위치 저장

        public RewardData Data => _data;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _cachedTransform = transform;
        }

        /// <summary>
        /// 보상 아이템 초기화
        /// </summary>
        public void Initialize(RewardData data)
        {
            _data = data;
        }

        /// <summary>
        /// 등장 연출과 함께 시작 위치로 이동
        /// </summary>
        public void Spawn(Vector3 worldPosition, Action onComplete = null)
        {
            // UI RectTransform은 anchoredPosition을 사용
            if (_rectTransform != null)
            {
                // 부모의 RectTransform을 기준으로 로컬 좌표로 변환
                RectTransform parentRect = _rectTransform.parent as RectTransform;
                if (parentRect != null)
                {
                    Vector2 localPoint;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect,
                        RectTransformUtility.WorldToScreenPoint(null, worldPosition),
                        null,
                        out localPoint
                    );
                    _rectTransform.anchoredPosition = localPoint;
                }
                else
                {
                    _cachedTransform.position = worldPosition;
                }
            }
            else
            {
                _cachedTransform.position = worldPosition;
            }

            if (_data.UseSpawnAnimation)
            {
                // 등장 시작 스케일 → 이동 시작 스케일
                _cachedTransform.localScale = Vector3.one * _data.SpawnStartScale;

                Tweener scaleTween = _cachedTransform.DOScale(Vector3.one * _data.MoveStartScale, _data.SpawnDuration);

                // INTERNAL_Custom이면 커스텀 곡선 사용, 아니면 Ease 사용
                if (_data.SpawnEase == Ease.INTERNAL_Custom)
                {
                    if (_data.SpawnCustomCurve != null && _data.SpawnCustomCurve.length > 0)
                    {
                        scaleTween.SetEase(_data.SpawnCustomCurve);
                    }
                    else
                    {
                        // 커스텀 선택했지만 곡선이 없으면 기본 Linear 사용
                        scaleTween.SetEase(Ease.Linear);
                    }
                }
                else
                {
                    scaleTween.SetEase(_data.SpawnEase);
                }

                scaleTween.OnComplete(() => onComplete?.Invoke());
            }
            else
            {
                // 애니메이션 없이 바로 이동 시작 스케일
                _cachedTransform.localScale = Vector3.one * _data.MoveStartScale;
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// 목적지로 이동 시작
        /// </summary>
        public void MoveTo(Vector3 targetWorldPosition, Action<RewardItemEntity> onArrival = null)
        {
            _onArrivalCallback = onArrival;
            _targetWorldPosition = targetWorldPosition; // 도착 위치 저장

            // 기존 시퀀스 정리
            _moveSequence?.Kill();
            _moveSequence = DOTween.Sequence();

            // UI RectTransform은 anchoredPosition 기반 이동 사용
            if (_rectTransform != null)
            {
                RectTransform parentRect = _rectTransform.parent as RectTransform;
                if (parentRect != null)
                {
                    // 목표 월드 좌표를 로컬 좌표로 변환
                    Vector2 targetLocalPoint;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect,
                        RectTransformUtility.WorldToScreenPoint(null, targetWorldPosition),
                        null,
                        out targetLocalPoint
                    );

                    if (_data.UseBezierPath)
                    {
                        // 베지어 곡선을 사용한 이동 (anchoredPosition 기준)
                        Vector3[] path = GenerateBezierPathUI(_rectTransform.anchoredPosition, targetLocalPoint);

                        // DOPath 대신 수동으로 베지어 경로 구현
                        Vector3 startPos = _rectTransform.anchoredPosition;
                        Tween moveTween = DOTween.To(
                            () => 0f,
                            t =>
                            {
                                Vector3 position = CalculateBezierPoint(t, path);
                                _rectTransform.anchoredPosition = position;

                                // 스케일 변화: 이동 시작 스케일 → 이동 도착 스케일
                                float scaleValue = Mathf.Lerp(_data.MoveStartScale, _data.MoveEndScale, t);
                                _cachedTransform.localScale = Vector3.one * scaleValue;
                            },
                            1f,
                            _data.Duration
                        );

                        // INTERNAL_Custom이면 커스텀 곡선 사용, 아니면 Ease 사용
                        if (_data.MoveEase == Ease.INTERNAL_Custom)
                        {
                            if (_data.MoveCustomCurve != null && _data.MoveCustomCurve.length > 0)
                            {
                                moveTween.SetEase(_data.MoveCustomCurve);
                            }
                            else
                            {
                                moveTween.SetEase(Ease.Linear);
                            }
                        }
                        else
                        {
                            moveTween.SetEase(_data.MoveEase);
                        }

                        _moveSequence.Append(moveTween);
                    }
                    else
                    {
                        // 직선 이동 (anchoredPosition 기준)
                        Tweener moveTween = _rectTransform.DOAnchorPos(targetLocalPoint, _data.Duration);

                        // INTERNAL_Custom이면 커스텀 곡선 사용, 아니면 Ease 사용
                        if (_data.MoveEase == Ease.INTERNAL_Custom)
                        {
                            if (_data.MoveCustomCurve != null && _data.MoveCustomCurve.length > 0)
                            {
                                moveTween.SetEase(_data.MoveCustomCurve);
                            }
                            else
                            {
                                moveTween.SetEase(Ease.Linear);
                            }
                        }
                        else
                        {
                            moveTween.SetEase(_data.MoveEase);
                        }

                        _moveSequence.Append(moveTween);

                        // 스케일 변화: 이동 시작 스케일 → 이동 도착 스케일
                        _moveSequence.Join(_cachedTransform.DOScale(Vector3.one * _data.MoveEndScale, _data.Duration)
                            .SetEase(Ease.Linear));
                    }
                }
                else
                {
                    // 부모가 RectTransform이 아닌 경우 폴백
                    FallbackMoveTo(targetWorldPosition);
                }
            }
            else
            {
                // RectTransform이 아닌 경우 폴백
                FallbackMoveTo(targetWorldPosition);
            }

            _moveSequence.OnComplete(OnArrival);
        }

        /// <summary>
        /// 폴백: 일반 Transform 이동 (RectTransform이 아닌 경우)
        /// </summary>
        private void FallbackMoveTo(Vector3 targetPosition)
        {
            if (_data.UseBezierPath)
            {
                Vector3[] path = GenerateBezierPath(_cachedTransform.position, targetPosition);
                Tweener moveTween = _cachedTransform.DOPath(path, _data.Duration, PathType.CatmullRom);

                // INTERNAL_Custom이면 커스텀 곡선 사용, 아니면 Ease 사용
                if (_data.MoveEase == Ease.INTERNAL_Custom)
                {
                    if (_data.MoveCustomCurve != null && _data.MoveCustomCurve.length > 0)
                    {
                        moveTween.SetEase(_data.MoveCustomCurve);
                    }
                    else
                    {
                        moveTween.SetEase(Ease.Linear);
                    }
                }
                else
                {
                    moveTween.SetEase(_data.MoveEase);
                }

                _moveSequence.Append(moveTween);
            }
            else
            {
                Tweener moveTween = _cachedTransform.DOMove(targetPosition, _data.Duration);

                // INTERNAL_Custom이면 커스텀 곡선 사용, 아니면 Ease 사용
                if (_data.MoveEase == Ease.INTERNAL_Custom)
                {
                    if (_data.MoveCustomCurve != null && _data.MoveCustomCurve.length > 0)
                    {
                        moveTween.SetEase(_data.MoveCustomCurve);
                    }
                    else
                    {
                        moveTween.SetEase(Ease.Linear);
                    }
                }
                else
                {
                    moveTween.SetEase(_data.MoveEase);
                }

                _moveSequence.Append(moveTween);
            }
        }

        /// <summary>
        /// 베지어 경로 생성 (UI용 - Vector3로 반환)
        /// </summary>
        private Vector3[] GenerateBezierPathUI(Vector2 start, Vector2 end)
        {
            Vector2 midPoint = Vector2.Lerp(start, end, _data.BezierControlPointOffset);

            // 수직 방향으로 제어점 오프셋 추가
            float offsetDistance = Vector2.Distance(start, end) * 0.3f;
            midPoint += Vector2.up * offsetDistance;

            return new Vector3[] { start, midPoint, end };
        }

        /// <summary>
        /// 베지어 곡선 상의 점 계산 (Catmull-Rom)
        /// </summary>
        private Vector3 CalculateBezierPoint(float t, Vector3[] points)
        {
            if (points.Length == 3)
            {
                // 3점 베지어 곡선 (Quadratic)
                float u = 1 - t;
                float tt = t * t;
                float uu = u * u;

                Vector3 p = uu * points[0]; // (1-t)^2 * P0
                p += 2 * u * t * points[1]; // 2(1-t)t * P1
                p += tt * points[2];        // t^2 * P2

                return p;
            }
            else
            {
                // 기본 선형 보간
                return Vector3.Lerp(points[0], points[points.Length - 1], t);
            }
        }

        /// <summary>
        /// 베지어 경로 생성 (일반 Transform용 - Vector3)
        /// </summary>
        private Vector3[] GenerateBezierPath(Vector3 start, Vector3 end)
        {
            Vector3 midPoint = Vector3.Lerp(start, end, _data.BezierControlPointOffset);

            // 수직 방향으로 제어점 오프셋 추가
            float offsetDistance = Vector3.Distance(start, end) * 0.3f;
            midPoint += Vector3.up * offsetDistance;

            return new Vector3[] { start, midPoint, end };
        }

        /// <summary>
        /// 도착 시 호출
        /// </summary>
        private void OnArrival()
        {
            // 도착 이펙트 재생
            if (_data.ArrivalEffect != null)
            {
                // 현재 아이템의 실제 도착 위치 사용 (가장 정확함)
                Vector3 effectPosition = GetCurrentWorldPosition();
                
                GameObject effect = Instantiate(_data.ArrivalEffect);
                
                // UI Canvas가 있으면 Canvas를 부모로 설정하고 UI 좌표로 변환
                if (_rectTransform != null)
                {
                    RectTransform parentRect = _rectTransform.parent as RectTransform;
                    if (parentRect != null)
                    {
                        Canvas canvas = parentRect.GetComponentInParent<Canvas>();
                        if (canvas != null)
                        {
                            effect.transform.SetParent(canvas.transform, false);
                            
                            // UI 좌표로 변환
                            RectTransform effectRect = effect.GetComponent<RectTransform>();
                            if (effectRect != null)
                            {
                                // 월드 좌표를 UI 로컬 좌표로 변환
                                Vector2 localPoint;
                                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                                Camera uiCamera = canvas.worldCamera ?? Camera.main;
                                
                                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                    canvasRect,
                                    RectTransformUtility.WorldToScreenPoint(uiCamera, effectPosition),
                                    uiCamera,
                                    out localPoint
                                );
                                effectRect.anchoredPosition = localPoint;
                            }
                            else
                            {
                                // RectTransform이 없으면 일반 Transform으로 위치 설정
                                effect.transform.position = effectPosition;
                            }
                        }
                        else
                        {
                            // Canvas가 없으면 월드 좌표로 설정
                            effect.transform.position = effectPosition;
                        }
                    }
                    else
                    {
                        // 부모가 없으면 월드 좌표로 설정
                        effect.transform.position = effectPosition;
                    }
                }
                else
                {
                    // RectTransform이 없으면 월드 좌표로 설정
                    effect.transform.position = effectPosition;
                }
                
                // 파티클 시스템이 있으면 재생
                ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>();
                foreach (var particle in particles)
                {
                    if (!particle.isPlaying)
                    {
                        particle.Play();
                    }
                }
                
                // 파티클이 없으면 기본 2초 후 제거, 있으면 파티클 재생 시간에 맞춰 제거
                float destroyDelay = 2f;
                if (particles.Length > 0)
                {
                    float maxDuration = 0f;
                    foreach (var particle in particles)
                    {
                        float duration = particle.main.duration + particle.main.startLifetime.constantMax;
                        if (duration > maxDuration)
                        {
                            maxDuration = duration;
                        }
                    }
                    destroyDelay = maxDuration + 0.5f; // 여유 시간 추가
                }
                
                Destroy(effect, destroyDelay);
            }

            // 도착 사운드 재생
            if (_data.ArrivalSound != null)
            {
                // AudioSource가 있다면 사용, 없다면 추후 사운드 매니저와 연동
                AudioSource audioSource = GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    audioSource.PlayOneShot(_data.ArrivalSound);
                }
            }

            // 콜백 호출
            _onArrivalCallback?.Invoke(this);
        }

        /// <summary>
        /// 현재 위치의 정확한 월드 좌표 가져오기
        /// </summary>
        private Vector3 GetCurrentWorldPosition()
        {
            if (_rectTransform != null)
            {
                // RectTransform의 월드 좌표 계산
                Vector3[] corners = new Vector3[4];
                _rectTransform.GetWorldCorners(corners);
                return (corners[0] + corners[2]) * 0.5f; // 중앙점 반환
            }
            else
            {
                return _cachedTransform.position;
            }
        }

        /// <summary>
        /// 오브젝트 풀로 반환 전 정리
        /// </summary>
        public void Release()
        {
            _moveSequence?.Kill();
            _moveSequence = null;
            _onArrivalCallback = null;
            _targetWorldPosition = Vector3.zero;
            _cachedTransform.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            _moveSequence?.Kill();
        }
    }
}
