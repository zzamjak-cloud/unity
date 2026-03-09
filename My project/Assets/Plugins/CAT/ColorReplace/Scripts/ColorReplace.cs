using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// HSV 색상 변환 에디터 전용 컴포넌트
    /// 에디터에서 HSV 옵션을 조정하고 머티리얼 에셋으로 저장하는 워크플로 도구
    /// 런타임에서는 저장된 머티리얼이 렌더러에 직접 할당되어 있으므로 이 컴포넌트는 불필요
    /// </summary>
    [AddComponentMenu("CAT/Effects/ColorReplace")]
    public class ColorReplace : MonoBehaviour
    {
        public static readonly string SHADER_NAME = "CAT/Effects/ColorReplace";

        // Shader Property ID 캐싱
        public static readonly int PropHSVRangeMin = Shader.PropertyToID("_HSVRangeMin");
        public static readonly int PropHSVRangeMax = Shader.PropertyToID("_HSVRangeMax");
        public static readonly int PropHSVAdjust = Shader.PropertyToID("_HSVAAdjust");

        /// <summary>
        /// 주어진 셰이더가 ColorReplace 계열인지 확인
        /// (원본, SoftMaskLight Hidden, SoftMaskable Hidden 변형 모두 포함)
        /// </summary>
        public static bool IsColorReplaceShader(Shader shader)
        {
            if (shader == null) return false;
            return shader.name == SHADER_NAME || shader.name.Contains("ColorReplace");
        }

        [Header("HSV Range")]
        [SerializeField, Range(0f, 1f)] private float _hsvRangeMin = 0f;
        public float HSVRangeMin
        {
            get => _hsvRangeMin;
            set => _hsvRangeMin = Mathf.Clamp01(value);
        }

        [SerializeField, Range(0f, 1f)] private float _hsvRangeMax = 1f;
        public float HSVRangeMax
        {
            get => _hsvRangeMax;
            set => _hsvRangeMax = Mathf.Clamp01(value);
        }

        [Header("HSV Adjust")]
        [SerializeField] private Vector4 _hsvAdjust = Vector4.zero;
        public Vector4 HSVAdjust
        {
            get => _hsvAdjust;
            set => _hsvAdjust = value;
        }
    }
}
