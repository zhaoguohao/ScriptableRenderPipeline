namespace UnityEngine.Rendering.LWRP
{
    /// <summary>
    /// Render all opaque forward objects into the given color and depth target
    ///
    /// You can use this pass to render objects that have a material and/or shader
    /// with the pass names LightweightForward or SRPDefaultUnlit. The pass only
    /// renders objects in the rendering queue range of Opaque objects.
    /// </summary>
    internal class RenderOpaqueForwardPass : ScriptableRenderPass
    {
        AttachmentDescriptor m_ColorAtttachment;
        AttachmentDescriptor m_DepthAttachment;

        FilteringSettings m_FilteringSettings;
        string m_ProfilerTag = "Render Opaques";

        public RenderOpaqueForwardPass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            RegisterShaderPassName("LightweightForward");
            RegisterShaderPassName("SRPDefaultUnlit");
            renderPassEvent = evt;
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_ColorAtttachment = new AttachmentDescriptor(RenderTextureFormat.ARGB32);
            m_DepthAttachment = new AttachmentDescriptor(RenderTextureFormat.Depth);
        }

        /// <summary>
        /// Configure the pass before execution
        /// </summary>
        /// <param name="baseDescriptor">Current target descriptor</param>
        /// <param name="colorAttachmentHandle">Color attachment to render into</param>
        /// <param name="depthAttachmentHandle">Depth attachment to render into</param>
        /// <param name="clearFlag">Camera clear flag</param>
        /// <param name="clearColor">Camera clear color</param>
        /// <param name="configuration">Specific render configuration</param>
        public void Setup(
            RenderTextureDescriptor baseDescriptor,
            RenderTargetHandle colorAttachmentHandle,
            RenderTargetHandle depthAttachmentHandle,
            ClearFlag clearFlag,
            Color clearColor)
        {
            m_ColorAtttachment.format = baseDescriptor.colorFormat;
            m_ColorAtttachment.ConfigureClear(CoreUtils.ConvertSRGBToActiveColorSpace(clearColor));
            m_DepthAttachment.ConfigureClear(Color.black);
            m_ColorAtttachment.ConfigureTarget(colorAttachmentHandle.Identifier(), clearFlag == ClearFlag.None, true);
            m_DepthAttachment.ConfigureTarget(depthAttachmentHandle.Identifier(), clearFlag == ClearFlag.None, true);
            ConfigureRenderTarget(baseDescriptor.width, baseDescriptor.height, baseDescriptor.msaaSamples, m_ColorAtttachment,
                m_DepthAttachment);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingSample(cmd, m_ProfilerTag))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                Camera camera = renderingData.cameraData.camera;
                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(ref renderingData, sortFlags);
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);

                // Render objects that did not match any shader pass with error shader
                RenderObjectsWithError(context, ref renderingData.cullResults, camera, m_FilteringSettings, SortingCriteria.None);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
