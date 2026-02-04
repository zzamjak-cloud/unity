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
        private static float offsetValue = 0f;               // Offset 값
        private static bool isTimeInputMode = false;         // Offset 단위 모드 (Time, Frame)
        [System.Flags]
        private enum PropertyType { Position = 1, Rotation = 2, Scale = 4 } // Position, Rotation, Scale 속성 타입

        private static float objectNameWidth = 120f;         // 선택된 GameObject 이름 너비
        private static float inputFieldWidth = 100f;         // Offset 값 입력 필드
        private static float modeButtonWidth = 50f;          // [Time], [Frame]
        private static float resetButtonWidth = 40f;         // [Reset]
        private static float keyGenButtonWidth = 45f;        // [+All], [+Pos], [+Rot], [+Sca]
        private static float offsetButtonWidth = 68f;        // [Position], [Rotation], [Scale]
        private static float cleanButtonWidth = 60f;         // [Clean]
        private static float sectionSpacing = 10f;           // 버튼 사이 간격
        
        private static bool _isRefreshPending = false;       // 애니메이션 창 새로고침 상태 관리

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
            // Editor 어셈블리에서 AnimationWindow 타입을 찾습니다.
            var editorAssembly = typeof(Editor).Assembly;
            var animationWindows = Resources.FindObjectsOfTypeAll(editorAssembly.GetType("UnityEditor.AnimationWindow"));
            if (animationWindows.Length == 0) return;

            // AnimationWindow 윈도우의 rootVisualElement를 가져옵니다.
            // 스크립트 리컴파일시 UI가 중복으로 추가되는 것을 방지합니다.
            // 이미 주입된 UI가 있는지 확인합니다.
            var animationWindow = (EditorWindow)animationWindows[0];
            var rawRoot = animationWindow.rootVisualElement;
            if (rawRoot == null) return;
            if (rawRoot.Q<VisualElement>("AnimationOffsetContainer") != null) return;

            // 부모 컨테이너를 생성합니다. 중복 주입 방지를 위한 이름 지정
            var parentContainer = new VisualElement
            {
                name = "AnimationOffsetContainer",
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

            // IMGUIContainer를 생성하고 UI를 그리는 함수를 설정합니다.
            // Animation Window의 모든 UI 그리기는 여기서 처리됩니다.
            var imguiContainer = new IMGUIContainer(OnInjectedGUI);
            imguiContainer.style.flexGrow = 1;
            parentContainer.Add(imguiContainer);
            rawRoot.Add(parentContainer);
        }

        // Animation Window 위에 삽입될 UI를 그립니다.
        private static void OnInjectedGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 선택된 GameObject 이름을 표시합니다.
            GameObject selectedObject = Selection.activeGameObject;
            string selectedObjectName = (selectedObject != null) ? selectedObject.name : "None";
            EditorGUILayout.LabelField(new GUIContent(selectedObjectName, "Selected GameObject"), GUILayout.Width(objectNameWidth));

            // Offset 모드(Time, Frame) 버튼을 그립니다.
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

            // Offset 모드(Time, Frame) 버튼을 클릭하면 모드를 전환합니다.
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

            string inputTooltip = isTimeInputMode ? "Time offset" : "Frame offset";
            offsetValue = EditorGUILayout.FloatField(new GUIContent("", inputTooltip), offsetValue, EditorStyles.toolbarTextField, GUILayout.Width(inputFieldWidth));

            // Offset 값을 0으로 초기화하는 "[Reset]" 버튼을 그립니다.
            if (GUILayout.Button(new GUIContent("R", "Reset to 0"), EditorStyles.toolbarButton, GUILayout.Width(resetButtonWidth)))
            {
                offsetValue = 0f;
                GUI.FocusControl(null);
            }

            GUILayout.Space(sectionSpacing);

            // 모든 객체의 불필요한 키를 제거하는 "[Clean]" 버튼을 그립니다.
            Color cleanButtonColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
            if (GUILayout.Button(new GUIContent("Clean", "Remove unnecessary keys for all objects"), EditorStyles.toolbarButton, GUILayout.Width(cleanButtonWidth)))
            {
                CleanAllUnnecessaryKeys();
            }
            GUI.backgroundColor = cleanButtonColor;

            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // 키 생성 버튼 "[+All], [+Pos], [+Rot], [+Sca]" 을 그립니다.
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

        /// <summary>
        /// 선택된 GameObject의 Transform 속성에 대한 키를 추가합니다.
        /// </summary>
        private static void AddTransformKeys(PropertyType propertyTypes)
        {
            // Property 생성 과정
            // 1. 선택된 GameObject를 가져옵니다.
            // 2. Animation Window State를 가져옵니다.
            // 3. 현재 선택된 Animation Clip을 가져옵니다.
            // 4. Animation Clip의 경로를 가져옵니다.
            // 5. Animation Clip을 로드합니다.
            // 6. 애니메이션 루트 GameObject를 가져옵니다.
            // 7. 선택된 GameObject의 경로를 가져옵니다.
            // 8. 클립 길이를 가져옵니다.
            // 9. 키를 추가합니다.
            // 10. 애니메이션 창을 새로고침합니다.
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

            // RectTransform인지 확인
            RectTransform rectTransform = selectedObject.GetComponent<RectTransform>();
            bool isRectTransform = rectTransform != null;

            Undo.RecordObject(sourceClip, "Add Transform Keys");

            bool anyKeyAdded = false;
            string addedProperties = "";

            // Position 키 추가
            if ((propertyTypes & PropertyType.Position) != 0)
            {
                if (isRectTransform)
                {
                    // RectTransform: m_AnchoredPosition.x/y 사용
                    Vector2 anchoredPosition = rectTransform.anchoredPosition;
                    if (AddRectTransformPositionKeys(sourceClip, selectedObjectPath, anchoredPosition, clipDuration))
                    {
                        anyKeyAdded = true;
                        addedProperties += "Position ";
                    }
                }
                else
                {
                    // Transform: m_LocalPosition.x/y/z 사용
                    if (AddPropertyKeys(sourceClip, selectedObjectPath, "m_LocalPosition", selectedObject.transform.localPosition, clipDuration, typeof(Transform)))
                    {
                        anyKeyAdded = true;
                        addedProperties += "Position ";
                    }
                }
            }

            // Rotation 키 추가
            if ((propertyTypes & PropertyType.Rotation) != 0)
            {
                // 1. 먼저 기존 애니메이션 클립에서 사용 중인 rotation 속성을 확인
                string rotationPropertyType = DetectRotationPropertyType(sourceClip, selectedObjectPath);

                // 2. 감지된 속성 타입에 맞춰 키 추가
                if (rotationPropertyType == "quaternion")
                {
                    // Quaternion 형식으로 키 추가
                    Quaternion rotation = selectedObject.transform.localRotation;
                    if (AddQuaternionKeys(sourceClip, selectedObjectPath, rotation, clipDuration, isRectTransform))
                    {
                        anyKeyAdded = true;
                        addedProperties += "Rotation ";
                        // Rotation 프로퍼티 초기화 (Z 값 변경 후 복원)
                        InitializeRotationProperty(sourceClip, selectedObjectPath, selectedObject.transform.localEulerAngles.z, isRectTransform);
                    }
                    // Euler 형식 제거
                    RemoveEulerRotationCurves(sourceClip, selectedObjectPath, isRectTransform);
                }
                else
                {
                    // Euler 방식 사용
                    RemoveQuaternionRotationCurves(sourceClip, selectedObjectPath, isRectTransform);
                    Vector3 eulerAngles = selectedObject.transform.localEulerAngles;

                    if (isRectTransform)
                    {
                        // RectTransform: localEulerAnglesRaw.z 사용
                        if (AddRectTransformRotationKeys(sourceClip, selectedObjectPath, eulerAngles.z, clipDuration))
                        {
                            anyKeyAdded = true;
                            addedProperties += "Rotation ";
                            // Rotation 프로퍼티 초기화 (Z 값 변경 후 복원)
                            InitializeRotationProperty(sourceClip, selectedObjectPath, eulerAngles.z, isRectTransform);
                        }
                    }
                    else
                    {
                        // Transform: m_LocalEulerAngles.x/y/z 사용
                        if (AddPropertyKeys(sourceClip, selectedObjectPath, "m_LocalEulerAngles", eulerAngles, clipDuration, typeof(Transform)))
                        {
                            anyKeyAdded = true;
                            addedProperties += "Rotation ";
                            // Rotation 프로퍼티 초기화 (Z 값 변경 후 복원)
                            InitializeRotationProperty(sourceClip, selectedObjectPath, eulerAngles.z, isRectTransform);
                        }
                    }
                }
            }

            // Scale 키 추가
            if ((propertyTypes & PropertyType.Scale) != 0)
            {
                System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);
                if (AddPropertyKeys(sourceClip, selectedObjectPath, "m_LocalScale", selectedObject.transform.localScale, clipDuration, bindingType))
                {
                    anyKeyAdded = true;
                    addedProperties += "Scale ";
                }
            }

            // 키가 추가되었다면 애니메이션 창을 새로고침합니다.
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

        /// <summary>
        /// 애니메이션 클립에서 사용 중인 회전 속성 타입을 감지합니다
        /// </summary>
        private static string DetectRotationPropertyType(AnimationClip clip, string objectPath)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

            foreach (var binding in bindings)
            {
                if (binding.path == objectPath)
                {
                    if (binding.propertyName.Contains("m_LocalRotation"))
                        return "quaternion";
                    if (binding.propertyName.Contains("localEulerAngles") ||
                        binding.propertyName.Contains("m_LocalEulerAngles") ||
                        binding.propertyName.Contains("localEulerAnglesRaw"))
                        return "euler";
                }
            }

            // 기본값은 euler
            return "euler";
        }

        /// <summary>
        /// Quaternion 형식으로 회전 키를 추가합니다
        /// </summary>
        private static bool AddQuaternionKeys(AnimationClip clip, string objectPath, Quaternion rotation, float duration, bool isRectTransform)
        {
            try
            {
                string[] components = { ".x", ".y", ".z", ".w" };
                float[] values = { rotation.x, rotation.y, rotation.z, rotation.w };
                System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);

                for (int i = 0; i < 4; i++)
                {
                    string fullPropertyName = "m_LocalRotation" + components[i];
                    EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, fullPropertyName);

                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null)
                    {
                        curve = new AnimationCurve();
                    }

                    // 0프레임에 키 추가
                    curve.AddKey(0f, values[i]);

                    // 마지막 프레임에 키 추가
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
                Debug.LogError($"Failed to add quaternion keys: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Quaternion 기반의 m_LocalRotation 커브를 제거합니다. (Euler 각도 방식과의 중복 방지)
        /// </summary>
        private static void RemoveQuaternionRotationCurves(AnimationClip clip, string objectPath, bool isRectTransform)
        {
            try
            {
                System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);
                // Quaternion의 x, y, z, w 컴포넌트를 모두 제거
                string[] quaternionComponents = { ".x", ".y", ".z", ".w" };
                foreach (string component in quaternionComponents)
                {
                    EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalRotation" + component);
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve != null)
                    {
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to remove Quaternion rotation curves: {e.Message}");
            }
        }

        /// <summary>
        /// Euler 기반의 m_LocalEulerAngles 또는 localEulerAnglesRaw 커브를 제거합니다. (Quaternion 방식과의 중복 방지)
        /// </summary>
        private static void RemoveEulerRotationCurves(AnimationClip clip, string objectPath, bool isRectTransform)
        {
            try
            {
                System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);

                if (isRectTransform)
                {
                    // RectTransform: localEulerAnglesRaw.z 제거
                    EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "localEulerAnglesRaw.z");
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve != null)
                    {
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                    }
                }
                else
                {
                    // Transform: m_LocalEulerAngles.x/y/z 제거
                    string[] eulerComponents = { ".x", ".y", ".z" };
                    foreach (string component in eulerComponents)
                    {
                        EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalEulerAngles" + component);
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        if (curve != null)
                        {
                            AnimationUtility.SetEditorCurve(clip, binding, null);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to remove Euler rotation curves: {e.Message}");
            }
        }

        /// <summary>
        /// 선택된 GameObject의 Transform 속성에 대한 키를 추가합니다.
        /// </summary>
        private static bool AddPropertyKeys(AnimationClip clip, string objectPath, string propertyName, Vector3 value, float duration, System.Type bindingType)
        {
            try
            {
                // X, Y, Z 각각에 대해 키 추가
                string[] components = { ".x", ".y", ".z" };
                float[] values = { value.x, value.y, value.z };

                for (int i = 0; i < 3; i++)
                {
                    string fullPropertyName = propertyName + components[i];
                    EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, fullPropertyName);

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

        /// <summary>
        /// RectTransform의 Position 키를 추가합니다 (m_AnchoredPosition.x/y)
        /// </summary>
        private static bool AddRectTransformPositionKeys(AnimationClip clip, string objectPath, Vector2 anchoredPosition, float duration)
        {
            try
            {
                string[] components = { ".x", ".y" };
                float[] values = { anchoredPosition.x, anchoredPosition.y };

                for (int i = 0; i < 2; i++)
                {
                    string fullPropertyName = "m_AnchoredPosition" + components[i];
                    EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, typeof(RectTransform), fullPropertyName);

                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null)
                    {
                        curve = new AnimationCurve();
                    }

                    // 0프레임에 키 추가
                    curve.AddKey(0f, values[i]);

                    // 마지막 프레임에 키 추가
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
                Debug.LogError($"Failed to add RectTransform position keys: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// RectTransform의 Rotation 키를 추가합니다 (localEulerAnglesRaw.z)
        /// </summary>
        private static bool AddRectTransformRotationKeys(AnimationClip clip, string objectPath, float rotationZ, float duration)
        {
            try
            {
                string fullPropertyName = "localEulerAnglesRaw.z";
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, typeof(RectTransform), fullPropertyName);

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                {
                    curve = new AnimationCurve();
                }

                // 0프레임에 키 추가
                curve.AddKey(0f, rotationZ);

                // 마지막 프레임에 키 추가
                if (duration > 0f)
                {
                    curve.AddKey(duration, rotationZ);
                }

                AnimationUtility.SetEditorCurve(clip, binding, curve);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to add RectTransform rotation keys: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Rotation 프로퍼티를 초기화합니다. 0프레임 Z 값에 +0.1을 적용한 후 원래 값으로 복원합니다.
        /// </summary>
        private static void InitializeRotationProperty(AnimationClip clip, string objectPath, float originalZValue, bool isRectTransform)
        {
            try
            {
                System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);

                // 먼저 Euler 각도 Z 값 커브를 확인
                string eulerPropertyName = isRectTransform ? "localEulerAnglesRaw.z" : "m_LocalEulerAngles.z";
                EditorCurveBinding eulerBinding = EditorCurveBinding.FloatCurve(objectPath, bindingType, eulerPropertyName);
                AnimationCurve eulerCurve = AnimationUtility.GetEditorCurve(clip, eulerBinding);

                // Quaternion 방식인지 확인 (m_LocalRotation 커브 존재 여부)
                EditorCurveBinding quatBinding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalRotation.x");
                AnimationCurve quatCurve = AnimationUtility.GetEditorCurve(clip, quatBinding);

                if (quatCurve != null && quatCurve.keys.Length > 0)
                {
                    // Quaternion 방식: Quaternion에서 Euler Z 값을 계산하여 초기화
                    InitializeQuaternionRotationProperty(clip, objectPath, originalZValue, isRectTransform);
                }
                else if (eulerCurve != null && eulerCurve.keys.Length > 0)
                {
                    // Euler 방식: 직접 Euler Z 커브를 수정
                    InitializeEulerRotationProperty(clip, eulerBinding, eulerCurve, originalZValue);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to initialize rotation property: {e.Message}");
            }
        }

        /// <summary>
        /// Euler 방식 Rotation 프로퍼티를 초기화합니다.
        /// </summary>
        private static void InitializeEulerRotationProperty(AnimationClip clip, EditorCurveBinding binding, AnimationCurve curve, float originalZValue)
        {
            // 0프레임 키 찾기
            int keyIndex = -1;
            for (int i = 0; i < curve.keys.Length; i++)
            {
                if (Mathf.Approximately(curve.keys[i].time, 0f))
                {
                    keyIndex = i;
                    break;
                }
            }
            
            if (keyIndex == -1) return;
            
            // 0프레임 키의 값을 +0.1 증가
            var key = curve.keys[keyIndex];
            float tempValue = key.value + 0.1f;
            Keyframe newKey = new Keyframe(key.time, tempValue, key.inTangent, key.outTangent, key.inWeight, key.outWeight) 
            { 
                weightedMode = key.weightedMode 
            };
            curve.MoveKey(keyIndex, newKey);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            
            // 원래 값으로 복원
            Keyframe restoredKey = new Keyframe(key.time, originalZValue, key.inTangent, key.outTangent, key.inWeight, key.outWeight) 
            { 
                weightedMode = key.weightedMode 
            };
            curve.MoveKey(keyIndex, restoredKey);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        /// <summary>
        /// Quaternion 방식 Rotation 프로퍼티를 초기화합니다.
        /// </summary>
        private static void InitializeQuaternionRotationProperty(AnimationClip clip, string objectPath, float originalZValue, bool isRectTransform)
        {
            System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);

            // Quaternion의 x, y, z, w 값을 가져와서 Euler Z로 변환
            string[] quatComponents = { ".x", ".y", ".z", ".w" };
            float[] quatValues = new float[4];

            for (int i = 0; i < 4; i++)
            {
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalRotation" + quatComponents[i]);
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.keys.Length == 0) return;

                // 0프레임 키 찾기
                int keyIndex = -1;
                for (int j = 0; j < curve.keys.Length; j++)
                {
                    if (Mathf.Approximately(curve.keys[j].time, 0f))
                    {
                        keyIndex = j;
                        break;
                    }
                }
                if (keyIndex == -1) return;

                quatValues[i] = curve.keys[keyIndex].value;
            }

            // Quaternion에서 Euler Z 값 계산
            Quaternion quat = new Quaternion(quatValues[0], quatValues[1], quatValues[2], quatValues[3]);
            Vector3 euler = quat.eulerAngles;

            // Euler Z 값을 +0.1 증가한 후 Quaternion으로 변환
            Vector3 modifiedEuler = new Vector3(euler.x, euler.y, euler.z + 0.1f);
            Quaternion modifiedQuat = Quaternion.Euler(modifiedEuler);

            // Quaternion 값을 업데이트 (+0.1)
            float[] modifiedQuatValues = { modifiedQuat.x, modifiedQuat.y, modifiedQuat.z, modifiedQuat.w };
            for (int i = 0; i < 4; i++)
            {
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalRotation" + quatComponents[i]);
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;

                int keyIndex = -1;
                for (int j = 0; j < curve.keys.Length; j++)
                {
                    if (Mathf.Approximately(curve.keys[j].time, 0f))
                    {
                        keyIndex = j;
                        break;
                    }
                }
                if (keyIndex == -1) continue;

                var key = curve.keys[keyIndex];
                Keyframe newKey = new Keyframe(key.time, modifiedQuatValues[i], key.inTangent, key.outTangent, key.inWeight, key.outWeight)
                {
                    weightedMode = key.weightedMode
                };
                curve.MoveKey(keyIndex, newKey);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            // 원래 Quaternion 값으로 복원
            for (int i = 0; i < 4; i++)
            {
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalRotation" + quatComponents[i]);
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;

                int keyIndex = -1;
                for (int j = 0; j < curve.keys.Length; j++)
                {
                    if (Mathf.Approximately(curve.keys[j].time, 0f))
                    {
                        keyIndex = j;
                        break;
                    }
                }
                if (keyIndex == -1) continue;

                var key = curve.keys[keyIndex];
                Keyframe restoredKey = new Keyframe(key.time, quatValues[i], key.inTangent, key.outTangent, key.inWeight, key.outWeight)
                {
                    weightedMode = key.weightedMode
                };
                curve.MoveKey(keyIndex, restoredKey);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }

        /// <summary>
        /// 모든 객체의 불필요한 키를 제거합니다.
        /// </summary>
        private static void CleanAllUnnecessaryKeys()
        {
            // 불필요한 키를 모두 제거하는 과정
            // 1. Animation Window State를 가져옵니다.
            // 2. 현재 선택된 Animation Clip을 가져옵니다.
            // 3. Animation Clip의 경로를 가져옵니다.
            // 4. Animation Clip을 로드합니다.
            // 5. 애니메이션 루트 GameObject를 가져옵니다.
            // 6. 모든 커브 바인딩을 가져옵니다.
            // 7. 불필요한 키를 제거합니다.
            // 8. 애니메이션 창을 새로고침합니다.
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
            var processedObjects = new HashSet<string>(); // 이미 처리된 객체를 저장하기 위한 해시셋을 생성합니다.

            foreach (var binding in bindings)
            {
                if (!processedObjects.Contains(binding.path)) // 이미 처리된 객체가 아니라면 처리합니다.
                {
                    processedObjects.Add(binding.path);
                    objectsProcessed++; // 처리된 객체 수를 증가시킵니다.
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                if (curve == null || curve.keys.Length <= 2) continue; // 커브가 없거나 키가 2개 이하라면 건너뜁니다.

                AnimationCurve cleanedCurve = RemoveUnnecessaryKeys(curve);
                if (cleanedCurve != null && cleanedCurve.keys.Length != curve.keys.Length) // 불필요한 키가 제거되었다면 커브를 업데이트합니다.
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

        /// <summary>
        /// 불필요한 키를 제거합니다.
        /// </summary>
        private static AnimationCurve RemoveUnnecessaryKeys(AnimationCurve originalCurve)
        {
            if (originalCurve.keys.Length <= 2) return originalCurve; // 키가 2개 이하라면 원본 반환합니다.

            var keys = new List<Keyframe>(originalCurve.keys); // 원본 커브의 키를 리스트로 변환합니다.
            var cleanedKeys = new List<Keyframe>(); // 정리된 키를 저장하기 위한 리스트를 생성합니다.
            
            // 첫 번째 키는 항상 유지
            cleanedKeys.Add(keys[0]); // 첫 번째 키는 항상 유지합니다.
            
            for (int i = 1; i < keys.Count - 1; i++) // 두 번째 키부터 마지막 키 전까지 반복합니다.
            {
                var currentKey = keys[i]; // 현재 키를 가져옵니다.
                var previousKey = keys[i - 1]; // 이전 키를 가져옵니다.
                var nextKey = keys[i + 1]; // 다음 키를 가져옵니다.
                
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

        /// <summary>
        /// 루프 애니메이션 오프셋을 적용합니다.
        /// </summary>
        private static void ApplyLoopOffset(PropertyType propertyType)
        {
            if (offsetValue == 0) { Debug.LogWarning("Offset value is 0."); return; } // 오프셋 값이 0이라면 건너뜁니다.

            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null) { Debug.LogError("Select a GameObject."); return; } // 선택된 GameObject가 없다면 건너뜁니다.

            object state = GetAnimationWindowState();
            if (state == null) { Debug.LogError("Animation Window is not open."); return; } // Animation Window가 열려 있지 않다면 건너뜁니다.

            AnimationClip activeClip = GetActiveAnimationClipFromState(state);
            if (activeClip == null) { Debug.LogError("Select an Animation Clip."); return; } // 선택된 Animation Clip이 없다면 건너뜁니다.

            string clipPath = AssetDatabase.GetAssetPath(activeClip);
            if (string.IsNullOrEmpty(clipPath)) { Debug.LogError("Cannot find asset path for the clip."); return; } // Animation Clip의 경로를 찾을 수 없다면 건너뜁니다.

            AnimationClip sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (sourceClip == null) { Debug.LogError($"Failed to load clip from path: {clipPath}"); return; } // Animation Clip을 로드할 수 없다면 건너뜁니다.

            GameObject rootObject = GetActiveRootGameObjectFromState(state);
            if (rootObject == null) { Debug.LogError("Cannot find animation root GameObject."); return; } // 애니메이션 루트 GameObject를 찾을 수 없다면 건너뜁니다.

            float loopDurationSecs = sourceClip.length;
            if (loopDurationSecs <= 0) { Debug.LogError("Clip length must be greater than 0."); return; } // 클립 길이가 0 이하라면 건너뜁니다.

            float timeOffset = isTimeInputMode ? offsetValue : (offsetValue / sourceClip.frameRate); // 시간 오프셋 또는 프레임 오프셋을 계산합니다.
            timeOffset %= loopDurationSecs;
            if (timeOffset < 0) timeOffset += loopDurationSecs; // 오프셋 값이 음수라면 양수로 변환합니다.

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

        /// <summary>
        /// 오프셋 커브를 생성합니다.
        /// </summary>
        private static AnimationCurve CreateOffsetCurve(AnimationCurve originalCurve, float timeOffset, float loopDuration)
        {
            if (originalCurve.keys.Length == 0) return null; // 커브에 키가 없다면 null을 반환합니다.

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
                case PropertyType.Position:
                    return propertyName.Contains("Position") ||
                           propertyName.Contains("m_AnchoredPosition");
                case PropertyType.Rotation:
                    return propertyName.Contains("Euler") ||
                           propertyName.Contains("Rotation") ||
                           propertyName.Contains("localEulerAngles") ||
                           propertyName.Contains("localEulerAnglesRaw");
                case PropertyType.Scale:
                    return propertyName.Contains("Scale");
                default:
                    return false;
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
