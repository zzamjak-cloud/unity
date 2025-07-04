using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{


    internal static class FR2_AssetGroupDrawer
    {
        // ------------------------------- STATIC -----------------------------

        internal static readonly FR2_AssetGroup[] FILTERS =
        {
            new FR2_AssetGroup("Scene", ".unity"),
            new FR2_AssetGroup("Prefab", ".prefab"),
            new FR2_AssetGroup("Model", ".3df", ".3dm", ".3dmf", ".3dv", ".3dx", ".c5d", ".lwo", ".lws", ".ma", ".mb",
                ".mesh", ".vrl", ".wrl", ".wrz", ".fbx", ".dae", ".3ds", ".dxf", ".obj", ".skp", ".max", ".blend"),
            new FR2_AssetGroup("Material", ".mat", ".cubemap", ".physicsmaterial"),
            new FR2_AssetGroup("Texture", ".ai", ".apng", ".png", ".bmp", ".cdr", ".dib", ".eps", ".exif", ".ico", ".icon",
                ".j", ".j2c", ".j2k", ".jas", ".jiff", ".jng", ".jp2", ".jpc", ".jpe", ".jpeg", ".jpf", ".jpg", "jpw",
                "jpx", "jtf", ".mac", ".omf", ".qif", ".qti", "qtif", ".tex", ".tfw", ".tga", ".tif", ".tiff", ".wmf",
                ".psd", ".exr", ".rendertexture"),
            new FR2_AssetGroup("Video", ".asf", ".asx", ".avi", ".dat", ".divx", ".dvx", ".mlv", ".m2l", ".m2t", ".m2ts",
                ".m2v", ".m4e", ".m4v", "mjp", ".mov", ".movie", ".mp21", ".mp4", ".mpe", ".mpeg", ".mpg", ".mpv2",
                ".ogm", ".qt", ".rm", ".rmvb", ".wmv", ".xvid", ".flv"),
            new FR2_AssetGroup("Audio", ".mp3", ".wav", ".ogg", ".aif", ".aiff", ".mod", ".it", ".s3m", ".xm"),
            new FR2_AssetGroup("Script", ".cs", ".js", ".boo", ".h"),
            new FR2_AssetGroup("Text", ".txt", ".json", ".xml", ".bytes", ".sql"),
            new FR2_AssetGroup("Shader", ".shader", ".cginc", ".shadervariants"),
            new FR2_AssetGroup("Animation", ".anim", ".controller", ".overridecontroller", ".mask"),
            new FR2_AssetGroup("Font", ".ttf", ".otf", ".dfont", ".ttc"),
            new FR2_AssetGroup("Unity Asset", ".asset", ".guiskin", ".flare", ".fontsettings", ".prefs", ".playable", ".signal"),
            new FR2_AssetGroup("Others") //
        };

        private static FR2_IgnoreDrawer _ignore;
        private static FR2_IgnoreDrawer ignore
        {
            get
            {
                if (_ignore == null) _ignore = new FR2_IgnoreDrawer();

                return _ignore;
            }
        }

        public static int GetIndex(string ext)
        {
            for (var i = 0; i < FILTERS.Length - 1; i++)
            {
                if (FILTERS[i].extension.Contains(ext)) return i;
            }

            return FILTERS.Length - 1; //Others
        }

        public static bool DrawSearchFilter()
        {
            int n = FILTERS.Length;
            var nCols = 4;
            int nRows = Mathf.CeilToInt(n / (float)nCols);
            var result = false;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                if (GUILayout.Button("All", EditorStyles.toolbarButton) && !FR2_Setting.IsIncludeAllType())
                {
                    FR2_Setting.IncludeAllType();
                    result = true;
                }

                if (GUILayout.Button("None", EditorStyles.toolbarButton) && (FR2_Setting.GetExcludeType() != -1))
                {
                    FR2_Setting.ExcludeAllType();
                    result = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (var i = 0; i < nCols; i++)
            {
                GUILayout.BeginVertical();
                for (var j = 0; j < nRows; j++)
                {
                    int idx = i * nCols + j;
                    if (idx >= n) break;

                    bool s = !FR2_Setting.IsTypeExcluded(idx);
                    bool s1 = GUILayout.Toggle(s, FILTERS[idx].name);
                    if (s1 != s)
                    {
                        result = true;
                        FR2_Setting.ToggleTypeExclude(idx);
                    }
                }

                GUILayout.EndVertical();
                if ((i + 1) * nCols >= n) break;
            }

            GUILayout.EndHorizontal();

            return result;
        }

        public static void SetDirtyIgnore()
        {
            ignore.SetDirty();
        }

        public static bool DrawIgnoreFolder()
        {
            var change = false;
            ignore.Draw();
            return change;
        }
    }
}
