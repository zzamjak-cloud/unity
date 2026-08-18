using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Text;

namespace SoftMaskLight
{
#if UNITY_EDITOR
    /// <summary>
    /// 빌드 직전 SoftMaskLight 셰이더 등록을 강제 갱신한다.
    ///
    /// 등록은 SoftMaskLightSettings(Resources) 에셋의 직렬화 참조로 빌드 포함을 보장하는데,
    /// 사람이 Tools > SoftMaskLight > Refresh Settings를 잊으면 목록이 낡아
    /// (a) 필요한 변형 셰이더가 빠지거나 (b) 쓰지 않는 변형이 빌드에 실린다.
    /// 이 전처리기가 매 빌드마다 목록을 다시 만들어 두 경우를 모두 차단한다.
    /// </summary>
    class SoftMaskLightBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            SoftMaskLightInstaller.ForceRefresh();

            var shaders = SoftMaskLightInstaller.GetRegisteredShaders();
            var sb = new StringBuilder();
            sb.Append("[SoftMaskLight] 빌드 포함 셰이더 ").Append(shaders.Length).Append("개");
            for (int i = 0; i < shaders.Length; i++)
            {
                if (shaders[i] == null) continue;
                sb.Append("\n  - ").Append(shaders[i].name);
            }
            sb.Append("\n(원본 셰이더가 프로젝트에 없는 변형은 자동 제외됩니다. " +
                      "런타임에 '변형 셰이더가 없어 ...로 대체합니다' 경고가 뜨면 이 목록을 확인하세요.)");
            Debug.Log(sb.ToString());
        }
    }
#endif
}
