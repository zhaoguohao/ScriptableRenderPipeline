#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif

namespace UnityEditor.Graphing
{
    public interface IHasSettings
    {
        VisualElement CreateSettingsElement();
    }
}
