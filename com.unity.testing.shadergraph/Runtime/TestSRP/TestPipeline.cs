using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.ShaderGraph.Tests
{
    public class TestPipeline : UnityEngine.Rendering.RenderPipeline
    {
        private const string k_cameraTag = "TestSRP - Render Camera";
        private ShaderTagId m_shaderTagId = new ShaderTagId("ShaderGraphTestDefaultUnlit");

        public TestPipeline()
        {
            Shader.globalRenderPipeline = "ShaderGraphTestPipeline";
            SetRenderFeatures();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Shader.globalRenderPipeline = "";
            SupportedRenderingFeatures.active = new SupportedRenderingFeatures();
        }

        private static void SetRenderFeatures()
        {
            #if UNITY_EDITOR
            SupportedRenderingFeatures.active = new SupportedRenderingFeatures
            {
                reflectionProbeModes = SupportedRenderingFeatures.ReflectionProbeModes.None,
                defaultMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.None,
                lightmapBakeTypes = LightmapBakeType.Baked,
                lightmapsModes = LightmapsMode.NonDirectional,
                lightProbeProxyVolumes = false,
                motionVectors = false,
                receiveShadows = false,
                reflectionProbes = false
            };
            #endif
        }

        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            if (cameras == null || cameras.Length == 0)
            {
                Debug.LogWarning("The camera list passed to the render pipeline is either null or empty.");
                return;
            }

            SetPerFramShaderConstants();

            int numCams = cameras.Length;
            for(int i = 0; i < numCams; ++i)
            {
                Camera camera = cameras[i];
                
                BeginCameraRendering(camera);
                
                RenderCamera(renderContext, camera);
            }
        }

        public void RenderCamera(ScriptableRenderContext renderContext, Camera camera)
        {
            renderContext.SetupCameraProperties(camera, false);
            SetPerCameraShaderConstants(camera);

            ScriptableCullingParameters cullingParameters;
            
            if(!camera.TryGetCullingParameters(false, out cullingParameters))
            {
                return;
            }

            cullingParameters.shadowDistance = 0.0f;

#if UNITY_EDITOR

            // Emit scene view UI
            if (camera.cameraType == CameraType.SceneView)
            {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            }
#endif
            
            CullingResults cullResults = renderContext.Cull(ref cullingParameters);

            // clear color and depth buffers
            CommandBuffer cmd = CommandBufferPool.Get(k_cameraTag);
            cmd.ClearRenderTarget(true, true, Color.black);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            // draw opaque renderers
            SortingSettings sortingSettings = new SortingSettings(camera);
            sortingSettings.criteria = SortingCriteria.CommonOpaque;

            DrawingSettings drawSettings = new DrawingSettings(m_shaderTagId, sortingSettings);
            
            FilteringSettings filterSettings = new FilteringSettings(RenderQueueRange.opaque);
            
            renderContext.DrawRenderers(cullResults, ref drawSettings, ref filterSettings);
            
            renderContext.Submit();
            
#if UNITY_EDITOR
            UnityEditor.Handles.DrawGizmos(camera);
#endif
        }

        private static void SetPerFramShaderConstants()
        {
            Shader.SetGlobalColor("_Color", Color.red);
        }

        private static void SetPerCameraShaderConstants(Camera camera)
        {
            Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 viewProjMatrix = projMatrix * viewMatrix;
            Matrix4x4 invViewProjMatrix = Matrix4x4.Inverse(viewProjMatrix);
            Shader.SetGlobalMatrix("unity_MatrixVP", viewProjMatrix);
        }
    }
}