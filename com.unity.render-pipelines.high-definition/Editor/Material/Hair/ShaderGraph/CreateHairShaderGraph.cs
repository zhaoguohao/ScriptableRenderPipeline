using System.IO;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.ShaderGraph;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class CreateHairShaderGraph
    {
        [MenuItem("Assets/Create/Shader/HDRP/Hair Graph", false, 208)]
        public static void CreateMaterialGraph()
        {
            GraphUtil.CreateNewGraph(new HairMasterNode());
        }
    }
}
