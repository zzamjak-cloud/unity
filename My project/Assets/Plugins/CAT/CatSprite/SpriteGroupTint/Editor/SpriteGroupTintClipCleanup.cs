using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
    // com.zzamjak.cateditor 패키지가 CAT.AnimationUtility 네임스페이스를 선언하고 있어
    // CAT.Effects 안에서는 UnityEditor.AnimationUtility 가 가려진다.
    // 별칭은 반드시 네임스페이스 안에 둬야 한다. 파일 최상단에 두면 상위 네임스페이스 CAT 의
    // 멤버(CAT.AnimationUtility)가 먼저 매칭돼 여전히 가려진다.
    using AnimationUtility = UnityEditor.AnimationUtility;

    /// <summary>
    /// SpriteGroupTint.tintColor 를 흰색으로 고정하기만 하는 상수 커브를 애니메이션 클립에서 제거한다.
    /// 이런 커브는 렌더링에 아무 영향이 없으면서 Animator 가 매 프레임 4개 커브를 평가하게 만들고,
    /// 스크립트로 설정한 틴트를 매 프레임 흰색으로 되돌려 틴트 기능 자체를 무력화한다.
    /// m_FloatCurves / m_EditorCurves / m_ClipBindingConstant 동기화가 필요하므로 AnimationUtility 를 사용한다.
    /// </summary>
    public static class SpriteGroupTintClipCleanup
    {
        private const string MenuRoot = "Tools/CAT/SpriteGroupTint/";

        [MenuItem(MenuRoot + "상수 tintColor 커브 검사 (변경 없음)")]
        private static void DryRun()
        {
            Run(false);
        }

        [MenuItem(MenuRoot + "상수 tintColor 커브 제거")]
        private static void Apply()
        {
            bool ok = EditorUtility.DisplayDialog(
                "상수 tintColor 커브 제거",
                "SpriteGroupTint.tintColor 의 r/g/b/a 4채널이 모두 상수 1(흰색)인 클립에서 해당 커브를 제거합니다.\n\n" +
                "전제: 해당 컴포넌트의 직렬화된 tintColor 가 흰색이어야 합니다. " +
                "먼저 '검사' 메뉴로 대상 목록을 확인하세요.\n\n계속하시겠습니까?",
                "제거", "취소");

            if (ok) Run(true);
        }

        private static void Run(bool write)
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
            var removedLog = new StringBuilder();
            var skippedLog = new StringBuilder();

            int scanned = 0;
            int touchedClips = 0;
            int removedCurves = 0;
            int skippedPaths = 0;

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                    // .fbx 등에 포함된 임포트 클립은 수정할 수 없으므로 네이티브 클립만 처리한다.
                    if (!path.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase)) continue;

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "상수 tintColor 커브 정리", path, (float)i / Mathf.Max(1, guids.Length)))
                    {
                        break;
                    }

                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip == null) continue;

                    scanned++;

                    // 경로별로 tintColor 채널을 모아서, 4채널이 모두 상수 1일 때만 통째로 제거한다.
                    Dictionary<string, List<EditorCurveBinding>> byPath = CollectTintBindings(clip);
                    if (byPath == null) continue;

                    bool clipTouched = false;

                    foreach (var pair in byPath)
                    {
                        List<EditorCurveBinding> bindings = pair.Value;

                        if (!IsRemovable(clip, bindings))
                        {
                            skippedPaths++;
                            skippedLog.AppendLine("  " + path + " (" + pair.Key + ")");
                            continue;
                        }

                        for (int b = 0; b < bindings.Count; b++)
                        {
                            if (write) AnimationUtility.SetEditorCurve(clip, bindings[b], null);
                            removedCurves++;
                        }

                        clipTouched = true;
                    }

                    if (clipTouched)
                    {
                        touchedClips++;
                        removedLog.AppendLine("  " + path);
                        if (write) EditorUtility.SetDirty(clip);
                    }
                }

                if (write)
                {
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string header = write ? "[SpriteGroupTint] 상수 tintColor 커브 제거 완료" : "[SpriteGroupTint] 상수 tintColor 커브 검사 결과";
            Debug.Log(header +
                      "\n검사한 클립: " + scanned +
                      "\n대상 클립: " + touchedClips +
                      "\n대상 커브: " + removedCurves +
                      "\n건너뛴 경로(값이 변하는 커브): " + skippedPaths +
                      (touchedClips > 0 ? "\n\n[대상]\n" + removedLog : "") +
                      (skippedPaths > 0 ? "\n[건너뜀]\n" + skippedLog : ""));
        }

        private static Dictionary<string, List<EditorCurveBinding>> CollectTintBindings(AnimationClip clip)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Dictionary<string, List<EditorCurveBinding>> byPath = null;

            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];

                if (binding.type != typeof(SpriteGroupTint)) continue;
                if (!binding.propertyName.StartsWith("tintColor")) continue;

                if (byPath == null) byPath = new Dictionary<string, List<EditorCurveBinding>>();

                List<EditorCurveBinding> list;
                if (!byPath.TryGetValue(binding.path, out list))
                {
                    list = new List<EditorCurveBinding>(4);
                    byPath[binding.path] = list;
                }
                list.Add(binding);
            }

            return byPath;
        }

        // r/g/b/a 4채널이 전부 존재하고 모두 상수 1일 때만 제거 대상으로 본다.
        // 일부 채널만 상수인 경우 제거하면 애니메이터가 강제하던 값이 사라져 결과가 달라질 수 있다.
        private static bool IsRemovable(AnimationClip clip, List<EditorCurveBinding> bindings)
        {
            if (bindings.Count != 4) return false;

            bool r = false, g = false, b = false, a = false;

            for (int i = 0; i < bindings.Count; i++)
            {
                switch (bindings[i].propertyName)
                {
                    case "tintColor.r": r = true; break;
                    case "tintColor.g": g = true; break;
                    case "tintColor.b": b = true; break;
                    case "tintColor.a": a = true; break;
                    default: return false;
                }

                if (!IsConstantOne(AnimationUtility.GetEditorCurve(clip, bindings[i]))) return false;
            }

            return r && g && b && a;
        }

        private static bool IsConstantOne(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return false;

            for (int i = 0; i < curve.length; i++)
            {
                Keyframe key = curve[i];

                if (key.value != 1f) return false;

                // 값이 같아도 탄젠트가 살아 있으면 키 사이에서 오버슈트가 발생할 수 있다.
                if (curve.length > 1 && (key.inTangent != 0f || key.outTangent != 0f)) return false;
            }

            return true;
        }
    }
}
