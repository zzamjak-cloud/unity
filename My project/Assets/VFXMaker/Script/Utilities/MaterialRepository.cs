using System;
using UnityEngine;

namespace CAT.VFX.Internal
{
    /// <summary>
    /// Hash128 기반 머티리얼 캐시
    /// ObjectRepository<Material> 래퍼로 레퍼런스 카운팅 제공
    /// </summary>
    internal static class MaterialRepository
    {
        private static readonly ObjectRepository<Material> s_Repository = new ObjectRepository<Material>();

        public static int count => s_Repository.count;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            s_Repository.Clear();
        }
#endif

        public static bool Valid(Hash128 hash, Material material)
        {
            return s_Repository.Valid(hash, material);
        }

        public static void Get(Hash128 hash, ref Material material, Func<Material> onCreate)
        {
            s_Repository.Get(hash, ref material, onCreate);
        }

        public static void Get(Hash128 hash, ref Material material, string shaderName)
        {
            s_Repository.Get(hash, ref material, x => new Material(Shader.Find(x))
            {
                hideFlags = HideFlags.DontSave | HideFlags.NotEditable
            }, shaderName);
        }

        public static void Get<T>(Hash128 hash, ref Material material, Func<T, Material> onCreate, T source)
        {
            s_Repository.Get(hash, ref material, onCreate, source);
        }

        public static void Release(ref Material material)
        {
            s_Repository.Release(ref material);
        }
    }
}
