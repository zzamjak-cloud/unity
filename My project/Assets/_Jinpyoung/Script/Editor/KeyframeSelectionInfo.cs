using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections;
using System.Text;

public class KeyframeSelectionInfo
{
    [MenuItem("Tools/Animation/Inspect NormalCurveRenderer")]
    private static void InspectCurveRenderer()
    {
        // 1. Animation Window의 'state' 객체 가져오기
        object state = GetAnimationWindowState();
        if (state == null) { Debug.LogError("'state' 객체를 가져올 수 없습니다."); return; }

        // 2. 'activeCurveWrappers' 목록 가져오기
        var curvesProperty = state.GetType().GetProperty("activeCurveWrappers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var curveWrapperList = curvesProperty?.GetValue(state) as IEnumerable;
        if (curveWrapperList == null) { Debug.LogError("'activeCurveWrappers' 목록이 null입니다."); return; }

        // 3. 목록의 첫 번째 CurveWrapper에서 renderer 객체 가져오기
        object firstCurveWrapper = null;
        foreach (object item in curveWrapperList) { firstCurveWrapper = item; break; }
        if (firstCurveWrapper == null) { Debug.LogWarning("활성화된 커브가 없습니다."); return; }
        
        var rendererProperty = firstCurveWrapper.GetType().GetProperty("renderer", BindingFlags.Public | BindingFlags.Instance);
        object renderer = rendererProperty?.GetValue(firstCurveWrapper);
        if (renderer == null) { Debug.LogError("'renderer' 객체가 null입니다."); return; }

        // 4. NormalCurveRenderer 객체의 모든 멤버 정보 출력
        var sb = new StringBuilder();
        var itemType = renderer.GetType();
        sb.AppendLine($"===== NormalCurveRenderer 객체 상세 정보 =====");
        sb.AppendLine($"타입 전체 이름: {itemType.FullName}");
        
        sb.AppendLine("\n--- Properties ---");
        foreach (var prop in itemType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            sb.AppendLine($"  {prop.Name} (Type: {prop.PropertyType.Name})");
        }
        
        sb.AppendLine("\n--- Fields ---");
        foreach (var field in itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            sb.AppendLine($"  {field.Name} (Type: {field.FieldType.Name})");
        }

        sb.AppendLine("\n--- Methods (일부) ---");
        foreach (var method in itemType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (method.DeclaringType == typeof(object) || method.IsSpecialName || method.Name.StartsWith("get_") || method.Name.StartsWith("set_")) continue;
            sb.AppendLine($"  {method.Name}");
        }
        
        Debug.Log(sb.ToString());
    }

    private static object GetAnimationWindowState()
    {
        var animationWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
        if (animationWindowType == null) return null;
        var window = EditorWindow.GetWindow(animationWindowType, false, null, false);
        if (window == null) return null;
        var stateProperty = animationWindowType.GetProperty("state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return stateProperty?.GetValue(window);
    }
}