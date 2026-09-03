using System.Collections;
using UnityEngine;

namespace CAT.Toon
{
    /// <summary>
    /// 씬에 올려두고 인스펙터/스크립트로 아웃라인을 제어하는 컴포넌트입니다.
    /// 실제 값은 <see cref="CATToonOutlineRuntime"/> 전역 오버라이드로 전달됩니다.
    /// </summary>
    [AddComponentMenu("CAT/Toon/Toon Outline Controller")]
    [ExecuteAlways]
    public class CATToonOutlineController : MonoBehaviour
    {
        [Header("아웃라인 오버라이드")]
        [SerializeField] private bool overrideColor = true;
        [ColorUsage(true, true)]
        [SerializeField] private Color outlineColor = new Color(0.07f, 0.06f, 0.11f, 1f);

        [SerializeField] private bool overrideThickness = false;
        [Range(0.5f, 6f)]
        [SerializeField] private float thickness = 1.2f;

        [SerializeField] private bool overrideSketchJitter = false;
        [Range(0f, 4f)]
        [SerializeField] private float sketchJitter = 0f;

        [SerializeField] private bool overrideEnabled = false;
        [SerializeField] private bool outlineEnabled = true;

        private Coroutine m_FlashRoutine;

        /// <summary>현재 적용 중인 아웃라인 컬러입니다. 대입하면 즉시 반영됩니다.</summary>
        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                outlineColor  = value;
                overrideColor = true;
                Apply();
            }
        }

        private void OnEnable()  => Apply();
        private void OnDisable() => Release();
        private void OnValidate() { if (isActiveAndEnabled) Apply(); }

        /// <summary>인스펙터 값을 전역 오버라이드에 반영합니다.</summary>
        public void Apply()
        {
            CATToonOutlineRuntime.Color        = overrideColor        ? outlineColor : (Color?)null;
            CATToonOutlineRuntime.Thickness    = overrideThickness    ? thickness    : (float?)null;
            CATToonOutlineRuntime.SketchJitter = overrideSketchJitter ? sketchJitter : (float?)null;
            CATToonOutlineRuntime.Enabled      = overrideEnabled      ? outlineEnabled : (bool?)null;
        }

        /// <summary>이 컴포넌트가 건 오버라이드를 해제합니다.</summary>
        public void Release() => CATToonOutlineRuntime.Reset();

        /// <summary>지정한 컬러로 잠깐 번쩍인 뒤 원래 컬러로 되돌립니다. (피격/획득 연출용)</summary>
        public void Flash(Color color, float duration = 0.15f)
        {
            if (!Application.isPlaying)
                return;

            if (m_FlashRoutine != null)
                StopCoroutine(m_FlashRoutine);

            m_FlashRoutine = StartCoroutine(FlashRoutine(color, duration));
        }

        private IEnumerator FlashRoutine(Color color, float duration)
        {
            Color original = outlineColor;

            CATToonOutlineRuntime.Color = color;
            yield return new WaitForSeconds(Mathf.Max(0f, duration));

            CATToonOutlineRuntime.Color = overrideColor ? original : (Color?)null;
            m_FlashRoutine = null;
        }
    }
}
