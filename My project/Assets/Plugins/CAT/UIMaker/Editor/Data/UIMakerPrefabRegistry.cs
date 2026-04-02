using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace CAT.Utility
{
    /// <summary>
    /// UICom 프리팹을 등록하고 교체/폴백을 관리하는 ScriptableObject 레지스트리.
    /// 싱글턴 패턴으로 접근하며, Inspector에서 프리팹 교체가 가능하다.
    /// </summary>
    [CreateAssetMenu(fileName = "UIMakerPrefabRegistry", menuName = "CAT/UIMaker Prefab Registry")]
    public class UIMakerPrefabRegistry : ScriptableObject
    {
        [SerializeField] List<PrefabRegistryEntry> _entries = new List<PrefabRegistryEntry>();

        // GUID → Entry 캐시
        Dictionary<string, PrefabRegistryEntry> _guidCache;
        // 논리 이름 → Entry 캐시
        Dictionary<string, PrefabRegistryEntry> _nameCache;

        /// <summary>
        /// 등록된 프리팹 엔트리 목록.
        /// </summary>
        public List<PrefabRegistryEntry> Entries => _entries;

        // ── 싱글턴 접근 ──────────────────────────────────────────────────

        static UIMakerPrefabRegistry _instance;

        /// <summary>
        /// 레지스트리 인스턴스를 반환한다. 에셋이 없으면 null.
        /// 구 경로(패키지 내부)에 있으면 새 경로(패키지 외부)로 자동 마이그레이션한다.
        /// </summary>
        public static UIMakerPrefabRegistry GetInstance()
        {
            if (_instance != null) return _instance;

            var guids = AssetDatabase.FindAssets("t:UIMakerPrefabRegistry");
            if (guids.Length == 0) return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);

            // 구 경로(패키지 내부) 감지 → 자동 마이그레이션
            if (path.Contains("Plugins/CAT/UIMaker/"))
            {
                _instance = MigrateFromOldPath(path);
                return _instance;
            }

            _instance = AssetDatabase.LoadAssetAtPath<UIMakerPrefabRegistry>(path);
            return _instance;
        }

        /// <summary>
        /// 구 경로의 레지스트리 + 스냅샷을 새 경로로 이동한다.
        /// </summary>
        static UIMakerPrefabRegistry MigrateFromOldPath(string oldAssetPath)
        {
            // 새 폴더 생성
            string newConfigPath = GetConfigFolderPath();
            if (!Directory.Exists(newConfigPath))
                Directory.CreateDirectory(newConfigPath);

            // 폴더가 AssetDatabase에 인식되도록 임포트
            AssetDatabase.ImportAsset(UserConfigFolder, ImportAssetOptions.ImportRecursive);

            string newAssetPath = UserConfigFolder + "/UIMakerPrefabRegistry.asset";

            // 레지스트리 에셋 이동
            string moveResult = AssetDatabase.MoveAsset(oldAssetPath, newAssetPath);
            if (!string.IsNullOrEmpty(moveResult))
            {
                Debug.LogWarning($"[UIMakerPrefabRegistry] 마이그레이션 실패 ({moveResult}), 기존 경로 사용");
                return AssetDatabase.LoadAssetAtPath<UIMakerPrefabRegistry>(oldAssetPath);
            }

            Debug.Log($"[UIMakerPrefabRegistry] 레지스트리를 패키지 외부로 마이그레이션: {newAssetPath}");

            // 구 스냅샷 폴더도 이동
            string oldDir = Path.GetDirectoryName(oldAssetPath).Replace('\\', '/');
            string oldSnapshotDir = oldDir + "/Snapshots";
            if (AssetDatabase.IsValidFolder(oldSnapshotDir))
            {
                string newSnapshotDir = UserConfigFolder + "/Snapshots";
                // 스냅샷 폴더 내 파일을 개별 이동
                var snapshotGuids = AssetDatabase.FindAssets("", new[] { oldSnapshotDir });
                if (snapshotGuids.Length > 0)
                {
                    if (!AssetDatabase.IsValidFolder(newSnapshotDir))
                        AssetDatabase.CreateFolder(UserConfigFolder, "Snapshots");

                    foreach (var sg in snapshotGuids)
                    {
                        string oldFilePath = AssetDatabase.GUIDToAssetPath(sg);
                        string fileName = Path.GetFileName(oldFilePath);
                        string newFilePath = newSnapshotDir + "/" + fileName;
                        AssetDatabase.MoveAsset(oldFilePath, newFilePath);
                    }
                }

                // 구 스냅샷 폴더 삭제
                AssetDatabase.DeleteAsset(oldSnapshotDir);
            }

            // 구 Config 폴더가 비었으면 삭제
            if (AssetDatabase.IsValidFolder(oldDir))
            {
                var remaining = AssetDatabase.FindAssets("", new[] { oldDir });
                if (remaining.Length == 0)
                    AssetDatabase.DeleteAsset(oldDir);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<UIMakerPrefabRegistry>(newAssetPath);
        }

        /// <summary>
        /// 레지스트리 인스턴스를 반환한다. 에셋이 없으면 자동 생성.
        /// </summary>
        public static UIMakerPrefabRegistry GetOrCreateInstance()
        {
            var inst = GetInstance();
            if (inst != null) return inst;

            // Config 폴더 경로 결정 (패키지 외부)
            string configPath = GetConfigFolderPath();
            if (!Directory.Exists(configPath))
                Directory.CreateDirectory(configPath);

            // 폴더가 AssetDatabase에 인식되도록 임포트
            AssetDatabase.ImportAsset(UserConfigFolder, ImportAssetOptions.ImportRecursive);

            string assetPath = UserConfigFolder + "/UIMakerPrefabRegistry.asset";

            inst = CreateInstance<UIMakerPrefabRegistry>();
            AssetDatabase.CreateAsset(inst, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);

            _instance = inst;
            Debug.Log($"[UIMakerPrefabRegistry] 레지스트리 에셋 생성됨: {assetPath}");
            return inst;
        }

        // ── 캐시 관리 ────────────────────────────────────────────────────

        /// <summary>
        /// Dictionary 캐시를 재구축한다.
        /// </summary>
        public void RebuildCache()
        {
            _guidCache = new Dictionary<string, PrefabRegistryEntry>();
            _nameCache = new Dictionary<string, PrefabRegistryEntry>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!string.IsNullOrEmpty(entry.originalGuid))
                    _guidCache[entry.originalGuid] = entry;
                if (!string.IsNullOrEmpty(entry.logicalName))
                    _nameCache[entry.logicalName] = entry;
            }
        }

        void EnsureCache()
        {
            if (_guidCache == null || _nameCache == null)
                RebuildCache();
        }

        // ── 프리팹 해결 ────────────────────────────────────────────────

        /// <summary>
        /// Override 프리팹만 조회한다 (Tier 1 — 최우선).
        /// 사용자가 교체한 프리팹이 있으면 반환, 없으면 null.
        /// </summary>
        public GameObject ResolveOverride(string guid, string name)
        {
            EnsureCache();

            // GUID로 엔트리 찾기 → Override만 반환
            if (!string.IsNullOrEmpty(guid) && _guidCache.TryGetValue(guid, out var entryByGuid))
            {
                if (entryByGuid.overridePrefab != null)
                    return entryByGuid.overridePrefab;
            }

            // 이름으로 엔트리 찾기 → Override만 반환
            if (!string.IsNullOrEmpty(name) && _nameCache.TryGetValue(name, out var entryByName))
            {
                if (entryByName.overridePrefab != null)
                    return entryByName.overridePrefab;
            }

            return null;
        }

        /// <summary>
        /// 이름으로 Default 프리팹을 조회한다 (Tier 3 — GUID 유실 시 폴백).
        /// </summary>
        public GameObject ResolveByName(string name)
        {
            EnsureCache();

            if (!string.IsNullOrEmpty(name) && _nameCache.TryGetValue(name, out var entry))
            {
                var resolved = entry.ResolvedPrefab;
                if (resolved != null)
                {
                    Debug.Log($"[UIMakerPrefabRegistry] 이름 매칭으로 프리팹 복원: {name}");
                    return resolved;
                }
            }

            return null;
        }

        // ── UICom 스캔 ──────────────────────────────────────────────────

        /// <summary>
        /// UICom 폴더를 스캔하여 엔트리를 갱신한다.
        /// 새 프리팹은 추가, 기존 엔트리의 defaultPrefab이 null이면 갱신.
        /// </summary>
        /// <returns>추가된 엔트리 수</returns>
        public int ScanUIComFolder()
        {
            string uicomPath = GetUIComFolderPath();
            if (string.IsNullOrEmpty(uicomPath) || !Directory.Exists(uicomPath))
            {
                Debug.LogWarning("[UIMakerPrefabRegistry] UICom 폴더를 찾을 수 없습니다.");
                return 0;
            }

            // UICom 폴더의 상대 경로 (Assets/...)
            string relPath = uicomPath.Substring(uicomPath.IndexOf("Assets", StringComparison.Ordinal));
            relPath = relPath.Replace('\\', '/');

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { relPath });
            int added = 0;

            // 기존 이름 세트 (중복 방지)
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _entries.Count; i++)
                existingNames.Add(_entries[i].logicalName);

            foreach (var prefabGuid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                string prefabName = Path.GetFileNameWithoutExtension(assetPath);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                if (existingNames.Contains(prefabName))
                {
                    // 기존 엔트리의 defaultPrefab이 null이면 갱신
                    for (int i = 0; i < _entries.Count; i++)
                    {
                        if (string.Equals(_entries[i].logicalName, prefabName, StringComparison.OrdinalIgnoreCase)
                            && _entries[i].defaultPrefab == null)
                        {
                            _entries[i].defaultPrefab = prefab;
                            _entries[i].originalGuid = prefabGuid;
                        }
                    }
                    continue;
                }

                _entries.Add(new PrefabRegistryEntry
                {
                    logicalName = prefabName,
                    originalGuid = prefabGuid,
                    defaultPrefab = prefab,
                    overridePrefab = null
                });
                existingNames.Add(prefabName);
                added++;
            }

            // 이름순 정렬
            _entries.Sort((a, b) => string.Compare(a.logicalName, b.logicalName, StringComparison.OrdinalIgnoreCase));

            // 프리팹 복제 → Override 자동 등록
            int cloned = ClonePrefabsToUserFolder();

            RebuildCache();
            EditorUtility.SetDirty(this);

            if (cloned > 0)
                Debug.Log($"[UIMakerPrefabRegistry] 프리팹 {cloned}개를 {UserPrefabFolder}에 복제하고 Override로 등록했습니다.");

            return added;
        }

        // ── 프리팹 복제 ────────────────────────────────────────────────

        const string UserPrefabFolder = "Assets/Prefabs/UI/UICom";

        /// <summary>
        /// 모든 등록 프리팹을 사용자 폴더에 복제하고 Override로 등록한다.
        /// 이미 존재하는 프리팹은 건너뛴다.
        /// </summary>
        /// <returns>새로 복제된 프리팹 수</returns>
        int ClonePrefabsToUserFolder()
        {
            // 대상 폴더 생성
            string fullDir = Path.Combine(Application.dataPath, UserPrefabFolder.Substring("Assets/".Length));
            if (!Directory.Exists(fullDir))
            {
                Directory.CreateDirectory(fullDir);
                AssetDatabase.ImportAsset(UserPrefabFolder, ImportAssetOptions.ImportRecursive);
            }

            int cloned = 0;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];

                // Override가 이미 있으면 건너뛰기
                if (entry.overridePrefab != null) continue;

                // 원본이 없으면 건너뛰기
                if (entry.defaultPrefab == null) continue;

                string destPath = $"{UserPrefabFolder}/{entry.logicalName}_new.prefab";

                // 대상 경로에 이미 파일이 있으면 기존 프리팹을 Override로 등록만 수행
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
                if (existing != null)
                {
                    entry.overridePrefab = existing;
                    cloned++;
                    continue;
                }

                // 프리팹 복제
                string sourcePath = AssetDatabase.GetAssetPath(entry.defaultPrefab);
                if (string.IsNullOrEmpty(sourcePath)) continue;

                if (!AssetDatabase.CopyAsset(sourcePath, destPath))
                {
                    Debug.LogWarning($"[UIMakerPrefabRegistry] 프리팹 복제 실패: {sourcePath} → {destPath}");
                    continue;
                }

                var clonedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(destPath);
                if (clonedPrefab != null)
                {
                    entry.overridePrefab = clonedPrefab;
                    cloned++;
                }
            }

            if (cloned > 0)
                AssetDatabase.SaveAssets();

            return cloned;
        }

        // ── 스냅샷 생성 ────────────────────────────────────────────────

        /// <summary>
        /// 모든 등록 프리팹의 JSON 스냅샷을 생성한다.
        /// </summary>
        /// <returns>생성된 스냅샷 수</returns>
        public int GenerateSnapshots()
        {
            string snapshotDir = GetSnapshotFolderPath();
            if (!Directory.Exists(snapshotDir))
                Directory.CreateDirectory(snapshotDir);

            int count = 0;
            foreach (var entry in _entries)
            {
                var prefab = entry.ResolvedPrefab;
                if (prefab == null) continue;

                // PrefabUtility로 프리팹 콘텐츠 로드
                string assetPath = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrEmpty(assetPath)) continue;

                var prefabContents = PrefabUtility.LoadPrefabContents(assetPath);
                if (prefabContents == null) continue;

                try
                {
                    // SerializedPropertyExtractor로 추출
                    var node = SerializedPropertyExtractor.ExtractGameObject(prefabContents, true);
                    var root = new PrefabJsonRoot
                    {
                        prefabName = entry.logicalName,
                        exportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        root = node
                    };

                    string json = root.ToJson();
                    string filePath = Path.Combine(snapshotDir, $"{entry.logicalName}.json");
                    File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabContents);
                }
            }

            // 스냅샷 폴더를 프로젝트에 임포트
            AssetDatabase.ImportAsset(UserConfigFolder + "/Snapshots", ImportAssetOptions.ImportRecursive);

            Debug.Log($"[UIMakerPrefabRegistry] 스냅샷 {count}개 생성 완료 ({snapshotDir})");
            return count;
        }

        /// <summary>
        /// 특정 프리팹의 스냅샷 파일 경로를 반환한다.
        /// </summary>
        public static string GetSnapshotPath(string logicalName)
        {
            return Path.Combine(GetSnapshotFolderPath(), $"{logicalName}.json");
        }

        /// <summary>
        /// 특정 프리팹의 스냅샷이 존재하는지 확인한다.
        /// </summary>
        public static bool HasSnapshot(string logicalName)
        {
            return File.Exists(GetSnapshotPath(logicalName));
        }

        // ── 경로 유틸리티 (캐시) ────────────────────────────────────────

        static string _cachedBasePath;
        static string _cachedConfigPath;
        static string _cachedSnapshotPath;
        static string _cachedUIComPath;

        /// <summary>
        /// UIMaker 모듈의 기본 경로를 반환한다. 결과를 캐싱한다.
        /// </summary>
        static string GetUIMakerBasePath()
        {
            if (!string.IsNullOrEmpty(_cachedBasePath))
                return _cachedBasePath;

            // UIDesignMaker.cs MonoScript로 위치 찾기
            var guids = AssetDatabase.FindAssets("t:MonoScript UIDesignMaker");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("UIMaker") &&
                    Path.GetFileNameWithoutExtension(path) == "UIDesignMaker")
                {
                    // Editor/ 폴더의 부모가 UIMaker 루트
                    string dir = Path.GetDirectoryName(path); // Editor/
                    dir = Path.GetDirectoryName(dir);         // UIMaker/
                    _cachedBasePath = Path.GetFullPath(dir);
                    return _cachedBasePath;
                }
            }

            // 폴백
            _cachedBasePath = Path.GetFullPath(Path.Combine(Application.dataPath, "Plugins/CAT/UIMaker"));
            return _cachedBasePath;
        }

        // Config와 Snapshots는 패키지 외부에 저장하여 패키지 업데이트 시 유실 방지
        const string UserConfigFolder = "Assets/Prefabs/UI/UIMakerConfig";

        static string GetConfigFolderPath()
        {
            if (string.IsNullOrEmpty(_cachedConfigPath))
                _cachedConfigPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                    UserConfigFolder.Substring("Assets/".Length)));
            return _cachedConfigPath;
        }

        static string GetSnapshotFolderPath()
        {
            if (string.IsNullOrEmpty(_cachedSnapshotPath))
                _cachedSnapshotPath = Path.Combine(GetConfigFolderPath(), "Snapshots");
            return _cachedSnapshotPath;
        }

        static string GetUIComFolderPath()
        {
            if (string.IsNullOrEmpty(_cachedUIComPath))
                _cachedUIComPath = Path.Combine(GetUIMakerBasePath(), "Prefabs", "UICom");
            return _cachedUIComPath;
        }
    }

    /// <summary>
    /// 프리팹 레지스트리 엔트리. 논리 이름 + 원본 GUID + 기본/교체 프리팹.
    /// </summary>
    [Serializable]
    public class PrefabRegistryEntry
    {
        [Tooltip("프리팹 논리 이름 (예: PopupButton)")]
        public string logicalName;

        [Tooltip("원본 프리팹의 Unity GUID")]
        public string originalGuid;

        [Tooltip("기본 프리팹 참조")]
        public GameObject defaultPrefab;

        [Tooltip("사용자 교체 프리팹 (null이면 기본 사용)")]
        public GameObject overridePrefab;

        /// <summary>
        /// Override가 있으면 Override, 없으면 Default를 반환한다.
        /// </summary>
        public GameObject ResolvedPrefab => overridePrefab != null ? overridePrefab : defaultPrefab;
    }
}
