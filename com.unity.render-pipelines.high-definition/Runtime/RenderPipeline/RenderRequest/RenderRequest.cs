using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    struct RenderRequest
    {
        public struct Target
        {
            public RenderTargetIdentifier id;
            public CubemapFace face;
            public RenderTexture copyToTarget;
        }
        public HDCamera hdCamera;
        public bool destroyCamera;
        public Target target;
        public HDCullingResults cullingResults;
        public int index;
        // Indices of render request to render before this one
        public List<int> dependsOnRenderRequestIndices;
        public CameraSettings cameraSettings;
    }
}
