using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

namespace CAT.UI
{
    /// <summary>
    /// TMP 텍스트 글자별 애니메이션
    /// - 각 글자를 독립적으로 애니메이션 (Appear, Loop, Disappear)
    /// - DOTween 기반 시퀀스 관리
    /// - 프리셋 시스템 지원
    /// - 모바일 최적화: Sequence 재사용, GC Alloc 최소화
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(20)]  // TMPCurve(10) 이후 실행
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("CAT/UI/TMP Character Animation")]
    public class TMPCharacterAnimation : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector 설정
        // ─────────────────────────────────────────────

        [Header("Animation Settings")]
        [Tooltip("애니메이션 프리셋")]
        [SerializeField]
        private TMPCharacterAnimationPreset _preset;

        [Tooltip("OnEnable 시 자동 재생")]
        [SerializeField]
        private bool _playOnEnable = true;

        [Tooltip("최초 실행 딜레이 (초) - 0이면 1프레임 후 자동 실행")]
        [SerializeField, Range(0f, 5f)]
        private float _initialDelay = 0f;

        [Tooltip("각 글자 간 딜레이 (초)")]
        [SerializeField, Range(0f, 0.5f)]
        private float _characterDelay = 0.05f;

        [Header("Appear Animation")]
        [Tooltip("등장 애니메이션 활성화")]
        [SerializeField]
        private bool _enableAppear = true;

        [Tooltip("상대 위치 사용 (오프셋 위치에서 시작 → 원래 위치로 이동)")]
        [SerializeField]
        private bool _appearRelative = true;

        [Tooltip("시작 위치 오프셋 (Relative: 현재+값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _appearPosition = new Vector3(0, 50, 0);

        [Tooltip("시작 스케일 (Relative: 현재×값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _appearScale = new Vector3(0.5f, 0.5f, 1);

        [Tooltip("시작 회전 오프셋 (Relative: 현재+값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _appearRotation = Vector3.zero;

        [Tooltip("시작 알파값 (0~1, 원래 위치에서는 1.0)")]
        [SerializeField, Range(0f, 1f)]
        private float _appearAlpha = 0f;

        [Tooltip("등장 애니메이션 시간 (초)")]
        [SerializeField]
        private float _appearDuration = 0.5f;

        [Tooltip("등장 이징 타입")]
        [SerializeField]
        private Ease _appearEase = Ease.OutBack;

        [Tooltip("커스텀 이징 곡선 사용")]
        [SerializeField]
        private bool _appearUseCustomCurve = false;

        [Tooltip("커스텀 이징 곡선 (Use Custom Curve 활성화 시)")]
        [SerializeField]
        private AnimationCurve _appearCustomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Loop Animation")]
        [Tooltip("반복 애니메이션 활성화")]
        [SerializeField]
        private bool _enableLoop = false;

        [Tooltip("상대 위치 사용 (현재 위치 기준)")]
        [SerializeField]
        private bool _loopRelative = true;

        [Tooltip("위치값")]
        [SerializeField]
        private Vector3 _loopPosition = new Vector3(0, 20, 0);

        [Tooltip("스케일값")]
        [SerializeField]
        private Vector3 _loopScale = Vector3.one;

        [Tooltip("회전값")]
        [SerializeField]
        private Vector3 _loopRotation = Vector3.zero;

        [Tooltip("반복 애니메이션 시간 (초)")]
        [SerializeField]
        private float _loopDuration = 1f;

        [Tooltip("반복 이징 타입")]
        [SerializeField]
        private Ease _loopEase = Ease.InOutSine;

        [Tooltip("커스텀 이징 곡선 사용")]
        [SerializeField]
        private bool _loopUseCustomCurve = false;

        [Tooltip("커스텀 이징 곡선")]
        [SerializeField]
        private AnimationCurve _loopCustomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("반복 횟수 (-1 = 무한)")]
        [SerializeField]
        private int _loopCount = -1;

        [Tooltip("반복 타입 (Yoyo: 왕복 반복, Restart: 처음부터 반복)")]
        [SerializeField]
        private LoopType _loopType = LoopType.Yoyo;

        [Header("Disappear Animation")]
        [Tooltip("사라짐 애니메이션 활성화")]
        [SerializeField]
        private bool _enableDisappear = false;

        [Tooltip("상대 위치 사용 (원래 위치에서 시작 → 오프셋 위치로 이동)")]
        [SerializeField]
        private bool _disappearRelative = true;

        [Tooltip("목표 위치 오프셋 (Relative: 현재+값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _disappearPosition = new Vector3(0, -50, 0);

        [Tooltip("목표 스케일 (Relative: 현재×값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _disappearScale = new Vector3(0.5f, 0.5f, 1);

        [Tooltip("목표 회전 오프셋 (Relative: 현재+값, Absolute: 절대값)")]
        [SerializeField]
        private Vector3 _disappearRotation = Vector3.zero;

        [Tooltip("알파값 (0~1)")]
        [SerializeField, Range(0f, 1f)]
        private float _disappearAlpha = 0f;

        [Tooltip("사라짐 애니메이션 시간 (초)")]
        [SerializeField]
        private float _disappearDuration = 0.5f;

        [Tooltip("사라짐 이징 타입")]
        [SerializeField]
        private Ease _disappearEase = Ease.InBack;

        [Tooltip("커스텀 이징 곡선 사용")]
        [SerializeField]
        private bool _disappearUseCustomCurve = false;

        [Tooltip("커스텀 이징 곡선")]
        [SerializeField]
        private AnimationCurve _disappearCustomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // ─────────────────────────────────────────────
        // 캐싱
        // ─────────────────────────────────────────────

        private TMP_Text _tmpText;
        private Sequence[] _sequences;
        private Vector3[] _originalPositions;
        private Vector3[][] _originalVertices;
        private Vector3[][] _originalVerticesSecondFace;
        private Color32[][] _originalColors;
        private Color32[][] _originalColorsSecondFace;
        private bool _isPlaying = false;
        private bool _isPlayingInProgress = false;
        private bool _hasStarted = false;

        // ─────────────────────────────────────────────
        // Public Properties
        // ─────────────────────────────────────────────

        public bool IsPlaying => _isPlaying;

        public TMPCharacterAnimationPreset Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                if (value != null)
                {
                    ApplyPreset(value);
                }
            }
        }

        // ─────────────────────────────────────────────
        // 라이프사이클
        // ─────────────────────────────────────────────

        private void Awake()
        {
            CacheComponents();
        }

        private IEnumerator Start()
        {
            if (_playOnEnable && Application.isPlaying)
            {
                enabled = false;

                if (_initialDelay > 0f)
                {
                    yield return new WaitForSeconds(_initialDelay);
                }
                else
                {
                    yield return null;
                }

                _hasStarted = true;
                enabled = true;
            }
            else
            {
                _hasStarted = true;
            }
        }

        private void OnEnable()
        {
            CacheComponents();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

            if (_playOnEnable && Application.isPlaying)
            {
                if (!_hasStarted) return;
                Canvas.willRenderCanvases += PlayOnce;
            }
        }

        private void PlayOnce()
        {
            Canvas.willRenderCanvases -= PlayOnce;
            Play();
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= PlayOnce;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            KillAllSequences();
            RestoreOriginalMesh();
            _sequences = null;
            _originalPositions = null;
            _originalVertices = null;
            _originalVerticesSecondFace = null;
            _originalColors = null;
            _originalColorsSecondFace = null;
            _isPlaying = false;
        }

        private void OnTextChanged(Object obj)
        {
            if (_isPlayingInProgress) return;
            if (obj == _tmpText && _isPlaying) Restart();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheComponents();
        }
#endif

        // ─────────────────────────────────────────────
        // Private Methods
        // ─────────────────────────────────────────────

        private void CacheComponents()
        {
            if (_tmpText == null) _tmpText = GetComponent<TMP_Text>();
        }

        private Vector3 GetCharacterCenter(TMP_CharacterInfo charInfo)
        {
            if (!charInfo.isVisible) return Vector3.zero;

            int vertexIndex = charInfo.vertexIndex;
            int materialIndex = charInfo.materialReferenceIndex;
            Vector3[] vertices = _tmpText.textInfo.meshInfo[materialIndex].vertices;

            return new Vector3(
                (vertices[vertexIndex].x + vertices[vertexIndex + 2].x) / 2f,
                charInfo.baseLine,
                0f
            );
        }

        private void TransformCharacterVertices(int charIndex, Vector3 position,
            Vector3 scale, Vector3 rotation, float alpha)
        {
            TransformCharacterVerticesInternal(_tmpText, charIndex, position, scale, rotation, alpha, false);

            var outlineEffect = GetComponent<TMPOutlineEffect>();
            if (outlineEffect != null && outlineEffect.EnableSecondFace)
            {
                var secondFaceText = outlineEffect.GetSecondFaceText();
                if (secondFaceText != null)
                {
                    TransformCharacterVerticesInternal(secondFaceText, charIndex, position, scale, rotation, alpha, true);
                }
            }
        }

        private void TransformCharacterVerticesInternal(TMP_Text tmpText, int charIndex,
            Vector3 position, Vector3 scale, Vector3 rotation, float alpha, bool isSecondFace)
        {
            if (tmpText == null) return;

            var charInfo = tmpText.textInfo.characterInfo[charIndex];
            if (!charInfo.isVisible) return;

            int vertexIndex = charInfo.vertexIndex;
            int materialIndex = charInfo.materialReferenceIndex;
            Vector3[] vertices = tmpText.textInfo.meshInfo[materialIndex].vertices;
            Color32[] colors = tmpText.textInfo.meshInfo[materialIndex].colors32;

            Quaternion rot = Quaternion.Euler(rotation);

            Vector3[][] originalVertices = isSecondFace ? _originalVerticesSecondFace : _originalVertices;
            Color32[][] originalColors = isSecondFace ? _originalColorsSecondFace : _originalColors;

            if (originalVertices == null || materialIndex >= originalVertices.Length) return;
            if (originalVertices[materialIndex] == null) return;
            if (originalColors == null || materialIndex >= originalColors.Length) return;
            if (originalColors[materialIndex] == null) return;

            Vector3 center = new Vector3(
                (originalVertices[materialIndex][vertexIndex].x + originalVertices[materialIndex][vertexIndex + 2].x) / 2f,
                charInfo.baseLine,
                0f
            );

            for (int i = 0; i < 4; i++)
            {
                int idx = vertexIndex + i;
                Vector3 v = originalVertices[materialIndex][idx] - center;

                if (rotation != Vector3.zero) v = rot * v;
                v = Vector3.Scale(v, scale);
                v += position;
                vertices[idx] = v + center;

                if (idx < originalColors[materialIndex].Length)
                {
                    Color32 originalColor = originalColors[materialIndex][idx];
                    Color32 c = colors[idx];
                    c.r = originalColor.r;
                    c.g = originalColor.g;
                    c.b = originalColor.b;
                    c.a = (byte)(originalColor.a * alpha);
                    colors[idx] = c;
                }
            }
        }

        private Tween AnimateCharacter(int charIndex,
            Vector3 fromPos, Vector3 toPos,
            Vector3 fromScale, Vector3 toScale,
            Vector3 fromRot, Vector3 toRot,
            float fromAlpha, float toAlpha,
            float duration, Ease ease,
            AnimationCurve customCurve = null)
        {
            var tween = DOTween.To(() => 0f, (t) =>
            {
                float easedT = customCurve != null ? customCurve.Evaluate(t) : t;

                Vector3 currentPos = Vector3.Lerp(fromPos, toPos, easedT);
                Vector3 currentScale = Vector3.Lerp(fromScale, toScale, easedT);
                Vector3 currentRot = Vector3.Lerp(fromRot, toRot, easedT);
                float currentAlpha = Mathf.Lerp(fromAlpha, toAlpha, easedT);

                TransformCharacterVertices(charIndex, currentPos, currentScale, currentRot, currentAlpha);

                _tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

                var outlineEffect = GetComponent<TMPOutlineEffect>();
                if (outlineEffect != null && outlineEffect.EnableSecondFace)
                {
                    var secondFaceText = outlineEffect.GetSecondFaceText();
                    if (secondFaceText != null)
                    {
                        secondFaceText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                    }
                }
            }, 1f, duration);

            if (customCurve == null)
            {
                tween.SetEase(ease);
            }
            else
            {
                tween.SetEase(Ease.Linear);
            }

            return tween;
        }

        private Sequence CreateCharacterSequence(int charIndex)
        {
            var seq = DOTween.Sequence();
            float delay = charIndex * _characterDelay;

            Vector3 originalPos = _originalPositions[charIndex];
            Vector3 originalScale = Vector3.one;
            Vector3 originalRot = Vector3.zero;
            float originalAlpha = 1f;

            // Appear
            if (_enableAppear)
            {
                Vector3 appearFromPos, appearToPos, appearFromScale, appearToScale, appearFromRot, appearToRot;
                float appearFromAlpha, appearToAlpha;

                CalculateFromTo(true, _appearRelative,
                    _appearPosition, originalPos,
                    _appearScale, originalScale,
                    _appearRotation, originalRot,
                    _appearAlpha, originalAlpha,
                    out appearFromPos, out appearToPos,
                    out appearFromScale, out appearToScale,
                    out appearFromRot, out appearToRot,
                    out appearFromAlpha, out appearToAlpha);

                AnimationCurve curve = _appearUseCustomCurve ? _appearCustomCurve : null;
                seq.Append(AnimateCharacter(charIndex,
                    appearFromPos, appearToPos,
                    appearFromScale, appearToScale,
                    appearFromRot, appearToRot,
                    appearFromAlpha, appearToAlpha,
                    _appearDuration, _appearEase, curve));
            }

            // Loop
            if (_enableLoop)
            {
                Vector3 loopFromPos, loopToPos, loopFromScale, loopToScale, loopFromRot, loopToRot;
                float loopFromAlpha, loopToAlpha;

                CalculateFromTo(false, _loopRelative,
                    _loopPosition, originalPos,
                    _loopScale, originalScale,
                    _loopRotation, originalRot,
                    1f, originalAlpha,
                    out loopFromPos, out loopToPos,
                    out loopFromScale, out loopToScale,
                    out loopFromRot, out loopToRot,
                    out loopFromAlpha, out loopToAlpha);

                var loopSeq = DOTween.Sequence();
                AnimationCurve loopCurve = _loopUseCustomCurve ? _loopCustomCurve : null;
                loopSeq.Append(AnimateCharacter(charIndex,
                    loopFromPos, loopToPos,
                    loopFromScale, loopToScale,
                    loopFromRot, loopToRot,
                    loopFromAlpha, loopToAlpha,
                    _loopDuration, _loopEase, loopCurve));
                loopSeq.SetLoops(_loopCount, _loopType);
                seq.Append(loopSeq);
            }

            // Disappear
            if (_enableDisappear)
            {
                Vector3 disappearFromPos, disappearToPos, disappearFromScale, disappearToScale, disappearFromRot, disappearToRot;
                float disappearFromAlpha, disappearToAlpha;

                CalculateFromTo(false, _disappearRelative,
                    _disappearPosition, originalPos,
                    _disappearScale, originalScale,
                    _disappearRotation, originalRot,
                    _disappearAlpha, originalAlpha,
                    out disappearFromPos, out disappearToPos,
                    out disappearFromScale, out disappearToScale,
                    out disappearFromRot, out disappearToRot,
                    out disappearFromAlpha, out disappearToAlpha);

                AnimationCurve disappearCurve = _disappearUseCustomCurve ? _disappearCustomCurve : null;
                seq.Append(AnimateCharacter(charIndex,
                    disappearFromPos, disappearToPos,
                    disappearFromScale, disappearToScale,
                    disappearFromRot, disappearToRot,
                    disappearFromAlpha, disappearToAlpha,
                    _disappearDuration, _disappearEase, disappearCurve));
            }

            seq.SetDelay(delay);
            seq.SetAutoKill(false);
            return seq;
        }

        private void CalculateFromTo(bool from, bool relative,
            Vector3 inputPos, Vector3 originalPos,
            Vector3 inputScale, Vector3 originalScale,
            Vector3 inputRot, Vector3 originalRot,
            float inputAlpha, float originalAlpha,
            out Vector3 fromPos, out Vector3 toPos,
            out Vector3 fromScale, out Vector3 toScale,
            out Vector3 fromRot, out Vector3 toRot,
            out float fromAlpha, out float toAlpha)
        {
            if (from)
            {
                if (relative)
                {
                    fromPos = inputPos;
                    fromScale = Vector3.Scale(originalScale, inputScale);
                    fromRot = originalRot + inputRot;
                }
                else
                {
                    fromPos = inputPos - originalPos;
                    fromScale = inputScale;
                    fromRot = inputRot;
                }
                fromAlpha = inputAlpha;

                toPos = Vector3.zero;
                toScale = originalScale;
                toRot = originalRot;
                toAlpha = originalAlpha;
            }
            else
            {
                fromPos = Vector3.zero;
                fromScale = originalScale;
                fromRot = originalRot;
                fromAlpha = originalAlpha;

                if (relative)
                {
                    toPos = inputPos;
                    toScale = Vector3.Scale(originalScale, inputScale);
                    toRot = originalRot + inputRot;
                }
                else
                {
                    toPos = inputPos - originalPos;
                    toScale = inputScale;
                    toRot = inputRot;
                }
                toAlpha = inputAlpha;
            }
        }

        private void UpdateAllVertexData()
        {
            _tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

            var outlineEffect = GetComponent<TMPOutlineEffect>();
            if (outlineEffect != null && outlineEffect.EnableSecondFace)
            {
                var secondFaceText = outlineEffect.GetSecondFaceText();
                if (secondFaceText != null)
                {
                    secondFaceText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                }
            }
        }

        private void KillAllSequences()
        {
            if (_sequences != null)
            {
                foreach (var seq in _sequences)
                {
                    if (seq != null && seq.IsActive()) seq.Kill();
                }
                _sequences = null;
            }
        }

        private void RestoreOriginalMesh()
        {
            if (_tmpText == null) return;

            if (_originalVertices != null && _originalColors != null)
            {
                for (int i = 0; i < _tmpText.textInfo.meshInfo.Length; i++)
                {
                    if (i < _originalVertices.Length && _originalVertices[i] != null)
                    {
                        var vertices = _tmpText.textInfo.meshInfo[i].vertices;
                        for (int j = 0; j < vertices.Length && j < _originalVertices[i].Length; j++)
                        {
                            vertices[j] = _originalVertices[i][j];
                        }
                    }

                    if (i < _originalColors.Length && _originalColors[i] != null)
                    {
                        var colors = _tmpText.textInfo.meshInfo[i].colors32;
                        for (int j = 0; j < colors.Length && j < _originalColors[i].Length; j++)
                        {
                            colors[j] = _originalColors[i][j];
                        }
                    }
                }

                _tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

                var outlineEffect = GetComponent<TMPOutlineEffect>();
                if (outlineEffect != null && outlineEffect.EnableSecondFace)
                {
                    var secondFaceText = outlineEffect.GetSecondFaceText();
                    if (secondFaceText != null && _originalVerticesSecondFace != null && _originalColorsSecondFace != null)
                    {
                        for (int i = 0; i < secondFaceText.textInfo.meshInfo.Length; i++)
                        {
                            if (i < _originalVerticesSecondFace.Length && _originalVerticesSecondFace[i] != null)
                            {
                                var vertices = secondFaceText.textInfo.meshInfo[i].vertices;
                                for (int j = 0; j < vertices.Length && j < _originalVerticesSecondFace[i].Length; j++)
                                {
                                    vertices[j] = _originalVerticesSecondFace[i][j];
                                }
                            }

                            if (i < _originalColorsSecondFace.Length && _originalColorsSecondFace[i] != null)
                            {
                                var colors = secondFaceText.textInfo.meshInfo[i].colors32;
                                for (int j = 0; j < colors.Length && j < _originalColorsSecondFace[i].Length; j++)
                                {
                                    colors[j] = _originalColorsSecondFace[i][j];
                                }
                            }
                        }

                        secondFaceText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                    }
                }
            }
            else
            {
                _tmpText.ForceMeshUpdate();
            }
        }

        // ─────────────────────────────────────────────
        // Public Methods
        // ─────────────────────────────────────────────

        public void Play()
        {
            if (_tmpText == null) return;

            if (_isPlayingInProgress)
            {
                Debug.LogWarning("[TMPCharAnim] Play() 이미 진행 중 - 재진입 차단");
                return;
            }

            _isPlayingInProgress = true;

            KillAllSequences();

            _tmpText.SetVerticesDirty();
            _tmpText.ForceMeshUpdate();

            var outlineEffect = GetComponent<TMPOutlineEffect>();
            TMP_Text secondFaceText = null;
            if (outlineEffect != null && outlineEffect.EnableSecondFace)
            {
                secondFaceText = outlineEffect.GetSecondFaceText();
                if (secondFaceText != null)
                {
                    secondFaceText.text = _tmpText.text;
                    secondFaceText.SetVerticesDirty();
                    secondFaceText.ForceMeshUpdate();
                    Canvas.ForceUpdateCanvases();

                    if (secondFaceText.textInfo.characterCount != _tmpText.textInfo.characterCount)
                    {
                        Debug.LogError($"[TMPCharAnim] Second Face 재생성 후에도 불일치! 부모:{_tmpText.textInfo.characterCount}, Second:{secondFaceText.textInfo.characterCount}");
                    }
                }
            }

            int charCount = _tmpText.textInfo.characterCount;
            if (charCount == 0)
            {
                _isPlayingInProgress = false;
                return;
            }

            _isPlaying = true;

            _sequences = new Sequence[charCount];
            _originalPositions = new Vector3[charCount];

            _originalVertices = new Vector3[_tmpText.textInfo.meshInfo.Length][];
            for (int i = 0; i < _tmpText.textInfo.meshInfo.Length; i++)
            {
                Vector3[] vertices = _tmpText.textInfo.meshInfo[i].vertices;
                _originalVertices[i] = new Vector3[vertices.Length];
                for (int j = 0; j < vertices.Length; j++)
                {
                    _originalVertices[i][j] = vertices[j];
                }
            }

            _originalColors = new Color32[_tmpText.textInfo.meshInfo.Length][];
            for (int i = 0; i < _tmpText.textInfo.meshInfo.Length; i++)
            {
                Color32[] colors = _tmpText.textInfo.meshInfo[i].colors32;
                _originalColors[i] = new Color32[colors.Length];
                for (int j = 0; j < colors.Length; j++)
                {
                    _originalColors[i][j] = colors[j];
                }
            }

            if (secondFaceText != null)
            {
                _originalVerticesSecondFace = new Vector3[secondFaceText.textInfo.meshInfo.Length][];
                for (int i = 0; i < secondFaceText.textInfo.meshInfo.Length; i++)
                {
                    Vector3[] vertices = secondFaceText.textInfo.meshInfo[i].vertices;
                    _originalVerticesSecondFace[i] = new Vector3[vertices.Length];
                    for (int j = 0; j < vertices.Length; j++)
                    {
                        _originalVerticesSecondFace[i][j] = vertices[j];
                    }
                }

                _originalColorsSecondFace = new Color32[secondFaceText.textInfo.meshInfo.Length][];
                for (int i = 0; i < secondFaceText.textInfo.meshInfo.Length; i++)
                {
                    Color32[] colors = secondFaceText.textInfo.meshInfo[i].colors32;
                    _originalColorsSecondFace[i] = new Color32[colors.Length];
                    for (int j = 0; j < colors.Length; j++)
                    {
                        _originalColorsSecondFace[i][j] = colors[j];
                    }
                }
            }

            for (int i = 0; i < charCount; i++)
            {
                var charInfo = _tmpText.textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                _originalPositions[i] = GetCharacterCenter(charInfo);
                _sequences[i] = CreateCharacterSequence(i);
            }

            if (_enableAppear)
            {
                for (int i = 0; i < charCount; i++)
                {
                    var charInfo = _tmpText.textInfo.characterInfo[i];
                    if (!charInfo.isVisible) continue;

                    Vector3 originalPos = _originalPositions[i];
                    Vector3 originalScale = Vector3.one;
                    Vector3 originalRot = Vector3.zero;
                    float originalAlpha = 1f;

                    Vector3 appearFromPos, appearToPos, appearFromScale, appearToScale, appearFromRot, appearToRot;
                    float appearFromAlpha, appearToAlpha;

                    CalculateFromTo(true, _appearRelative,
                        _appearPosition, originalPos,
                        _appearScale, originalScale,
                        _appearRotation, originalRot,
                        _appearAlpha, originalAlpha,
                        out appearFromPos, out appearToPos,
                        out appearFromScale, out appearToScale,
                        out appearFromRot, out appearToRot,
                        out appearFromAlpha, out appearToAlpha);

                    TransformCharacterVertices(i, appearFromPos, appearFromScale, appearFromRot, appearFromAlpha);
                }

                UpdateAllVertexData();
            }

            _isPlayingInProgress = false;
        }

        public void Stop()
        {
            KillAllSequences();
            _isPlaying = false;
            RestoreOriginalMesh();
        }

        public void Pause()
        {
            if (_sequences == null) return;

            foreach (var seq in _sequences)
            {
                if (seq != null && seq.IsActive()) seq.Pause();
            }
        }

        public void Resume()
        {
            if (_sequences == null) return;

            foreach (var seq in _sequences)
            {
                if (seq != null && seq.IsActive()) seq.Play();
            }
        }

        public void Restart()
        {
            Stop();
            Play();
        }

        public void ApplyPreset(TMPCharacterAnimationPreset preset)
        {
            if (preset == null) return;

            _preset = preset;
            _characterDelay = preset.CharacterDelay;

            _enableAppear = preset.EnableAppear;
            _appearRelative = preset.AppearRelative;
            _appearPosition = preset.AppearPosition;
            _appearScale = preset.AppearScale;
            _appearRotation = preset.AppearRotation;
            _appearAlpha = preset.AppearAlpha;
            _appearDuration = preset.AppearDuration;
            _appearEase = preset.AppearEase;
            _appearUseCustomCurve = preset.AppearUseCustomCurve;
            _appearCustomCurve = preset.AppearCustomCurve;

            _enableLoop = preset.EnableLoop;
            _loopRelative = preset.LoopRelative;
            _loopPosition = preset.LoopPosition;
            _loopScale = preset.LoopScale;
            _loopRotation = preset.LoopRotation;
            _loopDuration = preset.LoopDuration;
            _loopEase = preset.LoopEase;
            _loopUseCustomCurve = preset.LoopUseCustomCurve;
            _loopCustomCurve = preset.LoopCustomCurve;
            _loopCount = preset.LoopCount;
            _loopType = preset.LoopType;

            _enableDisappear = preset.EnableDisappear;
            _disappearRelative = preset.DisappearRelative;
            _disappearPosition = preset.DisappearPosition;
            _disappearScale = preset.DisappearScale;
            _disappearRotation = preset.DisappearRotation;
            _disappearAlpha = preset.DisappearAlpha;
            _disappearDuration = preset.DisappearDuration;
            _disappearEase = preset.DisappearEase;
            _disappearUseCustomCurve = preset.DisappearUseCustomCurve;
            _disappearCustomCurve = preset.DisappearCustomCurve;

            if (_isPlaying) Restart();
        }
    }
}
