using System.IO;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class CreatePBRShaderGraph
    {
        [MenuItem("Assets/Create/Shader/PBR Graph", false, 208)]
        public static void CreateMaterialGraph()
        {
            GraphUtil.CreateNewGraph(new PBRMasterNode());
        }
    }
}
