using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace SoftMaskLight
{
#if UNITY_EDITOR
    /// <summary>
    /// SoftMaskLight 자동 설치/설정 관리자
    ///
    /// [InitializeOnLoad]로 에디터 로드 시 자동 실행:
    /// 1. SoftMaskLight 설치 경로 자동 탐지 (SoftMaskLight_Core.cginc 기준)
    /// 2. Assets/ 루트에 redirect cginc 파일 생성 (외부 셰이더용 포터블 include)
    /// 3. UIEffect 패키지 부재 시 UIEffect 의존 셰이더 폴더 격리 (임포트 에러 방지)
    /// 4. Resources/ 폴더에 SoftMaskLightSettings 에셋 생성 (빌드 시 셰이더 포함 보장)
    /// 5. Hidden 변형 셰이더 중 "원본 셰이더가 실제로 존재하는 것만" 등록
    ///    → 미사용 변형이 모바일 빌드에 포함되지 않음 (셰이더는 Resources 밖에 위치)
    /// </summary>
    [InitializeOnLoad]
    static class SoftMaskLightInstaller
    {
        // 선택적 패키지 이름
        private const string PKG_UIEFFECT = "com.coffee.ui-effect";

        // 빌드 포함 후보 Hidden 변형 셰이더 이름 목록
        // 등록 조건: 대응하는 원본 셰이더가 프로젝트에 실제로 존재할 것
        private static readonly string[] HiddenShaderNames =
        {
            "Hidden/UI/Default (SoftMaskLight)",
            "Hidden/CAT/VFX/UIAdditive (SoftMaskLight)",
            "Hidden/CAT/VFX/UIAlphaBlend (SoftMaskLight)",
            "Hidden/CAT/Effects/ColorReplace (SoftMaskLight)",
            "Hidden/CAT/Effects/UIShining (SoftMaskLight)",
            "Hidden/CAT/Effects/Windable (SoftMaskLight)",
        };

        // UIEffect 마스킹용 오버라이드 셰이더 (패키지 셰이더와 이름 충돌을 피한 고유 이름)
        private const string UIEFFECT_OVERRIDE_SHADER = "Hidden/UI/Default (UIEffect) (SoftMaskLight)";

        // redirect cginc 파일 이름 (Assets/ 루트에 생성)
        private const string REDIRECT_FILENAME = "SoftMaskLight_Core.cginc";
        // 실제 Core cginc 파일 이름 (검색용)
        private const string CORE_CGINC_FILENAME = "SoftMaskLight_Core";
        // 에디터 세션당 자동 실행 1회 제한용 키
        private const string SESSION_KEY = "SoftMaskLightInstaller.Ran";

        static SoftMaskLightInstaller()
        {
            // 에디터 로드 후 지연 실행 (AssetDatabase 준비 대기)
            EditorApplication.delayCall += RunInstallerAuto;
            // 패키지 설치/제거 시 재실행 (세션 가드로 인해 갱신을 놓치는 것 방지)
            UnityEditor.PackageManager.Events.registeredPackages += OnPackagesChanged;
        }

        private static void OnPackagesChanged(UnityEditor.PackageManager.PackageRegistrationEventArgs args)
        {
            SessionState.EraseBool(SESSION_KEY);
            EditorApplication.delayCall += RunInstallerAuto;
        }

        private static void RunInstallerAuto()
        {
            // 도메인 리로드마다 전체 셰이더 스캔이 반복되지 않도록 세션당 1회만 자동 실행
            // (패키지 변경·빌드 직전에는 가드를 해제하고 다시 돌린다)
            if (SessionState.GetBool(SESSION_KEY, false)) return;
            RunInstaller();
        }

        /// <summary>
        /// 빌드 전처리기 등 외부에서 강제 갱신할 때 사용 (세션 가드 무시)
        /// </summary>
        internal static void ForceRefresh()
        {
            SessionState.EraseBool(SESSION_KEY);
            RunInstaller();
        }

        /// <summary>
        /// 현재 Settings 에셋에 등록된 셰이더 목록 (빌드 로그용)
        /// </summary>
        internal static Shader[] GetRegisteredShaders()
        {
            var settings = Resources.Load<SoftMaskLightSettings>("SoftMaskLightSettings");
            return settings != null ? settings.IncludedShaders : new Shader[0];
        }

        private static void RunInstaller()
        {
            // 에디터가 컴파일 중이면 다시 지연
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += RunInstaller;
                return;
            }

            SessionState.SetBool(SESSION_KEY, true);

            string shaderFolderPath = FindShaderFolderPath();
            if (string.IsNullOrEmpty(shaderFolderPath)) return;

            EnsureRedirectCginc(shaderFolderPath);
            EnsureUIEffectShaderQuarantine(shaderFolderPath);
            EnsureSettingsAsset(shaderFolderPath);
        }

        /// <summary>
        /// 패키지 설치 여부 확인 (Packages/manifest.json 기반)
        /// </summary>
        private static bool IsPackageInstalled(string packageName)
        {
            return UnityEditor.PackageManager.PackageInfo.FindForPackageName(packageName) != null;
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
                if (!assetPath.EndsWith("SoftMaskLight_Core.cginc")) continue;

                // Assets/ 루트의 redirect 파일 자신은 제외 (자기 자신을 원본으로 오인 방지)
                if (assetPath == "Assets/" + REDIRECT_FILENAME) continue;

                // "Assets/Plugins/CAT/SoftMaskLight/Shader/SoftMaskLight_Core.cginc"
                // → "Plugins/CAT/SoftMaskLight/Shader"
                string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                if (dir.StartsWith("Assets/"))
                    dir = dir.Substring("Assets/".Length);
                return dir;
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
        /// UIEffect 패키지 부재 시 UIEffect 의존 셰이더 폴더를 "UIEffect~"로 격리한다.
        /// (~ 접미사 폴더는 Unity가 임포트하지 않으므로 UIEffect.cginc include 에러가 발생하지 않음)
        /// 패키지가 다시 설치되면 자동으로 복원한다.
        /// </summary>
        private static void EnsureUIEffectShaderQuarantine(string shaderFolderPath)
        {
            string absShaderFolder = Path.Combine(Application.dataPath, shaderFolderPath);
            string activeDir = Path.Combine(absShaderFolder, "UIEffect");
            string quarantineDir = Path.Combine(absShaderFolder, "UIEffect~");
            bool uiEffectInstalled = IsPackageInstalled(PKG_UIEFFECT);

            try
            {
                if (uiEffectInstalled && Directory.Exists(quarantineDir) && !Directory.Exists(activeDir))
                {
                    // 패키지 재설치됨 → 격리 해제
                    Directory.Move(quarantineDir, activeDir);
                    AssetDatabase.Refresh();
                    Debug.Log("[SoftMaskLight] UIEffect 패키지 감지 — UIEffect 대응 셰이더를 활성화했습니다.");
                }
                else if (!uiEffectInstalled && Directory.Exists(activeDir))
                {
                    // 패키지 없음 → 임포트 에러 방지를 위해 격리
                    Directory.Move(activeDir, quarantineDir);
                    // 폴더 meta 제거 (파일 meta는 폴더째 이동되어 보존됨)
                    string meta = activeDir + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                    AssetDatabase.Refresh();
                    Debug.LogWarning("[SoftMaskLight] com.coffee.ui-effect 패키지가 없어 UIEffect 대응 셰이더를 " +
                                     "Shader/UIEffect~ 폴더로 격리했습니다. 패키지 설치 시 자동 복원됩니다.");
                }
            }
            catch (IOException e)
            {
                Debug.LogError($"[SoftMaskLight] UIEffect 셰이더 폴더 격리/복원 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Hidden 변형 셰이더 이름에서 원본 셰이더 이름을 추출한다.
        /// "Hidden/CAT/Effects/UIShining (SoftMaskLight)" → "CAT/Effects/UIShining"
        /// </summary>
        private static string GetOriginalShaderName(string variantName)
        {
            string name = variantName;
            int suffixIdx = name.LastIndexOf(" (SoftMask", System.StringComparison.Ordinal);
            if (suffixIdx > 0) name = name.Substring(0, suffixIdx);
            if (name.StartsWith("Hidden/")) name = name.Substring("Hidden/".Length);
            return name;
        }

        /// <summary>
        /// 변형 셰이더의 등록 필요 여부 판단:
        /// 원본 셰이더가 프로젝트/빌트인에 존재해야 한다.
        /// (원본이 없으면 런타임 교체 경로 자체가 생기지 않으므로 빌드에서 제외한다)
        /// 원본이 Hidden/ 셰이더인 중첩 변형(예: "Hidden/UI/Default (UIEffect) (SoftMaskLight)")은
        /// 접두사 유지본을 먼저 시도한다 — 무조건 제거하면 원본을 영영 찾지 못한다.
        /// </summary>
        private static bool ShouldRegisterVariant(string variantName)
        {
            string baseName = variantName;
            int suffixIdx = baseName.LastIndexOf(" (SoftMask", System.StringComparison.Ordinal);
            if (suffixIdx > 0) baseName = baseName.Substring(0, suffixIdx);

            if (Shader.Find(baseName) != null) return true;
            if (baseName.StartsWith("Hidden/") &&
                Shader.Find(baseName.Substring("Hidden/".Length)) != null) return true;
            return false;
        }

        /// <summary>
        /// SoftMaskLightSettings 에셋을 Resources 폴더에 생성/갱신한다.
        /// 사용 가능한 Hidden 변형 셰이더 참조만 수집하여 등록한다.
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

            bool uiEffectInstalled = IsPackageInstalled(PKG_UIEFFECT);

            // 등록 조건을 만족하는 Hidden 셰이더 참조 수집
            // 원본 부재로 제외된 변형은 경고로 알린다 — 무음 폴백(블렌드 모드 파괴)의 조기 발견용
            var shaders = new List<Shader>();
            foreach (string shaderName in HiddenShaderNames)
            {
                if (!ShouldRegisterVariant(shaderName))
                {
                    if (Shader.Find(shaderName) != null)
                        Debug.LogWarning($"[SoftMaskLight] 원본 셰이더 '{GetOriginalShaderName(shaderName)}'가 " +
                                         $"프로젝트에 없어 변형 '{shaderName}'를 빌드에서 제외합니다. " +
                                         "해당 셰이더를 쓰는 자식은 기본 UI 변형으로 폴백되어 블렌드 모드가 달라질 수 있습니다.");
                    continue;
                }

                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                    shaders.Add(shader);
            }

            // UIEffect 오버라이드 셰이더 — 고유 이름이므로 Shader.Find가 결정적으로 해석됨
            // UIEffect 설치 시에만 존재/등록 — 이펙트 키워드는 shader_feature이므로 변형 폭발 없음
            Shader uiEffectOverride = null;
            if (uiEffectInstalled)
            {
                uiEffectOverride = Shader.Find(UIEFFECT_OVERRIDE_SHADER);
                if (uiEffectOverride == null)
                {
                    Debug.LogWarning($"[SoftMaskLight] UIEffect 오버라이드 셰이더 '{UIEFFECT_OVERRIDE_SHADER}'를 " +
                                     "찾을 수 없습니다. UIEffect 자식에 마스킹이 적용되지 않습니다. " +
                                     "Shader/UIEffect/UIDefault_UIEffect.shader 임포트 상태를 확인하세요.");
                }
                else if (!shaders.Contains(uiEffectOverride))
                {
                    shaders.Add(uiEffectOverride);
                }
            }
            // 런타임이 Shader.Find 없이 사용할 수 있도록 직렬화 참조로 확정
            bool overrideChanged = settings.UIEffectOverrideShader != uiEffectOverride;
            settings.SetUIEffectOverrideShader(uiEffectOverride);

            // 프로젝트 내 추가 Optional Shader 자동 탐색
            // 이름에 "(SoftMaskLight)"가 포함된 셰이더 (커스텀 변형 셰이더 지원)
            string[] allShaderGuids = AssetDatabase.FindAssets("t:Shader");
            foreach (string guid in allShaderGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".shader")) continue;

                Shader s = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (s == null || shaders.Contains(s)) continue;
                if (!s.name.Contains("(SoftMaskLight)")) continue;
                if (!ShouldRegisterVariant(s.name)) continue;
                shaders.Add(s);
            }

            // 변경이 없으면 SetDirty/SaveAssets 스킵 (에디터 로드 비용/불필요한 에셋 dirty 방지)
            if (overrideChanged || !AreShaderListsEqual(settings.IncludedShaders, shaders))
            {
                settings.SetIncludedShaders(shaders.ToArray());
                AssetDatabase.SaveAssets();
            }
        }

        private static bool AreShaderListsEqual(Shader[] current, List<Shader> updated)
        {
            if (current == null || current.Length != updated.Count) return false;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] != updated[i]) return false;
            }
            return true;
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
