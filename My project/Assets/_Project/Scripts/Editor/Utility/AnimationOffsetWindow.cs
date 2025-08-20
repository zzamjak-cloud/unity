using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace CAT.Utility
{
    public class AnimationOffsetWindow : EditorWindow
    {
        private float offsetValue = 0f; 
        private bool isTimeInputMode = false; 

        private enum PropertyType { Position, Rotation, Scale }

        // ==========================================================
        // UI 레이아웃 제어를 위한 변수들
        // ==========================================================
        private float objectNameWidth = 120f;
        private float inputFieldWidth = 100f;
        private float modeButtonWidth = 50f;
        private float resetButtonWidth = 50f;
        private float actionButtonWidth = 150f;
        private float sectionSpacing = 20f; // 섹션 간 여백

        // 마지막으로 감지된 애니메이션 창의 루트 오브젝트
        private GameObject lastKnownAnimRootObject;

        [MenuItem("CAT/Utility/Animation Offset Window")]
        private static void ShowWindow()
        {
            GetWindow<AnimationOffsetWindow>("Offset").Show();
        }

        // ==========================================================
        // [수정] OnEnable: 선택 변경 감지를 위한 이벤트 구독
        // ==========================================================
        private void OnEnable()
        {
            // 하이어라키 창 또는 프로젝트 창의 선택 변경을 감지
            Selection.selectionChanged += Repaint;
            // 애니메이션 창의 선택 변경 등 에디터의 지속적인 업데이트를 감지
            EditorApplication.update += OnEditorUpdate;
        }

        // ==========================================================
        // [수정] OnDisable: 이벤트 구독 해제
        // ==========================================================
        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
            EditorApplication.update -= OnEditorUpdate;
        }

        // ==========================================================
        // [수정] OnEditorUpdate: 애니메이션 창의 선택 변경을 감지하여 UI 갱신
        // ==========================================================
        private void OnEditorUpdate()
        {
            // 애니메이션 창이 열려 있고, 그 안에서 선택된 루트 게임오브젝트가 변경되었는지 확인
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
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(22));

            // 1. 선택된 오브젝트명 (고정 너비)
            GameObject selectedObject = Selection.activeGameObject;
            string selectedObjectName = (selectedObject != null) ? selectedObject.name : "None";
            EditorGUILayout.LabelField(new GUIContent(selectedObjectName, "Currently selected GameObject"), GUILayout.Width(objectNameWidth));

            // 유연한 공간을 두어 오른쪽 정렬 효과
            // GUILayout.FlexibleSpace(); 
            
            // 2. 입력 모드 전환 버튼 (색상 및 텍스트 변경)
            Color originalColor = GUI.backgroundColor;
            string modeText;
            if (isTimeInputMode)
            {
                // Time 모드일 때: 빨간색 버튼
                GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f); 
                modeText = "Time";
            }
            else
            {
                // Frame 모드일 때: 파란색 버튼
                GUI.backgroundColor = new Color(0.5f, 0.7f, 1.0f); 
                modeText = "Frame";
            }

            if (GUILayout.Button(new GUIContent(modeText, "Switch input between Frames and Seconds"), EditorStyles.toolbarButton, GUILayout.Width(modeButtonWidth)))
            {
                if (offsetValue != 0)
                {
                    object state = GetAnimationWindowState();
                    AnimationClip activeClip = GetActiveAnimationClipFromState(state);
                    // 클립이 있으면 해당 클립의 frameRate 사용, 없으면 기본값 60 사용
                    float frameRate = (activeClip != null) ? activeClip.frameRate : 60f;

                    // 현재 Time 모드 -> Frame 모드로 전환
                    if (isTimeInputMode) 
                    {
                        offsetValue *= frameRate;
                    }
                    // 현재 Frame 모드 -> Time 모드로 전환
                    else 
                    {
                        offsetValue /= frameRate;
                    }
                }
                
                isTimeInputMode = !isTimeInputMode; // 모드 전환
                GUI.FocusControl(null); // 포커스를 해제하여 필드 값 즉시 갱신
            }
            
            GUI.backgroundColor = originalColor; // GUI 색상 원상 복구

            // 3. 오프셋 입력 필드 (고정 너비)
            string inputTooltip = isTimeInputMode ? "Time (s) offset value" : "Frame offset value";
            offsetValue = EditorGUILayout.FloatField(new GUIContent("", inputTooltip), offsetValue, EditorStyles.toolbarTextField, GUILayout.Width(inputFieldWidth));

            // 섹션 간 여백
            // GUILayout.Space(sectionSpacing);

            

            // 4. 리셋 버튼 (모드 버튼 바로 옆)
            if (GUILayout.Button(new GUIContent("Reset", "Reset offset value to 0"), EditorStyles.toolbarButton, GUILayout.Width(resetButtonWidth)))
            {
                offsetValue = 0f;
                GUI.FocusControl(null); 
            }

            // 섹션 간 여백
            GUILayout.Space(sectionSpacing);

            // 5. 적용 버튼들 (고정 너비)
            //GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
            if (GUILayout.Button(new GUIContent("Position", "Apply offset to Position curves"), EditorStyles.toolbarButton, GUILayout.Width(actionButtonWidth)))
            {
                ApplyLoopOffset(PropertyType.Position);
            }

            //GUI.backgroundColor = new Color(0.9f, 0.9f, 0.6f);
            if (GUILayout.Button(new GUIContent("Rotation", "Apply offset to Rotation curves"), EditorStyles.toolbarButton, GUILayout.Width(actionButtonWidth)))
            {
                ApplyLoopOffset(PropertyType.Rotation);
            }

            //GUI.backgroundColor = new Color(0.9f, 0.6f, 0.6f);
            if (GUILayout.Button(new GUIContent("Scale", "Apply offset to Scale curves"), EditorStyles.toolbarButton, GUILayout.Width(actionButtonWidth)))
            {
                ApplyLoopOffset(PropertyType.Scale);
            }
            GUI.backgroundColor = originalColor;

            EditorGUILayout.EndHorizontal();
        }
        
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
