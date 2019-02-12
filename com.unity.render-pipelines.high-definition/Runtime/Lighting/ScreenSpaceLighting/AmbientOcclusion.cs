using System;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    using RTHandle = RTHandleSystem.RTHandle;

    [Serializable]
    public sealed class AmbientOcclusion : VolumeComponent
    {
        [Tooltip("Degree of darkness added by ambient occlusion.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 4f);

        [Tooltip("Modifies thickness of occluders. This increases dark areas but also introduces dark halo around objects.")]
        public ClampedFloatParameter thicknessModifier = new ClampedFloatParameter(1f, 1f, 10f);

        [Tooltip("Defines how much of the occlusion should be affected by ambient lighting.")]
        public ClampedFloatParameter directLightingStrength = new ClampedFloatParameter(0f, 0f, 1f);

        // Hidden parameters
        [HideInInspector] public ClampedFloatParameter noiseFilterTolerance = new ClampedFloatParameter(0f, -8f, 0f);
        [HideInInspector] public ClampedFloatParameter blurTolerance = new ClampedFloatParameter(-4.6f, -8f, 1f);
        [HideInInspector] public ClampedFloatParameter upsampleTolerance = new ClampedFloatParameter(-12f, -12f, -1f);
    }

    public class AmbientOcclusionSystem
    {
        enum MipLevel { Original, L1, L2, L3, L4, L5, L6, Count }

        public static int mipCount { get { return (int)MipLevel.Count; } }

        RenderPipelineResources m_Resources;
        RenderPipelineSettings m_Settings;

        // The arrays below are reused between frames to reduce GC allocation.
        readonly float[] m_SampleThickness =
        {
            Mathf.Sqrt(1f - 0.2f * 0.2f),
            Mathf.Sqrt(1f - 0.4f * 0.4f),
            Mathf.Sqrt(1f - 0.6f * 0.6f),
            Mathf.Sqrt(1f - 0.8f * 0.8f),
            Mathf.Sqrt(1f - 0.2f * 0.2f - 0.2f * 0.2f),
            Mathf.Sqrt(1f - 0.2f * 0.2f - 0.4f * 0.4f),
            Mathf.Sqrt(1f - 0.2f * 0.2f - 0.6f * 0.6f),
            Mathf.Sqrt(1f - 0.2f * 0.2f - 0.8f * 0.8f),
            Mathf.Sqrt(1f - 0.4f * 0.4f - 0.4f * 0.4f),
            Mathf.Sqrt(1f - 0.4f * 0.4f - 0.6f * 0.6f),
            Mathf.Sqrt(1f - 0.4f * 0.4f - 0.8f * 0.8f),
            Mathf.Sqrt(1f - 0.6f * 0.6f - 0.6f * 0.6f)
        };

        readonly float[] m_InvThicknessTable = new float[12];
        readonly float[] m_SampleWeightTable = new float[12];

        readonly int[] m_Widths = new int[7];
        readonly int[] m_Heights = new int[7];

        // MSAA-specifics
        readonly MaterialPropertyBlock m_ResolvePropertyBlock;
        readonly Material m_ResolveMaterial;

#if ENABLE_RAYTRACING
        public HDRaytracingManager m_RayTracingManager = new HDRaytracingManager();
        readonly HDRaytracingAmbientOcclusion m_RaytracingAmbientOcclusion = new HDRaytracingAmbientOcclusion();
#endif

        public AmbientOcclusionSystem(HDRenderPipelineAsset hdAsset)
        {
            m_Settings = hdAsset.renderPipelineSettings;
            m_Resources = hdAsset.renderPipelineResources;

            if (!hdAsset.renderPipelineSettings.supportSSAO)
                return;

            bool supportMSAA = hdAsset.renderPipelineSettings.supportMSAA;

            if (supportMSAA)
            {
                m_ResolveMaterial = CoreUtils.CreateEngineMaterial(m_Resources.shaders.aoResolvePS);
                m_ResolvePropertyBlock = new MaterialPropertyBlock();
            }
        }

        public void Cleanup()
        {
#if ENABLE_RAYTRACING
            m_RaytracingAmbientOcclusion.Release();
#endif

            CoreUtils.Destroy(m_ResolveMaterial);
        }

#if ENABLE_RAYTRACING
        public void InitRaytracing(HDRaytracingManager raytracingManager, RTManager rtManager)
        {
            m_RayTracingManager = raytracingManager;
            m_RaytracingAmbientOcclusion.Init(m_Resources, m_Settings, m_RayTracingManager, rtManager);
        }
#endif

        public bool IsActive(HDCamera camera, AmbientOcclusion settings) => camera.frameSettings.IsEnabled(FrameSettingsField.SSAO) && settings.intensity.value > 0f;

        public void Render(CommandBuffer cmd, HDCamera camera, RTManager rtManager, ScriptableRenderContext renderContext)
        {

#if ENABLE_RAYTRACING
            HDRaytracingEnvironment rtEnvironement = m_RayTracingManager.CurrentEnvironment();
            if (m_Settings.supportRayTracing && rtEnvironement != null && rtEnvironement.raytracedAO)
                m_RaytracingAmbientOcclusion.RenderAO(camera, cmd, m_AmbientOcclusionTex, renderContext);
            else
#endif
            {
                Dispatch(cmd, camera, rtManager);
                PostDispatchWork(cmd, camera, rtManager);
            }
        }

        public void Dispatch(CommandBuffer cmd, HDCamera camera, RTManager rtManager)
        {
            // Grab current settings
            var settings = VolumeManager.instance.stack.GetComponent<AmbientOcclusion>();

            if (!IsActive(camera, settings))
                return;

            using (new ProfilingSample(cmd, "Render SSAO", CustomSamplerId.RenderSSAO.GetSampler()))
            {
                // Base size
                m_Widths[0] = camera.actualWidth;
                m_Heights[0] = camera.actualHeight;

                // L1 -> L6 sizes
                // We need to recalculate these on every frame, we can't rely on RTHandle width/height
                // values as they may have been rescaled and not the actual size we want
                for (int i = 1; i < (int)MipLevel.Count; i++)
                {
                    int div = 1 << i;
                    m_Widths[i] = (m_Widths[0] + (div - 1)) / div;
                    m_Heights[i] = (m_Heights[0] + (div - 1)) / div;
                }

                // Grab current viewport scale factor - needed to handle RTHandle auto resizing
                var viewport = camera.viewportScale;

                // Textures used for rendering
                RTHandle depthMap, destination;
                bool msaa = camera.frameSettings.IsEnabled(FrameSettingsField.MSAA);

                if (msaa)
                {
                    depthMap = rtManager.GetRenderTarget(RT.DepthValuesMSAA);
                    destination = rtManager.GetRenderTarget(RT.AmbientOcclusionMSAA);
                }
                else
                {
                    depthMap = rtManager.GetDepthTexture();
                    destination = rtManager.GetRenderTarget(RT.AmbientOcclusion);
                }

                // Render logic
                PushDownsampleCommands(cmd, depthMap, msaa, rtManager);

                float tanHalfFovH = CalculateTanHalfFovHeight(camera);

                var tileDepth1 = rtManager.GetRenderTarget(RT.AOTiledDepth1);
                var tileDepth2 = rtManager.GetRenderTarget(RT.AOTiledDepth2);
                var tileDepth3 = rtManager.GetRenderTarget(RT.AOTiledDepth3);
                var tileDepth4 = rtManager.GetRenderTarget(RT.AOTiledDepth4);

                var occlusion1 = rtManager.GetRenderTarget(RT.AOOcclusion1);
                var occlusion2 = rtManager.GetRenderTarget(RT.AOOcclusion2);
                var occlusion3 = rtManager.GetRenderTarget(RT.AOOcclusion3);
                var occlusion4 = rtManager.GetRenderTarget(RT.AOOcclusion4);

                var lowDepth1 = rtManager.GetRenderTarget(RT.AOLowDepth1);
                var lowDepth2 = rtManager.GetRenderTarget(RT.AOLowDepth2);
                var lowDepth3 = rtManager.GetRenderTarget(RT.AOLowDepth3);
                var lowDepth4 = rtManager.GetRenderTarget(RT.AOLowDepth4);

                var combined1 = rtManager.GetRenderTarget(RT.AOCombined1);
                var combined2 = rtManager.GetRenderTarget(RT.AOCombined2);
                var combined3 = rtManager.GetRenderTarget(RT.AOCombined3);

                var linearDepth = rtManager.GetRenderTarget(RT.AOLinearDepth);


                PushRenderCommands(cmd, viewport, tileDepth1, occlusion1, settings, GetSizeArray(MipLevel.L3), tanHalfFovH, msaa);
                PushRenderCommands(cmd, viewport, tileDepth2, occlusion2, settings, GetSizeArray(MipLevel.L4), tanHalfFovH, msaa);
                PushRenderCommands(cmd, viewport, tileDepth3, occlusion3, settings, GetSizeArray(MipLevel.L5), tanHalfFovH, msaa);
                PushRenderCommands(cmd, viewport, tileDepth4, occlusion4, settings, GetSizeArray(MipLevel.L6), tanHalfFovH, msaa);

                PushUpsampleCommands(cmd, viewport, lowDepth4, occlusion4, lowDepth3, occlusion3, combined3, settings, GetSize(MipLevel.L4), GetSize(MipLevel.L3), msaa);
                PushUpsampleCommands(cmd, viewport, lowDepth3, combined3, lowDepth2, occlusion2, combined2, settings, GetSize(MipLevel.L3), GetSize(MipLevel.L2), msaa);
                PushUpsampleCommands(cmd, viewport, lowDepth2, combined2, lowDepth1, occlusion1, combined1, settings, GetSize(MipLevel.L2), GetSize(MipLevel.L1), msaa);
                PushUpsampleCommands(cmd, viewport, lowDepth1, combined1, linearDepth, null, destination, settings, GetSize(MipLevel.L1), GetSize(MipLevel.Original), msaa);
            }
        }

        public void PostDispatchWork(CommandBuffer cmd, HDCamera camera, RTManager rtManager)
        {
            // Grab current settings
            var settings = VolumeManager.instance.stack.GetComponent<AmbientOcclusion>();

            // TODO: All the pushdebug stuff should be centralized somewhere and we should not need to get hdrp like this
            HDRenderPipeline hdrp = RenderPipelineManager.currentPipeline as HDRenderPipeline;

            if (!IsActive(camera, settings))
            {
                // No AO applied - neutral is black, see the comment in the shaders
                cmd.SetGlobalTexture(HDShaderIDs._AmbientOcclusionTexture, Texture2D.blackTexture);
                cmd.SetGlobalVector(HDShaderIDs._AmbientOcclusionParam, Vector4.zero);

                if (hdrp.debugDisplaySettings.data.fullScreenDebugMode == FullScreenDebugMode.SSAO)
                {
                    // Clear anyway if debug is enabled otherwise we get garbage on screen
                    HDUtils.SetRenderTarget(cmd, camera, rtManager.GetRenderTarget(RT.AmbientOcclusion), clearFlag: ClearFlag.Color, clearColor: Color.black);
                    hdrp.PushFullScreenDebugTexture(camera, cmd, rtManager.GetRenderTarget(RT.AmbientOcclusion), FullScreenDebugMode.SSAO);
                }

                return;
            }

            // MSAA Resolve
            if (camera.frameSettings.IsEnabled(FrameSettingsField.MSAA))
            {
                using (new ProfilingSample(cmd, "Resolve AO Buffer", CustomSamplerId.ResolveSSAO.GetSampler()))
                {
                    HDUtils.SetRenderTarget(cmd, camera, rtManager.GetRenderTarget(RT.AmbientOcclusion));
                    m_ResolvePropertyBlock.SetTexture(HDShaderIDs._DepthValuesTexture, rtManager.GetRenderTarget(RT.DepthValuesMSAA));
                    m_ResolvePropertyBlock.SetTexture(HDShaderIDs._MultiAmbientOcclusionTexture, rtManager.GetRenderTarget(RT.AmbientOcclusionMSAA));
                    cmd.DrawProcedural(Matrix4x4.identity, m_ResolveMaterial, 0, MeshTopology.Triangles, 3, 1, m_ResolvePropertyBlock);
                }
            }

            cmd.SetGlobalTexture(HDShaderIDs._AmbientOcclusionTexture, rtManager.GetRenderTarget(RT.AmbientOcclusion));
            cmd.SetGlobalVector(HDShaderIDs._AmbientOcclusionParam, new Vector4(0f, 0f, 0f, settings.directLightingStrength.value));

            hdrp.PushFullScreenDebugTexture(camera, cmd, rtManager.GetRenderTarget(RT.AmbientOcclusion), FullScreenDebugMode.SSAO);
        }


        float CalculateTanHalfFovHeight(HDCamera camera)
        {
            return 1f / camera.projMatrix[0, 0];
        }

        Vector2 GetSize(MipLevel mip)
        {
            return new Vector2(m_Widths[(int)mip], m_Heights[(int)mip]);
        }

        Vector3 GetSizeArray(MipLevel mip)
        {
            return new Vector3(m_Widths[(int)mip], m_Heights[(int)mip], 16);
        }

        void PushDownsampleCommands(CommandBuffer cmd, RTHandle depthMap, bool msaa, RTManager rtManager)
        {
            var kernelName = msaa ? "KMain_MSAA" : "KMain";

            // 1st downsampling pass.
            var cs = m_Resources.shaders.aoDownsample1CS;
            int kernel = cs.FindKernel(kernelName);

            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._LinearZ, rtManager.GetRenderTarget(RT.AOLinearDepth));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS2x, rtManager.GetRenderTarget(RT.AOLowDepth1));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS4x, rtManager.GetRenderTarget(RT.AOLowDepth2));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS2xAtlas, rtManager.GetRenderTarget(RT.AOTiledDepth1));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS4xAtlas, rtManager.GetRenderTarget(RT.AOTiledDepth2));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._Depth, depthMap, 0);

            cmd.DispatchCompute(cs, kernel, m_Widths[(int)MipLevel.L4], m_Heights[(int)MipLevel.L4], 1);

            // 2nd downsampling pass.
            cs = m_Resources.shaders.aoDownsample2CS;
            kernel = cs.FindKernel(kernelName);

            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS4x, rtManager.GetRenderTarget(RT.AOLowDepth2));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS8x, rtManager.GetRenderTarget(RT.AOLowDepth3));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS16x, rtManager.GetRenderTarget(RT.AOLowDepth4));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS8xAtlas, rtManager.GetRenderTarget(RT.AOTiledDepth3));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS16xAtlas, rtManager.GetRenderTarget(RT.AOTiledDepth4));

            cmd.DispatchCompute(cs, kernel, m_Widths[(int)MipLevel.L6], m_Heights[(int)MipLevel.L6], 1);
        }

        void PushRenderCommands(CommandBuffer cmd, in Vector4 viewport, RTHandle source, RTHandle destination, AmbientOcclusion settings, in Vector3 sourceSize, float tanHalfFovH, bool msaa)
        {
            // Here we compute multipliers that convert the center depth value into (the reciprocal
            // of) sphere thicknesses at each sample location. This assumes a maximum sample radius
            // of 5 units, but since a sphere has no thickness at its extent, we don't need to
            // sample that far out. Only samples whole integer offsets with distance less than 25
            // are used. This means that there is no sample at (3, 4) because its distance is
            // exactly 25 (and has a thickness of 0.)

            // The shaders are set up to sample a circular region within a 5-pixel radius.
            const float kScreenspaceDiameter = 10f;

            // SphereDiameter = CenterDepth * ThicknessMultiplier. This will compute the thickness
            // of a sphere centered at a specific depth. The ellipsoid scale can stretch a sphere
            // into an ellipsoid, which changes the characteristics of the AO.
            // TanHalfFovH: Radius of sphere in depth units if its center lies at Z = 1
            // ScreenspaceDiameter: Diameter of sample sphere in pixel units
            // ScreenspaceDiameter / BufferWidth: Ratio of the screen width that the sphere actually covers
            float thicknessMultiplier = 2f * tanHalfFovH * kScreenspaceDiameter / sourceSize.x;

            // This will transform a depth value from [0, thickness] to [0, 1].
            float inverseRangeFactor = 1f / thicknessMultiplier;

            // The thicknesses are smaller for all off-center samples of the sphere. Compute
            // thicknesses relative to the center sample.
            for (int i = 0; i < 12; i++)
                m_InvThicknessTable[i] = inverseRangeFactor / m_SampleThickness[i];

            // These are the weights that are multiplied against the samples because not all samples
            // are equally important. The farther the sample is from the center location, the less
            // they matter. We use the thickness of the sphere to determine the weight.  The scalars
            // in front are the number of samples with this weight because we sum the samples
            // together before multiplying by the weight, so as an aggregate all of those samples
            // matter more. After generating this table, the weights are normalized.
            m_SampleWeightTable[ 0] = 4 * m_SampleThickness[ 0];    // Axial
            m_SampleWeightTable[ 1] = 4 * m_SampleThickness[ 1];    // Axial
            m_SampleWeightTable[ 2] = 4 * m_SampleThickness[ 2];    // Axial
            m_SampleWeightTable[ 3] = 4 * m_SampleThickness[ 3];    // Axial
            m_SampleWeightTable[ 4] = 4 * m_SampleThickness[ 4];    // Diagonal
            m_SampleWeightTable[ 5] = 8 * m_SampleThickness[ 5];    // L-shaped
            m_SampleWeightTable[ 6] = 8 * m_SampleThickness[ 6];    // L-shaped
            m_SampleWeightTable[ 7] = 8 * m_SampleThickness[ 7];    // L-shaped
            m_SampleWeightTable[ 8] = 4 * m_SampleThickness[ 8];    // Diagonal
            m_SampleWeightTable[ 9] = 8 * m_SampleThickness[ 9];    // L-shaped
            m_SampleWeightTable[10] = 8 * m_SampleThickness[10];    // L-shaped
            m_SampleWeightTable[11] = 4 * m_SampleThickness[11];    // Diagonal

            // Zero out the unused samples.
            // FIXME: should we support SAMPLE_EXHAUSTIVELY mode?
            m_SampleWeightTable[0] = 0;
            m_SampleWeightTable[2] = 0;
            m_SampleWeightTable[5] = 0;
            m_SampleWeightTable[7] = 0;
            m_SampleWeightTable[9] = 0;

            // Normalize the weights by dividing by the sum of all weights
            float totalWeight = 0f;

            foreach (float w in m_SampleWeightTable)
                totalWeight += w;

            for (int i = 0; i < m_SampleWeightTable.Length; i++)
                m_SampleWeightTable[i] /= totalWeight;

            // Set the arguments for the render kernel.
            var cs = m_Resources.shaders.aoRenderCS;
            int kernel = cs.FindKernel(msaa ? "KMainInterleaved_MSAA" : "KMainInterleaved");

            cmd.SetComputeFloatParams(cs, HDShaderIDs._InvThicknessTable, m_InvThicknessTable);
            cmd.SetComputeFloatParams(cs, HDShaderIDs._SampleWeightTable, m_SampleWeightTable);
            cmd.SetComputeVectorParam(cs, HDShaderIDs._InvSliceDimension, new Vector2(1f / sourceSize.x * viewport.x, 1f / sourceSize.y * viewport.y));
            cmd.SetComputeVectorParam(cs, HDShaderIDs._AdditionalParams, new Vector2(-1f / settings.thicknessModifier.value, settings.intensity.value));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._Depth, source);
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._Occlusion, destination);

            // Calculate the thread group count and add a dispatch command with them.
            cs.GetKernelThreadGroupSizes(kernel, out var xsize, out var ysize, out var zsize);

            cmd.DispatchCompute(
                cs, kernel,
                ((int)sourceSize.x + (int)xsize - 1) / (int)xsize,
                ((int)sourceSize.y + (int)ysize - 1) / (int)ysize,
                ((int)sourceSize.z + (int)zsize - 1) / (int)zsize
            );
        }

        void PushUpsampleCommands(CommandBuffer cmd, in Vector4 viewport, RTHandle lowResDepth, RTHandle interleavedAO, RTHandle highResDepth, RTHandle highResAO, RTHandle dest, AmbientOcclusion settings, in Vector3 lowResDepthSize, in Vector2 highResDepthSize, bool msaa)
        {
            var cs = m_Resources.shaders.aoUpsampleCS;
            int kernel = msaa
                ? cs.FindKernel(highResAO == null ? "KMainInvert_MSAA" : "KMainBlendout_MSAA")
                : cs.FindKernel(highResAO == null ? "KMainInvert" : "KMainBlendout");

            float stepSize = 1920f / lowResDepthSize.x;
            float bTolerance = 1f - Mathf.Pow(10f, settings.blurTolerance.value) * stepSize;
            bTolerance *= bTolerance;
            float uTolerance = Mathf.Pow(10f, settings.upsampleTolerance.value);
            float noiseFilterWeight = 1f / (Mathf.Pow(10f, settings.noiseFilterTolerance.value) + uTolerance);

            cmd.SetComputeVectorParam(cs, HDShaderIDs._InvLowResolution, new Vector2(1f / lowResDepthSize.x * viewport.x, 1f / lowResDepthSize.y * viewport.y));
            cmd.SetComputeVectorParam(cs, HDShaderIDs._InvHighResolution, new Vector2(1f / highResDepthSize.x * viewport.x, 1f / highResDepthSize.y * viewport.y));
            cmd.SetComputeVectorParam(cs, HDShaderIDs._AdditionalParams, new Vector4(noiseFilterWeight, stepSize, bTolerance, uTolerance));

            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._LoResDB, lowResDepth);
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._HiResDB, highResDepth);
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._LoResAO1, interleavedAO);

            if (highResAO != null)
                cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._HiResAO, highResAO);

            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._AoResult, dest);

            int xcount = ((int)highResDepthSize.x + 17) / 16;
            int ycount = ((int)highResDepthSize.y + 17) / 16;
            cmd.DispatchCompute(cs, kernel, xcount, ycount, 1);
        }
    }
}
