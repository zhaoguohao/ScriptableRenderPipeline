
#if UNITY_2019_1_OR_NEWER
using UnityEditor.UIElements;
#else
using UnityEditor.Experimental.UIElements;
#endif



namespace UnityEditor.ShaderGraph.Drawing
{
    public class FloatField : DoubleField
    {
        protected override string ValueToString(double v)
        {
            return ((float)v).ToString();
        }
    }
}
