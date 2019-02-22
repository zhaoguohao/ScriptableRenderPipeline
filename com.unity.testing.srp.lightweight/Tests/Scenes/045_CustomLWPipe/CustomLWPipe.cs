using UnityEngine;
using UnityEngine.Rendering.LWRP;
using UnityEngine.Rendering;

public class CustomLWPipe : ScriptableRenderer
{
    private RenderOpaqueForwardPass m_RenderOpaqueForwardPass;
    const int k_DepthStencilBufferBits = 32;

    ForwardLights m_ForwardLights;

    public CustomLWPipe(CustomRenderGraphData data) : base(data)
    {
        m_RenderOpaqueForwardPass = new RenderOpaqueForwardPass(RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, -1);
        m_ForwardLights = new ForwardLights();
    }

    public override void Setup(ref RenderingData renderingData)
    {
        RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

        RenderTargetHandle colorHandle = RenderTargetHandle.CameraTarget;
        RenderTargetHandle depthHandle = RenderTargetHandle.CameraTarget;

        Camera camera = renderingData.cameraData.camera;

        for (int i = 0; i < m_RendererFeatures.Count; ++i)
        {
            m_RendererFeatures[i].AddRenderPasses(m_AdditionalRenderPasses, cameraTargetDescriptor, colorHandle, depthHandle);
        }
        m_AdditionalRenderPasses.Sort( (lhs, rhs)=>lhs.renderPassEvent.CompareTo(rhs.renderPassEvent));
        int customRenderPassIndex = 0;

        m_RenderOpaqueForwardPass.Setup(cameraTargetDescriptor, colorHandle, depthHandle, GetCameraClearFlag(camera.clearFlags), camera.backgroundColor);
        EnqueuePass(m_RenderOpaqueForwardPass);

        EnqueueAdditionalRenderPasses(RenderPassEvent.AfterRenderingOpaques, ref customRenderPassIndex,
            ref renderingData);
    }

    public override void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        m_ForwardLights.Setup(context, ref renderingData);
    }
}
