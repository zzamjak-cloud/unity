using System.IO;
using UnityEngine;

namespace vietlabs.fr2
{
    internal static class FR2_GitUtil
    {
        public static bool IsGitProject()
        {
            return Directory.Exists(".git");
        }

        public static bool CheckGitIgnoreContainsFR2Cache()
        {
            if (!File.Exists(".gitignore")) return false;
            
            string[] lines = File.ReadAllLines(".gitignore");
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#")) continue;
                
                if (trimmedLine == "**/FR2_Cache.asset*" || trimmedLine == "FR2_Cache.asset*" || trimmedLine == "*FR2_Cache.asset*")
                {
                    return true;
                }
            }
            
            return false;
        }

        public static void AddFR2CacheToGitIgnore()
        {
            try
            {
                string content = File.Exists(".gitignore") ? File.ReadAllText(".gitignore") : "";
                
                // Make sure the file ends with a newline
                if (!string.IsNullOrEmpty(content) && !content.EndsWith("\n"))
                {
                    content += "\n";
                }
                
                content += "**/FR2_Cache.asset*\n";
                File.WriteAllText(".gitignore", content);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to update .gitignore: {e.Message}");
            }
        }
    }
} 