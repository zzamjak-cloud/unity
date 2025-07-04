﻿//#define FR2_DEBUG_BRACE_LEVEL
//#define FR2_DEBUG_SYMBOL
//#define FR2_DEBUG


#if FR2_ADDRESSABLE
using UnityEditor.AddressableAssets;
using UnityEngine.AddressableAssets;
#endif

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{

    [Serializable]
    internal partial class FR2_Asset
    {

        public const int MIN_FILE_SIZE_2LOG = 1024 * 1024; // 1 MB
        internal static bool shouldWriteImportLog;


        // ------------------------------ CONSTANTS ---------------------------

        private static readonly HashSet<string> SCRIPT_EXTENSIONS = new HashSet<string>
        {
            ".cs", ".js", ".boo", ".h", ".java", ".cpp", ".m", ".mm", ".shader", ".hlsl", ".cginclude", ".shadersubgraph"
        };

        private static readonly HashSet<string> REFERENCABLE_EXTENSIONS = new HashSet<string>
        {
            ".anim", ".controller", ".mat", ".unity", ".guiskin", ".prefab",
            ".overridecontroller", ".mask", ".rendertexture", ".cubemap", ".flare", ".playable",
            ".mat", ".physicsmaterial", ".fontsettings", ".asset", ".prefs", ".spriteatlas",
            ".terrainlayer", ".asmdef", ".preset", ".spriteLib"
        };
        private static readonly HashSet<string> REFERENCABLE_JSON = new HashSet<string>
        {
            ".shadergraph", ".shadersubgraph"
        };
        private static readonly HashSet<string> UI_TOOLKIT = new HashSet<string>
        {
            ".uss", ".uxml", ".tss"
        };

        private static readonly HashSet<string> REFERENCABLE_META = new HashSet<string>
        {
            ".texture2darray"
        };

        /// <summary>
        ///     Extensions that rarely contain references to other assets and can be
        ///     ignored when deciding if an asset is "critical".
        /// </summary>
        private static readonly HashSet<string> NON_REFERENCE_EXTENSIONS =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".bmp", ".exr", ".gif",
                ".ttf", ".otf",
                ".wav", ".mp3", ".ogg", ".aif", ".aiff",
                ".txt", ".json", ".xml", ".csv",
                ".html", ".htm", ".yaml", ".md"
            };

        internal static readonly HashSet<string> BUILT_IN_ASSETS = new HashSet<string>
        {
            "0000000000000000f000000000000000",
            "0000000000000000e000000000000000",
            "0000000000000000d000000000000000"
        };

        private static readonly Dictionary<long, Type> HashClasses = new Dictionary<long, Type>();
        internal static Dictionary<string, GUIContent> cacheImage = new Dictionary<string, GUIContent>();

        public static float ignoreTS;
        private static string _logPath;


        private static int binaryLoaded;
        private static DateTime scanStartTime;

        // ----------------------------- DRAW  ---------------------------------------

        [SerializeField] public string guid;

        // Need to read FileInfo: soft-cache (always re-read when needed)
        [FormerlySerializedAs("type2")] [SerializeField] public AssetType type;
        [SerializeField] private string m_fileInfoHash;
        [SerializeField] private string m_assetbundle;
        [SerializeField] private string m_addressable;

        [SerializeField] private string m_atlas;
        [SerializeField] private long m_fileSize;

        [SerializeField] private int m_assetChangeTS; // Realtime when asset changed (trigger by import asset operation)
        [SerializeField] private int m_fileInfoReadTS; // Realtime when asset being read

        [SerializeField] private int m_fileWriteTS; // file's lastModification (file content + meta)
        [SerializeField] private int m_cachefileWriteTS; // file's lastModification at the time the content being read
        [SerializeField] private bool m_forceIncludeInBuild;

        [SerializeField] internal int refreshStamp; // use to check if asset has been deleted (refreshStamp not updated)
        [SerializeField] internal List<Classes> UseGUIDsList = new List<Classes>();

        private bool _isExcluded;
        private Dictionary<string, HashSet<long>> _UseGUIDs;
        private float excludeTS;


        // ----------------------------- DRAW  ---------------------------------------
        [NonSerialized] private GUIContent fileSizeText;
        internal HashSet<long> HashUsedByClassesIds = new HashSet<long>();
        [NonSerialized] private string m_assetFolder;
        [NonSerialized] private string m_assetName;
        [NonSerialized] private string m_assetPath;
        [NonSerialized] private string m_extension;
        [NonSerialized] private bool m_inEditor;
        [NonSerialized] private bool m_inPackage;
        [NonSerialized] private bool m_inPlugins;
        [NonSerialized] private bool m_inResources;
        [NonSerialized] private bool m_inStreamingAsset;
        // [NonSerialized] private bool m_isAssetFile;

        // easy to recalculate: will not cache
        [NonSerialized] private bool m_pathLoaded;


        // Do not cache
        [NonSerialized] internal AssetState state;
        internal Dictionary<string, FR2_Asset> UsedByMap = new Dictionary<string, FR2_Asset>();

        public FR2_Asset(string guid)
        {
            this.guid = guid;
            type = BUILT_IN_ASSETS.Contains(guid) ? AssetType.BUILT_IN : AssetType.UNKNOWN;
        }
        private static string logPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_logPath)) return _logPath;
                _logPath = Path.Combine(Application.dataPath, "../fr2-import.log");
                return _logPath;
            }
        }

        public bool forcedIncludedInBuild => m_forceIncludeInBuild;
        public string assetName => LoadPathInfo().m_assetName;
        public string assetPath
        {
            get
            {
                if (!string.IsNullOrEmpty(m_assetPath)) return m_assetPath;
                m_assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(m_assetPath)) state = AssetState.MISSING;
                return m_assetPath;
            }
        }

        public string parentFolderPath => LoadPathInfo().m_assetFolder;
        public string assetFolder => LoadPathInfo().m_assetFolder;
        public string extension => LoadPathInfo().m_extension;
        public bool inEditor => LoadPathInfo().m_inEditor;
        public bool inPlugins => LoadPathInfo().m_inPlugins;
        public bool inPackages => LoadPathInfo().m_inPackage;
        public bool inResources => LoadPathInfo().m_inResources;
        public bool inStreamingAsset => LoadPathInfo().m_inStreamingAsset;

        // ----------------------- TYPE INFO ------------------------

        internal bool IsFolder => type == AssetType.FOLDER;
        internal bool IsScript => type == AssetType.SCRIPT;
        internal bool IsMissing => (state == AssetState.MISSING) && !isBuiltIn;

        internal bool IsReferencable => type == AssetType.REFERENCABLE ||
            type == AssetType.SCENE;

        internal bool IsBinaryAsset => type == AssetType.BINARY_ASSET ||
            type == AssetType.MODEL ||
            type == AssetType.TERRAIN ||
            type == AssetType.LIGHTING_DATA;

        // ----------------------- PATH INFO ------------------------

        public bool fileInfoDirty => type == AssetType.UNKNOWN || m_fileInfoReadTS <= m_assetChangeTS;
        public bool fileContentDirty => (m_fileWriteTS != m_cachefileWriteTS) && !isBuiltIn;
        public bool isDirty => (fileInfoDirty || fileContentDirty) && !isBuiltIn;
        public bool isBuiltIn => type == AssetType.BUILT_IN;

        internal string fileInfoHash => LoadFileInfo().m_fileInfoHash;
        internal long fileSize => LoadFileInfo().m_fileSize;
        public string AtlasName => LoadFileInfo().m_atlas;
        public string AssetBundleName => LoadFileInfo().m_assetbundle;
        public string AddressableName => LoadFileInfo().m_addressable;
        
        public Dictionary<string, HashSet<long>> UseGUIDs
        {
            get
            {
                if (_UseGUIDs != null) return _UseGUIDs;

                _UseGUIDs = new Dictionary<string, HashSet<long>>(UseGUIDsList.Count);
                for (var i = 0; i < UseGUIDsList.Count; i++)
                {
                    string guid = UseGUIDsList[i].guid;
                    if (_UseGUIDs.ContainsKey(guid))
                    {
                        for (var j = 0; j < UseGUIDsList[i].ids.Count; j++)
                        {
                            long val = UseGUIDsList[i].ids[j];
                            if (_UseGUIDs[guid].Contains(val)) continue;

                            _UseGUIDs[guid].Add(UseGUIDsList[i].ids[j]);
                        }
                    } else
                    {
                        _UseGUIDs.Add(guid, new HashSet<long>(UseGUIDsList[i].ids));
                    }
                }

                return _UseGUIDs;
            }
        }

        // ------------------------------- GETTERS -----------------------------
        // internal bool IsLightMap => assetName.StartsWith("Lightmap-", StringComparison.InvariantCulture)
        //     && (assetName.EndsWith("_comp_dir.png", StringComparison.InvariantCulture) ||
        //         assetName.EndsWith("_comp_light.exr", StringComparison.InvariantCulture));

        internal bool IsExcluded
        {
            get
            {
                if (excludeTS >= ignoreTS) return _isExcluded;

                excludeTS = ignoreTS;
                _isExcluded = false;

                HashSet<string> h = FR2_Setting.IgnoreAsset;
                foreach (string item in h)
                {
                    if (!m_assetPath.StartsWith(item, false, CultureInfo.InvariantCulture)) continue;
                    _isExcluded = true;
                    return true;
                }

                return false;
            }
        }


        public string DebugUseGUID()
        {
            return $"{guid} : {assetPath}\n{string.Join("\n", UseGUIDsList.Select(item => item.guid).ToArray())}";
        }

        internal bool IsCriticalAsset()
        {
            // Fast checks on asset type
            switch (type)
            {
            case AssetType.REFERENCABLE:
            case AssetType.SCENE:
            case AssetType.BINARY_ASSET:
            case AssetType.MODEL:
            case AssetType.TERRAIN:
            case AssetType.LIGHTING_DATA:
                return true;

            case AssetType.SCRIPT:
            case AssetType.DLL:
            case AssetType.NON_READABLE:
                return false;
            }

            // If extension not yet loaded, we cannot decide – treat as non‑critical
            if (string.IsNullOrEmpty(m_extension)) return false;
            
            // Assets whose extension is known to be "reference‑free" are non‑critical.
            // Everything else is potentially critical.
            return !NON_REFERENCE_EXTENSIONS.Contains(m_extension);
        }

        // ----------------------- PATH INFO ------------------------
        public FR2_Asset LoadPathInfo()
        {
            if (m_pathLoaded) return this;
            m_pathLoaded = true;

            m_assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                state = AssetState.MISSING;
                return this;
            }

#if FR2_DEBUG
			Debug.LogWarning("Refreshing ... " + loadInfoTS + ":" + AssetDatabase.GUIDToAssetPath(guid));
			if (!m_assetPath.StartsWith("Assets"))
			{
				Debug.Log("LoadAssetInfo: " + m_assetPath);
			}
#endif
            FR2_Unity.SplitPath(m_assetPath, out m_assetName, out m_extension, out m_assetFolder);

            if (m_assetFolder.StartsWith("Assets/"))
            {
                m_assetFolder = m_assetFolder.Substring(7);
            } else if (!FR2_Unity.StringStartsWith(m_assetPath, "Packages/", "Project Settings/", "Library/")) m_assetFolder = "built-in/";

            m_inEditor = m_assetPath.Contains("/Editor/") || m_assetPath.Contains("/Editor Default Resources/");
            m_inResources = m_assetPath.Contains("/Resources/");
            m_inStreamingAsset = m_assetPath.Contains("/StreamingAssets/");
            m_inPlugins = m_assetPath.Contains("/Plugins/");
            m_inPackage = m_assetPath.StartsWith("Packages/");
            // m_isAssetFile = m_assetPath.EndsWith(".asset", StringComparison.Ordinal);
            return this;
        }

        private bool ExistOnDisk()
        {
            if (isBuiltIn) return true;
            if (IsMissing) return false; // asset not exist - no need to check FileSystem!
            if (type == AssetType.FOLDER || type == AssetType.UNKNOWN)
            {
                if (Directory.Exists(m_assetPath))
                {
                    if (type == AssetType.UNKNOWN) type = AssetType.FOLDER;
                    return true;
                }

                if (type == AssetType.FOLDER) return false;
            }

            // must be file here
            if (!File.Exists(m_assetPath)) return false;

            if (type == AssetType.UNKNOWN) GuessAssetType();
            return true;
        }

        internal FR2_Asset LoadFileInfo()
        {
            if (!fileInfoDirty) return this;
            if (string.IsNullOrEmpty(m_assetPath)) LoadPathInfo(); // always reload Path Info

            //Debug.Log("--> Read: " + assetPath + " --> " + m_fileInfoReadTS + "<" + m_assetChangeTS);
            m_fileInfoReadTS = FR2_Unity.Epoch(DateTime.Now);
            if (isBuiltIn) return this;
            if (IsMissing)
            {
                // Debug.LogWarning("Should never be here! - missing files can not trigger LoadFileInfo()");
                return this;
            }

            if (!ExistOnDisk())
            {
                state = AssetState.MISSING;
                return this;
            }

            if (type == AssetType.FOLDER) return this; // nothing to read

            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(m_assetPath);
            if (assetType == typeof(FR2_Cache)) return this;

            var info = new FileInfo(m_assetPath);
            m_fileSize = info.Length;
            m_fileInfoHash = info.Length + info.Extension;
            m_addressable = FR2_Unity.GetAddressable(guid);

            //if (!string.IsNullOrEmpty(m_addressable)) Debug.LogWarning(guid + " --> " + m_addressable);
            m_assetbundle = AssetDatabase.GetImplicitAssetBundleName(m_assetPath);

            if (assetType == typeof(Texture2D))
            {
                AssetImporter importer = AssetImporter.GetAtPath(m_assetPath);
                if (importer is TextureImporter tImporter)
                {
                    #pragma warning disable CS0618
                    if (tImporter.qualifiesForSpritePacking) m_atlas = tImporter.spritePackingTag;
                    #pragma warning restore CS0618
                }
            }

            // check if file content changed
            var metaInfo = new FileInfo(m_assetPath + ".meta");
            int assetTime = FR2_Unity.Epoch(info.LastWriteTime);
            int metaTime = FR2_Unity.Epoch(metaInfo.LastWriteTime);

            // update fileChangeTimeStamp
            m_fileWriteTS = Mathf.Max(metaTime, assetTime);
            return this;
        }

        public void AddUsedBy(string guid, FR2_Asset asset)
        {
            if (UsedByMap.ContainsKey(guid)) return;

            if (guid == this.guid)

                //Debug.LogWarning("self used");
            {
                return;
            }


            UsedByMap.Add(guid, asset);
            if (HashUsedByClassesIds == null) HashUsedByClassesIds = new HashSet<long>();

            if (asset.UseGUIDs.TryGetValue(this.guid, out HashSet<long> output))
            {
                foreach (int item in output)
                {
                    HashUsedByClassesIds.Add(item);
                }
            }

            // int classId = HashUseByClassesIds    
        }

        public int UsageCount()
        {
            return UsedByMap.Count;
        }

        public override string ToString()
        {
            return $"FR2_Asset[{m_assetName}]";
        }

        //--------------------------------- STATIC ----------------------------

        internal static bool IsValidGUID(string guid)
        {
            return AssetDatabase.GUIDToAssetPath(guid) != FR2_Cache.CachePath; // just skip FR2_Cache asset
        }

        internal void MarkAsDirty(bool isMoved = true, bool force = false)
        {
            if (isMoved)
            {
                string newPath = AssetDatabase.GUIDToAssetPath(guid);
                if (newPath != m_assetPath)
                {
                    m_pathLoaded = false;
                    m_assetPath = newPath;
                }
            }

            state = AssetState.CACHE;
            m_assetChangeTS = FR2_Unity.Epoch(DateTime.Now); // re-read FileInfo
            if (force) m_cachefileWriteTS = 0;
        }

        // --------------------------------- APIs ------------------------------

        internal void GuessAssetType()
        {
            if (SCRIPT_EXTENSIONS.Contains(m_extension))
            {
                type = AssetType.SCRIPT;
            } else if (REFERENCABLE_EXTENSIONS.Contains(m_extension))
            {
                bool isUnity = m_extension == ".unity";
                type = isUnity ? AssetType.SCENE : AssetType.REFERENCABLE;

                if (m_extension == ".asset" || isUnity || m_extension == ".spriteatlas")
                {
                    var buffer = new byte[5];
                    FileStream stream = null;

                    try
                    {
                        stream = File.OpenRead(m_assetPath);
                        stream.Read(buffer, 0, 5);
                        stream.Close();
                    }
#if FR2_DEBUG
                    catch (Exception e)
                    {
                        Debug.LogWarning("Guess Asset Type error :: " + e + "\n" + m_assetPath);
#else
                    catch
                    {
#endif
                        if (stream != null) stream.Close();
                        state = AssetState.MISSING;
                        return;
                    } finally
                    {
                        if (stream != null) stream.Close();
                    }

                    var str = string.Empty;
                    foreach (byte t in buffer)
                    {
                        str += (char)t;
                    }

                    if (str != "%YAML") type = AssetType.BINARY_ASSET;
                }
            } else if (REFERENCABLE_JSON.Contains(m_extension) || UI_TOOLKIT.Contains(m_extension))
            {
                // Debug.Log($"Found: {m_assetPath}");
                type = AssetType.REFERENCABLE;
            } else if (REFERENCABLE_META.Contains(m_extension))
            {
                type = AssetType.REFERENCABLE;
            } else if (m_extension == ".fbx")
            {
                type = AssetType.MODEL;
            } else if (m_extension == ".dll")
            {
                type = AssetType.DLL;
            } else
            {
                type = AssetType.NON_READABLE;
            }
        }

        internal void LoadContent()
        {
            if (!fileContentDirty) return;
            m_cachefileWriteTS = m_fileWriteTS;
            m_forceIncludeInBuild = false;

            if (IsMissing || type == AssetType.NON_READABLE) return;
            if (type == AssetType.DLL)
            {
#if FR2_DEBUG
            Debug.LogWarning("Parsing DLL not yet supportted ");
#endif
                return;
            }

            DateTime startTime = DateTime.Now;
            if (shouldWriteImportLog && (fileSize >= MIN_FILE_SIZE_2LOG))
            {
                var logMessage = $"{startTime:yyyy-MM-dd HH:mm:ss} - {assetPath}, Size: {fileSize} bytes";
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }

            if (!ExistOnDisk())
            {
                state = AssetState.MISSING;
                return;
            }

            ClearUseGUIDs();

            if (IsFolder)
            {
                LoadFolder();
            } else if (IsReferencable)
            {
                LoadYAML2();
            } else if (IsBinaryAsset)
            {
                LoadBinaryAsset();
            }

            if (shouldWriteImportLog && (fileSize >= MIN_FILE_SIZE_2LOG))
            {
                DateTime endTime = DateTime.Now;
                double duration = (endTime - startTime).TotalMilliseconds;
                var logMessage = $", Duration: {duration} ms";
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }
        }

        internal void AddUseGUID(string fguid, long fFileId = -1)
        {
            AddUseGUID(fguid, fFileId, true);
        }

        internal void AddUseGUID(string fguid, long fFileId, bool checkExist)
        {
            // if (checkExist && UseGUIDs.ContainsKey(fguid)) return;
            if (!IsValidGUID(fguid)) return;

            if (!UseGUIDs.ContainsKey(fguid))
            {
                UseGUIDsList.Add(new Classes
                {
                    guid = fguid,
                    ids = new List<long>()
                });
                UseGUIDs.Add(fguid, new HashSet<long>());
            }

            if (fFileId == -1) return;
            if (UseGUIDs[fguid].Contains(fFileId)) return;

            UseGUIDs[fguid].Add(fFileId);
            Classes i = UseGUIDsList.FirstOrDefault(x => x.guid == fguid);
            if (i != null) i.ids.Add(fFileId);
        }

        // ----------------------------- STATIC  ---------------------------------------

        internal static int SortByExtension(FR2_Asset a1, FR2_Asset a2)
        {
            if (a1 == null) return -1;
            if (a2 == null) return 1;

            int result = string.Compare(a1.m_extension, a2.m_extension, StringComparison.Ordinal);
            return result == 0 ? string.Compare(a1.m_assetName, a2.m_assetName, StringComparison.Ordinal) : result;
        }

        internal static List<FR2_Asset> FindUsage(FR2_Asset asset)
        {
            if (asset == null) return null;

            List<FR2_Asset> refs = FR2_Cache.Api.FindAssets(asset.UseGUIDs.Keys.ToArray(), true);


            return refs;
        }

        internal static List<FR2_Asset> FindUsedBy(FR2_Asset asset)
        {
            return asset.UsedByMap.Values.ToList();
        }

        internal static List<string> FindUsageGUIDs(FR2_Asset asset, bool includeScriptSymbols)
        {
            var result = new HashSet<string>();
            if (asset == null)
            {
                Debug.LogWarning("Asset invalid : " + asset.m_assetName);
                return result.ToList();
            }

            // for (var i = 0;i < asset.UseGUIDs.Count; i++)
            // {
            // 	result.Add(asset.UseGUIDs[i]);
            // }
            foreach (KeyValuePair<string, HashSet<long>> item in asset.UseGUIDs)
            {
                result.Add(item.Key);
            }

            //if (!includeScriptSymbols) return result.ToList();

            //if (asset.ScriptUsage != null)
            //{
            //	for (var i = 0; i < asset.ScriptUsage.Count; i++)
            //	{
            //    	var symbolList = FR2_Cache.Api.FindAllSymbol(asset.ScriptUsage[i]);
            //    	if (symbolList.Contains(asset)) continue;

            //    	var symbol = symbolList[0];
            //    	if (symbol == null || result.Contains(symbol.guid)) continue;

            //    	result.Add(symbol.guid);
            //	}	
            //}

            return result.ToList();
        }

        internal static List<string> FindUsedByGUIDs(FR2_Asset asset)
        {
            return asset.UsedByMap.Keys.ToList();
        }

        internal float Draw(
            Rect r,
            bool highlight,
            bool drawPath = true,
            bool showFileSize = true,
            bool showABName = false,
            bool showAtlasName = false,
            bool showUsageIcon = true,
            IWindow window = null,
            bool drawExtension = true
        )
        {
            bool singleLine = r.height <= 18f;
            float rw = r.width;
            bool selected = FR2_Bookmark.Contains(guid);

            r.height = 16f;
            bool hasMouse = (Event.current.type == EventType.MouseUp) && r.Contains(Event.current.mousePosition);

            if (hasMouse && (Event.current.button == 1))
            {
                var menu = new GenericMenu();
                if (m_extension == ".prefab") menu.AddItem(FR2_GUIContent.FromString("Edit in Scene"), false, EditPrefab);

                menu.AddItem(FR2_GUIContent.FromString("Open"), false, Open);
                menu.AddItem(FR2_GUIContent.FromString("Ping"), false, Ping);
                menu.AddItem(FR2_GUIContent.FromString(guid), false, CopyGUID);

                menu.AddItem(FR2_GUIContent.FromString("Select in Project Panel"), false, Select);

                menu.AddSeparator(string.Empty);
                menu.AddItem(FR2_GUIContent.FromString("Copy path"), false, CopyAssetPath);
                menu.AddItem(FR2_GUIContent.FromString("Copy full path"), false, CopyAssetPathFull);

                //if (IsScript)
                //{
                //    menu.AddSeparator(string.Empty);
                //    AddArray(menu, ScriptSymbols, "+ ", "Definitions", "No Definition", false);

                //    menu.AddSeparator(string.Empty);
                //    AddArray(menu, ScriptUsage, "-> ", "Depends", "No Dependency", true);
                //}

                menu.ShowAsContext();
                Event.current.Use();
            }

            if (IsMissing)
            {
                if (!singleLine) r.y += 16f;

                if (Event.current.type != EventType.Repaint) return 0;

                GUI.Label(r, FR2_GUIContent.FromString(guid), EditorStyles.whiteBoldLabel);
                return 0;
            }

            Rect iconRect = GUI2.LeftRect(16f, ref r);
            if (Event.current.type == EventType.Repaint)
            {
                Texture icon = AssetDatabase.GetCachedIcon(m_assetPath);
                if (icon != null) GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }


            if ((Event.current.type == EventType.MouseDown) && (Event.current.button == 0))
            {
                Rect pingRect = FR2_Setting.PingRow ? new Rect(0, r.y, r.x + r.width, r.height) : iconRect;
                if (pingRect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.control || Event.current.command)
                    {
                        if (selected)
                        {
                            RemoveFromSelection();
                        } else
                        {
                            AddToSelection();
                        }

                        if (window != null) window.Repaint();
                    } else
                    {
                        Ping();
                    }


                    //Event.current.Use();
                }
            }

            if (Event.current.type != EventType.Repaint) return 0;
            if ((UsedByMap != null) && (UsedByMap.Count > 0))
            {
                GUIContent str = FR2_GUIContent.FromInt(UsedByMap.Count);
                Rect countRect = iconRect;
                countRect.x -= 16f;
                countRect.xMin = -10f;
                GUI.Label(countRect, str, GUI2.miniLabelAlignRight);
            }

            float pathW = drawPath && !string.IsNullOrEmpty(assetFolder)
                ? EditorStyles.miniLabel.CalcSize(FR2_GUIContent.FromString(assetFolder)).x
                : 8f;

            float nameW = drawPath
                ? EditorStyles.boldLabel.CalcSize(FR2_GUIContent.FromString(assetName)).x
                : EditorStyles.label.CalcSize(FR2_GUIContent.FromString(assetName)).x;

            float extW = string.IsNullOrEmpty(extension) ? 0f : EditorStyles.miniLabel.CalcSize(FR2_GUIContent.FromString(extension)).x;
            Color cc = FR2_Cache.Api.setting.SelectedColor;

            // Debug.Log($"{assetPath} " + pathW + " " + nameW + " " + extW);

            if (singleLine)
            {
                Rect lbRect = GUI2.LeftRect(pathW + nameW + extW, ref r);

                if (selected)
                {
                    Color c1 = GUI.color;
                    GUI.color = cc;
                    GUI.DrawTexture(lbRect, EditorGUIUtility.whiteTexture);
                    GUI.color = c1;
                }

                if (drawPath)
                {
                    if (!string.IsNullOrEmpty(assetFolder))
                    {
                        Color c2 = GUI.color;
                        GUI.color = new Color(c2.r, c2.g, c2.b, c2.a * 0.5f);
                        GUI.Label(GUI2.LeftRect(pathW, ref lbRect), FR2_GUIContent.FromString(assetFolder), EditorStyles.miniLabel);
                        GUI.color = c2;
                    }

                    // lbRect.xMin -= 4f;
                    GUI.Label(lbRect, FR2_GUIContent.FromString(assetName), EditorStyles.boldLabel);
                } else
                {
                    Color c2 = GUI.color;
                    GUI.color = new Color(c2.r, c2.g, c2.b, c2.a * 0.9f);
                    GUI.Label(lbRect, FR2_GUIContent.FromString(assetName));
                    GUI.color = c2;
                }


                lbRect.xMin += nameW - 2f;
                lbRect.y += 1f;

                if (!string.IsNullOrEmpty(extension) && drawExtension)
                {
                    Color c3 = GUI.color;
                    GUI.color = new Color(c3.r, c3.g, c3.b, c3.a * 0.5f);
                    GUI.Label(lbRect, FR2_GUIContent.FromString(extension), EditorStyles.miniLabel);
                    GUI.color = c3;
                }
            } else
            {
                if (drawPath) GUI.Label(new Rect(r.x, r.y + 16f, r.width, r.height), FR2_GUIContent.FromString(m_assetFolder), EditorStyles.miniLabel);
                Rect lbRect = GUI2.LeftRect(nameW, ref r);
                if (selected) GUI2.Rect(lbRect, cc);
                GUI.Label(lbRect, FR2_GUIContent.FromString(assetName), EditorStyles.boldLabel);
            }

            Rect rr = GUI2.RightRect(10f, ref r); //margin
            if (highlight)
            {
                rr.xMin += 2f;
                rr.width = 1f;
                GUI2.Rect(rr, GUI2.darkGreen);
            }

            Color c = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * 0.5f);

            if (showFileSize)
            {
                Rect fsRect = GUI2.RightRect(40f, ref r); // filesize label
                if (fileSizeText == null) fileSizeText = FR2_GUIContent.FromString(FR2_Helper.GetfileSizeString(fileSize));
                GUI.Label(fsRect, fileSizeText, GUI2.miniLabelAlignRight);
            }

            if (!string.IsNullOrEmpty(m_addressable))
            {
                Rect adRect = GUI2.RightRect(100f, ref r);
                GUI.Label(adRect, FR2_GUIContent.FromString(m_addressable), GUI2.miniLabelAlignRight);
            }

            if (showUsageIcon && (HashUsedByClassesIds != null))
            {
                foreach (int item in HashUsedByClassesIds)
                {
                    if (!FR2_Unity.HashClassesNormal.ContainsKey(item)) continue;

                    string name = FR2_Unity.HashClassesNormal[item];
                    if (!HashClasses.TryGetValue(item, out Type t))
                    {
                        t = FR2_Unity.GetType(name);
                        HashClasses.Add(item, t);
                    }

                    bool isExisted = cacheImage.TryGetValue(name, out GUIContent content);
                    if (content == null)
                    {
                        content = t == null ? GUIContent.none : FR2_GUIContent.FromType(t, name);
                    }

                    if (!isExisted)
                    {
                        cacheImage.Add(name, content);
                    } else
                    {
                        cacheImage[name] = content;
                    }

                    if (content != null)
                    {
                        try
                        {
                            GUI.Label(GUI2.RightRect(15f, ref r), content, GUI2.miniLabelAlignRight);
                        }
#if !FR2_DEBUG
                        catch { }
#else
						catch (Exception e)
						{
							UnityEngine.Debug.LogWarning(e);
						}
#endif
                    }
                }
            }

            if (showAtlasName)
            {
                GUI2.RightRect(10f, ref r); //margin
                Rect abRect = GUI2.RightRect(120f, ref r); // filesize label
                if (!string.IsNullOrEmpty(m_atlas)) GUI.Label(abRect, FR2_GUIContent.FromString(m_atlas), GUI2.miniLabelAlignRight);
            }

            if (showABName)
            {
                GUI2.RightRect(10f, ref r); //margin
                Rect abRect = GUI2.RightRect(100f, ref r); // filesize label
                if (!string.IsNullOrEmpty(m_assetbundle)) GUI.Label(abRect, FR2_GUIContent.FromString(m_assetbundle), GUI2.miniLabelAlignRight);
            }

            if (true)
            {
                GUI2.RightRect(10f, ref r); //margin
                Rect abRect = GUI2.RightRect(100f, ref r); // filesize label
                if (!string.IsNullOrEmpty(m_addressable)) GUI.Label(abRect, FR2_GUIContent.FromString(m_addressable), GUI2.miniLabelAlignRight);
            }

            GUI.color = c;

            if (Event.current.type == EventType.Repaint) return rw < pathW + nameW ? 32f : 18f;

            return r.height;
        }



        internal GenericMenu AddArray(
            GenericMenu menu, List<string> list, string prefix, string title,
            string emptyTitle, bool showAsset, int max = 10)
        {
            //if (list.Count > 0)
            //{
            //    if (list.Count > max)
            //    {
            //        prefix = string.Format("{0} _{1}/", title, list.Count) + prefix;
            //    }

            //    //for (var i = 0; i < list.Count; i++)
            //    //{
            //    //    var def = list[i];
            //    //    var suffix = showAsset ? "/" + FR2_Cache.Api.FindSymbol(def).assetName : string.Empty;
            //    //    menu.AddItem(new GUIContent(prefix + def + suffix), false, () => OpenScript(def));
            //    //}
            //}
            //else
            {
                menu.AddItem(FR2_GUIContent.FromString(emptyTitle), true, null);
            }

            return menu;
        }

        internal void CopyGUID()
        {
            EditorGUIUtility.systemCopyBuffer = guid;
            Debug.Log(guid);
        }

        internal void CopyName()
        {
            EditorGUIUtility.systemCopyBuffer = m_assetName;
            Debug.Log(m_assetName);
        }

        internal void CopyAssetPath()
        {
            EditorGUIUtility.systemCopyBuffer = m_assetPath;
            Debug.Log(m_assetPath);
        }

        internal void CopyAssetPathFull()
        {
            string fullName = new FileInfo(m_assetPath).FullName;
            EditorGUIUtility.systemCopyBuffer = fullName;
            Debug.Log(fullName);
        }

        internal void Select()
        {
            EditorWindow window = EditorWindow.focusedWindow;
            if ((window != null) && window is FR2_WindowAll fr2Window && (fr2Window.selection != null))
            {
                fr2Window.selection.isLock = true;
            }

            if (!FR2_Bookmark.Contains(guid))
            {
                Selection.objects = new[] { FR2_Unity.LoadAssetAtPath<UnityObject>(assetPath) };
            } else
            {
                FR2_Bookmark.Commit();
            }
        }

        internal void RemoveFromSelection()
        {
            if (FR2_Bookmark.Contains(guid)) FR2_Bookmark.Remove(guid);
        }

        internal void AddToSelection()
        {
            if (!FR2_Bookmark.Contains(guid)) FR2_Bookmark.Add(guid);
        }

        internal void Ping()
        {
            EditorGUIUtility.PingObject(
                AssetDatabase.LoadAssetAtPath(m_assetPath, typeof(UnityObject))
            );

            Event.current.Use();
        }

        internal void Open()
        {
            AssetDatabase.OpenAsset(
                AssetDatabase.LoadAssetAtPath(m_assetPath, typeof(UnityObject))
            );
        }

        internal void EditPrefab()
        {
            UnityObject prefab = AssetDatabase.LoadAssetAtPath(m_assetPath, typeof(UnityObject));
            UnityObject.Instantiate(prefab);
        }

        //internal void OpenScript(string definition)
        //{
        //    var asset = FR2_Cache.Api.FindSymbol(definition);
        //    if (asset == null) return;

        //    EditorGUIUtility.PingObject(
        //        AssetDatabase.LoadAssetAtPath(asset.assetPath, typeof(Object))
        //        );
        //}

        // ----------------------------- SERIALIZED UTILS ---------------------------------------



        // ----------------------------- LOAD ASSETS ---------------------------------------

        internal void LoadGameObject(GameObject go)
        {
            Component[] compList = go.GetComponentsInChildren<Component>();
            for (var i = 0; i < compList.Length; i++)
            {
                LoadSerialized(compList[i]);
            }
        }

        internal void LoadSerialized(UnityObject target)
        {
            SerializedProperty[] props = FR2_Unity.xGetSerializedProperties(target, true);

            for (var i = 0; i < props.Length; i++)
            {
                if (props[i].propertyType != SerializedPropertyType.ObjectReference) continue;

                UnityObject refObj = props[i].objectReferenceValue;
                if (refObj == null) continue;

                string refGUID = AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(refObj)
                );

                //Debug.Log("Found Reference BinaryAsset <" + assetPath + "> : " + refGUID + ":" + refObj);
                AddUseGUID(refGUID);
            }
        }

        private void AddTextureGUID(SerializedProperty prop)
        {
            if (prop == null || prop.objectReferenceValue == null) return;
            string path = AssetDatabase.GetAssetPath(prop.objectReferenceValue);
            if (string.IsNullOrEmpty(path)) return;
            AddUseGUID(AssetDatabase.AssetPathToGUID(path));
        }

        internal void LoadLightingData(LightingDataAsset asset)
        {
            foreach (Texture texture in FR2_Lightmap.Read(asset))
            {
                if (texture == null) continue;
                string path = AssetDatabase.GetAssetPath(texture);
                string assetGUID = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(assetGUID))
                {
                    AddUseGUID(assetGUID);

                    // Debug.Log($"Found Lightmap Texture: {path}, GUID: {assetGUID}");
                }
            }
        }

        internal void LoadTerrainData(TerrainData terrain)
        {
#if UNITY_2018_3_OR_NEWER
            TerrainLayer[] arr0 = terrain.terrainLayers;
            for (var i = 0; i < arr0.Length; i++)
            {
                string aPath = AssetDatabase.GetAssetPath(arr0[i]);
                string refGUID = AssetDatabase.AssetPathToGUID(aPath);
                AddUseGUID(refGUID);
            }
#endif


            DetailPrototype[] arr = terrain.detailPrototypes;

            for (var i = 0; i < arr.Length; i++)
            {
                string aPath = AssetDatabase.GetAssetPath(arr[i].prototypeTexture);
                string refGUID = AssetDatabase.AssetPathToGUID(aPath);
                AddUseGUID(refGUID);
            }

            TreePrototype[] arr2 = terrain.treePrototypes;
            for (var i = 0; i < arr2.Length; i++)
            {
                string aPath = AssetDatabase.GetAssetPath(arr2[i].prefab);
                string refGUID = AssetDatabase.AssetPathToGUID(aPath);
                AddUseGUID(refGUID);
            }

            FR2_Unity.TerrainTextureData[] arr3 = FR2_Unity.GetTerrainTextureDatas(terrain);
            for (var i = 0; i < arr3.Length; i++)
            {
                FR2_Unity.TerrainTextureData texs = arr3[i];
                for (var k = 0; k < texs.textures.Length; k++)
                {
                    Texture2D tex = texs.textures[k];
                    if (tex == null) continue;

                    string aPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(aPath)) continue;

                    string refGUID = AssetDatabase.AssetPathToGUID(aPath);
                    if (string.IsNullOrEmpty(refGUID)) continue;

                    AddUseGUID(refGUID);
                }
            }
        }

        private void ClearUseGUIDs()
        {
#if FR2_DEBUG
		    Debug.Log("ClearUseGUIDs: " + assetPath);
#endif

            UseGUIDs.Clear();
            UseGUIDsList.Clear();
        }
        internal void LoadBinaryAsset()
        {
            ClearUseGUIDs();

            UnityObject assetData = AssetDatabase.LoadAssetAtPath(m_assetPath, typeof(UnityObject));
            if (assetData is GameObject go)
            {
                type = AssetType.MODEL;
                LoadGameObject(go);
                binaryLoaded += 10;
            } else if (assetData is TerrainData terrainData)
            {
                type = AssetType.TERRAIN;
                LoadTerrainData(terrainData);
                binaryLoaded += 20;
            } else if (assetData is LightingDataAsset lightAsset)
            {
                type = AssetType.LIGHTING_DATA;
                LoadLightingData(lightAsset);
                binaryLoaded += 20;
            } else
            {
                LoadSerialized(assetData);
                binaryLoaded++;
            }

#if FR2_DEBUG
			Debug.Log("LoadBinaryAsset :: " + assetData + ":" + type);
#endif
            if (binaryLoaded <= 30) return;
            binaryLoaded = 0;
            FR2_Unity.UnloadUnusedAssets();
        }

        internal void LoadYAML2()
        {
            // Debug.LogWarning($"LoadYAML2: {m_assetPath}");

            if (!m_pathLoaded) LoadPathInfo();

            if (!File.Exists(m_assetPath))
            {
                state = AssetState.MISSING;
                return;
            }

            if (m_assetPath == "ProjectSettings/EditorBuildSettings.asset")
            {
                EditorBuildSettingsScene[] listScenes = EditorBuildSettings.scenes;
                foreach (EditorBuildSettingsScene scene in listScenes)
                {
                    if (!scene.enabled) continue;
                    string path = scene.path;
                    string guid = AssetDatabase.AssetPathToGUID(path);

                    AddUseGUID(guid, 0);
#if FR2_DEBUG
					Debug.Log("AddScene: " + path);
#endif
                }
            }

            if (string.IsNullOrEmpty(extension))
            {
                Debug.LogWarning($"Something wrong? <{m_extension}>");
            }

            if (extension == ".spriteatlas") // check for force include in build
            {
                var atlasAsset = AssetDatabase.LoadAssetAtPath<UnityObject>(m_assetPath);
                if (atlasAsset != null)
                {
                    var so = new SerializedObject(atlasAsset);
                    SerializedProperty prop = so.FindProperty("m_EditorData.bindAsDefault");
                    m_forceIncludeInBuild = prop.boolValue;
                }
            }

            if (UI_TOOLKIT.Contains(m_extension))
            {
                // Debug.Log($"Found: {m_extension}");

                if (m_extension == ".tss")
                {
                    FR2_Parser.ReadTss(m_assetPath, AddUseGUID);
                } else
                {
                    FR2_Parser.ReadUssUxml(m_assetPath, AddUseGUID);
                }
                return;
            }

            if (REFERENCABLE_JSON.Contains(m_extension))
            {
                FR2_Parser.ReadJson(m_assetPath, AddUseGUID);
                return;
            }

            if (REFERENCABLE_META.Contains(m_extension))
            {
                FR2_Parser.ReadYaml($"{m_assetPath}.meta", AddUseGUID);
                return;
            }

            FR2_Parser.ReadYaml(m_assetPath, AddUseGUID);
        }

        internal void LoadFolder()
        {
            if (!Directory.Exists(m_assetPath))
            {
                state = AssetState.MISSING;
                return;
            }

            // do not analyse folders outside project
            if (!m_assetPath.StartsWith("Assets/")) return;

            try
            {
                string[] files = Directory.GetFiles(m_assetPath);
                string[] dirs = Directory.GetDirectories(m_assetPath);

                foreach (string f in files)
                {
                    if (f.EndsWith(".meta", StringComparison.Ordinal)) continue;

                    string fguid = AssetDatabase.AssetPathToGUID(f);
                    if (string.IsNullOrEmpty(fguid)) continue;

                    // AddUseGUID(fguid, true);
                    AddUseGUID(fguid);
                }

                foreach (string d in dirs)
                {
                    string fguid = AssetDatabase.AssetPathToGUID(d);
                    if (string.IsNullOrEmpty(fguid)) continue;

                    // AddUseGUID(fguid, true);
                    AddUseGUID(fguid);
                }
            }
#if FR2_DEBUG
            catch (Exception e)
            {
                Debug.LogWarning("LoadFolder() error :: " + e + "\n" + assetPath);
#else
            catch
            {
#endif
                state = AssetState.MISSING;
            }

            //Debug.Log("Load Folder :: " + assetName + ":" + type + ":" + UseGUIDs.Count);
        }

        internal static void ClearLog()
        {
            if (shouldWriteImportLog)
            {
                File.WriteAllText(logPath, string.Empty);
            } else
            {
                if (File.Exists(logPath)) File.Delete(logPath);
            }

            scanStartTime = DateTime.Now;
        }

        internal void LoadContentFast()
        {
            if (!fileContentDirty) return;
            m_cachefileWriteTS = m_fileWriteTS;
            m_forceIncludeInBuild = false;

            if (IsMissing) return;
            if (type == AssetType.SCRIPT || type == AssetType.DLL || type == AssetType.FOLDER) return;
            if (assetPath.StartsWith("Packages/")) return;


            DateTime startTime = DateTime.Now;
            if (shouldWriteImportLog && (fileSize >= MIN_FILE_SIZE_2LOG))
            {
                var logMessage = $"{startTime:yyyy-MM-dd HH:mm:ss} - {assetPath}, Size: {fileSize} bytes";
                File.AppendAllText(logPath, logMessage);
            }

            ClearUseGUIDs();

            if (fileSize > 5 * 1024 * 1024 || type == AssetType.NON_READABLE || IsBinaryAsset)
            {
                string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                foreach (string dependency in dependencies)
                {
                    string guid = AssetDatabase.AssetPathToGUID(dependency);
                    if (!string.IsNullOrEmpty(guid) && (guid != this.guid))
                    {
                        AddUseGUID(guid);
                    }
                }
            } else if (IsReferencable)
            {
                LoadYAML2();
            }

            if (shouldWriteImportLog && (fileSize >= MIN_FILE_SIZE_2LOG))
            {
                DateTime endTime = DateTime.Now;
                double duration = (endTime - startTime).TotalMilliseconds;
                var logMessage = $", Duration: {duration} ms";
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }
        }

        internal static void WriteTotalScanTime()
        {
            if (!shouldWriteImportLog) return;
            double totalScanTime = (DateTime.Now - scanStartTime).TotalSeconds;
            File.AppendAllText(logPath, $"\nTotal scan time: {totalScanTime} seconds\n");
        }

        // ----------------------------- REPLACE GUIDS ---------------------------------------

        internal bool ReplaceReference(string fromGUID, string toGUID, TerrainData terrain = null)
        {
            if (IsMissing) return false;

            if (IsReferencable)
            {
                if (!File.Exists(m_assetPath))
                {
                    state = AssetState.MISSING;
                    return false;
                }

                try
                {
                    string text = File.ReadAllText(m_assetPath).Replace("\r", "\n");
                    File.WriteAllText(m_assetPath, text.Replace(fromGUID, toGUID));
                    return true;
                } catch (Exception e)
                {
                    state = AssetState.MISSING;
                    Debug.LogWarning("Replace Reference error :: " + e + "\n" + m_assetPath);
                }

                return false;
            }

            if (type == AssetType.TERRAIN)
            {
                var fromObj = FR2_Unity.LoadAssetWithGUID<UnityObject>(fromGUID);
                var toObj = FR2_Unity.LoadAssetWithGUID<UnityObject>(toGUID);
                var found = 0;

                if (fromObj is Texture2D tex)
                {
                    DetailPrototype[] arr = terrain.detailPrototypes;
                    for (var i = 0; i < arr.Length; i++)
                    {
                        if (arr[i].prototypeTexture != tex) continue;
                        found++;
                        arr[i].prototypeTexture = (Texture2D)toObj;
                    }

                    terrain.detailPrototypes = arr;
                    FR2_Unity.ReplaceTerrainTextureDatas(terrain, tex, (Texture2D)toObj);
                }

                if (fromObj is GameObject go)
                {
                    TreePrototype[] arr2 = terrain.treePrototypes;
                    for (var i = 0; i < arr2.Length; i++)
                    {
                        if (arr2[i].prefab != go) continue;
                        found++;
                        arr2[i].prefab = (GameObject)toObj;
                    }

                    terrain.treePrototypes = arr2;
                }

                return found > 0;
            }

            #if FR2_DEV
            Debug.LogWarning("Something wrong, should never be here - Ignored <" + m_assetPath +
                "> : not a readable type, can not replace ! " + type);
            #endif

            return false;
        }

        internal string ReplaceFileIdIfNeeded(string line, long toFileId)
        {
            const string FileID = "fileID: ";
            int index = line.IndexOf(FileID, StringComparison.Ordinal);
            if (index < 0 || toFileId <= 0) return line;
            int startIndex = index + FileID.Length;
            int endIndex = line.IndexOf(',', startIndex);
            if (endIndex > startIndex)
            {
                string fromFileId = line.Substring(startIndex, endIndex - startIndex);
                if (long.TryParse(fromFileId, out long fileType) &&
                    fileType.ToString().StartsWith(toFileId.ToString().Substring(0, 3)))
                {
                    Debug.Log($"ReplaceReference: fromFileId {fromFileId} to File Id {toFileId}");
                    return line.Replace(fromFileId, toFileId.ToString());
                }
                Debug.LogWarning($"[Skip] Difference file type: {fromFileId} -> {toFileId}");
            } else
            {
                Debug.LogWarning("Cannot parse fileID in the line.");
            }
            return line;
        }

        internal bool ReplaceReference(string fromGUID, string toGUID, long toFileId, TerrainData terrain = null)
        {
            Debug.Log($"{assetPath} : ReplaceReference from " + fromGUID + "  to: " + toGUID + "  toFileId: " + toFileId);

            if (IsMissing) // Debug.Log("this asset is missing");
            {
                return false;
            }

            if (IsReferencable)
            {
                if (!File.Exists(m_assetPath)) // Debug.Log("this asset not exits");
                {
                    state = AssetState.MISSING;
                    return false;
                }

                try
                {
                    var sb = new StringBuilder();
                    string text = File.ReadAllText(assetPath);
                    var currentIndex = 0;

                    while (currentIndex < text.Length)
                    {
                        int lineEndIndex = text.IndexOfAny(new[] { '\r', '\n' }, currentIndex);
                        if (lineEndIndex == -1)
                        {
                            lineEndIndex = text.Length;
                        }

                        string line = text.Substring(currentIndex, lineEndIndex - currentIndex);

                        // Check if the line contains the GUID and possibly the fileID
                        if (line.Contains(fromGUID))
                        {
                            line = ReplaceFileIdIfNeeded(line, toFileId);
                            line = line.Replace(fromGUID, toGUID);
                        }

                        sb.Append(line);

                        // Skip through any EOL characters
                        while (lineEndIndex < text.Length)
                        {
                            char c = text[lineEndIndex];
                            if (c == '\r' || c == '\n')
                            {
                                sb.Append(c);
                                lineEndIndex++;
                            }
                            break;
                        }

                        currentIndex = lineEndIndex;
                    }

                    File.WriteAllText(assetPath, sb.ToString());

                    //AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
                    return true;
                } catch (Exception e)
                {
                    state = AssetState.MISSING;

                    //#if FR2_DEBUG
                    Debug.LogWarning("Replace Reference error :: " + e + "\n" + m_assetPath);

                    //#endif
                }

                return false;
            }

            if (type == AssetType.TERRAIN)
            {
                var fromObj = FR2_Unity.LoadAssetWithGUID<UnityObject>(fromGUID);
                var toObj = FR2_Unity.LoadAssetWithGUID<UnityObject>(toGUID);
                var found = 0;

                // var terrain = AssetDatabase.LoadAssetAtPath(assetPath, typeof(Object)) as TerrainData;

                if (fromObj is Texture2D)
                {
                    DetailPrototype[] arr = terrain.detailPrototypes;
                    for (var i = 0; i < arr.Length; i++)
                    {
                        if (arr[i].prototypeTexture == (Texture2D)fromObj)
                        {
                            found++;
                            arr[i].prototypeTexture = (Texture2D)toObj;
                        }
                    }

                    terrain.detailPrototypes = arr;
                    FR2_Unity.ReplaceTerrainTextureDatas(terrain, (Texture2D)fromObj, (Texture2D)toObj);
                }

                if (fromObj is GameObject go)
                {
                    TreePrototype[] arr2 = terrain.treePrototypes;
                    for (var i = 0; i < arr2.Length; i++)
                    {
                        if (arr2[i].prefab != go) continue;
                        found++;
                        arr2[i].prefab = (GameObject)toObj;
                    }

                    terrain.treePrototypes = arr2;
                }

                // EditorUtility.SetDirty(terrain);
                // AssetDatabase.SaveAssets();
                // FR2_Unity.UnloadUnusedAssets();
                return found > 0;
            }

            Debug.LogWarning("Something wrong, should never be here - Ignored <" + m_assetPath +
                "> : not a readable type, can not replace ! " + type);
            return false;
        }

    }
}
