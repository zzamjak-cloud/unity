using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace SoftMaskLight
{
#if UNITY_EDITOR
    /// <summary>
    /// 에디터 성능 문제 진단용 임시 도구.
    /// 에디터 프로파일러 기록을 텍스트 파일로 덤프하여 원인 분석에 사용한다.
    /// 진단이 끝나면 이 파일은 삭제해도 된다.
    /// </summary>
    static class SoftMaskLightProfilerDump
    {
        private const string OUTPUT_NAME = "SoftMaskLight_ProfilerDump.txt";
        private const string OUTPUT_NAME_OFF = "SoftMaskLight_ProfilerDump_OFF.txt";
        private const int MAX_FRAMES = 400;
        private const int TOP_N = 30;

        [MenuItem("Tools/SoftMaskLight/Diagnostics/1. 프로파일링 시작 (에디터)")]
        private static void StartProfiling()
        {
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = true;   // 에디터 루프까지 기록 (Edit Mode 필수)
            ProfilerDriver.deepProfiling = false;  // Deep은 과부하로 결과를 왜곡시키므로 끔
            ProfilerDriver.enabled = true;

            Debug.Log("[SoftMaskLight] 프로파일링 시작. 이제 자식 오브젝트를 3~5초간 드래그한 뒤 " +
                      "'Tools > SoftMaskLight > Diagnostics > 2. 결과 덤프'를 실행하세요.");
        }

        [MenuItem("Tools/SoftMaskLight/Diagnostics/2-A. 결과 덤프 (컴포넌트 켠 상태)")]
        private static void DumpProfilingOn() => Dump(OutputPathFor(OUTPUT_NAME));

        [MenuItem("Tools/SoftMaskLight/Diagnostics/2-B. 결과 덤프 (컴포넌트 끈 상태)")]
        private static void DumpProfilingOff() => Dump(OutputPathFor(OUTPUT_NAME_OFF));

        private static string OutputPathFor(string fileName) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", fileName));

        private static void Dump(string outputPath)
        {
            ProfilerDriver.enabled = false;

            var sb = new StringBuilder();
            sb.AppendLine("SoftMaskLight Profiler Dump");
            sb.AppendLine($"Unity {Application.unityVersion} / isPlaying={Application.isPlaying}");
            sb.AppendLine($"frames: {ProfilerDriver.firstFrameIndex} ~ {ProfilerDriver.lastFrameIndex}");
            sb.AppendLine();

            int first = ProfilerDriver.firstFrameIndex;
            int last = ProfilerDriver.lastFrameIndex;

            // 프로파일링 시작 직후 프레임은 ProfilerDriver 초기화 비용으로 오염되어 있으므로 제외
            const int WARMUP_SKIP = 30;
            if (last - first > WARMUP_SKIP * 2) first += WARMUP_SKIP;
            if (last < 0 || last < first)
            {
                sb.AppendLine("!! 기록된 프레임이 없습니다. '1. 프로파일링 시작'을 먼저 실행했는지, " +
                              "그리고 Profiler 창이 Record 상태인지 확인하세요.");
                File.WriteAllText(outputPath, sb.ToString());
                Debug.LogWarning($"[SoftMaskLight] 기록된 프레임 없음 → {outputPath}");
                return;
            }

            if (last - first + 1 > MAX_FRAMES) first = last - MAX_FRAMES + 1;

            // 이름별 누적 (self / total) + 프레임 시간 통계
            var selfByName = new Dictionary<string, double>();
            var totalByName = new Dictionary<string, double>();
            var callsByName = new Dictionary<string, double>();
            double worstFrameMs = 0; int worstFrameIndex = -1;
            double sumFrameMs = 0; int frameCount = 0;

            var children = new List<int>();
            var stack = new Stack<int>();

            for (int f = first; f <= last; f++)
            {
                using (var view = ProfilerDriver.GetHierarchyFrameDataView(
                           f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                           HierarchyFrameDataView.columnTotalTime, false))
                {
                    if (view == null || !view.valid) continue;

                    double frameMs = view.frameTimeMs;
                    sumFrameMs += frameMs;
                    frameCount++;
                    if (frameMs > worstFrameMs) { worstFrameMs = frameMs; worstFrameIndex = f; }

                    stack.Clear();
                    stack.Push(view.GetRootItemID());
                    int guard = 0;
                    while (stack.Count > 0 && guard++ < 200000)
                    {
                        int id = stack.Pop();
                        string name = view.GetItemName(id);
                        if (!string.IsNullOrEmpty(name))
                        {
                            float self = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
                            float total = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
                            float calls = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls);
                            selfByName.TryGetValue(name, out double s); selfByName[name] = s + self;
                            totalByName.TryGetValue(name, out double t); totalByName[name] = t + total;
                            callsByName.TryGetValue(name, out double c); callsByName[name] = c + calls;
                        }

                        children.Clear();
                        view.GetItemChildren(id, children);
                        for (int i = 0; i < children.Count; i++) stack.Push(children[i]);
                    }
                }
            }

            double avgFrameMs = frameCount > 0 ? sumFrameMs / frameCount : 0;
            sb.AppendLine($"분석 프레임 수: {frameCount}");
            sb.AppendLine($"평균 프레임: {avgFrameMs:F2} ms   최악 프레임: {worstFrameMs:F2} ms (frame {worstFrameIndex})");
            sb.AppendLine();

            // SoftMaskLight 전용 집계 (이름 마커 기준)
            sb.AppendLine("===== SoftMaskLight 마커 (프레임당 평균) =====");
            bool foundAny = false;
            foreach (var kv in totalByName)
            {
                if (kv.Key.IndexOf("SoftMask", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                foundAny = true;
                selfByName.TryGetValue(kv.Key, out double sf);
                callsByName.TryGetValue(kv.Key, out double cl);
                sb.AppendLine($"  total {kv.Value / frameCount,8:F4} ms   self {sf / frameCount,8:F4} ms" +
                              $"   x{cl / frameCount,6:F1}   {kv.Key}");
            }
            if (!foundAny)
                sb.AppendLine("  (SoftMaskLight 마커가 기록되지 않음 → 이 컴포넌트는 프레임 비용에 기여하지 않음)");
            sb.AppendLine();

            sb.AppendLine($"===== Self Time 누적 상위 {TOP_N} (프레임당 평균) =====");
            AppendTop(sb, selfByName, callsByName, frameCount, TOP_N);
            sb.AppendLine();

            sb.AppendLine($"===== Total Time 누적 상위 {TOP_N} (프레임당 평균) =====");
            AppendTop(sb, totalByName, callsByName, frameCount, TOP_N);
            sb.AppendLine();

            // 최악 프레임의 호출 트리 (깊이 제한)
            if (worstFrameIndex >= 0)
            {
                sb.AppendLine($"===== 최악 프레임({worstFrameIndex}) 호출 트리 (total 0.05ms 이상, 깊이 8) =====");
                using (var view = ProfilerDriver.GetHierarchyFrameDataView(
                           worstFrameIndex, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                           HierarchyFrameDataView.columnTotalTime, false))
                {
                    if (view != null && view.valid)
                        AppendTree(sb, view, view.GetRootItemID(), 0, 8, 0.05f);
                }
            }

            File.WriteAllText(outputPath, sb.ToString());
            Debug.Log($"[SoftMaskLight] 프로파일러 덤프 저장 완료 → {outputPath}");
        }

        private static void AppendTop(StringBuilder sb, Dictionary<string, double> map,
                                      Dictionary<string, double> calls, int frameCount, int topN)
        {
            var list = new List<KeyValuePair<string, double>>(map);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            int n = Mathf.Min(topN, list.Count);
            for (int i = 0; i < n; i++)
            {
                double perFrame = frameCount > 0 ? list[i].Value / frameCount : 0;
                calls.TryGetValue(list[i].Key, out double c);
                double callsPerFrame = frameCount > 0 ? c / frameCount : 0;
                sb.AppendLine($"  {perFrame,8:F3} ms  x{callsPerFrame,8:F1}  {list[i].Key}");
            }
        }

        private static void AppendTree(StringBuilder sb, HierarchyFrameDataView view, int id,
                                       int depth, int maxDepth, float minMs)
        {
            if (depth > maxDepth) return;

            string name = view.GetItemName(id);
            float total = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
            float self = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
            float calls = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls);

            if (depth > 0)
            {
                if (total < minMs) return;
                sb.AppendLine($"  {new string(' ', depth * 2)}{total,7:F3} ms (self {self,6:F3}) x{calls,6:F0}  {name}");
            }

            var children = new List<int>();
            view.GetItemChildren(id, children);
            // total 내림차순
            children.Sort((a, b) => view.GetItemColumnDataAsFloat(b, HierarchyFrameDataView.columnTotalTime)
                                        .CompareTo(view.GetItemColumnDataAsFloat(a, HierarchyFrameDataView.columnTotalTime)));
            for (int i = 0; i < children.Count; i++)
                AppendTree(sb, view, children[i], depth + 1, maxDepth, minMs);
        }
    }
#endif
}
