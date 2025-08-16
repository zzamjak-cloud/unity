using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace CAT.Utility
{
    public class AnimationOffsetWindow : EditorWindow
    {
        private int frameOffset = 0;
        private enum PropertyType { Position, Rotation, Scale }

        [MenuItem("CAT/Utility/Animation Offset Window")]
        private static void ShowWindow()
        {
            GetWindow<AnimationOffsetWindow>("Offset").Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += RepaintOnFocus;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintOnFocus;
        }

        private void RepaintOnFocus()
        {
            if (EditorWindow.focusedWindow == this)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            // 윈도우 최소 크기 설정
            this.minSize = new Vector2(120, 200);
            
            GameObject selectedObject = Selection.activeGameObject;
            string selectedObjectName = (selectedObject != null) ? selectedObject.name : "None";
            
            // 선택된 오브젝트 (축약된 텍스트)
            EditorGUILayout.LabelField("Selected", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            // 긴 이름일 경우 축약
            if (selectedObjectName.Length > 12)
                selectedObjectName = selectedObjectName.Substring(0, 9) + "...";
                
            EditorGUILayout.LabelField(selectedObjectName);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);

            // 프레임 오프셋 (컴팩트 레이아웃)
            EditorGUILayout.LabelField("Offset", EditorStyles.boldLabel);
            
            // 버튼 스타일의 입력 필드
            GUIStyle compactIntField = new GUIStyle(EditorStyles.numberField);
            compactIntField.fixedHeight = 25;
            
            frameOffset = EditorGUILayout.IntField(frameOffset, compactIntField, GUILayout.Height(25));
            
            // 축약된 도움말
            GUIStyle helpStyle = new GUIStyle(EditorStyles.helpBox);
            helpStyle.fontSize = 9;
            helpStyle.wordWrap = true;
            EditorGUILayout.LabelField("Move loop cycle", helpStyle);

            EditorGUILayout.Space(8);

            // 적용 버튼들 (축약된 텍스트)
            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
            if (GUILayout.Button("Position", GUILayout.Height(22)))
            {
                ApplyLoopOffset(PropertyType.Position);
            }

            GUI.backgroundColor = new Color(0.9f, 0.9f, 0.6f);
            if (GUILayout.Button("Rotation", GUILayout.Height(22)))
            {
                ApplyLoopOffset(PropertyType.Rotation);
            }

            GUI.backgroundColor = new Color(0.9f, 0.6f, 0.6f);
            if (GUILayout.Button("Scale", GUILayout.Height(22)))
            {
                ApplyLoopOffset(PropertyType.Scale);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);

            // 리셋 버튼
            if (GUILayout.Button("Reset", GUILayout.Height(20)))
            {
                frameOffset = 0;
            }
        }

        private void ApplyLoopOffset(PropertyType propertyType)
        {
            if (frameOffset == 0) 
            {
                Debug.LogWarning("프레임 오프셋이 0입니다. 오프셋 값을 설정해주세요.");
                return;
            }

            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null) 
            { 
                Debug.LogError("오브젝트를 선택해주세요."); 
                return; 
            }

            object state = GetAnimationWindowState();
            if (state == null)
            {
                Debug.LogError("애니메이션 윈도우가 열려있지 않습니다.");
                return;
            }

            AnimationClip activeClip = GetActiveAnimationClipFromState(state);
            if (activeClip == null) 
            { 
                Debug.LogError("애니메이션 클립을 선택해주세요."); 
                return; 
            }

            // ==========================================================
            // [수정] 실제 에셋 경로를 가져와 원본 클립을 로드합니다.
            // ==========================================================
            string clipPath = AssetDatabase.GetAssetPath(activeClip);
            if (string.IsNullOrEmpty(clipPath))
            {
                Debug.LogError("선택된 클립의 에셋 경로를 찾을 수 없습니다. (저장되지 않은 클립일 수 있습니다)");
                return;
            }
            AnimationClip sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (sourceClip == null)
            {
                Debug.LogError($"에셋 경로에서 클립을 불러오는 데 실패했습니다: {clipPath}");
                return;
            }
            // ==========================================================

            GameObject rootObject = GetActiveRootGameObjectFromState(state);
            if (rootObject == null) 
            { 
                Debug.LogError("애니메이션 루트 오브젝트를 찾을 수 없습니다."); 
                return; 
            }

            // 이제부터 activeClip 대신 sourceClip을 사용합니다.
            float loopDurationSecs = sourceClip.length;
            if (loopDurationSecs <= 0) 
            { 
                Debug.LogError("클립 길이가 0보다 커야 합니다."); 
                return; 
            }
            
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(sourceClip);
            if (!settings.loopTime)
            {
                Debug.LogWarning("애니메이션이 Loop로 설정되어 있지 않습니다. Loop 애니메이션에만 사용하는 것을 권장합니다.");
            }

            float timeOffset = (float)frameOffset / sourceClip.frameRate;
            timeOffset = timeOffset % loopDurationSecs;
            if (timeOffset < 0)
                timeOffset += loopDurationSecs;

            // Undo 기록 대상을 sourceClip으로 변경합니다.
            Undo.RecordObject(sourceClip, "Apply Loop Animation Offset");
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(sourceClip);
            bool anyCurveModified = false;

            string selectedObjectPath = AnimationUtility.CalculateTransformPath(selectedObject.transform, rootObject.transform);

            foreach (var binding in bindings)
            {
                if (binding.path == selectedObjectPath && IsPropertyTypeMatch(binding.propertyName, propertyType))
                {
                    // 커브를 가져오고 설정할 때도 sourceClip을 사용합니다.
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
                // 변경사항 저장을 위해 sourceClip을 dirty 처리합니다.
                EditorUtility.SetDirty(sourceClip);
                // AssetDatabase.SaveAssets()는 필요 없으므로 제거합니다.
                Debug.Log($"루프 오프셋 적용 완료: {frameOffset} 프레임 ({timeOffset:F3}초)");
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