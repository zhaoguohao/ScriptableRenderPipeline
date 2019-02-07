using UnityEngine.Rendering;
using System;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public class SubsurfaceScatteringManager
    {
        // Currently we only support SSSBuffer with one buffer. If the shader code change, it may require to update the shader manager
        static int k_MaxSSSBuffer = 1;
        public static int sssBufferCount { get { return k_MaxSSSBuffer; } }

        // Disney SSS Model
        ComputeShader m_SubsurfaceScatteringCS;
        int m_SubsurfaceScatteringKernel;
        int m_SubsurfaceScatteringKernelMSAA;
        Material m_CombineLightingPass;
        // End Disney SSS Model

        // This is use to be able to read stencil value in compute shader
        Material m_CopyStencilForSplitLighting;

        bool m_MSAASupport = false;

        public SubsurfaceScatteringManager()
        {
        }

        public void Build(HDRenderPipelineAsset hdAsset)
        {
            // Disney SSS (compute + combine)
            string kernelName = hdAsset.renderPipelineSettings.increaseSssSampleCount ? "SubsurfaceScatteringHQ" : "SubsurfaceScatteringMQ";
            string kernelNameMSAA = hdAsset.renderPipelineSettings.increaseSssSampleCount ? "SubsurfaceScatteringHQ_MSAA" : "SubsurfaceScatteringMQ_MSAA";
            m_SubsurfaceScatteringCS = hdAsset.renderPipelineResources.shaders.subsurfaceScatteringCS;
            m_SubsurfaceScatteringKernel = m_SubsurfaceScatteringCS.FindKernel(kernelName);
            m_SubsurfaceScatteringKernelMSAA = m_SubsurfaceScatteringCS.FindKernel(kernelNameMSAA);
            m_CombineLightingPass = CoreUtils.CreateEngineMaterial(hdAsset.renderPipelineResources.shaders.combineLightingPS);
            m_CombineLightingPass.SetInt(HDShaderIDs._StencilMask, (int)HDRenderPipeline.StencilBitMask.LightingMask);

            m_CopyStencilForSplitLighting = CoreUtils.CreateEngineMaterial(hdAsset.renderPipelineResources.shaders.copyStencilBufferPS);
            m_CopyStencilForSplitLighting.SetInt(HDShaderIDs._StencilRef, (int)StencilLightingUsage.SplitLighting);
            m_CopyStencilForSplitLighting.SetInt(HDShaderIDs._StencilMask, (int)HDRenderPipeline.StencilBitMask.LightingMask);
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(m_CombineLightingPass);
            CoreUtils.Destroy(m_CopyStencilForSplitLighting);
        }

        public void PushGlobalParams(HDCamera hdCamera, CommandBuffer cmd, DiffusionProfileSettings sssParameters)
        {
            // Broadcast SSS parameters to all shaders.
            cmd.SetGlobalInt(HDShaderIDs._EnableSubsurfaceScattering, hdCamera.frameSettings.IsEnabled(FrameSettingsField.SubsurfaceScattering) ? 1 : 0);
            unsafe
            {
                // Warning: Unity is not able to losslessly transfer integers larger than 2^24 to the shader system.
                // Therefore, we bitcast uint to float in C#, and bitcast back to uint in the shader.
                uint texturingModeFlags = sssParameters.texturingModeFlags;
                uint transmissionFlags = sssParameters.transmissionFlags;
                cmd.SetGlobalFloat(HDShaderIDs._TexturingModeFlags, *(float*)&texturingModeFlags);
                cmd.SetGlobalFloat(HDShaderIDs._TransmissionFlags, *(float*)&transmissionFlags);
            }
            cmd.SetGlobalVectorArray(HDShaderIDs._ThicknessRemaps, sssParameters.thicknessRemaps);
            cmd.SetGlobalVectorArray(HDShaderIDs._ShapeParams, sssParameters.shapeParams);
            // To disable transmission, we simply nullify the transmissionTint
            cmd.SetGlobalVectorArray(HDShaderIDs._TransmissionTintsAndFresnel0, hdCamera.frameSettings.IsEnabled(FrameSettingsField.Transmission) ? sssParameters.transmissionTintsAndFresnel0 : sssParameters.disabledTransmissionTintsAndFresnel0);
            cmd.SetGlobalVectorArray(HDShaderIDs._WorldScales, sssParameters.worldScales);
        }

        public static bool NeedTemporarySubsurfaceBuffer()
        {
            // Caution: need to be in sync with SubsurfaceScattering.cs USE_INTERMEDIATE_BUFFER (Can't make a keyword as it is a compute shader)
            // Typed UAV loads from FORMAT_R16G16B16A16_FLOAT is an optional feature of Direct3D 11.
            // Most modern GPUs support it. We can avoid performing a costly copy in this case.
            // TODO: test/implement for other platforms.
            return SystemInfo.graphicsDeviceType != GraphicsDeviceType.PlayStation4 &&
                SystemInfo.graphicsDeviceType != GraphicsDeviceType.XboxOne &&
                SystemInfo.graphicsDeviceType != GraphicsDeviceType.XboxOneD3D12;
        }

        // Combines specular lighting and diffuse lighting with subsurface scattering.
        // In the case our frame is MSAA, for the moment given the fact that we do not have read/write access to the stencil buffer of the MSAA target; we need to keep this pass MSAA
        // However, the compute can't output and MSAA target so we blend the non-MSAA target into the MSAA one.
        public void SubsurfaceScatteringPass(HDCamera hdCamera, CommandBuffer cmd, DiffusionProfileSettings sssParameters, RTManager rtManager)
        {
            //hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA) ? GetRenderTarget(RT.ColorMSAA) : GetRenderTarget(RT.Color),
            //        GetRenderTarget(RT.SssDiffuseLighting), m_RTManager.GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA)), m_RTManager.GetDepthTexture()

            var colorBufferRT = hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA) ? rtManager.GetRenderTarget(RT.ColorMSAA) : rtManager.GetRenderTarget(RT.Color);
            var depthStencilBufferRT = rtManager.GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA));
            var depthTextureRT = rtManager.GetDepthTexture();
            var cameraFilteringRT = rtManager.GetRenderTarget(RT.SSSFiltering);
            var HTileBufferRT = rtManager.GetRenderTarget(RT.SSSHTile);
            var diffuseBufferRT = rtManager.GetRenderTarget(RT.SssDiffuseLighting);

            if (sssParameters == null || !hdCamera.frameSettings.IsEnabled(FrameSettingsField.SubsurfaceScattering))
                return;

            // TODO: For MSAA, at least initially, we can only support Jimenez, because we can't
            // create MSAA + UAV render targets.

            using (new ProfilingSample(cmd, "Subsurface Scattering", CustomSamplerId.SubsurfaceScattering.GetSampler()))
            {
                // For Jimenez we always need an extra buffer, for Disney it depends on platform
                if (NeedTemporarySubsurfaceBuffer() || hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA))
                {
                    // Clear the SSS filtering target
                    using (new ProfilingSample(cmd, "Clear SSS filtering target", CustomSamplerId.ClearSSSFilteringTarget.GetSampler()))
                    {
                        HDUtils.SetRenderTarget(cmd, hdCamera, cameraFilteringRT, ClearFlag.Color, CoreUtils.clearColorAllBlack);
                    }
                }

                using (new ProfilingSample(cmd, "HTile for SSS", CustomSamplerId.HTileForSSS.GetSampler()))
                {
                    // Currently, Unity does not offer a way to access the GCN HTile even on PS4 and Xbox One.
                    // Therefore, it's computed in a pixel shader, and optimized to only contain the SSS bit.

                    // Clear the HTile texture. TODO: move this to ClearBuffers(). Clear operations must be batched!
                    HDUtils.SetRenderTarget(cmd, hdCamera, HTileBufferRT, ClearFlag.Color, CoreUtils.clearColorAllBlack);

                    HDUtils.SetRenderTarget(cmd, hdCamera, depthStencilBufferRT); // No need for color buffer here
                    cmd.SetRandomWriteTarget(1, HTileBufferRT); // This need to be done AFTER SetRenderTarget
                    // Generate HTile for the split lighting stencil usage. Don't write into stencil texture (shaderPassId = 2)
                    // Use ShaderPassID 1 => "Pass 2 - Export HTILE for stencilRef to output"
                    CoreUtils.DrawFullScreen(cmd, m_CopyStencilForSplitLighting, null, 2);
                    cmd.ClearRandomWriteTargets();
                }

                unsafe
                {
                    // Warning: Unity is not able to losslessly transfer integers larger than 2^24 to the shader system.
                    // Therefore, we bitcast uint to float in C#, and bitcast back to uint in the shader.
                    uint texturingModeFlags = sssParameters.texturingModeFlags;
                    cmd.SetComputeFloatParam(m_SubsurfaceScatteringCS, HDShaderIDs._TexturingModeFlags, *(float*)&texturingModeFlags);
                }

                cmd.SetComputeVectorArrayParam(m_SubsurfaceScatteringCS, HDShaderIDs._WorldScales,        sssParameters.worldScales);
                cmd.SetComputeVectorArrayParam(m_SubsurfaceScatteringCS, HDShaderIDs._FilterKernels,      sssParameters.filterKernels);
                cmd.SetComputeVectorArrayParam(m_SubsurfaceScatteringCS, HDShaderIDs._ShapeParams,        sssParameters.shapeParams);

                int sssKernel = hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA) ? m_SubsurfaceScatteringKernelMSAA : m_SubsurfaceScatteringKernel;

                cmd.SetComputeTextureParam(m_SubsurfaceScatteringCS, sssKernel, HDShaderIDs._DepthTexture,      depthTextureRT);
                cmd.SetComputeTextureParam(m_SubsurfaceScatteringCS, sssKernel, HDShaderIDs._SSSHTile,          HTileBufferRT);
                cmd.SetComputeTextureParam(m_SubsurfaceScatteringCS, sssKernel, HDShaderIDs._IrradianceSource,  diffuseBufferRT);

                for (int i = 0; i < sssBufferCount; ++i)
                {
                    cmd.SetComputeTextureParam(m_SubsurfaceScatteringCS, sssKernel, HDShaderIDs._SSSBufferTexture[i], rtManager.GetSSSBuffer(i));
                }

                int numTilesX = ((int)(hdCamera.textureWidthScaling.x * hdCamera.screenSize.x) + 15) / 16;
                int numTilesY = ((int)hdCamera.screenSize.y + 15) / 16;

                if (NeedTemporarySubsurfaceBuffer() || hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA))
                {
                    cmd.SetComputeTextureParam(m_SubsurfaceScatteringCS, sssKernel, HDShaderIDs._CameraFilteringBuffer, cameraFilteringRT);

                    // Perform the SSS filtering pass which fills 'm_CameraFilteringBufferRT'.
                    cmd.DispatchCompute(m_SubsurfaceScatteringCS, sssKernel, numTilesX, numTilesY, 1);

                    cmd.SetGlobalTexture(HDShaderIDs._IrradianceSource, cameraFilteringRT);

                    // Additively blend diffuse and specular lighting into 'm_CameraColorBufferRT'.
                    HDUtils.DrawFullScreen(cmd, hdCamera, m_CombineLightingPass, colorBufferRT, depthStencilBufferRT);
                }
                else
                {
                    cmd.SetComputeTextureParam(m_SubsurfaceScatteringCS, m_SubsurfaceScatteringKernel, HDShaderIDs._CameraColorTexture, colorBufferRT);

                    // Perform the SSS filtering pass which performs an in-place update of 'colorBuffer'.
                    cmd.DispatchCompute(m_SubsurfaceScatteringCS, m_SubsurfaceScatteringKernel, numTilesX, numTilesY, 1);
                }
            }
        }
    }
}
