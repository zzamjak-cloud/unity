using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace SoftMaskLight
{
#if UNITY_EDITOR
    /// <summary>
    /// SoftMaskLight 자동 설치/설정 관리자
    ///
    /// [InitializeOnLoad]로 에디터 로드 시 자동 실행:
    /// 1. SoftMaskLight 설치 경로 자동 탐지 (SoftMaskLight_Core.cginc 기준)
    /// 2. Assets/ 루트에 redirect cginc 파일 생성 (외부 셰이더용 포터블 include)
    /// 3. Resources/ 폴더에 SoftMaskLightSettings 에셋 생성 (빌드 시 셰이더 포함 보장)
    /// 4. Hidden 변형 셰이더 참조 자동 수집 및 등록
    /// </summary>
    [InitializeOnLoad]
    static class SoftMaskLightInstaller
    {
        // 빌드에 포함할 Hidden 변형 셰이더 이름 목록
        private static readonly string[] HiddenShaderNames =
        {
            // SoftMaskLight 전용 변형
            "Hidden/UI/Default (SoftMaskLight)",
            "Hidden/UI/Default (UIEffect) (SoftMaskLight)",
            "Hidden/SoftMaskLight/Particles/UIAdditive (SoftMaskLight)",
            "Hidden/SoftMaskLight/Particles/UIAlphaBlend (SoftMaskLight)",
            "Hidden/CAT/Effects/ColorReplace (SoftMaskLight)",
            "Hidden/CAT/Particles/UIAdditiveCustom (SoftMaskLight)",
            "Hidden/CAT/Particles/UIAlphaBlendCustom (SoftMaskLight)",
            "Hidden/CAT/Particles/FlowUV (SoftMaskLight)",
            // mob-sakai SoftMaskable 전용 변형 (파티클 블렌드 모드 보존)
            "Hidden/SoftMaskLight/Particles/UIAdditive (SoftMaskable)",
            "Hidden/SoftMaskLight/Particles/UIAlphaBlend (SoftMaskable)",
        };

        // redirect cginc 파일 이름 (Assets/ 루트에 생성)
        private const string REDIRECT_FILENAME = "SoftMaskLight_Core.cginc";
        // 실제 Core cginc 파일 이름 (검색용)
        private const string CORE_CGINC_FILENAME = "SoftMaskLight_Core";

        static SoftMaskLightInstaller()
        {
            // 에디터 로드 후 지연 실행 (AssetDatabase 준비 대기)
            EditorApplication.delayCall += RunInstaller;
        }

        private static void RunInstaller()
        {
            // 에디터가 컴파일 중이면 다시 지연
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += RunInstaller;
                return;
            }

            string shaderFolderPath = FindShaderFolderPath();
            if (string.IsNullOrEmpty(shaderFolderPath)) return;

            EnsureRedirectCginc(shaderFolderPath);
            EnsureSettingsAsset(shaderFolderPath);
        }

        /// <summary>
        /// SoftMaskLight_Core.cginc 파일의 위치를 기반으로 Shader 폴더 경로를 탐지한다.
        /// 반환값: "Plugins/CAT/SoftMaskLight/Shader" (Assets/ 기준 상대 경로)
        /// </summary>
        private static string FindShaderFolderPath()
        {
            // AssetDatabase에서 SoftMaskLight_Core 검색 (확장자 없이)
            string[] guids = AssetDatabase.FindAssets(CORE_CGINC_FILENAME);
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith("SoftMaskLight_Core.cginc"))
                {
                    // "Assets/Plugins/CAT/SoftMaskLight/Shader/SoftMaskLight_Core.cginc"
                    // → "Plugins/CAT/SoftMaskLight/Shader"
                    string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                    if (dir.StartsWith("Assets/"))
                        dir = dir.Substring("Assets/".Length);
                    return dir;
                }
            }

            Debug.LogWarning("[SoftMaskLight] SoftMaskLight_Core.cginc를 찾을 수 없습니다.");
            return null;
        }

        /// <summary>
        /// Assets/ 루트에 redirect cginc 파일을 생성/갱신한다.
        /// 외부 셰이더에서 #include "SoftMaskLight_Core.cginc" 만으로 사용 가능하게 한다.
        /// </summary>
        private static void EnsureRedirectCginc(string shaderFolderPath)
        {
            string redirectPath = Path.Combine(Application.dataPath, REDIRECT_FILENAME);
            string includePath = shaderFolderPath + "/SoftMaskLight_Core.cginc";

            string expectedContent =
                "// SoftMaskLight redirect include (자동 생성 파일 - 수정 금지)\n" +
                "// 외부 셰이더에서 #include \"SoftMaskLight_Core.cginc\" 로 사용\n" +
                $"#include \"{includePath}\"\n";

            // 이미 올바른 내용이면 스킵
            if (File.Exists(redirectPath))
            {
                string existing = File.ReadAllText(redirectPath);
                if (existing == expectedContent) return;
            }

            File.WriteAllText(redirectPath, expectedContent);
            AssetDatabase.ImportAsset("Assets/" + REDIRECT_FILENAME, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// SoftMaskLightSettings 에셋을 Resources 폴더에 생성/갱신한다.
        /// Hidden 변형 셰이더 참조를 자동으로 수집하여 등록한다.
        /// </summary>
        private static void EnsureSettingsAsset(string shaderFolderPath)
        {
            // SoftMaskLight 폴더 내 Resources 폴더 경로 결정
            // "Plugins/CAT/SoftMaskLight/Shader" → "Plugins/CAT/SoftMaskLight"
            string pluginFolder = Path.GetDirectoryName(shaderFolderPath).Replace('\\', '/');
            string resourcesFolderAsset = "Assets/" + pluginFolder + "/Resources";
            string resourcesFolderDisk = Path.Combine(Application.dataPath, pluginFolder, "Resources");

            // Resources 폴더 생성
            if (!Directory.Exists(resourcesFolderDisk))
            {
                Directory.CreateDirectory(resourcesFolderDisk);
                AssetDatabase.ImportAsset(resourcesFolderAsset);
            }

            // Settings 에셋 로드 또는 생성
            string settingsAssetPath = resourcesFolderAsset + "/SoftMaskLightSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<SoftMaskLightSettings>(settingsAssetPath);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SoftMaskLightSettings>();
                AssetDatabase.CreateAsset(settings, settingsAssetPath);
            }

            // Hidden 셰이더 참조 수집
            var shaders = new List<Shader>();
            foreach (string shaderName in HiddenShaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                    shaders.Add(shader);
            }

            // 프로젝트 내 추가 Optional Shader 자동 탐색
            // "[OptionalShader] SoftMaskLight:" 주석이 있는 셰이더 파일 검색
            string[] allShaderGuids = AssetDatabase.FindAssets("t:Shader");
            foreach (string guid in allShaderGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".shader")) continue;

                Shader s = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (s != null && s.name.Contains("(SoftMaskLight)") && !shaders.Contains(s))
                    shaders.Add(s);
            }

            settings.SetIncludedShaders(shaders.ToArray());
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 메뉴에서 수동으로 재설치/갱신 가능
        /// </summary>
        [MenuItem("Tools/SoftMaskLight/Refresh Settings")]
        private static void ManualRefresh()
        {
            RunInstaller();
            Debug.Log("[SoftMaskLight] 설정이 갱신되었습니다.");
        }
    }
#endif
}
