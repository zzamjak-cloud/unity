using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{
    [Serializable]
    internal class FR2_Setting
    {
        private static FR2_Setting d;

        [NonSerialized] private static HashSet<string> _hashIgnore;

        //		private static Dictionary<string, List<string>> _IgnoreFiltered;
        public static Action OnIgnoreChange;

        public bool alternateColor = true;
        public int excludeTypes; //32-bit type Mask

        public List<string> listIgnore = new List<string>();
        public bool pingRow = true;
        public bool referenceCount = true;
        public bool showPackageAsset = true;
        public bool showSubAssetFileId;

        public bool showFileSize;
        public bool displayFileSize = true;
        public bool displayAtlasName;
        public bool displayAssetBundleName;

        public bool showUsedByClassed = true;
        public int treeIndent = 10;

        public Color32 rowColor = new Color32(0, 0, 0, 12);

        // public Color32 ScanColor = new Color32(0, 204, 102, 255);
        public Color SelectedColor = new Color(0, 0f, 1f, 0.25f);


        //public bool scanScripts		= false;



        /*
        Doesn't have a settings option - I will include one in next update

        2. Hide the reference number - Should be in the setting above so will be coming next
        3. Cache file path should be configurable - coming next in the setting
        4. Disable / Selectable color in alternative rows - coming next in the setting panel
        5. Applied filters aren't saved - Should be fixed in next update too
        6. Hide Selection part - should be com as an option so you can quickly toggle it on or off
        7. Click whole line to ping - coming next by default and can adjustable in the setting panel

        */

        internal static FR2_Setting s
        {
            get
            {
                if (FR2_Cache.Api != null) return FR2_Cache.Api.setting;
                if (d != null) return d;
                d = new FR2_Setting();
                return d;
            }
        }

        public static bool ShowUsedByClassed => s.showUsedByClassed;

        public static bool ShowFileSize => s.showFileSize;

        public static int TreeIndent
        {
            get => s.treeIndent;
            set
            {
                if (s.treeIndent == value) return;

                s.treeIndent = value;
                setDirty();
            }
        }

        public static bool ShowReferenceCount
        {
            get => s.referenceCount;
            set
            {
                if (s.referenceCount == value) return;

                s.referenceCount = value;
                setDirty();
            }
        }
        public static bool AlternateRowColor
        {
            get => s.alternateColor;
            set
            {
                if (s.alternateColor == value) return;

                s.alternateColor = value;
                setDirty();
            }
        }

        public static Color32 RowColor
        {
            get => s.rowColor;
            set
            {
                if (s.rowColor.Equals(value)) return;

                s.rowColor = value;
                setDirty();
            }
        }

        public static bool PingRow
        {
            get => s.pingRow;
            set
            {
                if (s.pingRow == value) return;

                s.pingRow = value;
                setDirty();
            }
        }

        public static HashSet<string> IgnoreAsset
        {
            get
            {
                if (_hashIgnore != null) return _hashIgnore;
                _hashIgnore = new HashSet<string>();
                if (s?.listIgnore == null) return _hashIgnore;

                for (var i = 0; i < s.listIgnore.Count; i++)
                {
                    _hashIgnore.Add(s.listIgnore[i]);
                }

                return _hashIgnore;
            }
        }

        //		public static Dictionary<string, List<string>> IgnoreFiltered
        //		{
        //			get
        //			{
        //				if (_IgnoreFiltered == null)
        //				{
        //					initIgnoreFiltered();
        //				}
        //
        //				return _IgnoreFiltered;
        //			}
        //		}

        //static public bool ScanScripts
        //{
        //	get  { return s.scanScripts; }
        //	set  {
        //		if (s.scanScripts == value) return;
        //		s.scanScripts = value; setDirty();
        //	}
        //}

        // public static FR2_RefDrawer.Mode GroupMode
        // {
        //     get => s.groupMode;
        //     set
        //     {
        //         if (s.groupMode.Equals(value)) return;
        //
        //         s.groupMode = value;
        //         setDirty();
        //     }
        // }
        //
        // public static FR2_RefDrawer.Sort SortMode
        // {
        //     get => s.sortMode;
        //     set
        //     {
        //         if (s.sortMode.Equals(value)) return;
        //
        //         s.sortMode = value;
        //         setDirty();
        //     }
        // }

        public static bool HasTypeExcluded => s.excludeTypes != 0;

        private static void setDirty()
        {
            if (FR2_Cache.Api != null) EditorUtility.SetDirty(FR2_Cache.Api);
        }

        //		private static void initIgnoreFiltered()
        //		{
        //			FR2_Asset.ignoreTS = Time.realtimeSinceStartup;
        //
        //			_IgnoreFiltered = new Dictionary<string, List<string>>();
        //			var lst = new List<string>(s.listIgnore);
        //			lst = lst.OrderBy(x => x.Length).ToList();
        //			int count = lst.Count;
        //			for (var i = 0; i < count; i++)
        //			{
        //				string str = lst[i];
        //				_IgnoreFiltered.Add(str, new List<string> {str});
        //				for (int j = count - 1; j > i; j--)
        //				{
        //					if (lst[j].StartsWith(str))
        //					{
        //						_IgnoreFiltered[str].Add(lst[j]);
        //						lst.RemoveAt(j);
        //						count--;
        //					}
        //				}
        //			}
        //		}

        public static void AddIgnore(string path)
        {
            if (string.IsNullOrEmpty(path) || IgnoreAsset.Contains(path) || path == "Assets") return;

            s.listIgnore.Add(path);
            _hashIgnore.Add(path);
            FR2_AssetGroupDrawer.SetDirtyIgnore();
            FR2_CacheHelper.InitIgnore();

            //initIgnoreFiltered();

            FR2_Asset.ignoreTS = Time.realtimeSinceStartup;
            if (OnIgnoreChange != null) OnIgnoreChange();
        }


        public static void RemoveIgnore(string path)
        {
            if (!IgnoreAsset.Contains(path)) return;

            _hashIgnore.Remove(path);
            s.listIgnore.Remove(path);
            FR2_AssetGroupDrawer.SetDirtyIgnore();
            FR2_CacheHelper.InitIgnore();

            //initIgnoreFiltered();

            FR2_Asset.ignoreTS = Time.realtimeSinceStartup;
            if (OnIgnoreChange != null) OnIgnoreChange();
        }

        public static bool IsTypeExcluded(int type)
        {
            return ((s.excludeTypes >> type) & 1) != 0;
        }

        public static void ToggleTypeExclude(int type)
        {
            bool v = ((s.excludeTypes >> type) & 1) != 0;
            if (v)
            {
                s.excludeTypes &= ~(1 << type);
            } else
            {
                s.excludeTypes |= 1 << type;
            }

            setDirty();
        }

        public static int GetExcludeType()
        {
            return s.excludeTypes;
        }

        public static bool IsIncludeAllType()
        {
            // Debug.Log ((AssetType.FILTERS.Length & s.excludeTypes) + "  " + Mathf.Pow(2, AssetType.FILTERS.Length) ); 
            return s.excludeTypes == 0 || Mathf.Abs(s.excludeTypes) == Mathf.Pow(2, FR2_AssetGroupDrawer.FILTERS.Length);
        }

        public static void ExcludeAllType()
        {
            s.excludeTypes = -1;
        }

        public static void IncludeAllType()
        {
            s.excludeTypes = 0;
        }

        public void DrawSettings()
        {
            // if (FR2_Unity.DrawToggle(ref pingRow, "Full Row click to Ping")) setDirty();

            GUILayout.BeginHorizontal();
            {
                if (FR2_Unity.DrawToggle(ref alternateColor, "Alternate Odd & Even Row Color"))
                {
                    setDirty();
                    FR2_Unity.RepaintFR2Windows();
                }

                EditorGUI.BeginDisabledGroup(!alternateColor);
                {
                    Color c = EditorGUILayout.ColorField(rowColor);
                    if (!c.Equals(rowColor))
                    {
                        rowColor = c;
                        setDirty();
                        FR2_Unity.RepaintFR2Windows();
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            GUILayout.EndHorizontal();

            if (FR2_Unity.DrawToggle(ref referenceCount, "Show Usage Count in Project panel"))
            {
                setDirty();
                FR2_Unity.RepaintProjectWindows();
            }

            if (FR2_Unity.DrawToggle(ref showSubAssetFileId, "Show SubAsset FileId"))
            {
                setDirty();
                FR2_Unity.RepaintFR2Windows();
            }

            if (FR2_Unity.DrawToggle(ref showUsedByClassed, "Show Asset Type in use"))
            {
                setDirty();
                FR2_Unity.RepaintFR2Windows();
            }

            if (FR2_Unity.DrawToggle(ref showPackageAsset, "Show Asset in Packages"))
            {
                setDirty();
                FR2_Unity.RepaintFR2Windows();
            }
        }
    }
}
