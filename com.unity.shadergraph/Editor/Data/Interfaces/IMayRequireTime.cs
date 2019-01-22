using UnityEditor.Graphing;

namespace UnityEditor.ShaderGraph
{
    interface IMayRequireTime
    {
        bool RequiresTime();
    }


    static class MayRequireTimeExtensions
    {
        public static bool RequiresTime(this INode node)
        {
            return node is IMayRequireTime mayRequireTime && mayRequireTime.RequiresTime();
        }
    }
}
