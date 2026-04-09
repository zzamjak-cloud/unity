using UnityEngine;
using UnityEngine.U2D;
#if UNITY_EDITOR
using System;
using System.Reflection;
#endif

namespace CAT.VFX.Internal
{
    /// <summary>
    /// Sprite 확장 메서드 - 실제 텍스처 참조 (아틀라스 대응)
    /// </summary>
    internal static class SpriteExtensions
    {
#if UNITY_EDITOR
        private static readonly Type s_SpriteEditorExtensionType =
            Type.GetType("UnityEditor.Experimental.U2D.SpriteEditorExtension, UnityEditor")
            ?? Type.GetType("UnityEditor.U2D.SpriteEditorExtension, UnityEditor");

        private static readonly Func<Sprite, Texture2D> s_GetActiveAtlasTextureMethod =
            (Func<Sprite, Texture2D>)Delegate.CreateDelegate(typeof(Func<Sprite, Texture2D>),
                s_SpriteEditorExtensionType
                    .GetMethod("GetActiveAtlasTexture", BindingFlags.Static | BindingFlags.NonPublic));

        private static readonly Func<Sprite, SpriteAtlas> s_GetActiveAtlasMethod =
            (Func<Sprite, SpriteAtlas>)Delegate.CreateDelegate(typeof(Func<Sprite, SpriteAtlas>),
                s_SpriteEditorExtensionType
                    .GetMethod("GetActiveAtlas", BindingFlags.Static | BindingFlags.NonPublic));

        /// <summary>
        /// 에디터/런타임에서 스프라이트의 실제 텍스처를 가져온다 (아틀라스 포함)
        /// </summary>
        public static Texture2D GetActualTexture(this Sprite self)
        {
            if (!self) return null;

            var ret = s_GetActiveAtlasTextureMethod(self);
            return ret ? ret : self.texture;
        }

        /// <summary>
        /// 스프라이트의 활성 아틀라스를 가져온다
        /// </summary>
        public static SpriteAtlas GetActiveAtlas(this Sprite self)
        {
            if (!self) return null;

            return s_GetActiveAtlasMethod(self);
        }
#else
        /// <summary>
        /// 런타임에서 스프라이트의 실제 텍스처를 가져온다
        /// </summary>
        internal static Texture2D GetActualTexture(this Sprite self)
        {
            return self ? self.texture : null;
        }
#endif
    }
}
