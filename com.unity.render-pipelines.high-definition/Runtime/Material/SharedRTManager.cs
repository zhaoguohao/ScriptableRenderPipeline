using UnityEngine.Rendering;
using System;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public class SharedRTManager
    {
        // The render target used when we do not support MSAA
        RTHandleSystem.RTHandle[] m_NormalRT = null;
        RTHandleSystem.RTHandle[] m_VelocityRT = null;
        RTHandleSystem.RTHandle m_NormalInstanced = null; // TODO VR change to tempRTs
        RTHandleSystem.RTHandle[] m_CameraDepthStencilBuffer = null;
        RTHandleSystem.RTHandle m_CameraDepthStencilInstanced = null;
        RTHandleSystem.RTHandle[] m_CameraDepthBufferMipChain;
        RTHandleSystem.RTHandle[] m_CameraStencilBufferCopy;
        HDUtils.PackedMipChainInfo m_CameraDepthBufferMipChainInfo; // This is metadata

        // The two render targets that should be used when we render in MSAA
        RTHandleSystem.RTHandle[] m_NormalMSAART = null;
        RTHandleSystem.RTHandle[] m_VelocityMSAART = null;
        RTHandleSystem.RTHandle m_NormalMSAAInstanced = null; // TODO VR change to tempRTs
        // This texture must be used because reading directly from an MSAA Depth buffer is way to expensive. The solution that we went for is writing the depth in an additional color buffer (10x cheaper to solve on ps4)
        RTHandleSystem.RTHandle[] m_DepthAsColorMSAART = null;
        RTHandleSystem.RTHandle m_DepthAsColorMSAAInstanced = null;
        RTHandleSystem.RTHandle[] m_CameraDepthStencilMSAABuffer;
        RTHandleSystem.RTHandle m_CameraDepthStencilMSAAInstanced = null;
        // This texture stores a set of depth values that are required for evaluating a bunch of effects in MSAA mode (R = Samples Max Depth, G = Samples Min Depth, G =  Samples Average Depth)
        RTHandleSystem.RTHandle[] m_CameraDepthValuesBuffer = null;

        // MSAA resolve materials
        Material m_DepthResolveMaterial  = null;
        Material m_ColorResolveMaterial = null;

        // Flags that defines if we are using a local texture or external
        bool m_ReuseGBufferMemory = false;
        bool m_VelocitySupport = false;
        bool m_MSAASupported = false;
        MSAASamples m_MSAASamples = MSAASamples.None;

        // Arrays of RTIDs that are used to set render targets (when MSAA and when not MSAA)
        protected RenderTargetIdentifier[] m_RTIDs1 = new RenderTargetIdentifier[1];
        protected RenderTargetIdentifier[] m_RTIDs2 = new RenderTargetIdentifier[2];
        protected RenderTargetIdentifier[] m_RTIDs3 = new RenderTargetIdentifier[3];

        // Property block used for the resolves
        MaterialPropertyBlock m_PropertyBlock = new MaterialPropertyBlock();

        public SharedRTManager()
        {
        }

        public void InitSharedBuffers(GBufferManager gbufferManager, RenderPipelineSettings settings, RenderPipelineResources resources)
        {
            // Set the flags
            m_MSAASupported = settings.supportMSAA;
            m_MSAASamples = m_MSAASupported ? settings.msaaSampleCount : MSAASamples.None;
            m_VelocitySupport = settings.supportMotionVectors;
            m_ReuseGBufferMemory = settings.supportedLitShaderMode != RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly;

            int numStereoPasses = XRGraphics.usingTexArray() ? XRGraphics.eyeTextureDesc.volumeDepth : 1;

            m_NormalRT = new RTHandleSystem.RTHandle[numStereoPasses];
            m_VelocityRT = new RTHandleSystem.RTHandle[numStereoPasses];
            m_CameraDepthStencilBuffer = new RTHandleSystem.RTHandle[numStereoPasses];
            m_CameraDepthBufferMipChain = new RTHandleSystem.RTHandle[numStereoPasses];
            m_CameraStencilBufferCopy = new RTHandleSystem.RTHandle[numStereoPasses];
            if (m_MSAASupported)
            {
                m_NormalMSAART = new RTHandleSystem.RTHandle[numStereoPasses];
                m_VelocityMSAART = new RTHandleSystem.RTHandle[numStereoPasses];
                m_DepthAsColorMSAART = new RTHandleSystem.RTHandle[numStereoPasses];
                m_CameraDepthStencilMSAABuffer = new RTHandleSystem.RTHandle[numStereoPasses];
                m_CameraDepthValuesBuffer = new RTHandleSystem.RTHandle[numStereoPasses];
            }
            // Instanced buffer for forward pass
            if (XRGraphics.usingTexArray())
            {
                m_CameraDepthStencilInstanced = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.Depth32, colorFormat: RenderTextureFormat.Depth, filterMode: FilterMode.Point, name: "CameraDepthStencilInstanced", useInstancing: true);
                m_NormalInstanced = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGB32, sRGB: false, enableRandomWrite: true, name: "NormalBufferInstanced", useInstancing: true); // TODO VR convert to TempRTs
                if (m_MSAASupported)
                {
                    m_CameraDepthStencilMSAAInstanced = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.Depth24, colorFormat: RenderTextureFormat.Depth, filterMode: FilterMode.Point, bindTextureMS: true, enableMSAA: true, name: "CameraDepthStencilMSAAInstanced", useInstancing: true);
                    m_NormalMSAAInstanced = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGB32, sRGB: false, enableMSAA: true, bindTextureMS: true, name: "NormalBufferMSAAInstanced", useInstancing: true); // TODO VR convert to TempRTs
                    m_DepthAsColorMSAAInstanced = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.RFloat, sRGB: false, bindTextureMS: true, enableMSAA: true, name: "DepthAsColorMSAAInstanced", useInstancing: true); // TODO VR convert to TempRTs
                }
            }


            for (int vrPass = 0; vrPass < numStereoPasses; vrPass++)
            {
                // Create the depth/stencil buffer
                m_CameraDepthStencilBuffer[vrPass] = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.Depth32, colorFormat: RenderTextureFormat.Depth, filterMode: FilterMode.Point, name: "CameraDepthStencil");

                // Create the mip chain buffer
                m_CameraDepthBufferMipChainInfo = new HDUtils.PackedMipChainInfo();
                m_CameraDepthBufferMipChainInfo.Allocate();
                m_CameraDepthBufferMipChain[vrPass] = RTHandles.Alloc(ComputeDepthBufferMipChainSize, colorFormat: RenderTextureFormat.RFloat, filterMode: FilterMode.Point, sRGB: false, enableRandomWrite: true, name: "CameraDepthBufferMipChain");

                // Technically we won't need this buffer in some cases, but nothing that we can determine at init time.
                m_CameraStencilBufferCopy[vrPass] = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.None, colorFormat: RenderTextureFormat.R8, sRGB: false, filterMode: FilterMode.Point, enableRandomWrite: true, name: "CameraStencilCopy"); // DXGI_FORMAT_R8_UINT is not supported by Unity

                if (m_VelocitySupport)
                {
                    m_VelocityRT[vrPass] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetVelocityBufferFormat(), sRGB: Builtin.GetVelocityBufferSRGBFlag(), name: "Velocity");
                    if (m_MSAASupported)
                    {
                        m_VelocityMSAART[vrPass] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetVelocityBufferFormat(), sRGB: Builtin.GetVelocityBufferSRGBFlag(), enableMSAA: true, bindTextureMS: true, name: "VelocityMSAA");
                    }
                }

                // Allocate the additional textures only if MSAA is supported
                if (m_MSAASupported)
                {
                    // Let's create the MSAA textures
                    m_CameraDepthStencilMSAABuffer[vrPass] = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.Depth24, colorFormat: RenderTextureFormat.Depth, filterMode: FilterMode.Point, bindTextureMS: true, enableMSAA: true, name: "CameraDepthStencilMSAA");
                    m_CameraDepthValuesBuffer[vrPass] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGBFloat, sRGB: false, name: "DepthValuesBuffer");
                    m_DepthAsColorMSAART[vrPass] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.RFloat, sRGB: false, bindTextureMS: true, enableMSAA: true, name: "DepthAsColorMSAA");

                    // We need to allocate this texture as long as msaa is supported because on both mode, one of the cameras can be forward only using the framesettings
                    m_NormalMSAART[vrPass] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGB32, sRGB: false, enableMSAA: true, bindTextureMS: true, name: "NormalBufferMSAA");

                    // Create the required resolve materials
                    m_DepthResolveMaterial = CoreUtils.CreateEngineMaterial(resources.shaders.depthValuesPS);
                    m_ColorResolveMaterial = CoreUtils.CreateEngineMaterial(resources.shaders.colorResolvePS);
                }

                // If we are in the forward only mode
                if (!m_ReuseGBufferMemory)
                {
                    // In case of full forward we must allocate the render target for normal buffer (or reuse one already existing)
                    // TODO: Provide a way to reuse a render target
                    m_NormalRT[vrPass] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGB32, sRGB: false, enableRandomWrite: true, name: "NormalBuffer");
                }
                else
                {
                    // When not forward only we should are using the normal buffer of the gbuffer
                    // In case of deferred, we must be in sync with NormalBuffer.hlsl and lit.hlsl files and setup the correct buffers
                    m_NormalRT[vrPass] = gbufferManager.GetNormalBuffer(vrPass); // Normal + Roughness
                }
            }
        }

        public bool IsConsolePlatform()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.PlayStation4 ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.XboxOne ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.XboxOneD3D12;
        }

        // Function that will return the set of buffers required for the prepass (depending on if msaa is enabled or not)
        public RenderTargetIdentifier[] GetPrepassBuffersRTI(FrameSettings frameSettings, int vrPass = 0)
        {
            if (frameSettings.enableMSAA)
            {
                Debug.Assert(m_MSAASupported);
                m_RTIDs2[0] = m_NormalMSAART[vrPass].nameID;
                m_RTIDs2[1] = m_DepthAsColorMSAART[vrPass].nameID;
                return m_RTIDs2;
            }
            else
            {
                m_RTIDs1[0] = m_NormalRT[vrPass].nameID;
                return m_RTIDs1;
            }
        }

        public RenderTargetIdentifier[] GetInstancedPrepassBuffersRTI(FrameSettings frameSettings)
        {
            if (XRGraphics.usingTexArray())
            {
                if (frameSettings.enableMSAA)
                { // TODO VR: use GetTemporaryRT (then release these temp buffers after blitting them) to save memory
                    Debug.Assert(m_MSAASupported);
                    m_RTIDs2[0] = m_NormalMSAAInstanced.nameID;
                    m_RTIDs2[1] = m_DepthAsColorMSAAInstanced.nameID;
                    return m_RTIDs2;
                }
                else
                {
                    m_RTIDs1[0] = m_NormalInstanced.nameID;
                    return m_RTIDs1;
                }
            }
            else
            {
                return GetPrepassBuffersRTI(frameSettings);
            }
        }

        public void BlitInstancedPrepassBuffersRTI(FrameSettings frameSettings, CommandBuffer cmd)
        {
            if (!XRGraphics.usingTexArray())
                return;
            if (frameSettings.enableMSAA)
            {
                for (int vrPass = 0; vrPass < XRGraphics.numPass(); vrPass++)
                {
                    cmd.Blit(m_NormalMSAAInstanced, m_NormalMSAART[vrPass], vrPass, 0);
                    cmd.Blit(m_DepthAsColorMSAAInstanced, m_DepthAsColorMSAART[vrPass], vrPass, 0);
                }

                // TODOVR deallocate instanced Normal and DepthAsColor
            }
            else
            {
                for (int vrPass = 0; vrPass < XRGraphics.numPass(); vrPass++)
                {
                    cmd.Blit(m_NormalInstanced, m_NormalRT[vrPass], vrPass, 0);
                }

                // TODOVR deallocate instanced Normal and DepthAsColor
            }
        }

        // Function that will return the set of buffers required for the motion vector pass
        public RenderTargetIdentifier[] GetVelocityPassBuffersRTI(FrameSettings frameSettings, int vrPass = 0)
        {
            Debug.Assert(m_VelocitySupport);
            if (frameSettings.enableMSAA)
            {
                Debug.Assert(m_MSAASupported);
                m_RTIDs3[0] = m_VelocityMSAART[vrPass].nameID;
                m_RTIDs3[1] = m_NormalMSAART[vrPass].nameID;
                m_RTIDs3[2] = m_DepthAsColorMSAART[vrPass].nameID;
                return m_RTIDs3;
            }
            else
            {
                Debug.Assert(m_VelocitySupport);
                m_RTIDs2[0] = m_VelocityRT[vrPass].nameID;
                m_RTIDs2[1] = m_NormalRT[vrPass].nameID;
                return m_RTIDs2;
            }
        }

        // Request the normal buffer (MSAA or not)
        public RTHandleSystem.RTHandle GetNormalBuffer(bool isMSAA = false, int vrPass = 0)
        {
            if (isMSAA)
            {
                Debug.Assert(m_MSAASupported);
                return m_NormalMSAART[vrPass];
            }
            else
            {
                return m_NormalRT[vrPass];
            }
        }

        // Request the velocity buffer (MSAA or not)
        public RTHandleSystem.RTHandle GetVelocityBuffer(bool isMSAA = false, int vrPass = 0)
        {
            Debug.Assert(m_VelocitySupport);
            if (isMSAA)
            {
                Debug.Assert(m_MSAASupported);
                return m_VelocityMSAART[vrPass];
            }
            else
            {
                return m_VelocityRT[vrPass];
            }
        }

        // Request the depth stencil buffer (MSAA or not)
        public RTHandleSystem.RTHandle GetDepthStencilBuffer(bool isMSAA = false, int vrPass = 0)
        {
            if (isMSAA)
            {
                Debug.Assert(m_MSAASupported);
                return m_CameraDepthStencilMSAABuffer[vrPass];
            }
            else
            {
                return m_CameraDepthStencilBuffer[vrPass];
            }
        }
        public RTHandleSystem.RTHandle GetInstancedDepthStencilBuffer(bool isMSAA = false)
        {
            if (XRGraphics.usingTexArray())
            {
                if (isMSAA)
                {
                    Debug.Assert(m_MSAASupported);
                    return m_CameraDepthStencilMSAAInstanced;
                }
                else
                {
                    return m_CameraDepthStencilInstanced;
                }
            }
            else
            {
                return GetDepthStencilBuffer(isMSAA);
            }
        }

        public RTHandleSystem.RTHandle GetInstancedMSAADepthTexture()
        {
            if (XRGraphics.usingTexArray())
                return m_DepthAsColorMSAAInstanced;
            return GetDepthTexture(true);
        }
        // Request the depth texture (MSAA or not)
        public RTHandleSystem.RTHandle GetDepthTexture(bool isMSAA = false, int vrPass = 0)
        {
            if (isMSAA)
            {
                Debug.Assert(m_MSAASupported);
                return m_DepthAsColorMSAART[vrPass];
            }
            else
            {
                return m_CameraDepthBufferMipChain[vrPass];
            }
        }

        public RTHandleSystem.RTHandle GetDepthValuesTexture(int vrPass = 0)
        {
            Debug.Assert(m_MSAASupported);
            return m_CameraDepthValuesBuffer[vrPass];
        }

        public void SetNumMSAASamples(MSAASamples msaaSamples)
        {
            m_MSAASamples = msaaSamples;
        }

        public RTHandleSystem.RTHandle GetStencilBufferCopy(int vrPass = 0)
        {
            return m_CameraStencilBufferCopy[vrPass];
        }

        public Vector2Int ComputeDepthBufferMipChainSize(Vector2Int screenSize)
        {
            m_CameraDepthBufferMipChainInfo.ComputePackedMipChainInfo(screenSize);
            return m_CameraDepthBufferMipChainInfo.textureSize;
        }

        public HDUtils.PackedMipChainInfo GetDepthBufferMipChainInfo()
        {
            return m_CameraDepthBufferMipChainInfo;
        }

        public void Build(HDRenderPipelineAsset hdAsset)
        {
        }

        public void Cleanup()
        {
            if (XRGraphics.usingTexArray())
            {
                RTHandles.Release(m_CameraDepthStencilInstanced);
                RTHandles.Release(m_NormalInstanced);
                if (m_MSAASupported)
                {
                    RTHandles.Release(m_CameraDepthStencilMSAAInstanced);
                    RTHandles.Release(m_DepthAsColorMSAAInstanced);
                }
            }

            int numStereoPasses = XRGraphics.usingTexArray() ? XRGraphics.eyeTextureDesc.volumeDepth : 1;
            for (int vrPass = 0; vrPass < numStereoPasses; vrPass++)
            {
                if (!m_ReuseGBufferMemory)
                {
                    RTHandles.Release(m_NormalRT[vrPass]);
                }

                if (m_VelocitySupport)
                {
                    RTHandles.Release(m_VelocityRT[vrPass]);
                    if (m_MSAASupported)
                    {
                        RTHandles.Release(m_VelocityMSAART[vrPass]);
                    }
                }

                RTHandles.Release(m_CameraDepthStencilBuffer[vrPass]);
                RTHandles.Release(m_CameraDepthBufferMipChain[vrPass]);
                RTHandles.Release(m_CameraStencilBufferCopy[vrPass]);

                if (m_MSAASupported)
                {
                    RTHandles.Release(m_CameraDepthValuesBuffer[vrPass]);
                    RTHandles.Release(m_CameraDepthStencilMSAABuffer[vrPass]);

                    RTHandles.Release(m_NormalMSAART[vrPass]);
                    RTHandles.Release(m_DepthAsColorMSAART[vrPass]);

                    // Do not forget to release the materials
                    if (vrPass == 1)
                    {
                        CoreUtils.Destroy(m_DepthResolveMaterial);
                        CoreUtils.Destroy(m_ColorResolveMaterial);
                    }
                }
            }
        }

        public static int SampleCountToPassIndex(MSAASamples samples)
        {
            switch (samples)
            {
                case MSAASamples.None:
                    return 0;
                case MSAASamples.MSAA2x:
                    return 1;
                case MSAASamples.MSAA4x:
                    return 2;
                case MSAASamples.MSAA8x:
                    return 3;
            };
            return 0;
        }


        // Bind the normal buffer that is needed
        public void BindNormalBuffer(CommandBuffer cmd, bool isMSAA = false)
        {
            // NormalBuffer can be access in forward shader, so need to set global texture
            cmd.SetGlobalTexture(HDShaderIDs._NormalBufferTexture, GetNormalBuffer(isMSAA));
        }

        public void ResolveSharedRT(CommandBuffer cmd, HDCamera hdCamera, int vrPass = 0)
        {
            if (hdCamera.frameSettings.enableMSAA)
            {
                Debug.Assert(m_MSAASupported);
                using (new ProfilingSample(cmd, "ComputeDepthValues", CustomSamplerId.VolumeUpdate.GetSampler()))
                {
                    // Grab the RTIs and set the output render targets
                    m_RTIDs2[0] = m_CameraDepthValuesBuffer[vrPass].nameID;
                    m_RTIDs2[1] = m_NormalRT[vrPass].nameID;
                    HDUtils.SetRenderTarget(cmd, hdCamera, m_RTIDs2, m_CameraDepthStencilBuffer[vrPass]);

                    // Set the input textures
                    Shader.SetGlobalTexture(HDShaderIDs._NormalTextureMS, m_NormalMSAART[vrPass]);
                    Shader.SetGlobalTexture(HDShaderIDs._DepthTextureMS, m_DepthAsColorMSAART[vrPass]);

                    // Resolve the depth and normal buffers
                    cmd.DrawProcedural(Matrix4x4.identity, m_DepthResolveMaterial, SampleCountToPassIndex(m_MSAASamples), MeshTopology.Triangles, 3, 1);
                }
            }
        }
        public void ResolveMSAAColor(CommandBuffer cmd, HDCamera hdCamera, RTHandleSystem.RTHandle msaaTarget, RTHandleSystem.RTHandle simpleTarget)
        {
            if (hdCamera.frameSettings.enableMSAA)
            {
                Debug.Assert(m_MSAASupported);
                using (new ProfilingSample(cmd, "ResolveColor", CustomSamplerId.VolumeUpdate.GetSampler()))
                {
                    // Grab the RTIs and set the output render targets
                    HDUtils.SetRenderTarget(cmd, hdCamera, simpleTarget);

                    // Set the input textures
                    m_PropertyBlock.SetTexture(HDShaderIDs._ColorTextureMS, msaaTarget);

                    // Resolve the depth and normal buffers
                    cmd.DrawProcedural(Matrix4x4.identity, m_ColorResolveMaterial, SampleCountToPassIndex(m_MSAASamples), MeshTopology.Triangles, 3, 1, m_PropertyBlock);
                }
            }
        }
    }
}
