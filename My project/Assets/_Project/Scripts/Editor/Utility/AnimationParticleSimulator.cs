using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
using UnityEngine.UIElements;

public static class AnimationParticleSimulator 
{
    private struct AnimFrameInfo
    {
        public float time;
        public float value;
    }
    
    private static AnimationClip _animationClip;  // AnimationWindowState를 가져오기 위한 변수
    private static EditorWindow _cachedEditorWindow;  // AnimationWindow 위젯의 state 정보를 가져오기 위한 변수
    private static int _editorFrame;  // 현재 에디터 프레임(현재 프레임)
    private static Dictionary<string, AnimFrameInfo> _propertyDirties = new();  // 프로퍼티 변화 정보
    private static IMGUIContainer _imguiContainer;  // 파티클 시뮬레이션 토글 버튼
    private static IMGUIContainer _timelineContainer;  // 타임라인 컨트롤 위젯
    private static bool _isOnSimulation;  // 파티클 시뮬레이션 상태 토글 위젯의 상태
    private static GUIContent _iconContent;  // 파티클 시뮬레이션 아이콘 컨텐트(타이틀)
    private static object _stateField;  // 파티클 시뮬레이션 토글 위젯의 상태(isOnSimulation)
    private static bool _isForceClearDirty;  // 파티클 시뮬레이션 토글 위젯의 상태(isOnSimulation)

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        
        if (_cachedEditorWindow == null)  // 캐시된 에디터 윈도우가 없으면 초기화
        {
            var editorAssembly = typeof(Editor).Assembly;
            var animationWindows =
                Resources.FindObjectsOfTypeAll(editorAssembly.GetType("UnityEditor.AnimationWindow"));

            if (animationWindows.Length == 0)
                return;

            _cachedEditorWindow = (EditorWindow)animationWindows[0];
        }
        
        var rawRoot = _cachedEditorWindow.rootVisualElement;
        
        if (_imguiContainer == null)
        {
            var parent = new VisualElement
            {
                style =
                {
                    position = Position.Relative,
                    left = 212f,
                    width = 30f,
                    height = 20f
                }
            };

            _imguiContainer = new IMGUIContainer();
            _imguiContainer.style.flexGrow = 1;
            _imguiContainer.onGUIHandler -= OnGUIParticleSimulationToggle;
            _imguiContainer.onGUIHandler += OnGUIParticleSimulationToggle;
            parent.Add(_imguiContainer);
            rawRoot.Add(parent);
        }
        
        if (_isOnSimulation == false && _isForceClearDirty == false)
            return;

        if (_stateField == null)
        {
            var animEditorField = _cachedEditorWindow.GetType().GetField("m_AnimEditor", bindingFlags);
            var animEditor = animEditorField?.GetValue(_cachedEditorWindow);

            if (animEditor == null) 
                return;
            
            var stateFieldInfo = animEditor.GetType().GetField("m_State", bindingFlags);

            if (stateFieldInfo == null)
                return;

            _stateField = stateFieldInfo.GetValue(animEditor);
        }

        if (_stateField == null)
            return;
        
        // AnimationWindowState 타입
        var stateType = _stateField.GetType();
        var playingProp = stateType.GetProperty("playing", bindingFlags);
        var frameProp = stateType.GetProperty("currentFrame", bindingFlags);
        var timeProp = stateType.GetProperty("currentTime", bindingFlags);
        var activeRootGameObjectProp = stateType.GetProperty("activeRootGameObject", bindingFlags);
        
        if (playingProp == null || frameProp == null || timeProp == null || activeRootGameObjectProp == null) 
            return;
        
        var isPlaying = (bool)playingProp.GetValue(_stateField, null);
        var currentFrame = (int)frameProp.GetValue(_stateField, null);
        var currentTime = (float)timeProp.GetValue(_stateField, null);

        if (currentFrame != _editorFrame)
        {
            var activeAnimationClipProp = stateType.GetProperty("activeAnimationClip", bindingFlags);
            
            if (activeAnimationClipProp == null)
                return;
            
            var animationClip = activeAnimationClipProp!.GetValue(_stateField, null) as AnimationClip;

            if (_animationClip != animationClip)
            {
                _propertyDirties.Clear();
                _animationClip = animationClip;
            }
            
            if (_animationClip == null)
                return;
            
            var activeRootGameObject = activeRootGameObjectProp.GetValue(_stateField, null) as GameObject;

            if (activeRootGameObject == null)
                return;
            
            var curveBindings = AnimationUtility.GetCurveBindings(_animationClip);

            foreach (var binding in curveBindings)
            {
                var curve = AnimationUtility.GetEditorCurve(_animationClip, binding);
                var value = curve.Evaluate(currentTime);
                var findTarget = activeRootGameObject.transform.Find(binding.path);
                
                if (findTarget == null)
                    continue;

                // 게임 오브젝트가 켜지는 애니메이션 키 프레임에서만 동작
                if (binding.propertyName != "m_IsActive" || !(value >= 0f)) 
                    continue;
                
                var particleSystems = findTarget.GetComponentsInChildren<ParticleSystem>(true);
                    
                var key = $"{binding.path}_{binding.propertyName}";
                _propertyDirties.TryAdd(key, new AnimFrameInfo());
                var prevValue = _propertyDirties[key].value;
                        
                if (Math.Abs(prevValue - value) > float.Epsilon)
                {
                    _propertyDirties[key] = new AnimFrameInfo
                    {
                        time = currentTime,
                        value = value
                    };
                }
                        
                var currentSelection = Selection.objects;

                var newSelection = new Object[currentSelection.Length + 1];
                newSelection[0] = findTarget;
                currentSelection.CopyTo(newSelection, 1);
                
                foreach (var particleSystem in particleSystems)
                {
                    particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

                    if (!particleSystem.useAutoRandomSeed) 
                        continue;
                    
                    particleSystem.useAutoRandomSeed = false;
                    particleSystem.randomSeed = (uint)particleSystem.GetHashCode();
                }

                if (_isForceClearDirty == false)
                {
                    var t = currentTime - _propertyDirties[key].time;
                        
                    foreach (var particleSystem in particleSystems)
                    {
                        particleSystem.Play(false);
                        particleSystem.time = t;
                        particleSystem.Simulate(t, false, true, true);
                    }
                }
                
                _isForceClearDirty = false;
            }
        }
        
        _editorFrame = currentFrame;
    }
    
    private static void OnGUIParticleSimulationToggle()
    {
        if (_iconContent == null)
        {
            var icon = EditorGUIUtility.IconContent("Particle Effect");
            _iconContent = new GUIContent(icon.image, "Particle Simulation");
        }
        
        using (new GUILayout.AreaScope(new Rect(0f, 0f, 30f, 30f)))
        {
            var isOnSimulation = GUILayout.Toggle(_isOnSimulation, _iconContent, EditorStyles.toolbarButton);

            if (_isOnSimulation == isOnSimulation) 
                return;
            
            _isOnSimulation = isOnSimulation;
            _isForceClearDirty = true;
        }
    }
}
