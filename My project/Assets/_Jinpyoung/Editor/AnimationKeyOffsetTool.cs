using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace CAT.Utility
{
    public class AnimationKeyOffsetTool : EditorWindow
    {
        private Vector3 rotationOffset = Vector3.zero;
        private Vector3 positionOffset = Vector3.zero;
        private Vector3 scaleOffset = Vector3.zero;

        [MenuItem("Tools/Animation/Offset Selected Keys")]
        static void Init()
        {
            GetWindow<AnimationKeyOffsetTool>("Key Offset Tool").Show();
        }

        void OnGUI()
        {
            GUILayout.Label("선택된 키에 오프셋 적용", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.Label("Rotation", EditorStyles.boldLabel);
            rotationOffset = EditorGUILayout.Vector3Field("", rotationOffset);

            GUILayout.Label("Position", EditorStyles.boldLabel);
            positionOffset = EditorGUILayout.Vector3Field("", positionOffset);

            GUILayout.Label("Scale", EditorStyles.boldLabel);
            scaleOffset = EditorGUILayout.Vector3Field("", scaleOffset);

            EditorGUILayout.Space();
            
            Color defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("오프셋 적용"))
            {
                ApplyOffsetToSelectedKeys();
            }
            GUI.backgroundColor = defaultColor;

            if (GUILayout.Button("값 초기화"))
            {
                ResetOffsets();
            }
        }

        private void ResetOffsets()
        {
            rotationOffset = Vector3.zero;
            positionOffset = Vector3.zero;
            scaleOffset = Vector3.zero;
            Debug.Log("오프셋 값이 초기화되었습니다.");
        }

        private void ApplyOffsetToSelectedKeys()
        {
            Debug.Log("오프셋 적용 시작...");

            var state = GetAnimationWindowState();
            if (state == null)
            {
                Debug.LogError("Animation Window가 열려있지 않거나 상태를 가져올 수 없습니다.");
                return;
            }

            var stateType = state.GetType();
            AnimationClip clip = GetActiveAnimationClip(state, stateType);
            if (clip == null)
            {
                Debug.LogError("Animation Window에서 유효한 클립을 찾을 수 없습니다. 클립이 편집 모드인지 확인하세요.");
                return;
            }

            IList selectedCurves = GetSelectedCurves(state, stateType);
            if (selectedCurves == null || selectedCurves.Count == 0)
            {
                Debug.LogError("선택된 커브가 없습니다. Animation Window에서 커브를 선택해 주세요.");
                return;
            }

            Debug.Log($"클립 '{clip.name}'의 선택된 {selectedCurves.Count}개 커브에 오프셋을 적용합니다.");
            Undo.RecordObject(clip, "Offset Selected Keys");

            foreach (var curveWrapper in selectedCurves)
            {
                if (curveWrapper == null) continue;

                var wrapperType = curveWrapper.GetType();
                var bindingField = wrapperType.GetField("binding", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (bindingField == null) continue;

                EditorCurveBinding binding = (EditorCurveBinding)bindingField.GetValue(curveWrapper);

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;

                Keyframe[] keys = curve.keys;
                bool curveModified = false;
                
                var selectedKeyIndices = GetSelectedKeyIndicesInCurveWrapper(curveWrapper);
                if (selectedKeyIndices == null) continue;

                foreach (int keyframeIndex in selectedKeyIndices)
                {
                    if (keyframeIndex < 0 || keyframeIndex >= keys.Length) continue;
                    
                    Keyframe key = keys[keyframeIndex];
                    float originalValue = key.value;

                    float offsetValue = 0f;
                    if (binding.propertyName.Contains("localEulerAnglesRaw"))
                    {
                        if (binding.propertyName.EndsWith(".x")) offsetValue = rotationOffset.x;
                        else if (binding.propertyName.EndsWith(".y")) offsetValue = rotationOffset.y;
                        else if (binding.propertyName.EndsWith(".z")) offsetValue = rotationOffset.z;
                    }
                    else if (binding.propertyName.Contains("m_LocalPosition") || binding.propertyName.Contains("m_AnchoredPosition"))
                    {
                        if (binding.propertyName.EndsWith(".x")) offsetValue = positionOffset.x;
                        else if (binding.propertyName.EndsWith(".y")) offsetValue = positionOffset.y;
                        else if (binding.propertyName.EndsWith(".z")) offsetValue = positionOffset.z;
                    }
                    else if (binding.propertyName.Contains("m_LocalScale"))
                    {
                        if (binding.propertyName.EndsWith(".x")) offsetValue = scaleOffset.x;
                        else if (binding.propertyName.EndsWith(".y")) offsetValue = scaleOffset.y;
                        else if (binding.propertyName.EndsWith(".z")) offsetValue = scaleOffset.z;
                    }

                    if (offsetValue != 0f)
                    {
                        key.value += offsetValue;
                        keys[keyframeIndex] = key;
                        curveModified = true;
                        
                        Debug.Log($"  > 키 변경 - 바인딩: {binding.propertyName}, 인덱스: {keyframeIndex}, 시간: {key.time}s");
                        Debug.Log($"    - 이전 값: {originalValue}, 변경 후 값: {key.value}");
                    }
                }

                if (curveModified)
                {
                    curve.keys = keys;
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
            }

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            Debug.Log("오프셋 적용 완료.");
        }

        // 헬퍼 메서드: AnimationWindow의 state 객체 가져오기
        private object GetAnimationWindowState()
        {
            var animationWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
            if (animationWindowType == null) return null;
            var animationWindows = Resources.FindObjectsOfTypeAll(animationWindowType);
            if (animationWindows == null || animationWindows.Length == 0) return null;
            var window = animationWindows[0];
            var stateProperty = animationWindowType.GetProperty("state", BindingFlags.NonPublic | BindingFlags.Instance);
            return stateProperty?.GetValue(window);
        }

        // 헬퍼 메서드: 활성 클립 가져오기
        private AnimationClip GetActiveAnimationClip(object state, System.Type stateType)
        {
            var allMembers = stateType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var member in allMembers)
            {
                if ((member.MemberType == MemberTypes.Property || member.MemberType == MemberTypes.Field) && member.Name.Contains("activeAnimationClip"))
                {
                    object value = null;
                    if (member.MemberType == MemberTypes.Property)
                        value = ((PropertyInfo)member).GetValue(state);
                    else
                        value = ((FieldInfo)member).GetValue(state);
                    
                    if (value is AnimationClip)
                    {
                         Debug.Log($"활성 클립을 '{member.Name}'에서 찾았습니다.");
                        return value as AnimationClip;
                    }
                }
            }
            return null;
        }

        // 헬퍼 메서드: 선택된 커브 목록 가져오기 (가장 유연한 리플렉션)
        private IList GetSelectedCurves(object state, System.Type stateType)
        {
            var allMembers = stateType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var member in allMembers)
            {
                if (member.MemberType == MemberTypes.Field)
                {
                    var field = (FieldInfo)member;
                    if (typeof(IList).IsAssignableFrom(field.FieldType))
                    {
                        var list = field.GetValue(state) as IList;
                        if (list != null && list.Count > 0)
                        {
                            var firstItem = list[0];
                            if (firstItem != null && firstItem.GetType().GetField("binding") != null)
                            {
                                Debug.Log($"선택된 커브 목록을 필드 '{field.Name}'에서 찾았습니다.");
                                return list;
                            }
                        }
                    }
                }
                else if (member.MemberType == MemberTypes.Property)
                {
                    var property = (PropertyInfo)member;
                    if (typeof(IList).IsAssignableFrom(property.PropertyType))
                    {
                        var list = property.GetValue(state) as IList;
                        if (list != null && list.Count > 0)
                        {
                            var firstItem = list[0];
                            if (firstItem != null && firstItem.GetType().GetField("binding") != null)
                            {
                                Debug.Log($"선택된 커브 목록을 프로퍼티 '{property.Name}'에서 찾았습니다.");
                                return list;
                            }
                        }
                    }
                }
            }
            return null;
        }

        // 헬퍼 메서드: 커브 래퍼 객체에서 선택된 키 인덱스 목록 가져오기
        private IList GetSelectedKeyIndicesInCurveWrapper(object curveWrapper)
        {
            if (curveWrapper == null) return null;
            var wrapperType = curveWrapper.GetType();
            
            var allMembers = wrapperType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var member in allMembers)
            {
                if (member.Name.Contains("selectedKeys") && (member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property))
                {
                    object value = null;
                    if (member.MemberType == MemberTypes.Field)
                        value = ((FieldInfo)member).GetValue(curveWrapper);
                    else
                        value = ((PropertyInfo)member).GetValue(curveWrapper);
                    
                    if (value is IList list && list.Count > 0)
                    {
                        Debug.Log($"  선택된 키 인덱스 목록을 '{member.Name}'에서 찾았습니다.");
                        return list;
                    }
                }
            }
            return null;
        }
    }
}