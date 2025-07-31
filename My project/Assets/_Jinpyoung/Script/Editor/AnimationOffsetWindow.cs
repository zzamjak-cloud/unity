// C#
// 이 스크립트는 반드시 Assets 폴더 하위의 "Editor"라는 이름의 폴더 안에 위치해야 합니다.
// 만약 Editor 폴더가 없다면 새로 생성해주세요. (예: Assets/Editor/AnimationOffset.cs)

using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace CAT.Utitlty
{
    public class AnimationOffsetWindow : EditorWindow
    {
        private int startFrame;
        private int endFrame;
        private Vector3 positionOffset;
        private Vector3 rotationOffset;
        private Vector3 scaleOffset;

        // 유니티 에디터 상단 메뉴에 "Tools/Animation/Keys Offset" 항목을 추가하고, 클릭 시 윈도우를 엽니다.
        [MenuItem("Tools/Animation/Keys Offset")]
        private static void ShowWindow()
        {
            // 기존에 열려있는 윈도우를 가져오거나 없으면 새로 생성합니다.
            GetWindow<AnimationOffsetWindow>("Keys Offset");
        }

        // Scene이나 Hierarchy에서 선택이 변경될 때마다 호출됩니다.
        private void OnSelectionChange()
        {
            Repaint(); // 창을 다시 그려서 선택된 오브젝트 이름을 업데이트합니다.
        }

        // 에디터 윈도우의 GUI를 그리는 함수입니다.
        private void OnGUI()
        {
            // 현재 선택된 오브젝트 이름 표시
            GameObject selectedObject = Selection.activeGameObject;
            string selectedObjectName = (selectedObject != null) ? selectedObject.name : "없음";
            
            EditorGUILayout.LabelField("Selected Object", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(selectedObjectName);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Offset 적용 범위", EditorStyles.boldLabel);
            
            // 프레임 입력 필드 UI 개선
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Start", GUILayout.Width(40));
            startFrame = EditorGUILayout.IntField(startFrame);
            EditorGUILayout.LabelField("End", GUILayout.Width(30));
            endFrame = EditorGUILayout.IntField(endFrame);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();

            positionOffset = EditorGUILayout.Vector3Field("Position", positionOffset);
            rotationOffset = EditorGUILayout.Vector3Field("Rotation", rotationOffset);
            scaleOffset = EditorGUILayout.Vector3Field("Scale", scaleOffset);

            EditorGUILayout.Space(20);

            // "Offset 적용" 버튼 스타일링
            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f); // 밝은 파란색
            if (GUILayout.Button("Offset 적용", GUILayout.Height(30)))
            {
                ApplyOffsets();
            }
            GUI.backgroundColor = Color.white; // 기본 색상으로 복원

            EditorGUILayout.Space(5);

            // "Offset 초기화" 버튼
            if (GUILayout.Button("Offset 초기화"))
            {
                // [수정] Offset 값만 초기화합니다.
                positionOffset = Vector3.zero;
                rotationOffset = Vector3.zero;
                scaleOffset = Vector3.zero;
            }
        }

        /// <summary>
        /// 입력된 Offset 값을 지정된 프레임 범위 내의 모든 키프레임에 적용합니다.
        /// </summary>
        private void ApplyOffsets()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                Debug.LogWarning("오브젝트가 선택되지 않았습니다. Hierarchy 뷰에서 오브젝트를 선택해주세요.");
                return;
            }

            Animator animator = selectedObject.GetComponentInParent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"'{selectedObject.name}' 오브젝트 또는 그 부모 계층에 Animator 컴포넌트가 없습니다.");
                return;
            }

            AnimationClip activeClip = GetActiveAnimationClip();
            if (activeClip == null)
            {
                Debug.LogWarning("활성화된 Animation Clip이 없습니다. Animation 창을 열고 클립을 선택해주세요.");
                return;
            }

            // 입력된 프레임을 시간으로 변환합니다.
            float frameRate = activeClip.frameRate;
            float startTime = startFrame / frameRate;
            float endTime = endFrame / frameRate;

            if (startTime > endTime)
            {
                Debug.LogWarning("Start Frame은 End Frame보다 클 수 없습니다.");
                return;
            }

            // Undo 기능을 위해 클립의 현재 상태를 기록합니다.
            Undo.RecordObject(activeClip, "Apply Animation Key Offsets");

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(activeClip);
            string relativePath = AnimationUtility.CalculateTransformPath(selectedObject.transform, animator.transform);

            Debug.Log($"--- '{selectedObject.name}'의 프레임({startFrame}f ~ {endFrame}f) 키에 Offset 적용 시작 ---");
            bool anyKeyModified = false;

            foreach (var binding in bindings)
            {
                // 선택된 오브젝트에 대한 바인딩만 필터링합니다.
                if (binding.path == relativePath)
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(activeClip, binding);
                    if (curve == null) continue;

                    Keyframe[] keys = curve.keys;
                    bool curveModified = false;

                    for (int i = 0; i < keys.Length; i++)
                    {
                        // 지정된 시간 범위 내의 키만 대상으로 합니다.
                        if (keys[i].time >= startTime && keys[i].time <= endTime)
                        {
                            float originalValue = keys[i].value;
                            float offsetToAdd = 0f;
                            bool wasModifiedInLoop = false;

                            string propName = binding.propertyName;

                            // Position 속성 확인 (Transform & RectTransform)
                            if (propName.Contains("Position") || propName.Contains("m_AnchoredPosition"))
                            {
                                offsetToAdd = GetValueForProperty(propName, positionOffset);
                                keys[i].value += offsetToAdd;
                                wasModifiedInLoop = true;
                            }
                            // Rotation 속성 확인 (Quaternion & Euler Angles)
                            else if (propName.Contains("Rotation") || propName.Contains("Euler"))
                            {
                                offsetToAdd = GetValueForProperty(propName, rotationOffset);
                                keys[i].value += offsetToAdd;
                                wasModifiedInLoop = true;
                            }
                            // Scale/Size 속성 확인 (Transform & RectTransform)
                            else if (propName.Contains("Scale") || propName.Contains("SizeDelta"))
                            {
                                offsetToAdd = GetValueForProperty(propName, scaleOffset);
                                keys[i].value += offsetToAdd;
                                wasModifiedInLoop = true;
                            }
                            
                            // 값이 변경된 경우에만 로그를 출력합니다.
                            if (wasModifiedInLoop && !Mathf.Approximately(offsetToAdd, 0))
                            {
                                Debug.Log($"  [Property: {propName}] Time: {keys[i].time:F3} | Original: {originalValue:F3} -> New: {keys[i].value:F3} (Offset: {offsetToAdd:F3})");
                                curveModified = true;
                            }
                        }
                    }

                    // 커브에 변경이 있었으면, 키를 업데이트하고 클립에 다시 적용합니다.
                    if (curveModified)
                    {
                        anyKeyModified = true;
                        curve.keys = keys;
                        AnimationUtility.SetEditorCurve(activeClip, binding, curve);
                    }
                }
            }
            
            if (!anyKeyModified)
            {
                Debug.Log("적용할 키를 찾지 못했습니다. 오브젝트, 속성, 프레임 범위를 확인해주세요.");
            }

            // 변경사항을 에디터에 알립니다.
            EditorUtility.SetDirty(activeClip);
            Debug.Log("--- Offset 적용 완료 ---");
        }

        /// <summary>
        /// "m_LocalPosition.x"와 같은 프로퍼티 이름에서 해당하는 Vector3의 컴포넌트 값을 반환합니다.
        /// Quaternion의 'w' 컴포넌트는 처리하지 않습니다.
        /// </summary>
        private float GetValueForProperty(string propertyName, Vector3 offset)
        {
            char lastChar = propertyName[propertyName.Length - 1];
            switch (lastChar)
            {
                case 'x': return offset.x;
                case 'y': return offset.y;
                case 'z': return offset.z;
                // 'w'는 Vector3 offset에 해당 값이 없으므로 0을 반환합니다.
                case 'w': return 0f; 
                default: return 0f;
            }
        }

        #region Helper Methods
        
        /// <summary>
        /// 현재 열려있는 Animation 창의 인스턴스를 가져옵니다.
        /// </summary>
        private static EditorWindow GetAnimationWindow()
        {
            var animationWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
            if (animationWindowType == null) return null;
            var allAnimationWindows = Resources.FindObjectsOfTypeAll(animationWindowType);
            return (allAnimationWindows.Length > 0) ? (allAnimationWindows[0] as EditorWindow) : null;
        }

        /// <summary>
        /// Reflection을 사용하여 현재 Animation 창에서 활성화된(선택된) AnimationClip을 가져옵니다.
        /// </summary>
        private static AnimationClip GetActiveAnimationClip()
        {
            try
            {
                var animationWindow = GetAnimationWindow();
                if (animationWindow == null) return null;
                var clipProperty = animationWindow.GetType().GetProperty("animationClip", BindingFlags.Public | BindingFlags.Instance);
                return (clipProperty != null) ? clipProperty.GetValue(animationWindow) as AnimationClip : null;
            }
            catch { return null; }
        }

        #endregion
    }
}
