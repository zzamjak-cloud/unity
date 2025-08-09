using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

        private void UpdateRangeFromSelection()
        {
            List<float> selectedKeyTimes = GetSelectedKeyTimesFromDopesheet();
            if (selectedKeyTimes == null || selectedKeyTimes.Count == 0) return;

            float minTime = selectedKeyTimes.Min();
            float maxTime = selectedKeyTimes.Max();

            AnimationClip activeClip = GetActiveAnimationClipFromState(GetAnimationWindowState());
            if (activeClip == null) return;
            
            startTime = minTime;
            endTime = maxTime;
            startFrame = Mathf.RoundToInt(minTime * activeClip.frameRate);
            endFrame = Mathf.RoundToInt(maxTime * activeClip.frameRate);
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                e.Use(); 
                UpdateRangeFromSelection();
            }
            
            EditorGUILayout.HelpBox("Dopesheet에서 키 선택 후, 이 창을 클릭하고 Enter 키를 누르면 범위가 갱신됩니다.", MessageType.Info);
            
            GameObject selectedObject = Selection.activeGameObject;
            string selectedObjectName = (selectedObject != null) ? selectedObject.name : "없음";
            
            EditorGUILayout.LabelField("Selected Object", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(selectedObjectName);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Offset 적용 범위", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(0.9f, 0.9f, 0.5f);
            if (GUILayout.Button("선택된 키 범위 가져오기"))
            {
                UpdateRangeFromSelection();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space(2);

            Color defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = isTimeBased ? Color.green : Color.yellow;
            string toggleText = isTimeBased ? "Time 활성중" : "Frame 활성중";
            isTimeBased = GUILayout.Toggle(isTimeBased, toggleText, "Button");
            GUI.backgroundColor = defaultColor;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Start", GUILayout.Width(40));
            if (isTimeBased)
            {
                startTime = EditorGUILayout.FloatField(startTime);
                EditorGUILayout.LabelField("End", GUILayout.Width(30));
                endTime = EditorGUILayout.FloatField(endTime);
            }
            else
            {
                startFrame = EditorGUILayout.IntField(startFrame);
                EditorGUILayout.LabelField("End", GUILayout.Width(30));
                endFrame = EditorGUILayout.IntField(endFrame);
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

        private void ApplyOffsets()
        {
            int originalFrame = GetCurrentFrame();

            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                Debug.LogError("오프셋을 적용할 오브젝트를 Hierarchy에서 선택해주세요.");
                return;
            }

            object state = GetAnimationWindowState();
            AnimationClip activeClip = GetActiveAnimationClipFromState(state);
            if (activeClip == null)
            {
                Debug.LogError("활성화된 Animation Clip이 없습니다. Animation 창을 열고 클립을 선택해주세요.");
                return;
            }
            
            GameObject rootObject = GetActiveRootGameObjectFromState(state);
            if (rootObject == null)
            {
                Debug.LogError("애니메이션의 루트 오브젝트를 찾을 수 없습니다.");
                return;
            }

            string selectedObjectPath = AnimationUtility.CalculateTransformPath(selectedObject.transform, rootObject.transform);

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
            bool anyKeyModified = false;

            foreach (var binding in bindings)
            {
                if (binding.path == selectedObjectPath)
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(activeClip, binding);
                    if (curve == null) continue;
                    Keyframe[] keys = curve.keys;
                    bool curveModified = false;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (keys[i].time >= startTimeInSeconds && keys[i].time <= endTimeInSeconds)
                        {
                            float offsetToAdd = GetValueForProperty(binding.propertyName, positionOffset, rotationOffset, scaleOffset);
                            if (!Mathf.Approximately(offsetToAdd, 0))
                            {
                                keys[i].value += offsetToAdd;
                                curveModified = true;
                                anyKeyModified = true;
                            }
                        }
                    }
                    if (curveModified)
                    {
                        curve.keys = keys;
                        AnimationUtility.SetEditorCurve(activeClip, binding, curve);
                    }
                }
            }
            
            if (!anyKeyModified)
            {
                Debug.Log($"'{selectedObject.name}' 오브젝트에서 적용할 키를 찾지 못했습니다. 오브젝트, 속성, 시간 범위를 확인해주세요.");
            }

            EditorUtility.SetDirty(activeClip);
            ForceRefreshAnimationWindow(originalFrame);
        }

        private float GetValueForProperty(string propertyName, Vector3 positionOffset, Vector3 rotationOffset, Vector3 scaleOffset)
        {
            char lastChar = propertyName.Length > 0 ? propertyName[propertyName.Length - 1] : ' ';
            if (propertyName.Contains("m_LocalPosition") || propertyName.Contains("m_AnchoredPosition"))
            {
                if (lastChar == 'x') return positionOffset.x; if (lastChar == 'y') return positionOffset.y; if (lastChar == 'z') return positionOffset.z;
            }
            else if (propertyName.Contains("localEulerAnglesRaw"))
            {
                if (lastChar == 'x') return rotationOffset.x; if (lastChar == 'y') return rotationOffset.y; if (lastChar == 'z') return rotationOffset.z;
            }
            else if (propertyName.Contains("m_LocalScale"))
            {
                if (lastChar == 'x') return scaleOffset.x; if (lastChar == 'y') return scaleOffset.y; if (lastChar == 'z') return scaleOffset.z;
            }
            return 0f;
        }

        private object GetAnimationWindowState()
        {
            var animationWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
            if (animationWindowType == null) return null;
            var window = GetWindow(animationWindowType, false, null, false);
            if (window == null) return null;
            var stateProperty = animationWindowType.GetProperty("state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return stateProperty?.GetValue(window);
        }

        private AnimationClip GetActiveAnimationClipFromState(object state)
        {
            if (state == null) return null;
            var activeClipProperty = state.GetType().GetProperty("activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
            return activeClipProperty?.GetValue(state) as AnimationClip;
        }
        
        private GameObject GetActiveRootGameObjectFromState(object state)
        {
            if (state == null) return null;
            var rootGoProperty = state.GetType().GetProperty("activeRootGameObject", BindingFlags.Public | BindingFlags.Instance);
            return rootGoProperty?.GetValue(state) as GameObject;
        }

        private List<float> GetSelectedKeyTimesFromDopesheet()
        {
            var selectedKeyTimes = new List<float>();
            object state = GetAnimationWindowState();
            if (state == null) return null;
            var dopesheetKeysProperty = state.GetType().GetProperty("selectedKeys", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var dopesheetKeyObjectList = dopesheetKeysProperty?.GetValue(state) as IEnumerable;
            if (dopesheetKeyObjectList != null)
            {
                foreach (object keyObject in dopesheetKeyObjectList)
                {
                    var timeProperty = keyObject.GetType().GetProperty("time", BindingFlags.Public | BindingFlags.Instance);
                    if (timeProperty != null) selectedKeyTimes.Add((float)timeProperty.GetValue(keyObject));
                }
            }
            return selectedKeyTimes;
        }
        
        private int GetCurrentFrame()
        {
            object state = GetAnimationWindowState();
            if (state == null) return -1;
            // [수정된 부분] Binding.Instance -> BindingFlags.Instance
            var frameProperty = state.GetType().GetProperty("currentFrame", BindingFlags.Public | BindingFlags.Instance);
            if (frameProperty == null) return -1;
            return (int)frameProperty.GetValue(state, null);
        }

        private void ForceRefreshAnimationWindow(int originalFrame)
        {
            if (originalFrame < 0) return;
            object state = GetAnimationWindowState();
            if (state == null) return;
            var frameProperty = state.GetType().GetProperty("currentFrame", BindingFlags.Public | BindingFlags.Instance);
            if (frameProperty == null) return;
            
            frameProperty.SetValue(state, originalFrame + 1, null);
            EditorApplication.delayCall += () =>
            {
                if (frameProperty != null && state != null)
                {
                    frameProperty.SetValue(state, originalFrame, null);
                }
            };
        }
    }
}