using System.IO;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.ShaderGraph;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class CreateDecalShaderGraph
    {
        [MenuItem("Assets/Create/Shader/HDRP/Decal Graph", false, 208)]
        public static void CreateMaterialGraph()
        {
            GraphUtil.CreateNewGraph(new DecalMasterNode());
        }
    }
}
