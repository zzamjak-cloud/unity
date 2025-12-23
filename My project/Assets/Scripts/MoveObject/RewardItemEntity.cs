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
        // 상수 정의
        private const float BEZIER_OFFSET_MULTIPLIER = 0.3f;
        private const float DEFAULT_EFFECT_DESTROY_DELAY = 2f;
        private const float EFFECT_DESTROY_BUFFER_TIME = 0.5f;
        private const int BEZIER_POINT_COUNT = 3;
        private const float BEZIER_TWEEN_START = 0f;
        private const float BEZIER_TWEEN_END = 1f;

        private RectTransform _rectTransform;
        private Transform _cachedTransform;
        private Sequence _moveSequence;
        private RewardData _data;
        private Action<RewardItemEntity> _onArrivalCallback;
        private Vector3 _targetWorldPosition;

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
                _cachedTransform.localScale = Vector3.one * _data.SpawnStartScale;
                Tweener scaleTween = _cachedTransform.DOScale(Vector3.one * _data.MoveStartScale, _data.SpawnDuration);
                ApplyEaseToTween(scaleTween, _data.SpawnEase, _data.SpawnCustomCurve);
                scaleTween.OnComplete(() => onComplete?.Invoke());
            }
            else
            {
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
                        CreateBezierMoveTweenUI(targetLocalPoint);
                    }
                    else
                    {
                        CreateLinearMoveTweenUI(targetLocalPoint);
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
                ApplyEaseToTween(moveTween, _data.MoveEase, _data.MoveCustomCurve);
                _moveSequence.Append(moveTween);
            }
            else
            {
                Tweener moveTween = _cachedTransform.DOMove(targetPosition, _data.Duration);
                ApplyEaseToTween(moveTween, _data.MoveEase, _data.MoveCustomCurve);
                _moveSequence.Append(moveTween);
            }
        }

        /// <summary>
        /// UI RectTransform용 베지어 이동 트윈 생성
        /// </summary>
        private void CreateBezierMoveTweenUI(Vector2 targetLocalPoint)
        {
            Vector3[] path = GenerateBezierPathUI(_rectTransform.anchoredPosition, targetLocalPoint);
            Tween moveTween = DOTween.To(
                () => BEZIER_TWEEN_START,
                t =>
                {
                    Vector3 position = CalculateBezierPoint(t, path);
                    _rectTransform.anchoredPosition = position;
                    float scaleValue = Mathf.Lerp(_data.MoveStartScale, _data.MoveEndScale, t);
                    _cachedTransform.localScale = Vector3.one * scaleValue;
                },
                BEZIER_TWEEN_END,
                _data.Duration
            );
            ApplyEaseToTween(moveTween, _data.MoveEase, _data.MoveCustomCurve);
            _moveSequence.Append(moveTween);
        }

        /// <summary>
        /// UI RectTransform용 직선 이동 트윈 생성
        /// </summary>
        private void CreateLinearMoveTweenUI(Vector2 targetLocalPoint)
        {
            Tweener moveTween = _rectTransform.DOAnchorPos(targetLocalPoint, _data.Duration);
            ApplyEaseToTween(moveTween, _data.MoveEase, _data.MoveCustomCurve);
            _moveSequence.Append(moveTween);
            _moveSequence.Join(_cachedTransform.DOScale(Vector3.one * _data.MoveEndScale, _data.Duration)
                .SetEase(Ease.Linear));
        }

        /// <summary>
        /// Tween에 Ease 설정 적용 (중복 코드 제거)
        /// </summary>
        private void ApplyEaseToTween(Tween tween, Ease ease, AnimationCurve customCurve)
        {
            if (ease == Ease.INTERNAL_Custom)
            {
                if (customCurve != null && customCurve.length > 0)
                {
                    tween.SetEase(customCurve);
                }
                else
                {
                    tween.SetEase(Ease.Linear);
                }
            }
            else
            {
                tween.SetEase(ease);
            }
        }

        /// <summary>
        /// 베지어 경로 생성 (UI용 - Vector3로 반환)
        /// </summary>
        private Vector3[] GenerateBezierPathUI(Vector2 start, Vector2 end)
        {
            Vector2 midPoint = Vector2.Lerp(start, end, _data.BezierControlPointOffset);
            float offsetDistance = Vector2.Distance(start, end) * BEZIER_OFFSET_MULTIPLIER;
            midPoint += Vector2.up * offsetDistance;
            return new Vector3[] { start, midPoint, end };
        }

        /// <summary>
        /// 베지어 곡선 상의 점 계산 (Quadratic Bezier)
        /// </summary>
        private Vector3 CalculateBezierPoint(float t, Vector3[] points)
        {
            if (points.Length == BEZIER_POINT_COUNT)
            {
                // 3점 베지어 곡선 (Quadratic): P(t) = (1-t)²P₀ + 2(1-t)tP₁ + t²P₂
                float u = 1f - t;
                float tt = t * t;
                float uu = u * u;

                return uu * points[0] + 2f * u * t * points[1] + tt * points[2];
            }
            else
            {
                return Vector3.Lerp(points[0], points[points.Length - 1], t);
            }
        }

        /// <summary>
        /// 베지어 경로 생성 (일반 Transform용 - Vector3)
        /// </summary>
        private Vector3[] GenerateBezierPath(Vector3 start, Vector3 end)
        {
            Vector3 midPoint = Vector3.Lerp(start, end, _data.BezierControlPointOffset);
            float offsetDistance = Vector3.Distance(start, end) * BEZIER_OFFSET_MULTIPLIER;
            midPoint += Vector3.up * offsetDistance;
            return new Vector3[] { start, midPoint, end };
        }

        /// <summary>
        /// 도착 시 호출
        /// </summary>
        private void OnArrival()
        {
            PlayArrivalEffect();
            PlayArrivalSound();
            _onArrivalCallback?.Invoke(this);
        }

        /// <summary>
        /// 도착 이펙트 재생
        /// </summary>
        private void PlayArrivalEffect()
        {
            if (_data.ArrivalEffect == null) return;

            Vector3 effectPosition = GetCurrentWorldPosition();
            GameObject effect = Instantiate(_data.ArrivalEffect);
            SetupEffectTransform(effect, effectPosition);
            PlayParticleSystems(effect);
            DestroyEffectAfterDelay(effect);
        }

        /// <summary>
        /// 이펙트 Transform 설정 (UI 좌표계 지원)
        /// </summary>
        private void SetupEffectTransform(GameObject effect, Vector3 worldPosition)
        {
            if (_rectTransform != null)
            {
                RectTransform parentRect = _rectTransform.parent as RectTransform;
                Canvas canvas = parentRect?.GetComponentInParent<Canvas>();
                
                if (canvas != null)
                {
                    effect.transform.SetParent(canvas.transform, false);
                    RectTransform effectRect = effect.GetComponent<RectTransform>();
                    
                    if (effectRect != null)
                    {
                        SetEffectUIPosition(effectRect, canvas, worldPosition);
                    }
                    else
                    {
                        effect.transform.position = worldPosition;
                    }
                }
                else
                {
                    effect.transform.position = worldPosition;
                }
            }
            else
            {
                effect.transform.position = worldPosition;
            }
        }

        /// <summary>
        /// 이펙트 UI 위치 설정
        /// </summary>
        private void SetEffectUIPosition(RectTransform effectRect, Canvas canvas, Vector3 worldPosition)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera uiCamera = canvas.worldCamera ?? Camera.main;
            
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(uiCamera, worldPosition),
                uiCamera,
                out Vector2 localPoint
            );
            effectRect.anchoredPosition = localPoint;
        }

        /// <summary>
        /// 파티클 시스템 재생
        /// </summary>
        private void PlayParticleSystems(GameObject effect)
        {
            ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>();
            foreach (var particle in particles)
            {
                if (!particle.isPlaying)
                {
                    particle.Play();
                }
            }
        }

        /// <summary>
        /// 이펙트 지연 제거
        /// </summary>
        private void DestroyEffectAfterDelay(GameObject effect)
        {
            ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>();
            float destroyDelay = CalculateEffectDestroyDelay(particles);
            Destroy(effect, destroyDelay);
        }

        /// <summary>
        /// 이펙트 제거 지연 시간 계산
        /// </summary>
        private float CalculateEffectDestroyDelay(ParticleSystem[] particles)
        {
            if (particles.Length == 0)
            {
                return DEFAULT_EFFECT_DESTROY_DELAY;
            }

            float maxDuration = 0f;
            foreach (var particle in particles)
            {
                float duration = particle.main.duration + particle.main.startLifetime.constantMax;
                maxDuration = Mathf.Max(maxDuration, duration);
            }
            
            return maxDuration + EFFECT_DESTROY_BUFFER_TIME;
        }

        /// <summary>
        /// 도착 사운드 재생
        /// </summary>
        private void PlayArrivalSound()
        {
            if (_data.ArrivalSound == null) return;

            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.PlayOneShot(_data.ArrivalSound);
            }
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
