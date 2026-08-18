using UnityEditor;
using UnityEngine;
using System.Text;

// SoftMaskLight 검증 씬 자동화 유틸 (검증용 임시 도구 — 검증 완료 후 삭제 가능)
public static class SMLVerifyTools
{
    private const string CapturePath =
        "/private/tmp/claude-501/-Users-woody-Desktop-UnityClient-unity-My-project/bc2cb879-c1ea-4f9e-9c21-9f3d5d4f5a80/scratchpad/sml_capture.png";

    [MenuItem("Tools/SMLVerify/Capture GameView")]
    public static void Capture()
    {
        ScreenCapture.CaptureScreenshot(CapturePath, 1);
        Debug.Log("[SMLVerify] capture requested: " + CapturePath);
    }

    [MenuItem("Tools/SMLVerify/Dump Stats")]
    public static void DumpStats()
    {
        var masks = Object.FindObjectsByType<SoftMaskLight.SoftMaskLight>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var sb = new StringBuilder();
        sb.Append("[SMLVerify] masks=").Append(masks.Length);
        sb.Append(" drawCalls=").Append(UnityStats.drawCalls);
        sb.Append(" batches=").Append(UnityStats.batches);
        sb.Append(" setPass=").Append(UnityStats.setPassCalls);
        foreach (var m in masks)
            sb.Append("\n  - ").Append(m.gameObject.name)
              .Append(" maskedChildren=").Append(m.MaskedChildCount);
        Debug.Log(sb.ToString());
        // read_console이 로그를 반환하지 못하는 환경 대비: 파일로도 덤프
        System.IO.File.WriteAllText(
            System.IO.Path.GetDirectoryName(CapturePath) + "/sml_stats.txt", sb.ToString());
    }

    // Radial180 정답 대조: 마스크 그래픽(Unity 실제 지오메트리, 녹색)을 표시하고
    // 자식(우리 셰이더 커버리지)이 정확히 덮는지 확인 — 어긋나면 녹색 프린지가 보임
    [MenuItem("Tools/SMLVerify/R180 Bottom CCW 35%")]
    public static void R180BottomCCW() { SetupR180(0, false, 0.35f); }

    [MenuItem("Tools/SMLVerify/R180 Left CW 60%")]
    public static void R180LeftCW() { SetupR180(1, true, 0.6f); }

    private static void SetupR180(int origin, bool cw, float fill)
    {
        var go = GameObject.Find("SML_VerifyRoot/Grid/7 Radial360 55%/Mask");
        if (go == null) { Debug.LogWarning("[SMLVerify] cell7 Mask 미발견"); return; }
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.fillMethod = UnityEngine.UI.Image.FillMethod.Radial180;
        img.fillOrigin = origin;
        img.fillClockwise = cw;
        img.fillAmount = fill;
        img.color = new Color(0f, 1f, 0.2f, 1f); // 정답 지오메트리 = 녹색
        go.GetComponent<SoftMaskLight.SoftMaskLight>().ShowMaskGraphic = true;
        Debug.Log($"[SMLVerify] R180 origin={origin} cw={cw} fill={fill}");
    }

    [MenuItem("Tools/SMLVerify/Radial Fill 25%")]
    public static void RadialFill25() { SetRadialFill(0.25f); }

    [MenuItem("Tools/SMLVerify/Radial Fill 85%")]
    public static void RadialFill85() { SetRadialFill(0.85f); }

    private static void SetRadialFill(float v)
    {
        foreach (var img in Object.FindObjectsByType<UnityEngine.UI.Image>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (img.type == UnityEngine.UI.Image.Type.Filled &&
                img.fillMethod == UnityEngine.UI.Image.FillMethod.Radial360)
            {
                img.fillAmount = v;
                Debug.Log("[SMLVerify] fillAmount=" + v + " → " +
                          img.transform.parent.name + "/" + img.name);
            }
        }
    }
}
