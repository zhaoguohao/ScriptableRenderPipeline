using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;

namespace UnityEngine.Experimental.Rendering.LWRP
{
    /// <summary>
    /// Set up camera properties for the current pass.
    ///
    /// This pass is used to configure shader uniforms and other unity properties that are required for rendering.
    /// * Setup Camera RenderTarget and Viewport
    /// * VR Camera Setup and SINGLE_PASS_STEREO props
    /// * Setup camera view, projection and their inverse matrices.
    /// * Setup properties: _WorldSpaceCameraPos, _ProjectionParams, _ScreenParams, _ZBufferParams, unity_OrthoParams
    /// * Setup camera world clip planes properties
    /// * Setup HDR keyword
    /// * Setup global time properties (_Time, _SinTime, _CosTime)
    /// </summary>
    internal class SetupForwardRenderingPass : ScriptableRenderPass
    {
        private int eyeIndex = 0;

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");

            context.SetupCameraProperties(renderingData.cameraData.camera, renderingData.cameraData.isStereoEnabled, eyeIndex);
            ++eyeIndex;
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            eyeIndex = 0;
        }
    }
}
