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
            
            GameObject rootObject = GetActiveRootGameObjectFromState(state);
            if (rootObject == null) 
            { 
                Debug.LogError("애니메이션 루트 오브젝트를 찾을 수 없습니다."); 
                return; 
            }

            float loopDurationSecs = activeClip.length;
            if (loopDurationSecs <= 0) 
            { 
                Debug.LogError("클립 길이가 0보다 커야 합니다."); 
                return; 
            }

            // 애니메이션이 Loop로 설정되어 있는지 확인
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(activeClip);
            if (!settings.loopTime)
            {
                Debug.LogWarning("애니메이션이 Loop로 설정되어 있지 않습니다. Loop 애니메이션에만 사용하는 것을 권장합니다.");
            }

            float timeOffset = (float)frameOffset / activeClip.frameRate;
            // 오프셋을 루프 길이로 정규화
            timeOffset = timeOffset % loopDurationSecs;
            if (timeOffset < 0)
                timeOffset += loopDurationSecs;

            Undo.RecordObject(activeClip, "Apply Loop Animation Offset");
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(activeClip);
            bool anyCurveModified = false;

            string selectedObjectPath = AnimationUtility.CalculateTransformPath(selectedObject.transform, rootObject.transform);

            foreach (var binding in bindings)
            {
                if (binding.path == selectedObjectPath && IsPropertyTypeMatch(binding.propertyName, propertyType))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(activeClip, binding);
                    if (curve == null || curve.keys.Length == 0) continue;

                    // 새로운 커브 생성
                    AnimationCurve newCurve = CreateOffsetCurve(curve, timeOffset, loopDurationSecs);
                    
                    if (newCurve != null)
                    {
                        AnimationUtility.SetEditorCurve(activeClip, binding, newCurve);
                        anyCurveModified = true;
                    }
                }
            }

            if (anyCurveModified)
            {
                EditorUtility.SetDirty(activeClip);
                AssetDatabase.SaveAssets();
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
            
            // 원본 키프레임들을 시간 오프셋만큼 이동
            foreach (var originalKey in originalCurve.keys)
            {
                float newTime = originalKey.time + timeOffset;
                
                // 루프 범위를 벗어나는 경우 래핑
                while (newTime >= loopDuration) newTime -= loopDuration;
                while (newTime < 0) newTime += loopDuration;
                
                // 모든 키프레임 속성을 그대로 복사
                var newKey = new Keyframe(newTime, originalKey.value, originalKey.inTangent, originalKey.outTangent)
                {
                    inWeight = originalKey.inWeight,
                    outWeight = originalKey.outWeight,
                    weightedMode = originalKey.weightedMode
                };
                
                offsetKeys.Add(newKey);
            }

            // 시간순으로 정렬
            offsetKeys.Sort((a, b) => a.time.CompareTo(b.time));

            // 중복 시간 키프레임 제거
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

            // 루프 연속성 처리: 시작과 끝에서 값이 연속되는지 확인하고 필요시 조정
            if (finalKeys.Count > 0)
            {
                // 0초 지점의 값 계산 (오프셋된 위치에서의 원본 값)
                float valueAt0 = originalCurve.Evaluate((0 - timeOffset + loopDuration * 100) % loopDuration);
                
                // 0초 키프레임이 있는지 확인
                bool hasKeyAt0 = false;
                for (int i = 0; i < finalKeys.Count; i++)
                {
                    if (Mathf.Abs(finalKeys[i].time) < timeEpsilon)
                    {
                        hasKeyAt0 = true;
                        break;
                    }
                }

                // 0초 키프레임이 없으면 추가
                if (!hasKeyAt0)
                {
                    // 원본에서 해당 시점의 탄젠트 계산
                    float originalTimeAt0 = (0 - timeOffset + loopDuration * 100) % loopDuration;
                    float deltaTime = 0.001f;
                    float valueBefore = originalCurve.Evaluate((originalTimeAt0 - deltaTime + loopDuration) % loopDuration);
                    float valueAfter = originalCurve.Evaluate((originalTimeAt0 + deltaTime) % loopDuration);
                    float tangent = (valueAfter - valueBefore) / (2f * deltaTime);
                    
                    finalKeys.Insert(0, new Keyframe(0f, valueAt0, tangent, tangent));
                }

                // 루프 끝 지점 키프레임 처리
                bool hasKeyAtEnd = false;
                for (int i = 0; i < finalKeys.Count; i++)
                {
                    if (Mathf.Abs(finalKeys[i].time - loopDuration) < timeEpsilon)
                    {
                        hasKeyAtEnd = true;
                        break;
                    }
                }

                // 끝 키프레임이 없으면 추가 (시작과 같은 값)
                if (!hasKeyAtEnd)
                {
                    float originalTimeAtEnd = (loopDuration - timeOffset + loopDuration * 100) % loopDuration;
                    float deltaTime = 0.001f;
                    float valueBefore = originalCurve.Evaluate((originalTimeAtEnd - deltaTime + loopDuration) % loopDuration);
                    float valueAfter = originalCurve.Evaluate((originalTimeAtEnd + deltaTime) % loopDuration);
                    float tangent = (valueAfter - valueBefore) / (2f * deltaTime);
                    
                    finalKeys.Add(new Keyframe(loopDuration, valueAt0, tangent, tangent)); // valueAt0과 같아야 함
                }
                else
                {
                    // 기존 끝 키프레임의 값을 시작값과 맞춤
                    for (int i = 0; i < finalKeys.Count; i++)
                    {
                        if (Mathf.Abs(finalKeys[i].time - loopDuration) < timeEpsilon)
                        {
                            var endKey = finalKeys[i];
                            endKey.value = valueAt0; // 루프 연속성을 위해 시작값과 동일하게
                            finalKeys[i] = endKey;
                            break;
                        }
                    }
                }
            }

            // 최종 정렬
            finalKeys.Sort((a, b) => a.time.CompareTo(b.time));

            // 새 커브 생성
            var newCurve = new AnimationCurve(finalKeys.ToArray());
            
            // 원본 설정 보존
            newCurve.preWrapMode = originalCurve.preWrapMode;
            newCurve.postWrapMode = originalCurve.postWrapMode;

            return newCurve;
        }

        // 루프를 고려하여 커브 값을 평가하는 헬퍼 메서드
        private float EvaluateLoopingCurve(AnimationCurve curve, float time, float loopDuration)
        {
            // 시간을 루프 범위 내로 정규화
            float normalizedTime = time % loopDuration;
            if (normalizedTime < 0) normalizedTime += loopDuration;
            
            return curve.Evaluate(normalizedTime);
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