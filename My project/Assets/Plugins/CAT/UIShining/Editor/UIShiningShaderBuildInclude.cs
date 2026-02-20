using UnityEditor;
using UnityEngine;

namespace CAT.Effects
{
    /// <summary>
    /// UIShining 셰이더를 iOS/Android/WebGL 빌드에서 누락되지 않도록 Always Included Shaders에 등록하는 유틸리티.
    /// 메뉴에서 한 번 실행하면 GraphicsSettings에 반영됩니다.
    /// </summary>
    public static class UIShiningShaderBuildInclude
    {
        private const string MENU = "CAT/Effects/UIShining - 셰이더 빌드 포함 등록";
        private const string SHADER_GUID = "97b21d7b7f2f2614dbf18133253099d4";

        [MenuItem(MENU)]
        public static void EnsureShaderIncludedInBuild()
        {
            Shader shader = Shader.Find(UIShining.SHADER_NAME);
            if (shader == null)
            {
                string path = AssetDatabase.GUIDToAssetPath(SHADER_GUID);
                if (!string.IsNullOrEmpty(path))
                    shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            }

            if (shader == null)
            {
                Debug.LogError("[UIShining] 셰이더를 찾을 수 없습니다. CAT_UIShining.shader가 프로젝트에 있는지 확인하세요.");
                return;
            }

            SerializedObject graphicsSettings = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty arrayProp = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    Debug.Log("[UIShining] 셰이더가 이미 Always Included Shaders에 등록되어 있습니다.");
                    return;
                }
            }

            arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
            arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1).objectReferenceValue = shader;
            graphicsSettings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.Log("[UIShining] 셰이더를 Always Included Shaders에 등록했습니다. iOS/Android/WebGL 빌드에 포함됩니다.");
        }
    }
}
