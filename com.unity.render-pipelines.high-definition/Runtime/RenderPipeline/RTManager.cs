using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public enum RT
    {
        DepthStencil,
        DepthMipChain,
        StencilCopy,
        Velocity,
        Color,
        Normal,
        SssDiffuseLighting,
        Distortion,
        ScreenSpaceShadow,
        SSRHitPoint,
        SSRLighting,
        AfterPostProcessOffscreen,

        GBuffer0,
        GBuffer1,
        GBuffer2,
        GBuffer3,
        GBuffer4,
        GBuffer5,
        GBuffer6,
        GBuffer7,

        // Aliases
        LightLayers,
        ShadowMask,

        SSS0,
        SSS1,
        SSS2,
        SSS3,
        SSSHTile,
        SSSFiltering,

        DBuffer0,
        DBuffer1,
        DBuffer2,
        DBuffer3,
        DBufferHTile,

        // MSAA specific
        DepthStencilMSAA,
        DepthValuesMSAA,
        DepthAsColorMSAA,
        ColorMSAA,
        NormalMSAA,
        SssDiffuseLightingMSAA,
        VelocityMSAA,
        SSSMSAA0,
        SSSMSAA1,
        SSSMSAA2,
        SSSMSAA3,

        // Debug specific
        DebugColorPicker,
        DebugFullScreen,
        IntermediateAfterPostProcess,
        SSRDebug,

        Count
    }

    public class RTManager
    {
        // Global texture list
        RTHandleSystem.RTHandle[]   m_RenderTargets = new RTHandleSystem.RTHandle[(int)RT.Count];
        bool[]                      m_RenderTargetIsShared = new bool[(int)RT.Count];

        // GBuffer
        protected int m_GBufferCount = 0;
        // As this will probably not change, we don't really need a more generic approach here.
        RenderTargetIdentifier[] m_GBufferRTI;
        RenderTargetIdentifier[] m_GBufferShadowMaskRTI;
        RenderTargetIdentifier[] m_GBufferLightLayersRTI;
        RenderTargetIdentifier[] m_GBufferShadowMaskLightLayerRTI;

        // DBuffer
        RenderTargetIdentifier[] m_DBufferRTI;

        HDUtils.PackedMipChainInfo m_CameraDepthBufferMipChainInfo; // This is metadata
        RenderTargetIdentifier[] m_PrepassRTI;
        RenderTargetIdentifier[] m_VelocityPassRTI;

        // MSAA
        RenderTargetIdentifier[] m_ResolveMSAADepthNormalRTI;
        RenderTargetIdentifier[] m_MSAAPrepassRTI;
        RenderTargetIdentifier[] m_MSAAVelocityPassRTI;

        // Public interface
        public void Initialize(HDRenderPipelineAsset hdrpAsset)
        {
            InitializeRenderTextures(hdrpAsset);
        }

        public void Release()
        {
            ReleaseRenderTextures();
        }

        public virtual void BindGBufferTextures(CommandBuffer cmd)
        {
            for (int i = 0; i < m_GBufferCount; ++i)
            {
                cmd.SetGlobalTexture(HDShaderIDs._GBufferTexture[i], GetRenderTarget((RT)((int)RT.GBuffer0 + i)));
            }

            var shadowMask = GetRenderTarget(RT.ShadowMask);
            if (shadowMask != null)
                cmd.SetGlobalTexture(HDShaderIDs._ShadowMaskTexture, shadowMask);

            var lightLayers = GetRenderTarget(RT.LightLayers);
            cmd.SetGlobalTexture(HDShaderIDs._LightLayersTexture, lightLayers ?? (Texture)Texture2D.whiteTexture);
        }

        public virtual RenderTargetIdentifier[] GetGBufferRTI(FrameSettings frameSettings)
        {
            bool shadowMask = frameSettings.IsEnabled(FrameSettingsField.ShadowMask);
            bool lightLayers = frameSettings.IsEnabled(FrameSettingsField.LightLayers);

            if (shadowMask && lightLayers)
                return m_GBufferShadowMaskLightLayerRTI;
            else if (shadowMask)
                return m_GBufferShadowMaskRTI;
            else if (lightLayers)
                return m_GBufferLightLayersRTI;
            else
                return m_GBufferRTI;
        }

        public virtual RenderTargetIdentifier[] GetDBufferRTI()
        {
            return m_DBufferRTI;
        }

        public RenderTargetIdentifier[] GetResolveMSAADepthNormalRTI()
        {
            return m_ResolveMSAADepthNormalRTI;
        }

        public RenderTargetIdentifier[] GetPrepassRTI(FrameSettings frameSettings)
        {
            return frameSettings.IsEnabled(FrameSettingsField.MSAA) ? m_MSAAPrepassRTI : m_PrepassRTI;
        }

        public RenderTargetIdentifier[] GetVelocityPassRTI(FrameSettings frameSettings)
        {
            return frameSettings.IsEnabled(FrameSettingsField.MSAA) ? m_MSAAVelocityPassRTI : m_VelocityPassRTI;
        }

        // Request the normal buffer (MSAA or not)
        public RTHandleSystem.RTHandle GetNormalBuffer(bool isMSAA = false)
        {
            return isMSAA ? GetRenderTarget(RT.NormalMSAA) : GetRenderTarget(RT.Normal);
        }

        // Request the velocity buffer (MSAA or not)
        public RTHandleSystem.RTHandle GetVelocityBuffer(bool isMSAA = false)
        {
            return isMSAA ? GetRenderTarget(RT.VelocityMSAA) : GetRenderTarget(RT.Velocity);
        }

        // Request the depth stencil buffer (MSAA or not)
        public RTHandleSystem.RTHandle GetDepthStencilBuffer(bool isMSAA = false)
        {
            return isMSAA ? GetRenderTarget(RT.DepthStencilMSAA) : GetRenderTarget(RT.DepthStencil);
        }

        // Request the depth texture (MSAA or not)
        public RTHandleSystem.RTHandle GetDepthTexture(bool isMSAA = false)
        {
            return isMSAA ? GetRenderTarget(RT.DepthAsColorMSAA) : GetRenderTarget(RT.DepthMipChain);
        }

        public RTHandleSystem.RTHandle GetSSSBuffer(int index)
        {
            Debug.Assert(index < SubsurfaceScatteringManager.sssBufferCount);
            return m_RenderTargets[(int)RT.SSS0 + index];
        }

        public RTHandleSystem.RTHandle GetSSSBufferMSAA(int index)
        {
            Debug.Assert(index < SubsurfaceScatteringManager.sssBufferCount);
            return m_RenderTargets[(int)RT.SSSMSAA0 + index];
        }

        public RTHandleSystem.RTHandle GetRenderTarget(RT rt)
        {
            return m_RenderTargets[(int)rt];
        }

        protected virtual void InitializeMainBuffers(RenderPipelineSettings settings)
        {
            // Depth/Stencil buffer
            m_CameraDepthBufferMipChainInfo = new HDUtils.PackedMipChainInfo();
            m_CameraDepthBufferMipChainInfo.Allocate();
            m_RenderTargets[(int)RT.DepthStencil] = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.Depth32, filterMode: FilterMode.Point, xrInstancing: true, useDynamicScale: true, name: "CameraDepthStencil");
            m_RenderTargets[(int)RT.DepthMipChain] = RTHandles.Alloc(ComputeDepthBufferMipChainSize, colorFormat: GraphicsFormat.R32_SFloat, filterMode: FilterMode.Point, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "CameraDepthBufferMipChain");
            // Technically we won't need this buffer in some cases, but nothing that we can determine at init time.
            m_RenderTargets[(int)RT.StencilCopy] = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.None, colorFormat: GraphicsFormat.R8_UNorm, filterMode: FilterMode.Point, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "CameraStencilCopy"); // DXGI_FORMAT_R8_UINT is not supported by Unity

            m_RenderTargets[(int)RT.Color] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite: true, useMipMap: false, xrInstancing: true, useDynamicScale: true, name: "CameraColor");
            m_RenderTargets[(int)RT.SssDiffuseLighting] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.B10G11R11_UFloatPack32, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "CameraSSSDiffuseLighting");
            m_RenderTargets[(int)RT.Distortion] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetDistortionBufferFormat(), xrInstancing: true, useDynamicScale: true, name: "Distortion");

            if (settings.supportMotionVectors)
            {
                m_RenderTargets[(int)RT.Velocity] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetVelocityBufferFormat(), xrInstancing: true, useDynamicScale: true, name: "Velocity");
                if (settings.supportMSAA)
                {
                    m_RenderTargets[(int)RT.VelocityMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetVelocityBufferFormat(), enableMSAA: true, bindTextureMS: true, xrInstancing: true, useDynamicScale: true, name: "VelocityMSAA");
                }
            }

            if (settings.supportedLitShaderMode == RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly)
            {
                // In case of full forward we must allocate the render target for normal buffer (or reuse one already existing)
                // TODO: Provide a way to reuse a render target
                m_RenderTargets[(int)RT.Normal] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "NormalBuffer");
            }

            // Use RG16 as we only have one deferred directional and one screen space shadow light currently
            m_RenderTargets[(int)RT.ScreenSpaceShadow] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "ScreenSpaceShadowsBuffer");
        }

        protected virtual void InitializeDBuffer(RenderPipelineSettings settings)
        {
            if (settings.supportDecals)
            {
                int bufferCount = settings.decalSettings.perChannelMask ? 4 : 3;

                GraphicsFormat[] rtFormat;
                Decal.GetMaterialDBufferDescription(out rtFormat);
                m_DBufferRTI = new RenderTargetIdentifier[bufferCount];

                for (int dbufferIndex = 0; dbufferIndex < bufferCount; ++dbufferIndex)
                {
                    m_RenderTargets[(int)RT.DBuffer0 + dbufferIndex] = RTHandles.Alloc(Vector2.one, colorFormat: rtFormat[dbufferIndex], filterMode: FilterMode.Point, xrInstancing: true, useDynamicScale: true, name: string.Format("DBuffer{0}", dbufferIndex));
                    m_DBufferRTI[dbufferIndex] = m_RenderTargets[(int)RT.DBuffer0 + dbufferIndex];
                }

                // We use 8x8 tiles in order to match the native GCN HTile as closely as possible.
                m_RenderTargets[(int)RT.DBufferHTile] = RTHandles.Alloc(size => new Vector2Int((size.x + 7) / 8, (size.y + 7) / 8), filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "DBufferHTile"); // Enable UAV
            }
        }

        protected virtual void InitializeGBuffer(RenderPipelineSettings settings)
        {
            if (settings.supportedLitShaderMode != RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly)
            {
                m_GBufferCount = 4; // ShadowMask and LightLayers are handled specifically
                m_RenderTargets[(int)RT.GBuffer0] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_SRGB, xrInstancing: true, useDynamicScale: true, enableRandomWrite: false, name: string.Format("GBuffer0"));
                m_RenderTargets[(int)RT.GBuffer1] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, xrInstancing: true, useDynamicScale: true, enableRandomWrite: true, name: string.Format("GBuffer1"));
                m_RenderTargets[(int)RT.GBuffer2] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, xrInstancing: true, useDynamicScale: true, enableRandomWrite: false, name: string.Format("GBuffer2"));
                m_RenderTargets[(int)RT.GBuffer3] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetLightingBufferFormat(), xrInstancing: true, useDynamicScale: true, enableRandomWrite: false, name: string.Format("GBuffer3"));

                if (settings.supportShadowMask)
                {
                    m_RenderTargets[(int)RT.GBuffer4] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetShadowMaskBufferFormat(), xrInstancing: true, useDynamicScale: true, enableRandomWrite: false, name: string.Format("GBuffer4"));
                    ShareRT(RT.ShadowMask, RT.GBuffer4); // Alias
                }

                if (settings.supportLightLayers)
                {
                    m_RenderTargets[(int)RT.GBuffer5] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, xrInstancing: true, useDynamicScale: true, enableRandomWrite: false, name: string.Format("GBuffer5"));
                    ShareRT(RT.LightLayers, RT.GBuffer5); // Alias
                }

                ShareRT(RT.Normal, RT.GBuffer1);

                // Init MRT arrays
                m_GBufferRTI = new RenderTargetIdentifier[m_GBufferCount];
                m_GBufferShadowMaskRTI = new RenderTargetIdentifier[m_GBufferCount + 1];
                m_GBufferLightLayersRTI = new RenderTargetIdentifier[m_GBufferCount + 1];
                m_GBufferShadowMaskLightLayerRTI = new RenderTargetIdentifier[m_GBufferCount + 2];

                for (int i = 0; i < m_GBufferCount; ++i)
                {
                    m_GBufferRTI[i] = m_RenderTargets[(int)RT.GBuffer0 + i];
                    m_GBufferShadowMaskRTI[i] = m_RenderTargets[(int)RT.GBuffer0 + i];
                    m_GBufferLightLayersRTI[i] = m_RenderTargets[(int)RT.GBuffer0 + i];
                    m_GBufferShadowMaskLightLayerRTI[i] = m_RenderTargets[(int)RT.GBuffer0 + i];
                }

                if (settings.supportShadowMask)
                {
                    m_GBufferShadowMaskRTI[m_GBufferCount] = m_RenderTargets[(int)RT.ShadowMask];
                    m_GBufferShadowMaskLightLayerRTI[m_GBufferCount + 1] = m_RenderTargets[(int)RT.ShadowMask];
                }

                if (settings.supportLightLayers)
                {
                    m_GBufferLightLayersRTI[m_GBufferCount] = m_RenderTargets[(int)RT.LightLayers];
                    m_GBufferShadowMaskLightLayerRTI[m_GBufferCount] = m_RenderTargets[(int)RT.LightLayers];
                }
            }
        }

        protected virtual void InitializeSSRBuffers(RenderPipelineSettings settings)
        {
            if (settings.supportSSR)
            {
                // m_SsrDebugTexture    = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGBFloat, sRGB: false, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "SSR_Debug_Texture");
                m_RenderTargets[(int)RT.SSRHitPoint] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "SSR_Hit_Point_Texture");
                m_RenderTargets[(int)RT.SSRLighting] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "SSR_Lighting_Texture");

                //if (settings.supportSSR)
                //    m_RenderTargets[(int)RT.SSRDebug] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: RenderTextureFormat.ARGBFloat, sRGB: false, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "SSR_Debug_Texture");
            }
        }

        protected virtual void InitializeSSSBuffers(RenderPipelineSettings settings)
        {
            // For now we only support one input buffer for SSS so it's hardcoded here
            if (settings.supportedLitShaderMode == RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly) //forward only
            {
                // In case of full forward we must allocate the render target for forward SSS (or reuse one already existing)
                // TODO: Provide a way to reuse a render target
                m_RenderTargets[(int)RT.SSS0] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_SRGB, xrInstancing: true, useDynamicScale: true, name: "SSSBuffer0");
            }
            else
            {
                // In case of deferred, we must be in sync with SubsurfaceScattering.hlsl and lit.hlsl files and setup the correct buffers
                ShareRT(RT.SSS0, RT.GBuffer0);
            }

            // We need to allocate the texture if we are in forward or both in case one of the cameras is in enable forward only mode
            if (settings.supportMSAA)
            {
                m_RenderTargets[(int)RT.SSSMSAA0] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_SRGB, enableMSAA: true, bindTextureMS: true, xrInstancing: true, useDynamicScale: true, name: "SSSBufferMSAA0");
            }

            if (SubsurfaceScatteringManager.NeedTemporarySubsurfaceBuffer() || settings.supportMSAA)
            {
                // Caution: must be same format as RT.SssDiffuseLightingBuffer
                m_RenderTargets[(int)RT.SSSFiltering] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.B10G11R11_UFloatPack32, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "SSSCameraFiltering"); // Enable UAV
            }

            // We use 8x8 tiles in order to match the native GCN HTile as closely as possible.
            m_RenderTargets[(int)RT.SSSHTile] = RTHandles.Alloc(size => new Vector2Int((size.x + 7) / 8, (size.y + 7) / 8), filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "SSSHtile"); // Enable UAV

        }

        protected virtual void InitializeMSAABuffers(RenderPipelineSettings settings)
        {
            if (settings.supportMSAA)
            {
                m_RenderTargets[(int)RT.DepthStencilMSAA] = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.Depth24, filterMode: FilterMode.Point, bindTextureMS: true, enableMSAA: true, xrInstancing: true, useDynamicScale: true, name: "CameraDepthStencilMSAA");
                m_RenderTargets[(int)RT.DepthValuesMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R32G32B32A32_SFloat, xrInstancing: true, useDynamicScale: true, name: "DepthValuesBuffer");
                m_RenderTargets[(int)RT.DepthAsColorMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R32_SFloat, bindTextureMS: true, enableMSAA: true, xrInstancing: true, useDynamicScale: true, name: "DepthAsColorMSAA");

                m_RenderTargets[(int)RT.ColorMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, bindTextureMS: true, enableMSAA: true, xrInstancing: true, useDynamicScale: true, name: "CameraColorMSAA");
                m_RenderTargets[(int)RT.SssDiffuseLightingMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.B10G11R11_UFloatPack32, bindTextureMS: true, enableMSAA: true, xrInstancing: true, useDynamicScale: true, name: "CameraSSSDiffuseLightingMSAA");

                // We need to allocate this texture as long as msaa is supported because on both mode, one of the cameras can be forward only using the framesettings
                m_RenderTargets[(int)RT.NormalMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, enableMSAA: true, bindTextureMS: true, xrInstancing: true, useDynamicScale: true, name: "NormalBufferMSAA");

                m_ResolveMSAADepthNormalRTI = new RenderTargetIdentifier[2];
                m_ResolveMSAADepthNormalRTI[0] = GetRenderTarget(RT.DepthValuesMSAA);
                m_ResolveMSAADepthNormalRTI[1] = GetRenderTarget(RT.Normal);

                m_MSAAPrepassRTI = new RenderTargetIdentifier[2];
                m_MSAAPrepassRTI[0] = GetRenderTarget(RT.NormalMSAA);
                m_MSAAPrepassRTI[1] = GetRenderTarget(RT.DepthAsColorMSAA);

                m_MSAAVelocityPassRTI = new RenderTargetIdentifier[3];
                m_MSAAVelocityPassRTI[0] = GetRenderTarget(RT.VelocityMSAA);
                m_MSAAVelocityPassRTI[1] = GetRenderTarget(RT.NormalMSAA);
                m_MSAAVelocityPassRTI[2] = GetRenderTarget(RT.DepthAsColorMSAA);
            }
        }

        protected virtual void InitializeDebugBuffers(RenderPipelineSettings settings)
        {
            if (Debug.isDebugBuild)
            {
                m_RenderTargets[(int)RT.DebugColorPicker] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true, name: "DebugColorPicker");
                m_RenderTargets[(int)RT.DebugFullScreen] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true, name: "DebugFullScreen");
                // This target is only used in Dev builds as an intermediate destination for post process and where debug rendering will be done.
                m_RenderTargets[(int)RT.IntermediateAfterPostProcess] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true, name: "AfterPostProcess"); // Needs to be FP16 because output target might be HDR
            }
        }

        protected virtual void InitializeMRTHandles()
        {
            m_PrepassRTI = new RenderTargetIdentifier[1];
            m_PrepassRTI[0] = GetRenderTarget(RT.Normal);

            m_VelocityPassRTI = new RenderTargetIdentifier[2];
            m_VelocityPassRTI[0] = GetRenderTarget(RT.Velocity);
            m_VelocityPassRTI[1] = GetRenderTarget(RT.Normal);
        }

        protected virtual void InitializeRenderTextures(HDRenderPipelineAsset hdrpAsset)
        {
            RenderPipelineSettings settings = hdrpAsset.renderPipelineSettings;

            //if (settings.supportDecals)
            //    m_DbufferManager.CreateBuffers();

            InitializeDBuffer(settings);
            InitializeMainBuffers(settings);
            InitializeGBuffer(settings);
            InitializeSSSBuffers(settings);
            InitializeSSRBuffers(settings);
            InitializeDebugBuffers(settings);
            InitializeMSAABuffers(settings);

            InitializeMRTHandles();

            // SSS0 will either be an original target in forward, are shared with GBuffer0 in deferred.
            ShareRT(RT.AfterPostProcessOffscreen, RT.SSS0);
        }

        Vector2Int ComputeDepthBufferMipChainSize(Vector2Int screenSize)
        {
            m_CameraDepthBufferMipChainInfo.ComputePackedMipChainInfo(screenSize);
            return m_CameraDepthBufferMipChainInfo.textureSize;
        }

        public HDUtils.PackedMipChainInfo GetDepthBufferMipChainInfo()
        {
            return m_CameraDepthBufferMipChainInfo;
        }

        void ShareRT(RT newAlias, RT originalTexture)
        {
            Debug.Assert(m_RenderTargets[(int)originalTexture] != null);

            m_RenderTargetIsShared[(int)newAlias] = true;
            m_RenderTargets[(int)newAlias] = m_RenderTargets[(int)originalTexture];
        }

        void ReleaseRenderTextures()
        {
            for (int i = 0; i < (int)RT.Count; ++i)
            {
                if (!m_RenderTargetIsShared[i])
                {
                    RTHandles.Release(m_RenderTargets[i]);
                }

                m_RenderTargets[i] = null;
                m_RenderTargetIsShared[i] = false;
            }
        }
    }
}
