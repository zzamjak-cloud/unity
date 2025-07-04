using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace vietlabs.fr2
{
    internal static class FR2_Helper
    {
        internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> collection)
        {
            var result = new HashSet<T>();
            if (collection == null) return result;

            foreach (T item in collection)
            {
                result.Add(item);
            }
            return result;
        }

        public static IEnumerable<GameObject> getAllObjsInCurScene()
        {
            // foreach (GameObject obj in Object.FindObjectsOfType(typeof(GameObject)))
            // {
            //    yield return obj;
            // }


            for (var j = 0; j < SceneManager.sceneCount; j++)
            {
                Scene scene = SceneManager.GetSceneAt(j);
                foreach (GameObject item in GetGameObjectsInScene(scene))
                {
                    yield return item;
                }
            }

            if (EditorApplication.isPlaying)
            {
                //dont destroy scene
                GameObject temp = null;
                try
                {
                    temp = new GameObject();
                    Object.DontDestroyOnLoad(temp);
                    Scene dontDestroyOnLoad = temp.scene;
                    Object.DestroyImmediate(temp);
                    temp = null;

                    foreach (GameObject item in GetGameObjectsInScene(dontDestroyOnLoad))
                    {
                        yield return item;
                    }
                } finally
                {
                    if (temp != null) Object.DestroyImmediate(temp);
                }
            }
        }

        private static IEnumerable<GameObject> GetGameObjectsInScene(Scene scene)
        {
            var rootObjects = new List<GameObject>();
            scene.GetRootGameObjects(rootObjects);

            // iterate root objects and do something
            for (var i = 0; i < rootObjects.Count; ++i)
            {
                GameObject gameObject = rootObjects[i];

                foreach (GameObject item in getAllChild(gameObject))
                {
                    yield return item;
                }

                yield return gameObject;
            }
        }

        public static IEnumerable<GameObject> getAllChild(GameObject target)
        {
            if (target.transform.childCount > 0)
            {
                for (var i = 0; i < target.transform.childCount; i++)
                {
                    yield return target.transform.GetChild(i).gameObject;
                    foreach (GameObject item in getAllChild(target.transform.GetChild(i).gameObject))
                    {
                        yield return item;
                    }
                }
            }
        }

        public static IEnumerable<Object> GetAllRefObjects(GameObject obj)
        {
            Component[] components = obj.GetComponents<Component>();
            foreach (Component com in components)
            {
                if (com == null) continue;

                var serialized = new SerializedObject(com);
                SerializedProperty it = serialized.GetIterator().Copy();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

                    if (it.objectReferenceValue == null) continue;

                    yield return it.objectReferenceValue;
                }
            }
        }

        public static int StringMatch(string pattern, string input)
        {
            if (input == pattern) return int.MaxValue;

            if (input.Contains(pattern)) return int.MaxValue - 1;

            var pidx = 0;
            var score = 0;
            var tokenScore = 0;

            for (var i = 0; i < input.Length; i++)
            {
                char ch = input[i];
                if (ch == pattern[pidx])
                {
                    tokenScore += tokenScore + 1; //increasing score for continuos token
                    pidx++;
                    if (pidx >= pattern.Length) break;
                } else
                {
                    tokenScore = 0;
                }

                score += tokenScore;
            }

            return score;
        }

        public static string GetfileSizeString(long fileSize)
        {
            return fileSize <= 1024
                ? fileSize + " B"
                : fileSize <= 1024 * 1024
                    ? Mathf.RoundToInt(fileSize / 1024f) + " KB"
                    : Mathf.RoundToInt(fileSize / 1024f / 1024f) + " MB";
        }
    }
}
