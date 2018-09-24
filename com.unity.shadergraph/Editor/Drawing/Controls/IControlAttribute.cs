using System.Reflection;

#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif

namespace UnityEditor.ShaderGraph.Drawing.Controls
{
    public interface IControlAttribute
    {
        VisualElement InstantiateControl(AbstractMaterialNode node, PropertyInfo propertyInfo);
    }
}
