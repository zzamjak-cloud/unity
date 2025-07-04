//#define FR2_DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using AssetState = vietlabs.fr2.FR2_Asset.AssetState;

namespace vietlabs.fr2
{
    internal class FR2_Cache : ScriptableObject
    {
        internal const string DEFAULT_CACHE_PATH = "Assets/FR2_Cache.asset";
        internal const string CACHE_VERSION = "2.5.12";

        internal static int cacheStamp;
        internal static Action onReady;

        internal static bool _triedToLoadCache;
        internal static FR2_Cache _cache;

        internal static string _cacheGUID;
        internal static string _cachePath;
        public static readonly int priority = 5;

        private static readonly HashSet<string> SPECIAL_USE_ASSETS = new HashSet<string>
        {
            "Assets/link.xml", // this file used to control build/link process do not remove
            "Assets/csc.rsp",
            "Assets/mcs.rsp",
            "Assets/GoogleService-Info.plist",
            "Assets/google-services.json"
        };

        private static readonly HashSet<string> SPECIAL_EXTENSIONS = new HashSet<string>
        {
            ".asmdef",
            ".cginc",
            ".cs",
            ".dll",
            ".mdb",
            ".pdb",
            ".rsp",
            ".md",
            ".winmd",
            ".xml",
            ".XML",
            ".tsv",
            ".csv",
            ".json",
            ".pdf",
            ".txt",
            ".giparams",
            ".wlt",
            ".preset",
            ".exr",
            ".aar",
            ".srcaar",
            ".pom",
            ".bin",
            ".html",
            ".chm",
            ".data",
            ".jsp",
            ".unitypackage"
        };

        [NonSerialized] internal static int delayCounter;

        [SerializeField] private bool _autoRefresh;
        [SerializeField] private string _curCacheVersion;

        [SerializeField] public List<FR2_Asset> AssetList;
        [SerializeField] internal FR2_Setting setting = new FR2_Setting();

        // ----------------------------------- INSTANCE -------------------------------------

        [SerializeField] public int timeStamp;
        [NonSerialized] internal Dictionary<string, FR2_Asset> AssetMap;

        // Track the current asset being processed
        [NonSerialized] internal string currentAssetName;

        private int frameSkipped;

        internal int GC_CountDown = 5;
        [NonSerialized] internal List<FR2_Asset> queueLoadContent;

        internal bool ready;
        [NonSerialized] internal int workCount;

        internal static string CacheGUID
        {
            get
            {
                if (!string.IsNullOrEmpty(_cacheGUID)) return _cacheGUID;

                if (_cache != null)
                {
                    _cachePath = AssetDatabase.GetAssetPath(_cache);
                    _cacheGUID = AssetDatabase.AssetPathToGUID(_cachePath);
                    return _cacheGUID;
                }

                return null;
            }
        }

        internal static string CachePath
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachePath)) return _cachePath;

                if (_cache != null)
                {
                    _cachePath = AssetDatabase.GetAssetPath(_cache);
                    return _cachePath;
                }

                return null;
            }
        }

        public bool HasChanged { get; private set; }
        internal static FR2_Cache Api
        {
            get
            {
                if (_cache != null) return _cache;
                if (!_triedToLoadCache) TryLoadCache();
                return _cache;
            }
        }

        internal static bool isReady
        {
            get
            {
                if (FR2_SettingExt.disable) return false;
                if (!_triedToLoadCache) TryLoadCache();
                return (_cache != null) && _cache.ready;
            }
        }

        internal static bool hasCache
        {
            get
            {
                if (!_triedToLoadCache) TryLoadCache();

                return _cache != null;
            }
        }

        internal float progress
        {
            get
            {
                int n = workCount - queueLoadContent.Count;
                return workCount == 0 ? 1 : n / (float)workCount;
            }
        }

        private void OnEnable()
        {
#if FR2_DEBUG
		Debug.Log("OnEnabled : " + _cache);
#endif
            if (_cache == null) _cache = this;
        }

        public static bool CheckSameVersion()
        {
            if (_cache == null) return false;
            return _cache._curCacheVersion == CACHE_VERSION;
        }

        public void MarkChanged()
        {
            HasChanged = true;
        }

        private static void FoundCache(bool savePrefs, bool writeFile)
        {
            //Debug.LogWarning("Found Cache!");

            _cachePath = AssetDatabase.GetAssetPath(_cache);
            _cache.ReadFromCache();
            
            _cacheGUID = AssetDatabase.AssetPathToGUID(_cachePath);
            if (savePrefs) EditorPrefs.SetString("fr2_cache.guid", _cacheGUID);
            if (writeFile) File.WriteAllText("Library/fr2_cache.guid", _cacheGUID);
            
            if (FR2_SettingExt.isAutoRefreshEnabled)
            {
                _cache.Check4Changes(false);    
            }
            else
            {
                _cache.ReadFromProject(false);
                _cache.ready = true;   
                _cache.Check4Usage();
            }
        }

        private static bool RestoreCacheFromGUID(string guid, bool savePrefs, bool writeFile)
        {
            if (string.IsNullOrEmpty(guid)) return false;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return false;

            return RestoreCacheFromPath(path, savePrefs, writeFile);
        }

        private static bool RestoreCacheFromPath(string path, bool savePrefs, bool writeFile)
        {
            if (string.IsNullOrEmpty(path)) return false;

            _cache = FR2_Unity.LoadAssetAtPath<FR2_Cache>(path);
            if (_cache != null) FoundCache(savePrefs, writeFile);

            return _cache != null;
        }

        private static void TryLoadCache()
        {
            _triedToLoadCache = true;

            if (RestoreCacheFromPath(DEFAULT_CACHE_PATH, false, false)) return;

            // Check EditorPrefs
            string pref = EditorPrefs.GetString("fr2_cache.guid", string.Empty);
            if (RestoreCacheFromGUID(pref, false, false)) return;

            // Read GUID from File
            if (File.Exists("Library/fr2_cache.guid"))
            {
                if (RestoreCacheFromGUID(File.ReadAllText("Library/fr2_cache.guid"), true, false))
                {
                    return;
                }
            }

            // Search whole project
            string[] allAssets = AssetDatabase.GetAllAssetPaths();
            for (var i = 0; i < allAssets.Length; i++)
            {
                if (allAssets[i].EndsWith("/FR2_Cache.asset", StringComparison.Ordinal))
                {
                    RestoreCacheFromPath(allAssets[i], true, true);
                    break;
                }
            }
        }

        internal static void DeleteCache()
        {
            if (_cache == null) return;

            try
            {
                _cache.AssetList.Clear();
                _cache.AssetMap.Clear();
                _cache.queueLoadContent.Clear();
                _cache = null;
                if (!string.IsNullOrEmpty(_cachePath)) AssetDatabase.DeleteAsset(_cachePath);
            } catch
            { // ignored
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        internal static void CreateCache()
        {
            _cache = CreateInstance<FR2_Cache>();
            _cache._curCacheVersion = CACHE_VERSION;
            string path = Application.dataPath + DEFAULT_CACHE_PATH
                .Substring(0, DEFAULT_CACHE_PATH.LastIndexOf('/') + 1).Replace("Assets", string.Empty);

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            AssetDatabase.CreateAsset(_cache, DEFAULT_CACHE_PATH);
            EditorUtility.SetDirty(_cache);

            FoundCache(true, true);

            // Delay the scan by one frame so UI can update first
            EditorApplication.delayCall += () =>
            {
                DelayCheck4Changes();
            };
        }

        internal static List<string> FindUsage(string[] listGUIDs)
        {
            if (!isReady) return null;

            List<FR2_Asset> refs = Api.FindAssets(listGUIDs, true);

            for (var i = 0; i < refs.Count; i++)
            {
                List<FR2_Asset> tmp = FR2_Asset.FindUsage(refs[i]);

                for (var j = 0; j < tmp.Count; j++)
                {
                    FR2_Asset itm = tmp[j];
                    if (refs.Contains(itm)) continue;

                    refs.Add(itm);
                }
            }

            return refs.Select(item => item.guid).ToList();
        }

        internal void ReadFromCache()
        {
            if (FR2_SettingExt.disable)
            {
                Debug.LogWarning("Something wrong??? FR2 is disabled!");
            }

            if (AssetList == null) AssetList = new List<FR2_Asset>();

            FR2_Unity.Clear(ref queueLoadContent);
            FR2_Unity.Clear(ref AssetMap);

            for (var i = 0; i < AssetList.Count; i++)
            {
                FR2_Asset item = AssetList[i];
                item.state = AssetState.CACHE;

                string path = AssetDatabase.GUIDToAssetPath(item.guid);
                if (string.IsNullOrEmpty(path))
                {
                    item.type = FR2_Asset.AssetType.UNKNOWN; // to make sure if GUIDs being reused for a different kind of asset
                    item.state = AssetState.MISSING;
                    AssetMap.Add(item.guid, item);
                    continue;
                }

                if (AssetMap.ContainsKey(item.guid))
                {
#if FR2_DEBUG
					Debug.LogWarning("Something wrong, cache found twice <" + item.guid + ">");
#endif
                    continue;
                }

                AssetMap.Add(item.guid, item);
            }
        }

        internal void ReadFromProject(bool force)
        {
            if (AssetMap == null || AssetMap.Count == 0) ReadFromCache();
            foreach (string b in FR2_Asset.BUILT_IN_ASSETS)
            {
                if (AssetMap.ContainsKey(b)) continue;
                var asset = new FR2_Asset(b);
                AssetMap.Add(b, asset);
                AssetList.Add(asset);
            }

            string[] paths = AssetDatabase.GetAllAssetPaths();
            cacheStamp++;
            workCount = 0;
            if (queueLoadContent != null) queueLoadContent.Clear();

            // Check for new assets
            foreach (string p in paths)
            {
                bool isValid = FR2_Unity.StringStartsWith(p, "Assets/", "Packages/", "Library/", "ProjectSettings/");

                if (!isValid)
                {
#if FR2_DEBUG
					Debug.LogWarning("Ignore asset: " + p);
#endif
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(p);
                if (!FR2_Asset.IsValidGUID(guid)) continue;

                if (!AssetMap.TryGetValue(guid, out FR2_Asset asset))
                {
                    AddAsset(guid);
                } else
                {
                    asset.refreshStamp = cacheStamp; // mark this asset so it won't be deleted
                    if (!asset.isDirty && !force) continue;
                    if (force) asset.MarkAsDirty(true, true);
                    if (FR2_SettingExt.isAutoRefreshEnabled)
                    {
                        workCount++;
                        queueLoadContent.Add(asset);    
                    }
                }
            }

            // Check for deleted assets
            for (int i = AssetList.Count - 1; i >= 0; i--)
            {
                if (AssetList[i].refreshStamp != cacheStamp) RemoveAsset(AssetList[i]);
            }
        }

        internal static void DelayCheck4Changes()
        {
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
        }

        private static void Check()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || FR2_SettingExt.disable)
            {
                delayCounter = 100;
                return;
            }

            if (Api == null) return;
            if (delayCounter-- > 0) return;
            EditorApplication.update -= Check;
            Api.Check4Changes(false);
        }

        internal void Check4Changes(bool force)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || FR2_SettingExt.disable)
            {
                DelayCheck4Changes();
                return;
            }
            
            if (!force && !FR2_SettingExt.isAutoRefreshEnabled)
            {
                // Debug.Log("Skip refresh!");
                return;
            }
            
            ready = false;
            ReadFromProject(force);

#if FR2_DEBUG
		Debug.Log("After checking :: WorkCount :: " + workCount + ":" + AssetMap.Count + ":" + AssetList.Count);
#endif
            Check4Work();
        }

        internal void RefreshAsset(string guid, bool force)
        {

            if (!AssetMap.TryGetValue(guid, out FR2_Asset asset)) return;
            RefreshAsset(asset, force);
        }

        internal void RefreshSelection()
        {
            string[] list = FR2_Unity.Selection_AssetGUIDs;
            for (var i = 0; i < list.Length; i++)
            {
                RefreshAsset(list[i], true);
            }

            Check4Work();
        }

        internal void RefreshAsset(FR2_Asset asset, bool force)
        {
            asset.MarkAsDirty(true, force);
            DelayCheck4Changes();
        }

        internal void AddAsset(string guid)
        {
            if (AssetMap.ContainsKey(guid))
            {
                Debug.LogWarning("guid already exist <" + guid + ">");
                return;
            }

            var asset = new FR2_Asset(guid);
            asset.LoadPathInfo();
            asset.refreshStamp = cacheStamp;
            AssetMap.Add(guid, asset);

            // Do not load content for FR2_Cache asset
            if (guid == CacheGUID) return;

            if (!asset.IsCriticalAsset()) return;

            workCount++;
            AssetList.Add(asset);
            queueLoadContent.Add(asset);
        }

        internal void RemoveAsset(string guid)
        {
            if (!AssetMap.ContainsKey(guid)) return;

            RemoveAsset(AssetMap[guid]);
        }

        internal void RemoveAsset(FR2_Asset asset)
        {
            AssetList.Remove(asset);

            // Deleted Asset : still in the map but not in the AssetList
            asset.state = AssetState.MISSING;
        }

        internal void Check4Usage()
        {
#if FR2_DEBUG
			Debug.Log("Check 4 Usage");
#endif

            foreach (FR2_Asset item in AssetList)
            {
                if (item.IsMissing) continue;
                FR2_Unity.Clear(ref item.UsedByMap);
            }

            foreach (FR2_Asset item in AssetList)
            {
                if (item.IsMissing) continue;
                AsyncUsedBy(item);
            }

            workCount = 0;
            ready = true;
        }

        internal void Check4Work()
        {
            if (workCount == 0)
            {
                Check4Usage();
                return;
            }

            ready = false;
            EditorApplication.update -= AsyncProcess;
            EditorApplication.update += AsyncProcess;
            FR2_Asset.ClearLog();
        }

        internal void AsyncProcess()
        {
            if (this == null) return;
            if (FR2_SettingExt.disable) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (frameSkipped++ < 10 - 2 * priority) return;

            frameSkipped = 0;
            float t = Time.realtimeSinceStartup;

#if FR2_DEBUG
			Debug.Log(Mathf.Round(t) + " : " + progress*workCount + "/" + workCount + ":" + isReady + " ::: " + queueLoadContent.Count);
#endif

            if (!AsyncWork(queueLoadContent, AsyncLoadContent, t)) return;
            FR2_Asset.WriteTotalScanTime();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            EditorApplication.update -= AsyncProcess;
            Check4Usage();
        }
        internal bool AsyncWork<T>(List<T> arr, Action<int, T> action, float t)
        {
            float FRAME_DURATION = 1 / 60f; //prevent zero

            int c = arr.Count;
            while (c-- > 0)
            {
                T last = arr[c];
                arr.RemoveAt(c);
                action(c, last);

                float dt = Time.realtimeSinceStartup - t - FRAME_DURATION;
                if (dt >= 0) return false;
            }

            if (GC_CountDown-- <= 0) // GC every 5 frames
            {
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC_CountDown = 5;
            }

            return true;
        }

        internal void AsyncLoadContent(int idx, FR2_Asset asset)
        {
            // Update the current asset name
            currentAssetName = asset.assetPath;

            if (asset.fileInfoDirty) asset.LoadFileInfo();
            if (asset.fileContentDirty) asset.LoadContentFast();
        }

        internal void AsyncUsedBy(FR2_Asset asset)
        {
            if (AssetMap == null) Check4Changes(false);

            if (asset.IsFolder) return;

#if FR2_DEBUG
			Debug.Log("Async UsedBy: " + asset.assetPath);
#endif

            foreach (KeyValuePair<string, HashSet<long>> item in asset.UseGUIDs)
            {
                if (!AssetMap.TryGetValue(item.Key, out FR2_Asset tAsset)) continue;
                if (tAsset == null || tAsset.UsedByMap == null) continue;

                if (!tAsset.UsedByMap.ContainsKey(asset.guid)) tAsset.AddUsedBy(asset.guid, asset);
            }
        }


        //---------------------------- Dependencies -----------------------------

        internal FR2_Asset Get(string guid, bool isForce = false)
        {
            return AssetMap.ContainsKey(guid) ? AssetMap[guid] : null;
        }

        internal List<FR2_Asset> FindAssetsOfType(FR2_Asset.AssetType type)
        {
            var result = new List<FR2_Asset>();
            foreach (KeyValuePair<string, FR2_Asset> item in AssetMap)
            {
                if (item.Value.type != type) continue;

                result.Add(item.Value);
            }

            return result;
        }
        internal FR2_Asset FindAsset(string guid, string fileId)
        {
            if (AssetMap == null) Check4Changes(false);
            if (!isReady)
            {
#if FR2_DEBUG
			Debug.LogWarning("Cache not ready !");
#endif
                return null;
            }

            if (string.IsNullOrEmpty(guid)) return null;

            //for (var i = 0; i < guids.Length; i++)
            {
                //string guid = guids[i];
                if (!AssetMap.TryGetValue(guid, out FR2_Asset asset)) return null;

                if (asset.IsMissing) return null;

                if (asset.IsFolder) return null;
                return asset;
            }
        }
        internal List<FR2_Asset> FindAssets(string[] guids, bool scanFolder)
        {
            if (AssetMap == null) Check4Changes(false);

            var result = new List<FR2_Asset>();

            if (!isReady)
            {
#if FR2_DEBUG
			Debug.LogWarning("Cache not ready !");
#endif
                return result;
            }

            var folderList = new List<FR2_Asset>();

            if (guids.Length == 0) return result;

            for (var i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                FR2_Asset asset;
                if (!AssetMap.TryGetValue(guid, out asset)) continue;

                if (asset.IsMissing) continue;

                if (asset.IsFolder)
                {
                    if (!folderList.Contains(asset)) folderList.Add(asset);
                } else
                {
                    result.Add(asset);
                }
            }

            if (!scanFolder || folderList.Count == 0) return result;

            int count = folderList.Count;
            for (var i = 0; i < count; i++)
            {
                FR2_Asset item = folderList[i];

                // for (var j = 0; j < item.UseGUIDs.Count; j++)
                // {
                //     FR2_Asset a;
                //     if (!AssetMap.TryGetValue(item.UseGUIDs[j], out a)) continue;
                foreach (KeyValuePair<string, HashSet<long>> useM in item.UseGUIDs)
                {
                    FR2_Asset a;
                    if (!AssetMap.TryGetValue(useM.Key, out a)) continue;

                    if (a.IsMissing) continue;

                    if (a.IsFolder)
                    {
                        if (!folderList.Contains(a))
                        {
                            folderList.Add(a);
                            count++;
                        }
                    } else
                    {
                        result.Add(a);
                    }
                }
            }

            return result;
        }

        //---------------------------- Dependencies -----------------------------

        internal List<List<string>> ScanSimilar(Action IgnoreWhenScan, Action IgnoreFolderWhenScan)
        {
            if (AssetMap == null) Check4Changes(true);

            var dict = new Dictionary<string, List<FR2_Asset>>();
            foreach (KeyValuePair<string, FR2_Asset> item in AssetMap)
            {
                if (item.Value == null) continue;
                if (item.Value.IsMissing || item.Value.IsFolder) continue;
                if (item.Value.inPlugins) continue;
                if (item.Value.inEditor) continue;
                if (item.Value.IsExcluded) continue;
                if (!item.Value.assetPath.StartsWith("Assets/")) continue;
                if (FR2_Setting.IsTypeExcluded(FR2_AssetGroupDrawer.GetIndex(item.Value.extension)))
                {
                    if (IgnoreWhenScan != null) IgnoreWhenScan();
                    continue;
                }

                string hash = item.Value.fileInfoHash;
                if (string.IsNullOrEmpty(hash))
                {
#if FR2_DEBUG
                    Debug.LogWarning("Hash can not be null! ");
#endif
                    continue;
                }

                if (!dict.TryGetValue(hash, out List<FR2_Asset> list))
                {
                    list = new List<FR2_Asset>();
                    dict.Add(hash, list);
                }

                list.Add(item.Value);
            }

            return dict.Values
                .Where(item => item.Count > 1)
                .OrderByDescending(item => item[0].fileSize)
                .Select(item => item.Select(asset => asset.assetPath).ToList())
                .ToList();
        }

        internal List<FR2_Asset> ScanUnused(bool recursive = true)
        {
            if (AssetMap == null) Check4Changes(false);

            // Get Addressable assets
            HashSet<string> addressable = FR2_Addressable.isOk ? FR2_Addressable.GetAddresses()
                .SelectMany(item => item.Value.assetGUIDs.Union(item.Value.childGUIDs))
                .ToHashSet() : new HashSet<string>();

            var result = new List<FR2_Asset>();
            var unusedAssets = new HashSet<string>();
            
            // First pass: find directly unused assets (level 1)
            foreach (KeyValuePair<string, FR2_Asset> item in AssetMap)
            {
                FR2_Asset v = item.Value;
                if (v.IsMissing || v.inEditor || v.IsScript || v.inResources || v.inPlugins || v.inStreamingAsset || v.IsFolder) continue;

                if (!v.assetPath.StartsWith("Assets/")) continue; // ignore built-in / packages assets
                if (v.forcedIncludedInBuild) continue; // ignore assets that are forced to be included in build
                if (v.assetName == "LICENSE") continue; // ignore license files

                // --- Ignore assets in ignored folders or exact ignored paths ---
                bool isIgnored = vietlabs.fr2.FR2_Setting.IgnoreAsset.Any(ignore =>
                    v.assetPath.Equals(ignore, StringComparison.OrdinalIgnoreCase) ||
                    (v.assetPath.StartsWith(ignore + "/", StringComparison.OrdinalIgnoreCase))
                );
                if (isIgnored) continue;

                // --- Ignore assets with unknown or no extension ---
                string ext = System.IO.Path.GetExtension(v.assetPath);
                Type assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(v.assetPath);
                if (string.IsNullOrEmpty(ext) || assetType == typeof(DefaultAsset))
                {
                    continue;
                }

                if (SPECIAL_USE_ASSETS.Contains(v.assetPath)) continue; // ignore assets with special use (can not remove)
                if (SPECIAL_EXTENSIONS.Contains(v.extension)) continue;

                if (v.type == FR2_Asset.AssetType.DLL) continue;
                if (v.type == FR2_Asset.AssetType.SCRIPT) continue;
                if (v.type == FR2_Asset.AssetType.UNKNOWN) continue;
                if (addressable.Contains(v.guid)) continue;

                // special handler for .spriteatlas
                if (v.extension == ".spriteatlas")
                {
                    var isInUsed = false;
                    List<string> allSprites = v.UseGUIDs.Keys.ToList();
                    foreach (string spriteGUID in allSprites)
                    {
                        FR2_Asset asset = Api.Get(spriteGUID);
                        if (asset.UsedByMap.Count <= 1) continue; // only use by this atlas

                        isInUsed = true;
                        break; // this one is used by other assets
                    }

                    if (isInUsed) continue;
                }

                if (v.IsExcluded)
                {
                    // Debug.Log($"Excluded: {v.assetPath}");
                    continue;
                }

                if (!string.IsNullOrEmpty(v.AtlasName)) continue;
                if (!string.IsNullOrEmpty(v.AssetBundleName)) continue;
                if (!string.IsNullOrEmpty(v.AddressableName)) continue;

                if (v.UsedByMap.Count == 0) //&& !FR2_Asset.IGNORE_UNUSED_GUIDS.Contains(v.guid)
                {
                    result.Add(v);
                    unusedAssets.Add(v.guid);
                }
            }
            
            // If not recursive, return the level 1 results
            if (!recursive)
            {
                result.Sort((item1, item2) => item1.extension == item2.extension
                    ? string.Compare(item1.assetPath, item2.assetPath, StringComparison.Ordinal)
                    : string.Compare(item1.extension, item2.extension, StringComparison.Ordinal));
                    
                return result;
            }
            
            // Recursive scan for higher level unused assets
            bool foundNewUnused = true;
            while (foundNewUnused)
            {
                foundNewUnused = false;
                var newUnusedAssets = new HashSet<string>();
                
                foreach (KeyValuePair<string, FR2_Asset> item in AssetMap)
                {
                    FR2_Asset v = item.Value;
                    
                    // Skip if already in result or doesn't meet basic criteria
                    if (unusedAssets.Contains(v.guid)) continue;
                    if (v.IsMissing || v.inEditor || v.IsScript || v.inResources || v.inPlugins || v.inStreamingAsset || v.IsFolder) continue;
                    if (!v.assetPath.StartsWith("Assets/")) continue;
                    if (v.forcedIncludedInBuild) continue;
                    if (v.assetName == "LICENSE") continue;
                    // --- Ignore assets in ignored folders or exact ignored paths ---
                    bool isIgnored = vietlabs.fr2.FR2_Setting.IgnoreAsset.Any(ignore =>
                        v.assetPath.Equals(ignore, StringComparison.OrdinalIgnoreCase) ||
                        (v.assetPath.StartsWith(ignore + "/", StringComparison.OrdinalIgnoreCase))
                    );
                    if (isIgnored) continue;
                    // --- Ignore assets with unknown or no extension ---
                    string ext = System.IO.Path.GetExtension(v.assetPath);
                    Type assetType = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(v.assetPath);
                    if (string.IsNullOrEmpty(ext) || assetType == typeof(DefaultAsset))
                    {
                        continue;
                    }
                    if (SPECIAL_USE_ASSETS.Contains(v.assetPath)) continue;
                    if (SPECIAL_EXTENSIONS.Contains(v.extension)) continue;
                    if (v.type == FR2_Asset.AssetType.DLL) continue;
                    if (v.type == FR2_Asset.AssetType.SCRIPT) continue;
                    if (v.type == FR2_Asset.AssetType.UNKNOWN) continue;
                    if (addressable.Contains(v.guid)) continue;
                    if (v.IsExcluded) continue;
                    if (!string.IsNullOrEmpty(v.AtlasName)) continue;
                    if (!string.IsNullOrEmpty(v.AssetBundleName)) continue;
                    if (!string.IsNullOrEmpty(v.AddressableName)) continue;
                    // Check if this asset is only used by already identified unused assets
                    if (v.UsedByMap.Count > 0)
                    {
                        bool onlyUsedByUnusedAssets = true;
                        foreach (var usedBy in v.UsedByMap)
                        {
                            if (!unusedAssets.Contains(usedBy.Key))
                            {
                                onlyUsedByUnusedAssets = false;
                                break;
                            }
                        }
                        
                        if (onlyUsedByUnusedAssets)
                        {
                            result.Add(v);
                            newUnusedAssets.Add(v.guid);
                            foundNewUnused = true;
                        }
                    }
                }
                
                // Add newly found unused assets to the master list
                unusedAssets.UnionWith(newUnusedAssets);
            }

            result.Sort((item1, item2) => item1.extension == item2.extension
                ? string.Compare(item1.assetPath, item2.assetPath, StringComparison.Ordinal)
                : string.Compare(item1.extension, item2.extension, StringComparison.Ordinal));

            return result;
        }
    }

    [CustomEditor(typeof(FR2_Cache))]
    internal class FR2_CacheEditor : Editor
    {
        private static string inspectGUID;
        private static int index;

        public override void OnInspectorGUI()
        {
            var c = (FR2_Cache)target;

            GUILayout.Label("Total : " + c.AssetList.Count);

            // FR2_Cache.DrawPriorityGUI();

            UnityObject s = Selection.activeObject;
            if (s == null) return;

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(s));

            if (inspectGUID != guid)
            {
                inspectGUID = guid;
                index = c.AssetList.FindIndex(item => item.guid == guid);
            }

            if (index != -1)
            {
                if (index >= c.AssetList.Count) index = 0;

                serializedObject.Update();
                SerializedProperty prop = serializedObject.FindProperty("AssetList").GetArrayElementAtIndex(index);
                EditorGUILayout.PropertyField(prop, true);
            }
        }
    }
}
