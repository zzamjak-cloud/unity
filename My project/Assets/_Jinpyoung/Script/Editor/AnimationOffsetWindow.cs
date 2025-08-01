// C#
// 이 스크립트는 반드시 Assets 폴더 하위의 "Editor"라는 이름의 폴더 안에 위치해야 합니다.
// 만약 Editor 폴더가 없다면 새로 생성해주세요. (예: Assets/Editor/AnimationOffset.cs)

using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace CAT.Utility
{
    public class AnimationOffsetWindow : EditorWindow
    {
        private int startFrame;
        private int endFrame;
        private float startTime;
        private float endTime;
        private bool isTimeBased = false;

        private Vector3 rotationOffset = Vector3.zero;
        private Vector3 positionOffset = Vector3.zero;
        private Vector3 scaleOffset = Vector3.zero;

        [MenuItem("Tools/Animation/Keys Offset")]
        private static void ShowWindow()
        {
            GetWindow<AnimationOffsetWindow>("Keys Offset").Show();
        }

        private void OnEnable()
        {
            SetInitialTimeValues();
        }

        private void OnGUI()
        {
            GameObject selectedObject = Selection.activeGameObject;
            string selectedObjectName = (selectedObject != null) ? selectedObject.name : "없음";
            
            EditorGUILayout.LabelField("Selected Object", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(selectedObjectName);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Offset 적용 범위", EditorStyles.boldLabel);
            
            Color defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = isTimeBased ? Color.green : Color.yellow;
            string toggleText = isTimeBased ? "Time 활성중" : "Frame 활성중";
            isTimeBased = GUILayout.Toggle(isTimeBased, toggleText, "Button");
            GUI.backgroundColor = defaultColor;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Start", GUILayout.Width(40));
            if (isTimeBased)
            {
                startTime = EditorGUILayout.FloatField(startTime, GUILayout.MinWidth(80));
                EditorGUILayout.LabelField("End", GUILayout.Width(30));
                endTime = EditorGUILayout.FloatField(endTime, GUILayout.MinWidth(80));
            }
            else
            {
                startFrame = EditorGUILayout.IntField(startFrame, GUILayout.MinWidth(80));
                EditorGUILayout.LabelField("End", GUILayout.Width(30));
                endFrame = EditorGUILayout.IntField(endFrame, GUILayout.MinWidth(80));
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();

            positionOffset = EditorGUILayout.Vector3Field("Position", positionOffset);
            rotationOffset = EditorGUILayout.Vector3Field("Rotation", rotationOffset);
            scaleOffset = EditorGUILayout.Vector3Field("Scale", scaleOffset);

            EditorGUILayout.Space(20);

            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
            if (GUILayout.Button("Offset 적용", GUILayout.Height(30)))
            {
                ApplyOffsets();
            }
            GUI.backgroundColor = defaultColor;

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Offset 초기화"))
            {
                positionOffset = Vector3.zero;
                rotationOffset = Vector3.zero;
                scaleOffset = Vector3.zero;
                Debug.Log("오프셋 값이 초기화되었습니다.");
            }
        }

        private void SetInitialTimeValues()
        {
            AnimationClip activeClip = GetActiveAnimationClipFromWindow();
            if (activeClip != null)
            {
                endFrame = Mathf.RoundToInt(activeClip.length * activeClip.frameRate);
                endTime = activeClip.length;
            }
        }

        private void ApplyOffsets()
        {
            Debug.Log("--- Offset 적용 시작 ---");

            AnimationClip activeClip = GetActiveAnimationClipFromWindow();
            if (activeClip == null)
            {
                Debug.LogError("활성화된 Animation Clip이 없습니다. Animation 창을 열고 클립을 선택해주세요.");
                return;
            }

            float frameRate = activeClip.frameRate;
            float startTimeInSeconds = isTimeBased ? startTime : startFrame / frameRate;
            float endTimeInSeconds = isTimeBased ? endTime : endFrame / frameRate;

            if (startTimeInSeconds > endTimeInSeconds)
            {
                Debug.LogWarning("시작 시간이 종료 시간보다 클 수 없습니다.");
                return;
            }

            Undo.RecordObject(activeClip, "Apply Animation Key Offsets");

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(activeClip);

            Debug.Log($"--- '{activeClip.name}'의 키에 Offset 적용 시작 (Time: {startTimeInSeconds:F3}s ~ {endTimeInSeconds:F3}s) ---");
            bool anyKeyModified = false;

            foreach (var binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(activeClip, binding);
                if (curve == null) continue;

                Keyframe[] keys = curve.keys;
                bool curveModified = false;

                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i].time >= startTimeInSeconds && keys[i].time <= endTimeInSeconds)
                    {
                        float originalValue = keys[i].value;
                        float offsetToAdd = GetValueForProperty(binding.propertyName, positionOffset, rotationOffset, scaleOffset);
                        
                        if (!Mathf.Approximately(offsetToAdd, 0))
                        {
                            keys[i].value += offsetToAdd;
                            curveModified = true;
                            anyKeyModified = true;
                            Debug.Log($"  [Property: {binding.propertyName}] Time: {keys[i].time:F3} | Original: {originalValue:F3} -> New: {keys[i].value:F3} (Offset: {offsetToAdd:F3})");
                        }
                    }
                }

                if (curveModified)
                {
                    curve.keys = keys;
                    AnimationUtility.SetEditorCurve(activeClip, binding, curve);
                }
            }
            
            if (!anyKeyModified)
            {
                Debug.Log("적용할 키를 찾지 못했습니다. 오브젝트, 속성, 프레임 범위를 확인해주세요.");
            }

            EditorUtility.SetDirty(activeClip);
            Debug.Log("--- Offset 적용 완료 ---");
        }

        private float GetValueForProperty(string propertyName, Vector3 positionOffset, Vector3 rotationOffset, Vector3 scaleOffset)
        {
            char lastChar = propertyName.Length > 0 ? propertyName[propertyName.Length - 1] : ' ';

            if (propertyName.Contains("m_LocalPosition") || propertyName.Contains("m_AnchoredPosition"))
            {
                if (lastChar == 'x') return positionOffset.x;
                if (lastChar == 'y') return positionOffset.y;
                if (lastChar == 'z') return positionOffset.z;
            }
            else if (propertyName.Contains("localEulerAnglesRaw"))
            {
                if (lastChar == 'x') return rotationOffset.x;
                if (lastChar == 'y') return rotationOffset.y;
                if (lastChar == 'z') return rotationOffset.z;
            }
            else if (propertyName.Contains("m_LocalScale"))
            {
                if (lastChar == 'x') return scaleOffset.x;
                if (lastChar == 'y') return scaleOffset.y;
                if (lastChar == 'z') return scaleOffset.z;
            }
            return 0f;
        }

        #region Helper Methods (Reflection)
        
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

        private AnimationClip GetActiveAnimationClipFromWindow()
        {
            var state = GetAnimationWindowState();
            if (state == null) return null;
            var stateType = state.GetType();

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

        #endregion
    }
}