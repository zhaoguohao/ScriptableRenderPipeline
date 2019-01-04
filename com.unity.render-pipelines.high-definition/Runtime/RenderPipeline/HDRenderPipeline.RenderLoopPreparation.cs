using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public partial class HDRenderPipeline
    {
        // !! Temporary partial class of HDRenderPipeline !!
        // This is used to refactor the RenderLoop by several small steps
        // It is not intended to use a partial class for HDRenderPipeline for standard API

        unsafe struct RenderLoopPreparation : IDisposable
        {
            const int k_MaxRenderRequests = 64;
            const int k_MaxRenderRequestDependency = 64;

            HDRenderPipeline m_HDRP;
            void* m_StackMemoryBufferPtr;
            Graph<RenderRequest> m_RenderGraph;

            Dictionary<HDProbe, List<Graph.NodeID>> m_RenderRequestIndicesWhereTheProbeIsVisible;
            List<CameraSettings> m_CameraSettings;
            List<CameraPositionSettings> m_CameraPositionSettings;

            public int RequiredStackMemorySize
                => Graph<RenderRequest>.SizeFor(k_MaxRenderRequests, k_MaxRenderRequestDependency);

            public RenderLoopPreparation(HDRenderPipeline hdrp)
            {
                m_HDRP = hdrp;
                m_StackMemoryBufferPtr = null;
                m_RenderRequestIndicesWhereTheProbeIsVisible = DictionaryPool<HDProbe, List<Graph.NodeID>>.Get();
                m_CameraSettings = ListPool<CameraSettings>.Get();
                m_CameraPositionSettings = ListPool<CameraPositionSettings>.Get();
            }

            public void SetAllocatedStackMemory(void* stackMemoryBufferPtr)
            {
                m_StackMemoryBufferPtr = stackMemoryBufferPtr;

                m_RenderGraph = new Graph<RenderRequest>(m_StackMemoryBufferPtr,
                    RequiredStackMemorySize,
                    k_MaxRenderRequests,
                    k_MaxRenderRequestDependency
                );
            }

            public void PrepareFrameAndCull(
                Camera[] cameras,
                List<(HDCamera, HDAdditionalCameraData)> customRenders,
                ScriptableRenderContext renderContext
            )
            {
                if (m_StackMemoryBufferPtr == null)
                    throw new InvalidOperationException("Stackallocked memory was not provided.");

                foreach (var camera in cameras)
                {
                    if (camera == null)
                        continue;

                    // TODO: Very weird callbacks
                    //  They are called at the beginning of a camera render, but the very same camera may not end its rendering
                    //  for various reasons (full screen pass through, custom render, or just invalid parameters)
                    //  and in that case the associated ending call is never called.
                    UnityEngine.Rendering.RenderPipeline.BeginCameraRendering(camera);
                    UnityEngine.Experimental.VFX.VFXManager.ProcessCamera(camera); //Visual Effect Graph is not yet a required package but calling this method when there isn't any VisualEffect component has no effect (but needed for Camera sorting in Visual Effect Graph context)

                    // Reset pooled variables
                    m_CameraSettings.Clear();
                    m_CameraPositionSettings.Clear();

                    var cullingResults = GenericPool<HDCullingResults>.Get();
                    cullingResults.Clear();

                    var skipRequest = false;
                    if (!(m_HDRP.TryCalculateFrameParameters(
                            camera, assetFrameSettingsIsDirty,
                            out var additionalCameraData,
                            out var hdCamera,
                            out var cullingParameters
                        )
                        // Note: In case of a custom render, we have false here and 'TryCull' is not executed
                        && m_HDRP.TryCull(
                            camera, hdCamera, renderContext, cullingParameters,
                            ref cullingResults
                        )))
                    {
                        // We failed either to get proper rendering parameter
                        // Or to cull for this camera
                        skipRequest = true;
                    }

                    if (additionalCameraData != null && additionalCameraData.hasCustomRender)
                    {
                        skipRequest = true;
                        // Execute custom render
                        customRenders.Add((hdCamera, additionalCameraData));
                    }

                    if (skipRequest)
                        GenericPool<HDCullingResults>.Release(cullingResults);
                }
            }

            public void Dispose()
            {
                DictionaryPool<HDProbe, List<Graph.NodeID>>.Release(m_RenderRequestIndicesWhereTheProbeIsVisible);
                ListPool<CameraSettings>.Release(m_CameraSettings);
                ListPool<CameraPositionSettings>.Release(m_CameraPositionSettings);
            }
        }
    }
}
