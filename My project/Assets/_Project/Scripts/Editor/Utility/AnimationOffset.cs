using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace CAT.Utility
{
    /// <summary>
    /// Animation Window에 Offset 기능을 추가합니다.
    /// - Position, Rotation, Scale에 대해 각각 Offset을 적용할 수 있습니다.
    /// - Offset 단위를 Frame과 Time(초)로 전환할 수 있습니다.
    /// - Offset 값은 음수도 가능합니다.
    /// - Offset 적용 시 Animation Clip이 수정되며, Undo가 지원됩니다.
    /// - Animation Window가 열려 있어야 하며, Animation Clip이 선택되어 있어야 합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class AnimationOffset
    {
        private static float offsetValue = 0f;
        private static bool isTimeInputMode = false;
        private enum PropertyType { Position, Rotation, Scale }

        private static float objectNameWidth = 120f;
        private static float inputFieldWidth = 100f;
        private static float modeButtonWidth = 50f;
        private static float resetButtonWidth = 50f;
        private static float actionButtonWidth = 70f;
        private static float sectionSpacing = 10f;

        private static EditorWindow _cachedAnimationWindow;
        private static bool _isUiInjected = false;
        
        // [수정] 성능 저하 방지를 위한 플래그 추가
        private static bool _isRefreshPending = false;

        // Animation Window가 열려 있는지 확인하고, UI를 주입합니다.
        static AnimationOffset()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            // [수정] 창이 캐시되어 있다면, 닫혔는지 여부만 가볍게 확인합니다.
            if (_cachedAnimationWindow != null)
            {
                // 창이 닫혔으면 캐시를 초기화합니다.
                if (_cachedAnimationWindow.rootVisualElement.parent == null)
                {
                    _cachedAnimationWindow = null;
                    _isUiInjected = false;
                }
            }
            // [수정] 창이 캐시되어 있지 않을 때만 창을 찾는 무거운 작업을 수행합니다.
            else
            {
                var editorAssembly = typeof(Editor).Assembly;
                var animationWindows = Resources.FindObjectsOfTypeAll(editorAssembly.GetType("UnityEditor.AnimationWindow"));
                if (animationWindows.Length > 0)
                {
                    _cachedAnimationWindow = (EditorWindow)animationWindows[0];
                }
            }

            // 창이 존재하고, 아직 UI가 주입되지 않았다면 주입을 시도합니다.
            if (!_isUiInjected && _cachedAnimationWindow != null)
            {
                var rawRoot = _cachedAnimationWindow.rootVisualElement;
                if (rawRoot == null) return;

                var parentContainer = new VisualElement
                {
                    style =
                    {
                        position = Position.Absolute,
                        right = 25f,
                        bottom = 15f,
                        width = 550f,
                        height = 22f,
                        flexDirection = FlexDirection.Row
                    }
                };

                var imguiContainer = new IMGUIContainer(OnInjectedGUI);
                imguiContainer.style.flexGrow = 1;

                parentContainer.Add(imguiContainer);
                rawRoot.Add(parentContainer);

                _isUiInjected = true;
            }
        }

        // Animation Window에 Offset UI를 그립니다.
        private static void OnInjectedGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GameObject selectedObject = Selection.activeGameObject;
            string selectedObjectName = (selectedObject != null) ? selectedObject.name : "None";
            EditorGUILayout.LabelField(new GUIContent(selectedObjectName, "Selected GameObject"), GUILayout.Width(objectNameWidth));

            Color originalColor = GUI.backgroundColor;
            string modeText;
            if (isTimeInputMode)
            {
                GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
                modeText = "Time";
            }
            else
            {
                GUI.backgroundColor = new Color(0.5f, 0.7f, 1.0f);
                modeText = "Frame";
            }

            if (GUILayout.Button(new GUIContent(modeText, "Switch input: Frames vs Seconds"), EditorStyles.toolbarButton, GUILayout.Width(modeButtonWidth)))
            {
                if (offsetValue != 0)
                {
                    object state = GetAnimationWindowState();
                    AnimationClip activeClip = GetActiveAnimationClipFromState(state);
                    float frameRate = (activeClip != null) ? activeClip.frameRate : 60f;

                    if (isTimeInputMode) offsetValue *= frameRate;
                    else offsetValue /= frameRate;
                }
                isTimeInputMode = !isTimeInputMode;
                GUI.FocusControl(null);
            }

            GUI.backgroundColor = originalColor;

            string inputTooltip = isTimeInputMode ? "Time (s) offset" : "Frame offset";
            offsetValue = EditorGUILayout.FloatField(new GUIContent("", inputTooltip), offsetValue, EditorStyles.toolbarTextField, GUILayout.Width(inputFieldWidth));

            if (GUILayout.Button(new GUIContent("Reset", "Reset offset to 0"), EditorStyles.toolbarButton, GUILayout.Width(resetButtonWidth)))
            {
                offsetValue = 0f;
                GUI.FocusControl(null);
            }

            GUILayout.Space(sectionSpacing);

            if (GUILayout.Button(new GUIContent("Position", "Apply offset to Position"), EditorStyles.toolbarButton, GUILayout.Width(actionButtonWidth)))
            {
                ApplyLoopOffset(PropertyType.Position);
            }
            if (GUILayout.Button(new GUIContent("Rotation", "Apply offset to Rotation"), EditorStyles.toolbarButton, GUILayout.Width(actionButtonWidth)))
            {
                ApplyLoopOffset(PropertyType.Rotation);
            }
            if (GUILayout.Button(new GUIContent("Scale", "Apply offset to Scale"), EditorStyles.toolbarButton, GUILayout.Width(actionButtonWidth)))
            {
                ApplyLoopOffset(PropertyType.Scale);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ... ApplyLoopOffset 및 다른 헬퍼 메서드들은 이전과 동일하게 유지 ...
        private static void ApplyLoopOffset(PropertyType propertyType)
        {
            if (offsetValue == 0) { Debug.LogWarning("Offset value is 0."); return; }

            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null) { Debug.LogError("Select a GameObject."); return; }

            object state = GetAnimationWindowState();
            if (state == null) { Debug.LogError("Animation Window is not open."); return; }

            AnimationClip activeClip = GetActiveAnimationClipFromState(state);
            if (activeClip == null) { Debug.LogError("Select an Animation Clip."); return; }

            string clipPath = AssetDatabase.GetAssetPath(activeClip);
            if (string.IsNullOrEmpty(clipPath)) { Debug.LogError("Cannot find asset path for the clip."); return; }

            AnimationClip sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (sourceClip == null) { Debug.LogError($"Failed to load clip from path: {clipPath}"); return; }

            GameObject rootObject = GetActiveRootGameObjectFromState(state);
            if (rootObject == null) { Debug.LogError("Cannot find animation root GameObject."); return; }

            float loopDurationSecs = sourceClip.length;
            if (loopDurationSecs <= 0) { Debug.LogError("Clip length must be greater than 0."); return; }

            float timeOffset = isTimeInputMode ? offsetValue : (offsetValue / sourceClip.frameRate);
            timeOffset %= loopDurationSecs;
            if (timeOffset < 0) timeOffset += loopDurationSecs;

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
                // [수정] EditorUtility.SetDirty(sourceClip); 라인 제거
                Debug.Log($"Loop offset applied successfully: {offsetValue} " + (isTimeInputMode ? "s" : "frames"));
                ForceRefreshAnimationWindow();
            }
            else
            {
                Debug.LogWarning($"No '{propertyType}' curves found for '{selectedObject.name}'.");
            }
        }

        // 선택된 속성에 대해 오프셋이 적용된 새로운 AnimationCurve를 생성합니다.
        private static AnimationCurve CreateOffsetCurve(AnimationCurve originalCurve, float timeOffset, float loopDuration)
        {
            if (originalCurve.keys.Length == 0) return null;

            var offsetKeys = new List<Keyframe>();
            foreach (var originalKey in originalCurve.keys)
            {
                float newTime = (originalKey.time + timeOffset) % loopDuration;
                if (newTime < 0) newTime += loopDuration;
                offsetKeys.Add(new Keyframe(newTime, originalKey.value, originalKey.inTangent, originalKey.outTangent, originalKey.inWeight, originalKey.outWeight) { weightedMode = originalKey.weightedMode });
            }
            offsetKeys.Sort((a, b) => a.time.CompareTo(b.time));

            // [수정] 중복 키 제거를 위한 더 안정적인 로직
            var finalKeys = new List<Keyframe>();
            const float epsilon = 0.0001f;

            if (offsetKeys.Count > 0)
            {
                finalKeys.Add(offsetKeys[0]);
                for (int i = 1; i < offsetKeys.Count; i++)
                {
                    // 이전 키와 시간 값이 거의 동일하면 추가하지 않음
                    if (Mathf.Abs(offsetKeys[i].time - offsetKeys[i - 1].time) > epsilon)
                    {
                        finalKeys.Add(offsetKeys[i]);
                    }
                }
            }

            // 루핑을 위한 시작/끝 지점의 값과 탄젠트를 계산
            float originalTimeAt0 = (0 - timeOffset + loopDuration * 100) % loopDuration;
            float valueAt0 = originalCurve.Evaluate(originalTimeAt0);
            float tangentAt0 = CalculateTangent(originalCurve, originalTimeAt0);

            // 시작점에 키가 있는지 확인하고, 없으면 추가, 있으면 값/탄젠트 업데이트
            bool hasKeyAtStart = finalKeys.Count > 0 && Mathf.Abs(finalKeys[0].time) < epsilon;
            if (!hasKeyAtStart)
            {
                finalKeys.Insert(0, new Keyframe(0f, valueAt0, tangentAt0, tangentAt0));
            }
            else
            {
                var key = finalKeys[0];
                key.value = valueAt0;
                key.inTangent = tangentAt0;
                key.outTangent = tangentAt0;
                finalKeys[0] = key;
            }

            // 끝점에 키가 있는지 확인하고, 없으면 추가, 있으면 값/탄젠트 업데이트
            bool hasKeyAtEnd = finalKeys.Count > 0 && Mathf.Abs(finalKeys[finalKeys.Count - 1].time - loopDuration) < epsilon;
            if (!hasKeyAtEnd)
            {
                finalKeys.Add(new Keyframe(loopDuration, valueAt0, tangentAt0, tangentAt0));
            }
            else
            {
                var key = finalKeys[finalKeys.Count - 1];
                key.value = valueAt0;
                key.inTangent = tangentAt0;
                key.outTangent = tangentAt0;
                finalKeys[finalKeys.Count - 1] = key;
            }

            var newCurve = new AnimationCurve(finalKeys.ToArray());
            newCurve.preWrapMode = originalCurve.preWrapMode;
            newCurve.postWrapMode = originalCurve.postWrapMode;
            return newCurve;
        }

        // [추가] 특정 시간의 커브 탄젠트를 계산하는 헬퍼 메서드
        private static float CalculateTangent(AnimationCurve curve, float time)
        {
            const float deltaTime = 0.0001f;
            float valueBefore = curve.Evaluate(time - deltaTime);
            float valueAfter = curve.Evaluate(time + deltaTime);
            // 분모가 0이 되는 것을 방지
            float divisor = 2 * deltaTime;
            if (divisor == 0) return 0;
            return (valueAfter - valueBefore) / divisor;
        }

        // 속성 이름이 지정된 PropertyType과 일치하는지 확인합니다.
        private static bool IsPropertyTypeMatch(string propertyName, PropertyType type)
        {
            switch (type)
            {
                case PropertyType.Position: return propertyName.Contains("Position");
                case PropertyType.Rotation: return propertyName.Contains("Euler") || propertyName.Contains("Rotation");
                case PropertyType.Scale: return propertyName.Contains("Scale");
                default: return false;
            }
        }

        #region Animation Window Reflection Utilities

        // Animation Window의 상태 객체를 가져옵니다.
        private static object GetAnimationWindowState()
        {
            var animationWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
            if (animationWindowType == null) return null;
            var window = EditorWindow.GetWindow(animationWindowType, false, null, false);
            if (window == null) return null;
            var stateProperty = animationWindowType.GetProperty("state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return stateProperty?.GetValue(window);
        }

        // 상태 객체에서 현재 활성화된 AnimationClip을 가져옵니다.
        private static AnimationClip GetActiveAnimationClipFromState(object state)
        {
            if (state == null) return null;
            var activeClipProperty = state.GetType().GetProperty("activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
            return activeClipProperty?.GetValue(state) as AnimationClip;
        }

        // 상태 객체에서 현재 활성화된 루트 GameObject를 가져옵니다.
        private static GameObject GetActiveRootGameObjectFromState(object state)
        {
            if (state == null) return null;
            var rootGoProperty = state.GetType().GetProperty("activeRootGameObject", BindingFlags.Public | BindingFlags.Instance);
            return rootGoProperty?.GetValue(state) as GameObject;
        }

        // Animation Window를 강제로 새로고침하여 변경 사항을 반영합니다.
        private static void ForceRefreshAnimationWindow()
        {
            // [수정] 새로고침이 이미 예약된 경우 중복 실행을 방지합니다.
            if (_isRefreshPending) return;

            try
            {
                object state = GetAnimationWindowState();
                if (state == null) return;
                var frameProperty = state.GetType().GetProperty("currentFrame", BindingFlags.Public | BindingFlags.Instance);
                if (frameProperty == null) return;
                int currentFrame = (int)frameProperty.GetValue(state, null);
                
                _isRefreshPending = true; // 새로고침 예약 플래그 설정
                frameProperty.SetValue(state, currentFrame + 1, null);
                
                EditorApplication.delayCall += () => 
                { 
                    if (state != null) 
                    {
                        // 프레임을 원위치로 돌려놓습니다.
                        frameProperty.SetValue(state, currentFrame, null);
                    }
                    _isRefreshPending = false; // 작업 완료 후 플래그 해제
                };
            }
            catch { /* Fails silently */ }
        }
        #endregion
    }
}
