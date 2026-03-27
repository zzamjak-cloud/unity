using UnityEngine;

namespace CAT.VFX
{
    /// <summary>
    /// CatUIParticle 전역 설정
    /// </summary>
    internal static class CatUIParticleSettings
    {
        /// <summary>
        /// 생성된 오브젝트를 하이어라키에서 숨길지 여부
        /// </summary>
        public static readonly bool HideGeneratedObjects = true;

        /// <summary>
        /// 런타임에서 생성되는 오브젝트에 적용할 HideFlags
        /// </summary>
        public static HideFlags GlobalHideFlags => HideGeneratedObjects
            ? HideFlags.DontSave | HideFlags.NotEditable | HideFlags.HideInHierarchy | HideFlags.HideInInspector
            : HideFlags.DontSave | HideFlags.NotEditable;
    }
}
