using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;

namespace UnityEngine.Experimental.Rendering.LWRP
{
    /// <summary>
    /// End XR rendering
    ///
    /// This pass disables XR rendering. Pair this pass with the BeginXRRenderingPass.
    /// If this pass is issued without a matching BeginXRRenderingPass it will lead to
    /// undefined rendering results. 
    /// </summary>
    internal class EndXRRenderingPass : ScriptableRenderPass
    {
        private readonly int eyeIndex;

        public EndXRRenderingPass(int eye)
        {
            eyeIndex = eye;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");
            
            Camera camera = renderingData.cameraData.camera;
            context.StopMultiEye(camera);

            bool isLastPass = eyeIndex == renderingData.totalEyes-1;
            context.StereoEndRender(camera, eyeIndex, isLastPass);
        }
    }
}
