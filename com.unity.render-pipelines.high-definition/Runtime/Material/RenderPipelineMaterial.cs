using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public class RenderPipelineMaterial : Object
    {
        public virtual void Build(HDRenderPipelineAsset hdAsset) {}
        public virtual void Cleanup() {}

        // Following function can be use to initialize GPU resource (once or each frame) and bind them
        public virtual void RenderInit(CommandBuffer cmd) {}
        public virtual void Bind(CommandBuffer cmd) {}
    }
}
