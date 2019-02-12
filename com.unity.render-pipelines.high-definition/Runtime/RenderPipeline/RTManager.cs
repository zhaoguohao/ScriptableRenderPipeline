using System;
using System.Collections.Generic;
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

        AmbientOcclusion,
        AOLinearDepth,
        AOLowDepth1,
        AOLowDepth2,
        AOLowDepth3,
        AOLowDepth4,
        AOTiledDepth1,
        AOTiledDepth2,
        AOTiledDepth3,
        AOTiledDepth4,
        AOOcclusion1,
        AOOcclusion2,
        AOOcclusion3,
        AOOcclusion4,
        AOCombined1,
        AOCombined2,
        AOCombined3,

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
        AmbientOcclusionMSAA,

        // Debug specific
        DebugColorPicker,
        DebugFullScreen,
        IntermediateAfterPostProcess,
        SSRDebug,
        DebugLightVolumeCount, // light count
        DebugLightVolumeAccumulation, // color accumulated value
        DebugLightVolume, // The output texture of the debug

        Count
    }

    public class RTManager
    {
        static public readonly string k_RenderLoopMemoryTag = "RenderLoop";
        static public readonly string k_PostProcessMemoryTag = "PostProcess";
        static public readonly string k_ShadowsMemoryTag = "Shadows";
        static public readonly string k_DebugMemoryTag = "Debug";

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

        ScaleFunc[] m_AOScaleFunctors;

        // Public interface
        public void Initialize(HDRenderPipelineAsset hdrpAsset)
        {
            InitializeRenderTextures(hdrpAsset);
        }

        public void Cleanup()
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
            m_RenderTargets[(int)RT.DepthStencil] = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.Depth32, filterMode: FilterMode.Point, xrInstancing: true, useDynamicScale: true, name: "CameraDepthStencil", memoryTag: k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.DepthMipChain] = RTHandles.Alloc(ComputeDepthBufferMipChainSize, colorFormat: GraphicsFormat.R32_SFloat, filterMode: FilterMode.Point, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "CameraDepthBufferMipChain", memoryTag: k_RenderLoopMemoryTag);
            // Technically we won't need this buffer in some cases, but nothing that we can determine at init time.
            m_RenderTargets[(int)RT.StencilCopy] = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.None, colorFormat: GraphicsFormat.R8_UNorm, filterMode: FilterMode.Point, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "CameraStencilCopy", memoryTag: k_RenderLoopMemoryTag); // DXGI_FORMAT_R8_UINT is not supported by Unity

            m_RenderTargets[(int)RT.Color] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite: true, useMipMap: false, xrInstancing: true, useDynamicScale: true, name: "CameraColor", memoryTag: k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.SssDiffuseLighting] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.B10G11R11_UFloatPack32, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "CameraSSSDiffuseLighting", memoryTag: k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.Distortion] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetDistortionBufferFormat(), enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "Distortion", memoryTag: k_RenderLoopMemoryTag);

            if (settings.supportMotionVectors)
            {
                m_RenderTargets[(int)RT.Velocity] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetVelocityBufferFormat(), xrInstancing: true, useDynamicScale: true, name: "Velocity", memoryTag: k_RenderLoopMemoryTag);
                if (settings.supportMSAA)
                {
                    m_RenderTargets[(int)RT.VelocityMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetVelocityBufferFormat(), enableMSAA: true, bindTextureMS: true, xrInstancing: true, useDynamicScale: true, name: "VelocityMSAA", memoryTag: k_RenderLoopMemoryTag);
                }
            }

            if (settings.supportedLitShaderMode == RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly)
            {
                // In case of full forward we must allocate the render target for normal buffer (or reuse one already existing)
                // TODO: Provide a way to reuse a render target
                m_RenderTargets[(int)RT.Normal] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "NormalBuffer", memoryTag: k_RenderLoopMemoryTag);
            }

            // Use RG16 as we only have one deferred directional and one screen space shadow light currently
            m_RenderTargets[(int)RT.ScreenSpaceShadow] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "ScreenSpaceShadowsBuffer", memoryTag: k_RenderLoopMemoryTag);
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
                    m_RenderTargets[(int)RT.DBuffer0 + dbufferIndex] = RTHandles.Alloc(Vector2.one, colorFormat: rtFormat[dbufferIndex], filterMode: FilterMode.Point, xrInstancing: true, useDynamicScale: true, name: string.Format("DBuffer{0}", dbufferIndex), memoryTag: k_RenderLoopMemoryTag);
                    m_DBufferRTI[dbufferIndex] = m_RenderTargets[(int)RT.DBuffer0 + dbufferIndex];
                }

                // We use 8x8 tiles in order to match the native GCN HTile as closely as possible.
                m_RenderTargets[(int)RT.DBufferHTile] = RTHandles.Alloc(size => new Vector2Int((size.x + 7) / 8, (size.y + 7) / 8), filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "DBufferHTile", memoryTag: k_RenderLoopMemoryTag); // Enable UAV
            }
        }

        protected virtual void InitializeGBuffer(RenderPipelineSettings settings)
        {
            if (settings.supportedLitShaderMode != RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly)
            {
                m_GBufferCount = 4; // ShadowMask and LightLayers are handled specifically
                m_RenderTargets[(int)RT.GBuffer0] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_SRGB, xrInstancing: true, useDynamicScale: true, enableRandomWrite: false, name: string.Format("GBuffer0"), memoryTag: k_RenderLoopMemoryTag);
                m_RenderTargets[(int)RT.GBuffer1] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, xrInstancing: true, useDynamicScale: true, enableRandomWrite: true, name: string.Format("GBuffer1"), memoryTag: k_RenderLoopMemoryTag);
                m_RenderTargets[(int)RT.GBuffer2] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, xrInstancing: true, useDynamicScale: true, enableRandomWrite: false, name: string.Format("GBuffer2"), memoryTag: k_RenderLoopMemoryTag);
                m_RenderTargets[(int)RT.GBuffer3] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetLightingBufferFormat(), xrInstancing: true, useDynamicScale: true, enableRandomWrite: false, name: string.Format("GBuffer3"), memoryTag: k_RenderLoopMemoryTag);

                if (settings.supportShadowMask)
                {
                    m_RenderTargets[(int)RT.GBuffer4] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: Builtin.GetShadowMaskBufferFormat(), xrInstancing: true, useDynamicScale: true, enableRandomWrite: false, name: string.Format("GBuffer4"), memoryTag: k_RenderLoopMemoryTag);
                    ShareRT(RT.ShadowMask, RT.GBuffer4); // Alias
                }

                if (settings.supportLightLayers)
                {
                    m_RenderTargets[(int)RT.GBuffer5] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, xrInstancing: true, useDynamicScale: true, enableRandomWrite: false, name: string.Format("GBuffer5"), memoryTag: k_RenderLoopMemoryTag);
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
                m_RenderTargets[(int)RT.SSRHitPoint] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "SSR_Hit_Point_Texture", memoryTag: k_RenderLoopMemoryTag);
                if (GetRenderTarget(RT.Distortion).rt.graphicsFormat == GraphicsFormat.R16G16B16A16_SFloat)
                    ShareRT(RT.SSRLighting, RT.Distortion);
                else
                    m_RenderTargets[(int)RT.SSRLighting] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "SSR_Lighting_Texture", memoryTag: k_RenderLoopMemoryTag);

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
                m_RenderTargets[(int)RT.SSS0] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_SRGB, xrInstancing: true, useDynamicScale: true, name: "SSSBuffer0", memoryTag: k_RenderLoopMemoryTag);
            }
            else
            {
                // In case of deferred, we must be in sync with SubsurfaceScattering.hlsl and lit.hlsl files and setup the correct buffers
                ShareRT(RT.SSS0, RT.GBuffer0);
            }

            // We need to allocate the texture if we are in forward or both in case one of the cameras is in enable forward only mode
            if (settings.supportMSAA)
            {
                m_RenderTargets[(int)RT.SSSMSAA0] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_SRGB, enableMSAA: true, bindTextureMS: true, xrInstancing: true, useDynamicScale: true, name: "SSSBufferMSAA0", memoryTag: k_RenderLoopMemoryTag);
            }

            if (SubsurfaceScatteringManager.NeedTemporarySubsurfaceBuffer() || settings.supportMSAA)
            {
                // Caution: must be same format as RT.SssDiffuseLightingBuffer
                m_RenderTargets[(int)RT.SSSFiltering] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.B10G11R11_UFloatPack32, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "SSSCameraFiltering", memoryTag: k_RenderLoopMemoryTag); // Enable UAV
            }

            // We use 8x8 tiles in order to match the native GCN HTile as closely as possible.
            m_RenderTargets[(int)RT.SSSHTile] = RTHandles.Alloc(size => new Vector2Int((size.x + 7) / 8, (size.y + 7) / 8), filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "SSSHtile", memoryTag: k_RenderLoopMemoryTag); // Enable UAV

        }

        protected virtual void InitializeMSAABuffers(RenderPipelineSettings settings)
        {
            if (settings.supportMSAA)
            {
                m_RenderTargets[(int)RT.DepthStencilMSAA] = RTHandles.Alloc(Vector2.one, depthBufferBits: DepthBits.Depth24, filterMode: FilterMode.Point, bindTextureMS: true, enableMSAA: true, xrInstancing: true, useDynamicScale: true, name: "CameraDepthStencilMSAA", memoryTag: k_RenderLoopMemoryTag);
                m_RenderTargets[(int)RT.DepthValuesMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R32G32B32A32_SFloat, xrInstancing: true, useDynamicScale: true, name: "DepthValuesBuffer", memoryTag: k_RenderLoopMemoryTag);
                m_RenderTargets[(int)RT.DepthAsColorMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R32_SFloat, bindTextureMS: true, enableMSAA: true, xrInstancing: true, useDynamicScale: true, name: "DepthAsColorMSAA", memoryTag: k_RenderLoopMemoryTag);

                m_RenderTargets[(int)RT.ColorMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, bindTextureMS: true, enableMSAA: true, xrInstancing: true, useDynamicScale: true, name: "CameraColorMSAA", memoryTag: k_RenderLoopMemoryTag);
                m_RenderTargets[(int)RT.SssDiffuseLightingMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.B10G11R11_UFloatPack32, bindTextureMS: true, enableMSAA: true, xrInstancing: true, useDynamicScale: true, name: "CameraSSSDiffuseLightingMSAA", memoryTag: k_RenderLoopMemoryTag);

                // We need to allocate this texture as long as msaa is supported because on both mode, one of the cameras can be forward only using the framesettings
                m_RenderTargets[(int)RT.NormalMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, enableMSAA: true, bindTextureMS: true, xrInstancing: true, useDynamicScale: true, name: "NormalBufferMSAA", memoryTag: k_RenderLoopMemoryTag);

                m_RenderTargets[(int)RT.AmbientOcclusionMSAA] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Bilinear, colorFormat: GraphicsFormat.R8G8_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "Ambient Occlusion MSAA", memoryTag: RTManager.k_RenderLoopMemoryTag);

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

        protected virtual void InitializeAOBuffers(RenderPipelineSettings settings)
        {
            m_RenderTargets[(int)RT.AmbientOcclusion] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Bilinear, colorFormat: GraphicsFormat.R8_UNorm, enableRandomWrite: true, xrInstancing: true, useDynamicScale: true, name: "Ambient Occlusion", memoryTag: RTManager.k_RenderLoopMemoryTag);

            // Prepare scale functors
            m_AOScaleFunctors = new ScaleFunc[AmbientOcclusionSystem.mipCount];
            m_AOScaleFunctors[0] = size => size; // 0 is original size (mip0)

            for (int i = 1; i < m_AOScaleFunctors.Length; i++)
            {
                int mult = i;
                m_AOScaleFunctors[i] = size =>
                {
                    int div = 1 << mult;
                    return new Vector2Int(
                        (size.x + (div - 1)) / div,
                        (size.y + (div - 1)) / div
                    );
                };
            }

            bool supportMSAA = settings.supportMSAA;

            var fmtFP16 = supportMSAA ? GraphicsFormat.R16G16_SFloat : GraphicsFormat.R16_SFloat;
            var fmtFP32 = supportMSAA ? GraphicsFormat.R32G32_SFloat : GraphicsFormat.R32_SFloat;
            var fmtFX8 = supportMSAA ? GraphicsFormat.R8G8_UNorm : GraphicsFormat.R8_UNorm;

            m_RenderTargets[(int)RT.AOLinearDepth] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[0], colorFormat: fmtFP16, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOLinearDepth", memoryTag: RTManager.k_RenderLoopMemoryTag);

            m_RenderTargets[(int)RT.AOLowDepth1] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[1], colorFormat: fmtFP32, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOLowDepth1", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOLowDepth2] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[2], colorFormat: fmtFP32, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOLowDepth2", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOLowDepth3] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[3], colorFormat: fmtFP32, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOLowDepth3", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOLowDepth4] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[4], colorFormat: fmtFP32, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOLowDepth4", memoryTag: RTManager.k_RenderLoopMemoryTag);

            // XRTODO: multiply slices by eyeCount and handle indexing in shader
            m_RenderTargets[(int)RT.AOTiledDepth1] = RTHandles.Alloc(dimension: TextureDimension.Tex2DArray, slices: 16, scaleFunc: m_AOScaleFunctors[3], colorFormat: fmtFP16, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOTiledDepth1", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOTiledDepth2] = RTHandles.Alloc(dimension: TextureDimension.Tex2DArray, slices: 16, scaleFunc: m_AOScaleFunctors[4], colorFormat: fmtFP16, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOTiledDepth2", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOTiledDepth3] = RTHandles.Alloc(dimension: TextureDimension.Tex2DArray, slices: 16, scaleFunc: m_AOScaleFunctors[5], colorFormat: fmtFP16, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOTiledDepth3", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOTiledDepth4] = RTHandles.Alloc(dimension: TextureDimension.Tex2DArray, slices: 16, scaleFunc: m_AOScaleFunctors[6], colorFormat: fmtFP16, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOTiledDepth4", memoryTag: RTManager.k_RenderLoopMemoryTag);

            m_RenderTargets[(int)RT.AOOcclusion1] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[1], colorFormat: fmtFX8, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOOcclusion1", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOOcclusion2] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[2], colorFormat: fmtFX8, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOOcclusion2", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOOcclusion3] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[3], colorFormat: fmtFX8, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOOcclusion3", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOOcclusion4] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[4], colorFormat: fmtFX8, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOOcclusion4", memoryTag: RTManager.k_RenderLoopMemoryTag);

            m_RenderTargets[(int)RT.AOCombined1] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[1], colorFormat: fmtFX8, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOCombined1", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOCombined2] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[2], colorFormat: fmtFX8, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOCombined2", memoryTag: RTManager.k_RenderLoopMemoryTag);
            m_RenderTargets[(int)RT.AOCombined3] = RTHandles.Alloc(scaleFunc: m_AOScaleFunctors[3], colorFormat: fmtFX8, useDynamicScale: true, enableRandomWrite: true, filterMode: FilterMode.Point, xrInstancing: true, name: "AOCombined3", memoryTag: RTManager.k_RenderLoopMemoryTag);
        }

        protected virtual void InitializeDebugBuffers(RenderPipelineSettings settings)
        {
            if (Debug.isDebugBuild)
            {
                // We check the format because it's configurable so if it does not match we need to allocate a texture
                // Sharing DebugColorPicker and DebugFullScreen with Distortion also mean that we cannot do Distortion fullscreen debug because it's actually the same target.
                if (GetRenderTarget(RT.Distortion).rt.graphicsFormat == GraphicsFormat.R16G16B16A16_SFloat)
                    ShareRT(RT.DebugColorPicker, RT.Distortion);
                else
                    m_RenderTargets[(int)RT.DebugColorPicker] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true, name: "DebugColorPicker", memoryTag: k_DebugMemoryTag);

                // If used, DebugFullScreen is always done before DebugColorPicker so we can share the target. Currently use of targets is as follows:
                // (Color, Velocity, etc) => DebugFullScreen/DebugColorPicker => IntermediateAfterPostProcess => DebugColorPicker => IntermediateAfterPostProcess
                ShareRT(RT.DebugFullScreen, RT.DebugColorPicker);

                // This target is only used in Dev builds as an intermediate destination for post process and where debug rendering will be done.
                m_RenderTargets[(int)RT.IntermediateAfterPostProcess] = RTHandles.Alloc(Vector2.one, filterMode: FilterMode.Point, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true, name: "AfterPostProcess", memoryTag: k_DebugMemoryTag); // Needs to be FP16 because output target might be HDR

                ShareRT(RT.DebugLightVolumeCount, RT.AOLinearDepth);

                // DebugColorPicker and Color are always already used when doing Light Volume Debug (which is done at the very end of the frame with other overlays)
                ShareRT(RT.DebugLightVolumeAccumulation, RT.DebugColorPicker);
                ShareRT(RT.DebugLightVolume, RT.Color);
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

            InitializeDBuffer(settings);
            InitializeMainBuffers(settings);
            InitializeGBuffer(settings);
            InitializeSSSBuffers(settings);
            InitializeSSRBuffers(settings);
            InitializeMSAABuffers(settings);
            InitializeAOBuffers(settings);
            InitializeDebugBuffers(settings);

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
            if (m_RenderTargets[(int)originalTexture] == null)
            {
                Debug.LogError(string.Format("Failed sharing {0} render texture with {1}. Original texture is null. Check initialization order.", originalTexture, newAlias));
            }

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

        // Memory Logging
        public class DebugMemoryEntry
        {
            public string name;
            public uint sizeInByte;
        }

        static Dictionary<string, List<DebugMemoryEntry>> m_DebugMemoryEntries = new Dictionary<string, List<DebugMemoryEntry>>();

        static public void RegisterMemory(string tag, string name, uint sizeInByte)
        {
            List<DebugMemoryEntry> elementList;
            if (!m_DebugMemoryEntries.TryGetValue(tag, out elementList))
            {
                elementList = new List<DebugMemoryEntry>();
                m_DebugMemoryEntries.Add(tag, elementList);
            }

            elementList.Add(new DebugMemoryEntry { name = name, sizeInByte = sizeInByte });
        }

        static public DebugMemoryEntry GetDebugMemoryEntry(string tag, string name)
        {
            List<DebugMemoryEntry> elementList;
            if (m_DebugMemoryEntries.TryGetValue(tag, out elementList))
            {
                return elementList.Find(entry => entry.name == name);
            }
            else
            {
                Debug.LogError(string.Format("DebugMemoryEntry {0} with tag {1} not found.", name, tag));
                return null;
            }
        }

        static public void UnregisterMemory(string tag, string name)
        {
            List<DebugMemoryEntry> elementList;
            if (m_DebugMemoryEntries.TryGetValue(tag, out elementList))
            {
                elementList.Remove(elementList.Find(entry => entry.name == name));
            }
        }

        static public string DumpMemory(bool sortByName = false)
        {
            string result = "";
            uint totalMemory = 0;
            uint debugMemory = 0;
            uint textureCount = 0;

            foreach(var tag in m_DebugMemoryEntries)
            {
                string tagName = tag.Key;
                var elementList = tag.Value;
                uint tagSize = 0;
                string textureList = "";

                if (sortByName)
                {
                    elementList.Sort((element1, element2) => element1.name.CompareTo(element2.name));
                }
                else
                {
                    elementList.Sort((element1, element2) => element2.sizeInByte.CompareTo(element1.sizeInByte));
                }

                foreach (var element in elementList)
                {
                    textureList = string.Format("{0}\t{1},{2}{3}", textureList, element.name, CoreUtils.HumanizeWeight(element.sizeInByte), Environment.NewLine);
                    tagSize += element.sizeInByte;
                }

                string header = string.Format("==== {0} : {1} for {2} textures ===={3}", tagName, CoreUtils.HumanizeWeight(tagSize), elementList.Count, Environment.NewLine);
                result = string.Format("{0}{1}{2}", result, header, textureList);
                totalMemory += tagSize;
                textureCount += (uint)elementList.Count;

                if (tagName == k_DebugMemoryTag)
                    debugMemory = tagSize;
            }

            result = string.Format("{0}Total Memory: {1} (including {2} for debug){3}", result, CoreUtils.HumanizeWeight(totalMemory), CoreUtils.HumanizeWeight(debugMemory), Environment.NewLine);
            result = string.Format("{0}Texture Count: {1}{2}", result, textureCount, Environment.NewLine);

            return result;
        }

        static public uint ComputeRenderTextureSize(RenderTexture rt)
        {
            int width = rt.width;
            int height = rt.height;
            int depth = rt.volumeDepth;

            int mipCount = rt.useMipMap ? (int)(Mathf.Log(Math.Max(width, Math.Max(height, depth))) + 1) : 1;

            uint result = 0;
            for (int i = 0; i < mipCount; ++i)
            {
                result += GraphicsFormatUtility.ComputeMipmapSize(Math.Max(width >> i, 1), Math.Max(height >> i, 1), Math.Max(depth >> i, 1), rt.graphicsFormat);
            }

            return result;
        }
    }
}
