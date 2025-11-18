using UnityEngine;

namespace CAT.UI
{
    /// <summary>
    /// ReadOnly 속성을 위한 에디터 전용 속성
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute
    {
        public ReadOnlyAttribute() { }
    }
}