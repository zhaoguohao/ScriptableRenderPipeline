using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;

namespace UnityEngine.Experimental.Rendering.LWRP
{
    /// <summary>
    /// Start rendering that supports XR.
    ///
    /// This pass enables XR rendering. You must also configure
    /// the XR rendering in the global XR Graphics settings.
    ///
    /// Pair this pass with the EndXRRenderingPass.  If this
    /// pass is issued without a matching EndXRRenderingPass
    /// it will lead to undefined rendering results. 
    /// </summary>
    internal class BeginXRRenderingPass : ScriptableRenderPass
    {
        private readonly int eyeIndex;

        public BeginXRRenderingPass(int eye)
        {
            eyeIndex = eye;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");
            
            Camera camera = renderingData.cameraData.camera;
            context.StartMultiEye(camera, eyeIndex);
        }
    }
}
