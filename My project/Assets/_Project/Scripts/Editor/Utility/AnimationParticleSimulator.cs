using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class AnimationParticleSimulator 
{
    private struct AnimFrameInfo
    {
        public float time;
        public float value;
    }
    
    private static AnimationClip _animationClip;
    private static EditorWindow _cachedEditorWindow;
    private static int _editorFrame;
    private static Dictionary<string, AnimFrameInfo> _propertyDirties = new();
    private static IMGUIContainer _imguiContainer;
    private static IMGUIContainer _timelineContainer;
    private static bool _isOnSimulation;
    private static GUIContent _iconContent;
    private static object _stateField;
    private static bool _isForceClearDirty;

    static AnimationParticleSimulator()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    private static void Update()
    {
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        
        if (_cachedEditorWindow == null)
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
