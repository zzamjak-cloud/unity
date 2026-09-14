using UnityEngine;
using UnityEngine.UI;
using CAT.Effects;

namespace CAT.Effects.Samples
{
    /// <summary>
    /// OldTVEffect 샘플 씬 컨트롤러. 씬 내 모든 효과의 성능 모드/주사선 공간을 일괄 전환해
    /// 디바이스에서 비용 차이를 비교할 수 있게 한다.
    /// </summary>
    public class OldTVEffectSampleController : MonoBehaviour
    {
        [SerializeField] private Button performanceButton;
        [SerializeField] private Button scanSpaceButton;
        [SerializeField] private Text statusText;

        private OldTVEffect[] _effects;
        private bool _low;
        private bool _screenSpace;

        private void Awake()
        {
            _effects = FindObjectsByType<OldTVEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (performanceButton != null)
                performanceButton.onClick.AddListener(TogglePerformance);
            if (scanSpaceButton != null)
                scanSpaceButton.onClick.AddListener(ToggleScanSpace);

            RefreshStatus();
        }

        private void TogglePerformance()
        {
            _low = !_low;
            foreach (var fx in _effects)
            {
                fx.performanceMode = _low ? OldTVEffect.PerformanceMode.Low : OldTVEffect.PerformanceMode.High;
                fx.MarkDirty();
            }
            RefreshStatus();
        }

        private void ToggleScanSpace()
        {
            _screenSpace = !_screenSpace;
            foreach (var fx in _effects)
            {
                fx.scanLineSpace = _screenSpace ? OldTVEffect.ScanLineSpace.Screen : OldTVEffect.ScanLineSpace.Texture;
                fx.MarkDirty();
            }
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (statusText == null)
                return;

            statusText.text = $"효과 {_effects.Length}개 | 성능 모드: {(_low ? "Low (블리드 OFF)" : "High")} | 주사선: {(_screenSpace ? "Screen" : "Texture")}";
        }
    }
}
