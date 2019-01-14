using System.IO;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class CreateUnlitShaderGraph
    {
        [MenuItem("Assets/Create/Shader/Unlit Graph", false, 208)]
        public static void CreateMaterialGraph()
        {
            GraphUtil.CreateNewGraph(new UnlitMasterNode());
        }
    }
}
