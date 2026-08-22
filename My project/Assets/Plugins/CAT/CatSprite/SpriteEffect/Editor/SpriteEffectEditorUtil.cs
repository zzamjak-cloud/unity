using System;
using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// 에디터에서 값을 시간에 따라 흘려 보며 확인하는 프리뷰 드라이버.
    /// SpriteEffect / SpriteGroupEffect 인스펙터가 공유한다.
    /// </summary>
    internal sealed class SpriteEffectPreview
    {
        private const float Duration = 0.8f;

        private readonly Action repaint;

        private UnityEngine.Object owner;
        private Action<float> setter;
        private float originalValue;
        private bool pingPong;
        private double startTime;

        public bool Running { get; private set; }

        public SpriteEffectPreview(Action repaint)
        {
            this.repaint = repaint;
        }

        /// <summary>0 → 1 → 0. 피격 번쩍임 확인용.</summary>
        public void StartFlash(UnityEngine.Object owner, Action<float> setter, float originalValue)
        {
            Begin(owner, setter, originalValue, true);
        }

        /// <summary>0 → 1. 디졸브 확인용.</summary>
        public void StartRamp(UnityEngine.Object owner, Action<float> setter, float originalValue)
        {
            Begin(owner, setter, originalValue, false);
        }

        private void Begin(UnityEngine.Object owner, Action<float> setter, float originalValue, bool pingPong)
        {
            Stop();

            if (owner == null)
                return;

            this.owner = owner;
            this.setter = setter;
            this.originalValue = originalValue;
            this.pingPong = pingPong;

            startTime = EditorApplication.timeSinceStartup;
            Running = true;
            EditorApplication.update += Tick;
        }

        public void Stop()
        {
            if (!Running)
                return;

            Running = false;
            EditorApplication.update -= Tick;

            if (owner != null)
                setter(originalValue);

            owner = null;
            setter = null;
            SceneView.RepaintAll();
        }

        private void Tick()
        {
            if (owner == null)
            {
                Running = false;
                EditorApplication.update -= Tick;
                owner = null;
                setter = null;
                return;
            }

            float t = Mathf.Clamp01((float)(EditorApplication.timeSinceStartup - startTime) / Duration);
            setter(pingPong ? 1f - Mathf.Abs(t * 2f - 1f) : t);

            SceneView.RepaintAll();
            repaint?.Invoke();

            if (t >= 1f)
                Stop();
        }
    }

    /// <summary>
    /// 두 컴포넌트 모두 런타임에 Shader.Find로 머티리얼을 만들기 때문에,
    /// 통합 셰이더가 Always Included Shaders에 없으면 빌드에서 효과가 사라진다.
    /// </summary>
    internal static class SpriteEffectShaderRegistration
    {
        /// <summary>미등록 상태면 경고와 등록 버튼을 그린다.</summary>
        public static void DrawFixupUI()
        {
            if (IsRegistered())
                return;

            EditorGUILayout.HelpBox(
                $"셰이더 '{SpriteEffect.ShaderName}'가 Always Included Shaders에 등록되어 있지 않습니다.\n" +
                "런타임에 Shader.Find로 머티리얼을 만들므로 빌드에서 효과가 나오지 않습니다.",
                MessageType.Error);

            if (GUILayout.Button("Always Included Shaders에 등록"))
                Register();
        }

        private static SerializedProperty FindList()
        {
            UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (settings == null || settings.Length == 0)
                return null;

            var serialized = new SerializedObject(settings[0]);
            return serialized.FindProperty("m_AlwaysIncludedShaders");
        }

        private static bool IsRegistered()
        {
            SerializedProperty list = FindList();
            if (list == null)
                return true; // 확인 불가 시 경고를 띄우지 않는다.

            Shader shader = Shader.Find(SpriteEffect.ShaderName);
            if (shader == null)
                return false;

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return true;
            }

            return false;
        }

        private static void Register()
        {
            Shader shader = Shader.Find(SpriteEffect.ShaderName);
            if (shader == null)
                return;

            SerializedProperty list = FindList();
            if (list == null)
                return;

            SerializedObject serialized = list.serializedObject;
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            serialized.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            Debug.Log($"[SpriteEffect] '{SpriteEffect.ShaderName}'를 Always Included Shaders에 등록했습니다.");
        }
    }
}
