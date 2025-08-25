using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace CAT.Utility
{
    public class AnimationOffsetWindow : EditorWindow
    {
        private float offsetValue = 0f;     // 오프셋 값
        private bool isTimeInputMode = false;  // true: seconds, false: frames

        private enum PropertyType { Position, Rotation, Scale }

        private GameObject lastKnownAnimRootObject;  // 마지막으로 알려진 애니메이션 루트 오브젝트

        [MenuItem("CAT/Utility/Animation Offset Window")]
        private static void ShowWindow()
        {
            GetWindow<AnimationOffsetWindow>("Offset").Show();
        }
        // ==========================================================
        // OnEnable, OnDisable, OnEditorUpdate: 애니메이션 윈도우 상태 추적
        // ==========================================================
        private void OnEnable()
        {
            Selection.selectionChanged += Repaint;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            object state = GetAnimationWindowState();
            if (state != null)
            {
                GameObject currentAnimRootObject = GetActiveRootGameObjectFromState(state);
                if (currentAnimRootObject != lastKnownAnimRootObject)
                {
                    lastKnownAnimRootObject = currentAnimRootObject;
                    Repaint();
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(5, 5, 5, 5) });

            // --- 섹션 1: 타겟 정보 ---
            EditorGUILayout.LabelField("Target :", EditorStyles.boldLabel);
            GameObject selectedObject = Selection.activeGameObject;
            string selectedObjectName = (selectedObject != null) ? selectedObject.name : "None";
            EditorGUILayout.HelpBox(new GUIContent(selectedObjectName, "Selected Object"), true);

            EditorGUILayout.Space(10);

            // --- 섹션 2: 오프셋 입력 ---
            EditorGUILayout.LabelField("Offset :", EditorStyles.boldLabel);

            // 오프셋 입력 필드를 한 줄에 단독으로 배치
            string inputTooltip = isTimeInputMode ? "Time (s) offset value" : "Frame offset value";
            offsetValue = EditorGUILayout.FloatField(new GUIContent("", inputTooltip), offsetValue);

            // 버튼들을 입력 필드 아래에 별도의 가로 그룹으로 배치
            EditorGUILayout.BeginHorizontal();

            // 입력 모드 전환 토글 버튼 (프레임 <-> 시간)
            Color originalColor = GUI.backgroundColor;
            string modeText;
            if (isTimeInputMode)
            {
                GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
                modeText = "Sec";
            }
            else
            {
                GUI.backgroundColor = new Color(0.5f, 0.7f, 1.0f);
                modeText = "Frame";
            }
            
            if (GUILayout.Button(new GUIContent(modeText, "Switch Frames <-> Seconds"), GUILayout.Width(56)))
            {
                if (offsetValue != 0)
                {
                    object state = GetAnimationWindowState();
                    AnimationClip activeClip = GetActiveAnimationClipFromState(state);
                    float frameRate = (activeClip != null) ? activeClip.frameRate : 60f;

                    if (isTimeInputMode) // Time -> Frame
                    {
                        offsetValue *= frameRate;
                    }
                    else // Frame -> Time
                    {
                        offsetValue /= frameRate;
                    }
                }
                isTimeInputMode = !isTimeInputMode;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = originalColor;

            // 리셋 버튼을 오른쪽으로 배치
            // GUILayout.FlexibleSpace();

            // 리셋 버튼
            if (GUILayout.Button(new GUIContent("R", "offset value to 0"), GUILayout.Width(30)))
            {
                offsetValue = 0f;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // --- 섹션 3: 적용 ---
            EditorGUILayout.LabelField("Apply :", EditorStyles.boldLabel);

            if (GUILayout.Button(new GUIContent("Position", "Apply offset to Position curves"), GUILayout.Height(25)))
            {
                ApplyLoopOffset(PropertyType.Position);
            }
            if (GUILayout.Button(new GUIContent("Rotation", "Apply offset to Rotation curves"), GUILayout.Height(25)))
            {
                ApplyLoopOffset(PropertyType.Rotation);
            }
            if (GUILayout.Button(new GUIContent("Scale", "Apply offset to Scale curves"), GUILayout.Height(25)))
            {
                ApplyLoopOffset(PropertyType.Scale);
            }

            EditorGUILayout.EndVertical();
        }

        // ==========================================================
        // ApplyLoopOffset: 선택된 오브젝트와 애니메이션 클립에 오프셋 적용
        // ==========================================================
        private void ApplyLoopOffset(PropertyType propertyType)
        {
            if (offsetValue == 0)
            {
                Debug.LogWarning("오프셋 값이 0입니다. 오프셋 값을 설정해주세요.");
                return;
            }

            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null) { Debug.LogError("오브젝트를 선택해주세요."); return; }

            object state = GetAnimationWindowState();
            if (state == null) { Debug.LogError("애니메이션 윈도우가 열려있지 않습니다."); return; }

            AnimationClip activeClip = GetActiveAnimationClipFromState(state);
            if (activeClip == null) { Debug.LogError("애니메이션 클립을 선택해주세요."); return; }

            string clipPath = AssetDatabase.GetAssetPath(activeClip);
            if (string.IsNullOrEmpty(clipPath))
            {
                Debug.LogError("선택된 클립의 에셋 경로를 찾을 수 없습니다. (저장되지 않은 클립일 수 있습니다)");
                return;
            }
            AnimationClip sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (sourceClip == null) { Debug.LogError($"에셋 경로에서 클립을 불러오는 데 실패했습니다: {clipPath}"); return; }

            GameObject rootObject = GetActiveRootGameObjectFromState(state);
            if (rootObject == null) { Debug.LogError("애니메이션 루트 오브젝트를 찾을 수 없습니다."); return; }

            float loopDurationSecs = sourceClip.length;
            if (loopDurationSecs <= 0) { Debug.LogError("클립 길이가 0보다 커야 합니다."); return; }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(sourceClip);
            if (!settings.loopTime)
            {
                Debug.LogWarning("애니메이션이 Loop로 설정되어 있지 않습니다. Loop 애니메이션에만 사용하는 것을 권장합니다.");
            }

            float timeOffset;
            if (isTimeInputMode)
            {
                timeOffset = offsetValue;
            }
            else
            {
                timeOffset = offsetValue / sourceClip.frameRate;
            }

            timeOffset = timeOffset % loopDurationSecs;
            if (timeOffset < 0)
                timeOffset += loopDurationSecs;

            Undo.RecordObject(sourceClip, "Apply Loop Animation Offset");
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(sourceClip);
            bool anyCurveModified = false;

            string selectedObjectPath = AnimationUtility.CalculateTransformPath(selectedObject.transform, rootObject.transform);

            foreach (var binding in bindings)
            {
                if (binding.path == selectedObjectPath && IsPropertyTypeMatch(binding.propertyName, propertyType))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                    if (curve == null || curve.keys.Length == 0) continue;

                    AnimationCurve newCurve = CreateOffsetCurve(curve, timeOffset, loopDurationSecs);

                    if (newCurve != null)
                    {
                        AnimationUtility.SetEditorCurve(sourceClip, binding, newCurve);
                        anyCurveModified = true;
                    }
                }
            }

            if (anyCurveModified)
            {
                EditorUtility.SetDirty(sourceClip);
                string logMessage = isTimeInputMode ? $"{offsetValue:F3}초" : $"{offsetValue} 프레임 ({timeOffset:F3}초)";
                Debug.Log($"루프 오프셋 적용 완료: {logMessage}");
                ForceRefreshAnimationWindow();
            }
            else
            {
                Debug.LogWarning($"'{selectedObject.name}' 오브젝트에서 '{propertyType}' 속성의 애니메이션 커브를 찾지 못했습니다.");
            }
        }
        // ==========================================================
        // CreateOffsetCurve: 오프셋이 적용된 새로운 애니메이션 커브 생성
        // ==========================================================
        private AnimationCurve CreateOffsetCurve(AnimationCurve originalCurve, float timeOffset, float loopDuration)
        {
            if (originalCurve.keys.Length == 0) return null;

            var offsetKeys = new List<Keyframe>();

            foreach (var originalKey in originalCurve.keys)
            {
                float newTime = originalKey.time + timeOffset;

                while (newTime >= loopDuration) newTime -= loopDuration;
                while (newTime < 0) newTime += loopDuration;

                var newKey = new Keyframe(newTime, originalKey.value, originalKey.inTangent, originalKey.outTangent)
                {
                    inWeight = originalKey.inWeight,
                    outWeight = originalKey.outWeight,
                    weightedMode = originalKey.weightedMode
                };

                offsetKeys.Add(newKey);
            }

            offsetKeys.Sort((a, b) => a.time.CompareTo(b.time));

            var finalKeys = new List<Keyframe>();
            const float timeEpsilon = 0.0001f;

            foreach (var key in offsetKeys)
            {
                bool isDuplicate = false;
                foreach (var existingKey in finalKeys)
                {
                    if (Mathf.Abs(existingKey.time - key.time) < timeEpsilon)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    finalKeys.Add(key);
                }
            }

            if (finalKeys.Count > 0)
            {
                float valueAt0 = originalCurve.Evaluate((0 - timeOffset + loopDuration * 100) % loopDuration);

                bool hasKeyAt0 = false;
                for (int i = 0; i < finalKeys.Count; i++)
                {
                    if (Mathf.Abs(finalKeys[i].time) < timeEpsilon)
                    {
                        hasKeyAt0 = true;
                        break;
                    }
                }

                if (!hasKeyAt0)
                {
                    float originalTimeAt0 = (0 - timeOffset + loopDuration * 100) % loopDuration;
                    float deltaTime = 0.001f;
                    float valueBefore = originalCurve.Evaluate((originalTimeAt0 - deltaTime + loopDuration) % loopDuration);
                    float valueAfter = originalCurve.Evaluate((originalTimeAt0 + deltaTime) % loopDuration);
                    float tangent = (valueAfter - valueBefore) / (2f * deltaTime);

                    finalKeys.Insert(0, new Keyframe(0f, valueAt0, tangent, tangent));
                }

                bool hasKeyAtEnd = false;
                for (int i = 0; i < finalKeys.Count; i++)
                {
                    if (Mathf.Abs(finalKeys[i].time - loopDuration) < timeEpsilon)
                    {
                        hasKeyAtEnd = true;
                        break;
                    }
                }

                if (!hasKeyAtEnd)
                {
                    float originalTimeAtEnd = (loopDuration - timeOffset + loopDuration * 100) % loopDuration;
                    float deltaTime = 0.001f;
                    float valueBefore = originalCurve.Evaluate((originalTimeAtEnd - deltaTime + loopDuration) % loopDuration);
                    float valueAfter = originalCurve.Evaluate((originalTimeAtEnd + deltaTime) % loopDuration);
                    float tangent = (valueAfter - valueBefore) / (2f * deltaTime);

                    finalKeys.Add(new Keyframe(loopDuration, valueAt0, tangent, tangent));
                }
                else
                {
                    for (int i = 0; i < finalKeys.Count; i++)
                    {
                        if (Mathf.Abs(finalKeys[i].time - loopDuration) < timeEpsilon)
                        {
                            var endKey = finalKeys[i];
                            endKey.value = valueAt0;
                            finalKeys[i] = endKey;
                            break;
                        }
                    }
                }
            }

            finalKeys.Sort((a, b) => a.time.CompareTo(b.time));

            var newCurve = new AnimationCurve(finalKeys.ToArray());

            newCurve.preWrapMode = originalCurve.preWrapMode;
            newCurve.postWrapMode = originalCurve.postWrapMode;

            return newCurve;
        }
        // ==========================================================
        // IsPropertyTypeMatch: 바인딩된 속성이 지정된 타입과 일치하는지 확인
        // ==========================================================
        private bool IsPropertyTypeMatch(string propertyName, PropertyType type)
        {
            switch (type)
            {
                case PropertyType.Position:
                    return propertyName.Contains("m_LocalPosition") ||
                           propertyName.Contains("m_AnchoredPosition") ||
                           propertyName.Contains("localPosition");
                case PropertyType.Rotation:
                    return propertyName.Contains("localEulerAnglesRaw") ||
                           propertyName.Contains("localEulerAngles") ||
                           propertyName.Contains("m_LocalRotation");
                case PropertyType.Scale:
                    return propertyName.Contains("m_LocalScale") ||
                           propertyName.Contains("localScale");
                default:
                    return false;
            }
        }

        #region Animation Window Reflection Utilities

        // ==========================================================
        // 애니메이션 윈도우 State에 접근
        // ==========================================================
        private object GetAnimationWindowState()
        {
            try
            {
                var animationWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
                if (animationWindowType == null) return null;

                var window = GetWindow(animationWindowType, false, null, false);
                if (window == null) return null;

                var stateProperty = animationWindowType.GetProperty("state",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return stateProperty?.GetValue(window);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"애니메이션 윈도우 상태를 가져오는 중 오류 발생: {e.Message}");
                return null;
            }
        }

        // ==========================================================
        // State에서 활성 애니메이션 클립 가져오기
        // ==========================================================
        private AnimationClip GetActiveAnimationClipFromState(object state)
        {
            if (state == null) return null;

            try
            {
                var activeClipProperty = state.GetType().GetProperty("activeAnimationClip",
                    BindingFlags.Public | BindingFlags.Instance);
                return activeClipProperty?.GetValue(state) as AnimationClip;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"활성 애니메이션 클립을 가져오는 중 오류 발생: {e.Message}");
                return null;
            }
        }

        // ==========================================================
        // State에서 활성 Root 게임오브젝트 가져오기
        // ==========================================================
        private GameObject GetActiveRootGameObjectFromState(object state)
        {
            if (state == null) return null;

            try
            {
                var rootGoProperty = state.GetType().GetProperty("activeRootGameObject",
                    BindingFlags.Public | BindingFlags.Instance);
                return rootGoProperty?.GetValue(state) as GameObject;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"루트 게임오브젝트를 가져오는 중 오류 발생: {e.Message}");
                return null;
            }
        }

        // ==========================================================
        // 애니메이션 윈도우 강제 새로고침
        // ==========================================================
        private void ForceRefreshAnimationWindow()
        {
            try
            {
                object state = GetAnimationWindowState();
                if (state == null) return;

                var frameProperty = state.GetType().GetProperty("currentFrame",
                    BindingFlags.Public | BindingFlags.Instance);
                if (frameProperty == null) return;

                int currentFrame = (int)frameProperty.GetValue(state, null);

                frameProperty.SetValue(state, currentFrame + 1, null);
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        if (frameProperty != null && state != null)
                        {
                            frameProperty.SetValue(state, currentFrame, null);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"애니메이션 윈도우 새로고침 중 오류: {e.Message}");
                    }
                };
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"애니메이션 윈도우 새로고침 중 오류: {e.Message}");
            }
        }
        #endregion
    }
}