using System;
using Object = UnityEngine.Object;
namespace vietlabs.fr2
{
    partial class FR2_WindowAll
    {
        [Serializable] internal class SelectHistory
        {
            public bool isSceneAssets;
            public Object[] selection;

            public bool IsTheSame(Object[] objects)
            {
                if (objects.Length != selection.Length) return false;
                var j = 0;
                for (; j < objects.Length; j++)
                {
                    if (selection[j] != objects[j]) break;
                }
                return j == objects.Length;
            }
        }
    }
}
