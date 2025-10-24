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
    public static class AnimationOffset
    {
        private static float offsetValue = 0f;
        private static bool isTimeInputMode = false;
        [System.Flags]
        private enum PropertyType { Position = 1, Rotation = 2, Scale = 4 }

        private static float objectNameWidth = 120f;
        private static float inputFieldWidth = 100f;
        private static float modeButtonWidth = 50f;
        private static float resetButtonWidth = 40f;
        private static float keyGenButtonWidth = 45f;      // [+All], [+Pos], [+Rot], [+Sca]
        private static float offsetButtonWidth = 68f;      // [Position], [Rotation], [Scale]
        private static float cleanButtonWidth = 60f;         // [Clean]
        private static float sectionSpacing = 10f;
        
        private static bool _isRefreshPending = false;

        // EditorApplication.update를 제거하고, 에디터 초기화 후 단 한 번만 실행되도록 변경
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += InjectUI;
        }

        /// <summary>
        /// 애니메이션 창을 찾아 UI를 주입하는 핵심 로직. 에디터 시작 시 한 번만 호출됩니다.
        /// </summary>
        private static void InjectUI()
        {
            var editorAssembly = typeof(Editor).Assembly;
            var animationWindows = Resources.FindObjectsOfTypeAll(editorAssembly.GetType("UnityEditor.AnimationWindow"));
            if (animationWindows.Length == 0) return;

            var animationWindow = (EditorWindow)animationWindows[0];
            var rawRoot = animationWindow.rootVisualElement;
            if (rawRoot == null) return;

            // 스크립트 리컴파일 시 UI가 중복으로 추가되는 것을 방지
            if (rawRoot.Q<VisualElement>("AnimationOffsetContainer") != null)
            {
                return;
            }

            var parentContainer = new VisualElement
            {
                name = "AnimationOffsetContainer", // 중복 주입 방지를 위한 이름 지정
                style =
                {
                    position = Position.Absolute,
                    right = 25f,
                    bottom = 15f,
                    width = 390f,
                    height = 42f,
                    flexDirection = FlexDirection.Row
                }
            };

            var imguiContainer = new IMGUIContainer(OnInjectedGUI);
            imguiContainer.style.flexGrow = 1;

            parentContainer.Add(imguiContainer);
            rawRoot.Add(parentContainer);
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

            if (GUILayout.Button(new GUIContent("R", "Reset offset to 0"), EditorStyles.toolbarButton, GUILayout.Width(resetButtonWidth)))
            {
                offsetValue = 0f;
                GUI.FocusControl(null);
            }

            GUILayout.Space(sectionSpacing);

            Color cleanButtonColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
            if (GUILayout.Button(new GUIContent("Clean", "Remove unnecessary keys for all objects"), EditorStyles.toolbarButton, GUILayout.Width(cleanButtonWidth)))
            {
                CleanAllUnnecessaryKeys();
            }
            GUI.backgroundColor = cleanButtonColor;

            EditorGUILayout.EndHorizontal();
            
            // 두 번째 줄: 키 생성 버튼들과 Position, Rotation, Scale 버튼들
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            Color keyGenButtonColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 1.0f, 0.6f);
            
            if (GUILayout.Button(new GUIContent("+All", "Add keys for all Transform properties"), EditorStyles.toolbarButton, GUILayout.Width(keyGenButtonWidth)))
            {
                AddTransformKeys(PropertyType.Position | PropertyType.Rotation | PropertyType.Scale);
            }
            if (GUILayout.Button(new GUIContent("+Pos", "Add keys for Position"), EditorStyles.toolbarButton, GUILayout.Width(keyGenButtonWidth)))
            {
                AddTransformKeys(PropertyType.Position);
            }
            if (GUILayout.Button(new GUIContent("+Rot", "Add keys for Rotation"), EditorStyles.toolbarButton, GUILayout.Width(keyGenButtonWidth)))
            {
                AddTransformKeys(PropertyType.Rotation);
            }
            if (GUILayout.Button(new GUIContent("+Sca", "Add keys for Scale"), EditorStyles.toolbarButton, GUILayout.Width(keyGenButtonWidth)))
            {
                AddTransformKeys(PropertyType.Scale);
            }
            
            GUI.backgroundColor = keyGenButtonColor;
            
            GUILayout.Space(sectionSpacing);
            
            if (GUILayout.Button(new GUIContent("Position", "Apply offset to Position"), EditorStyles.toolbarButton, GUILayout.Width(offsetButtonWidth)))
            {
                ApplyLoopOffset(PropertyType.Position);
            }
            if (GUILayout.Button(new GUIContent("Rotation", "Apply offset to Rotation"), EditorStyles.toolbarButton, GUILayout.Width(offsetButtonWidth)))
            {
                ApplyLoopOffset(PropertyType.Rotation);
            }
            if (GUILayout.Button(new GUIContent("Scale", "Apply offset to Scale"), EditorStyles.toolbarButton, GUILayout.Width(offsetButtonWidth)))
            {
                ApplyLoopOffset(PropertyType.Scale);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void AddTransformKeys(PropertyType propertyTypes)
        {
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

            string selectedObjectPath = AnimationUtility.CalculateTransformPath(selectedObject.transform, rootObject.transform);
            float clipDuration = sourceClip.length;
            
            Undo.RecordObject(sourceClip, "Add Transform Keys");
            
            bool anyKeyAdded = false;
            string addedProperties = "";

            // Position 키 추가
            if ((propertyTypes & PropertyType.Position) != 0)
            {
                if (AddPropertyKeys(sourceClip, selectedObjectPath, "m_LocalPosition", selectedObject.transform.localPosition, clipDuration))
                {
                    anyKeyAdded = true;
                    addedProperties += "Position ";
                }
            }

            // Rotation 키 추가
            if ((propertyTypes & PropertyType.Rotation) != 0)
            {
                Vector3 eulerAngles = selectedObject.transform.localEulerAngles;
                if (AddPropertyKeys(sourceClip, selectedObjectPath, "m_LocalRotation", eulerAngles, clipDuration))
                {
                    anyKeyAdded = true;
                    addedProperties += "Rotation ";
                }
            }

            // Scale 키 추가
            if ((propertyTypes & PropertyType.Scale) != 0)
            {
                if (AddPropertyKeys(sourceClip, selectedObjectPath, "m_LocalScale", selectedObject.transform.localScale, clipDuration))
                {
                    anyKeyAdded = true;
                    addedProperties += "Scale ";
                }
            }

            if (anyKeyAdded)
            {
                Debug.Log($"Transform keys added for: {addedProperties.Trim()} at frame 0 and {(clipDuration > 0 ? "end" : "0")}");
                ForceRefreshAnimationWindow();
            }
            else
            {
                Debug.LogWarning("Failed to add transform keys.");
            }
        }

        private static bool AddPropertyKeys(AnimationClip clip, string objectPath, string propertyName, Vector3 value, float duration)
        {
            try
            {
                // X, Y, Z 각각에 대해 키 추가
                string[] components = { ".x", ".y", ".z" };
                float[] values = { value.x, value.y, value.z };
                
                for (int i = 0; i < 3; i++)
                {
                    string fullPropertyName = propertyName + components[i];
                    EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, typeof(Transform), fullPropertyName);
                    
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null)
                    {
                        curve = new AnimationCurve();
                    }
                    
                    // 0프레임에 키 추가
                    curve.AddKey(0f, values[i]);
                    
                    // 마지막 프레임에 키 추가 (시작과 다를 때만)
                    if (duration > 0f)
                    {
                        curve.AddKey(duration, values[i]);
                    }
                    
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to add keys for {propertyName}: {e.Message}");
                return false;
            }
        }

        private static void CleanAllUnnecessaryKeys()
        {
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

            Undo.RecordObject(sourceClip, "Clean All Unnecessary Keys");
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(sourceClip);
            bool anyCurveModified = false;
            int totalKeysRemoved = 0;
            int objectsProcessed = 0;
            var processedObjects = new HashSet<string>();

            foreach (var binding in bindings)
            {
                if (!processedObjects.Contains(binding.path))
                {
                    processedObjects.Add(binding.path);
                    objectsProcessed++;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                if (curve == null || curve.keys.Length <= 2) continue;

                AnimationCurve cleanedCurve = RemoveUnnecessaryKeys(curve);
                if (cleanedCurve != null && cleanedCurve.keys.Length != curve.keys.Length)
                {
                    AnimationUtility.SetEditorCurve(sourceClip, binding, cleanedCurve);
                    anyCurveModified = true;
                    totalKeysRemoved += curve.keys.Length - cleanedCurve.keys.Length;
                }
            }

            if (anyCurveModified)
            {
                Debug.Log($"All Clean completed: {totalKeysRemoved} keys removed from {objectsProcessed} objects");
                ForceRefreshAnimationWindow();
            }
            else
            {
                Debug.LogWarning("No unnecessary keys found in the animation clip.");
            }
        }

        private static AnimationCurve RemoveUnnecessaryKeys(AnimationCurve originalCurve)
        {
            if (originalCurve.keys.Length <= 2) return originalCurve;

            var keys = new List<Keyframe>(originalCurve.keys);
            var cleanedKeys = new List<Keyframe>();
            
            // 첫 번째 키는 항상 유지
            cleanedKeys.Add(keys[0]);
            
            for (int i = 1; i < keys.Count - 1; i++)
            {
                var currentKey = keys[i];
                var previousKey = keys[i - 1];
                var nextKey = keys[i + 1];
                
                // 현재 키의 값이 이전 키와 다음 키의 값과 동일한지 확인
                bool isUnnecessary = Mathf.Approximately(currentKey.value, previousKey.value) && 
                                   Mathf.Approximately(currentKey.value, nextKey.value);
                
                // 불필요한 키가 아닌 경우에만 추가
                if (!isUnnecessary)
                {
                    cleanedKeys.Add(currentKey);
                }
            }
            
            // 마지막 키는 항상 유지
            cleanedKeys.Add(keys[keys.Count - 1]);
            
            // 키가 변경되지 않았다면 원본 반환
            if (cleanedKeys.Count == originalCurve.keys.Length)
            {
                return originalCurve;
            }
            
            var newCurve = new AnimationCurve(cleanedKeys.ToArray());
            newCurve.preWrapMode = originalCurve.preWrapMode;
            newCurve.postWrapMode = originalCurve.postWrapMode;
            return newCurve;
        }

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
                Debug.Log($"Loop offset applied successfully: {offsetValue} " + (isTimeInputMode ? "s" : "frames"));
                ForceRefreshAnimationWindow();
            }
            else
            {
                Debug.LogWarning($"No '{propertyType}' curves found for '{selectedObject.name}'.");
            }
        }

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

            var finalKeys = new List<Keyframe>();
            const float epsilon = 0.0001f;

            if (offsetKeys.Count > 0)
            {
                finalKeys.Add(offsetKeys[0]);
                for (int i = 1; i < offsetKeys.Count; i++)
                {
                    if (Mathf.Abs(offsetKeys[i].time - offsetKeys[i - 1].time) > epsilon)
                    {
                        finalKeys.Add(offsetKeys[i]);
                    }
                }
            }

            float originalTimeAt0 = (0 - timeOffset + loopDuration * 100) % loopDuration;
            float valueAt0 = originalCurve.Evaluate(originalTimeAt0);
            float tangentAt0 = CalculateTangent(originalCurve, originalTimeAt0);

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

        private static float CalculateTangent(AnimationCurve curve, float time)
        {
            const float deltaTime = 0.0001f;
            float valueBefore = curve.Evaluate(time - deltaTime);
            float valueAfter = curve.Evaluate(time + deltaTime);
            float divisor = 2 * deltaTime;
            if (divisor == 0) return 0;
            return (valueAfter - valueBefore) / divisor;
        }

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

        private static object GetAnimationWindowState()
        {
            var animationWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
            if (animationWindowType == null) return null;
            var window = EditorWindow.GetWindow(animationWindowType, false, null, false);
            if (window == null) return null;
            var stateProperty = animationWindowType.GetProperty("state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return stateProperty?.GetValue(window);
        }

        private static AnimationClip GetActiveAnimationClipFromState(object state)
        {
            if (state == null) return null;
            var activeClipProperty = state.GetType().GetProperty("activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
            return activeClipProperty?.GetValue(state) as AnimationClip;
        }

        private static GameObject GetActiveRootGameObjectFromState(object state)
        {
            if (state == null) return null;
            var rootGoProperty = state.GetType().GetProperty("activeRootGameObject", BindingFlags.Public | BindingFlags.Instance);
            return rootGoProperty?.GetValue(state) as GameObject;
        }

        private static void ForceRefreshAnimationWindow()
        {
            if (_isRefreshPending) return;

            try
            {
                object state = GetAnimationWindowState();
                if (state == null) return;
                var frameProperty = state.GetType().GetProperty("currentFrame", BindingFlags.Public | BindingFlags.Instance);
                if (frameProperty == null) return;
                int currentFrame = (int)frameProperty.GetValue(state, null);
                
                _isRefreshPending = true;
                frameProperty.SetValue(state, currentFrame + 1, null);
                
                EditorApplication.delayCall += () => 
                { 
                    if (state != null) 
                    {
                        frameProperty.SetValue(state, currentFrame, null);
                    }
                    _isRefreshPending = false;
                };
            }
            catch { /* Fails silently */ }
        }
        #endregion
    }
}
