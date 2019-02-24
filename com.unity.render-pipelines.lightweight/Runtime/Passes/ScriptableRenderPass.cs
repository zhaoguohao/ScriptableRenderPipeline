using System;
using System.Diagnostics;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Rendering.PostProcessing;

namespace UnityEngine.Rendering.LWRP
{
    // Note: Spaced built-in events so we can add events in between them
    // We need to leave room as we sort render passes based on event.
    // Users can also inject render pass events in a specific point by doing RenderPassEvent + offset
    /// <summary>
    /// Controls when the render pass should execute.
    /// </summary>
    public enum RenderPassEvent
    {
        BeforeRendering = 0,
        BeforeRenderingOpaques = 10,
        AfterRenderingOpaques = 20,
        AfterRenderingSkybox = 30,
        AfterRenderingTransparentPasses = 40,
        AfterRendering = 50,
    }

    /// <summary>
    /// Inherit from this class to perform custom rendering in the Lightweight Render Pipeline.
    /// </summary>
    public abstract class ScriptableRenderPass : IComparable<ScriptableRenderPass>
    {
        internal ScriptableRenderContext m_Context;
        internal int m_ColorAttachmentId = -1;
        internal int m_DepthAttachmentId = -1;

        NativeArray<AttachmentDescriptor> m_Attachments;
        protected RenderTargetIdentifier m_BlitSource;
        List<int> m_TemporaryRenderTextures = new List<int>();
        public bool useCameraTarget = true;
        int m_DepthAttachmentIndex = -1;

        public RenderPassEvent renderPassEvent { get; set; }

        public int targetWidth { get; set; }
        public int targetHeight { get; set; }
        public int targetMsaa { get; set; }
        public NativeArray<AttachmentDescriptor> attachments
        {
            get => m_Attachments;
        }

        public int depthAttachmentIndex
        {
            get => m_DepthAttachmentIndex;
        }


        public ScriptableRenderPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            useCameraTarget = true;
            m_DepthAttachmentIndex = -1;
            m_BlitSource = BuiltinRenderTextureType.CameraTarget;
            m_TemporaryRenderTextures.Clear();
            targetWidth = -1;
            targetHeight = -1;
            targetMsaa = -1;
        }

        List<ShaderTagId> m_ShaderTagIDs = new List<ShaderTagId>();

        static List<ShaderTagId> m_LegacyShaderPassNames = new List<ShaderTagId>()
        {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM"),
        };

        static Material s_ErrorMaterial;
        static Material errorMaterial
        {
            get
            {
                if (s_ErrorMaterial == null)
                    s_ErrorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));

                return s_ErrorMaterial;
            }
        }

        static Mesh s_FullscreenMesh = null;
        public static Mesh fullscreenMesh
        {
            get
            {
                if (s_FullscreenMesh != null)
                    return s_FullscreenMesh;

                float topV = 1.0f;
                float bottomV = 0.0f;

                s_FullscreenMesh = new Mesh { name = "Fullscreen Quad" };
                s_FullscreenMesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(-1.0f,  1.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, 0.0f),
                    new Vector3(1.0f,  1.0f, 0.0f)
                });

                s_FullscreenMesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0.0f, bottomV),
                    new Vector2(0.0f, topV),
                    new Vector2(1.0f, bottomV),
                    new Vector2(1.0f, topV)
                });

                s_FullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
                s_FullscreenMesh.UploadMeshData(true);
                return s_FullscreenMesh;
            }
        }

        static PostProcessRenderContext m_PostProcessRenderContext;
        internal static PostProcessRenderContext postProcessRenderContext
        {
            get
            {
                if (m_PostProcessRenderContext == null)
                    m_PostProcessRenderContext = new PostProcessRenderContext();

                return m_PostProcessRenderContext;
            }
        }

        public RenderTargetIdentifier CreateTemporaryRT(int rtId, RenderTextureDescriptor descriptor, FilterMode filterMode)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Create Temporary RT");
            cmd.GetTemporaryRT(rtId, descriptor, filterMode);
            m_Context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            m_TemporaryRenderTextures.Add(rtId);
            return new RenderTargetIdentifier(rtId);
        }

        public void ConfigureRenderTarget(int width, int height, int msaaSamples,
            AttachmentDescriptor colorAttachment, AttachmentDescriptor depthAttachment)
        {
            useCameraTarget = false;
            targetWidth = width;
            targetHeight = height;
            targetMsaa = msaaSamples;
            m_Attachments = new NativeArray<AttachmentDescriptor>(2, Allocator.Persistent);
            m_Attachments[0] = colorAttachment;
            m_Attachments[1] = depthAttachment;
            m_DepthAttachmentIndex = m_Attachments.Length - 1;
        }

        public void ConfigureRenderTarget(int width, int height, int msaaSamples,
            AttachmentDescriptor attachment, bool isDepthAttachment = false)
        {
            useCameraTarget = false;
            targetWidth = width;
            targetHeight = height;
            targetMsaa = msaaSamples;
            m_Attachments = new NativeArray<AttachmentDescriptor>(1, Allocator.Persistent);
            m_Attachments[0] = attachment;
            m_DepthAttachmentIndex = (isDepthAttachment) ? 0 : -1;
        }

        public void ConfigureRendetTargetForBlit(RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            useCameraTarget = false;
            AttachmentDescriptor destinationAttachment = new AttachmentDescriptor(RenderTextureFormat.ARGB32);
            destinationAttachment.ConfigureTarget(destination, false, true);
            m_Attachments = new NativeArray<AttachmentDescriptor>(1, Allocator.Persistent);
            m_BlitSource = source;
        }

        /// <summary>
        /// Cleanup any allocated data that was created during the execution of the pass.
        /// </summary>
        /// <param name="cmd">Use this CommandBuffer to cleanup any generated data</param>
        public virtual void FrameCleanup(CommandBuffer cmd)
        {
            if (!useCameraTarget)
                m_Attachments.Dispose();

            for (int i = 0; i < m_TemporaryRenderTextures.Count; ++i)
                cmd.ReleaseTemporaryRT(m_TemporaryRenderTextures[i]);
            m_TemporaryRenderTextures.Clear();
        }

        /// <summary>
        /// Implement this to conditionally enqueue the pass depending on rendering state for the current frame.
        /// By default a render pass will always be enqueued for execution.
        /// </summary>
        /// <param name="renderingData">Current rendering state information</param>
        /// <returns></returns>
        public virtual bool ShouldExecute(ref RenderingData renderingData)
        {
            return true;
        }

        /// <summary>
        /// Execute the pass. This is where custom rendering occurs. Specific details are left to the implementation
        /// </summary>
        /// <param name="renderer">The currently executing renderer. Contains configuration for the current execute call.</param>
        /// <param name="context">Use this render context to issue any draw commands during execution</param>
        /// <param name="renderingData">Current rendering state information</param>
        public abstract void Execute(ScriptableRenderContext context, ref RenderingData renderingData);

        public int CompareTo(ScriptableRenderPass other)
        {
            return (int)renderPassEvent - (int)other.renderPassEvent;
        }

        protected void RegisterShaderPassName(string passName)
        {
            m_ShaderTagIDs.Add(new ShaderTagId(passName));
        }

        /// <summary>
        /// Renders PostProcessing.
        /// </summary>
        /// <param name="cameraData">Camera rendering data.</param>
        /// <param name="colorFormat">Color format of the source render target id.</param>
        /// <param name="source">Source render target id.</param>
        /// <param name="dest">Destination render target id.</param>
        /// <param name="opaqueOnly">Should only execute after opaque post processing effects.</param>
        /// <param name="flip">Should flip the image vertically.</param>
        protected void RenderPostProcess(ref CameraData cameraData, RenderTextureFormat colorFormat, RenderTargetIdentifier source, RenderTargetIdentifier dest, bool opaqueOnly, bool flip)
        {
            PostProcessLayer layer = cameraData.postProcessLayer;
            int effectsCount;
            if (opaqueOnly)
            {
                effectsCount = layer.sortedBundles[PostProcessEvent.BeforeTransparent].Count;
            }
            else
            {
                effectsCount = layer.sortedBundles[PostProcessEvent.BeforeStack].Count +
                               layer.sortedBundles[PostProcessEvent.AfterStack].Count;
            }

            CommandBuffer cmd = CommandBufferPool.Get("Render Post Process");
            Camera camera = cameraData.camera;
            postProcessRenderContext.Reset();
            postProcessRenderContext.camera = camera;
            postProcessRenderContext.source = source;
            postProcessRenderContext.sourceFormat = colorFormat;
            postProcessRenderContext.destination = dest;
            postProcessRenderContext.command = cmd;
            postProcessRenderContext.flip = flip;

            // If there's only one effect in the stack and soure is same as dest we
            // create an intermediate blit rendertarget to handle it.
            // Otherwise, PostProcessing system will create the intermediate blit targets itself.
            if (effectsCount == 1 && source == dest)
            {
                var tempRtId = new RenderTargetIdentifier(BuiltinShaderPropertyId.temporaryTexture);
                postProcessRenderContext.destination = tempRtId;
                cmd.GetTemporaryRT(BuiltinShaderPropertyId.temporaryTexture, cameraData.cameraTargetDescriptor, FilterMode.Point);
                if (opaqueOnly)
                    cameraData.postProcessLayer.RenderOpaqueOnly(postProcessRenderContext);
                else
                    cameraData.postProcessLayer.Render(postProcessRenderContext);
                cmd.Blit(tempRtId, dest);
            }
            else
            {
                if (opaqueOnly)
                    cameraData.postProcessLayer.RenderOpaqueOnly(postProcessRenderContext);
                else
                    cameraData.postProcessLayer.Render(postProcessRenderContext);
            }

            m_Context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// Creates <c>DrawingSettings</c> based on current rendering state.
        /// </summary>
        /// <param name="renderingData">Current rendering state.</param>
        /// <param name="sortingCriteria">Criteria to sort objects being rendered.</param>
        /// <returns></returns>
        /// <seealso cref="DrawingSettings"/>
        protected DrawingSettings CreateDrawingSettings(ref RenderingData renderingData, SortingCriteria sortingCriteria)
        {
            Camera camera = renderingData.cameraData.camera;
            SortingSettings sortingSettings = new SortingSettings(camera) { criteria = sortingCriteria };
            DrawingSettings settings = new DrawingSettings(m_ShaderTagIDs[0], sortingSettings)
            {
                perObjectData = renderingData.perObjectData,
                enableInstancing = true,
                mainLightIndex = renderingData.lightData.mainLightIndex,
                enableDynamicBatching = renderingData.supportsDynamicBatching,
            };
            for (int i = 1; i < m_ShaderTagIDs.Count; ++i)
                settings.SetShaderPassName(i, m_ShaderTagIDs[i]);
            return settings;
        }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        internal void RenderObjectsWithError(ScriptableRenderContext context, ref CullingResults cullResults, Camera camera, FilteringSettings filterSettings, SortingCriteria sortFlags)
        {
            SortingSettings sortingSettings = new SortingSettings(camera) { criteria = sortFlags };
            DrawingSettings errorSettings = new DrawingSettings(m_LegacyShaderPassNames[0], sortingSettings)
            {
                perObjectData = PerObjectData.None,
                overrideMaterial = errorMaterial,
                overrideMaterialPassIndex = 0
            };
            for (int i = 1; i < m_LegacyShaderPassNames.Count; ++i)
                errorSettings.SetShaderPassName(i, m_LegacyShaderPassNames[i]);

            context.DrawRenderers(cullResults, ref errorSettings, ref filterSettings);
        }

        public void SetRenderTarget(
            CommandBuffer cmd,
            RenderTargetIdentifier colorAttachment,
            RenderBufferLoadAction colorLoadAction,
            RenderBufferStoreAction colorStoreAction,
            ClearFlag clearFlags,
            Color clearColor,
            TextureDimension dimension)
        {
            if (dimension == TextureDimension.Tex2DArray)
                CoreUtils.SetRenderTarget(cmd, colorAttachment, clearFlags, clearColor, 0, CubemapFace.Unknown, -1);
            else
                CoreUtils.SetRenderTarget(cmd, colorAttachment, colorLoadAction, colorStoreAction, clearFlags, clearColor);
        }

        public void SetRenderTarget(
            CommandBuffer cmd,
            RenderTargetIdentifier colorAttachment,
            RenderBufferLoadAction colorLoadAction,
            RenderBufferStoreAction colorStoreAction,
            RenderTargetIdentifier depthAttachment,
            RenderBufferLoadAction depthLoadAction,
            RenderBufferStoreAction depthStoreAction,
            ClearFlag clearFlags,
            Color clearColor,
            TextureDimension dimension)
        {
            if (depthAttachment == BuiltinRenderTextureType.CameraTarget)
            {
                SetRenderTarget(cmd, colorAttachment, colorLoadAction, colorStoreAction, clearFlags, clearColor,
                    dimension);
            }
            else
            {
                if (dimension == TextureDimension.Tex2DArray)
                    CoreUtils.SetRenderTarget(cmd, colorAttachment, depthAttachment,
                        clearFlags, clearColor, 0, CubemapFace.Unknown, -1);
                else
                    CoreUtils.SetRenderTarget(cmd, colorAttachment, colorLoadAction, colorStoreAction,
                        depthAttachment, depthLoadAction, depthStoreAction, clearFlags, clearColor);
            }
        }
    }
}
