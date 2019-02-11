using System.Collections.Generic;
using UnityEngine.Rendering;
using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine.Experimental.GlobalIllumination;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public class HDRenderPipeline : UnityEngine.Rendering.RenderPipeline
    {
        public const string k_ShaderTagName = "HDRenderPipeline";
        const string k_OldQualityShadowKey = "HDRP:oldQualityShadows";

        enum ForwardPass
        {
            Opaque,
            PreRefraction,
            Transparent
        }

        static readonly string[] k_ForwardPassDebugName =
        {
            "Forward Opaque Debug",
            "Forward PreRefraction Debug",
            "Forward Transparent Debug"
        };

        static readonly string[] k_ForwardPassName =
        {
            "Forward Opaque",
            "Forward PreRefraction",
            "Forward Transparent"
        };

        readonly HDRenderPipelineAsset m_Asset;
        public HDRenderPipelineAsset asset { get { return m_Asset; } }

        DiffusionProfileSettings m_InternalSSSAsset;
        public DiffusionProfileSettings diffusionProfileSettings
        {
            get
            {
                // If no SSS asset is set, build / reuse an internal one for simplicity
                var asset = m_Asset.diffusionProfileSettings;

                if (asset == null)
                {
                    if (m_InternalSSSAsset == null)
                        m_InternalSSSAsset = ScriptableObject.CreateInstance<DiffusionProfileSettings>();

                    asset = m_InternalSSSAsset;
                }

                return asset;
            }
        }
        public RenderPipelineSettings renderPipelineSettings { get { return m_Asset.renderPipelineSettings; } }

        public bool IsInternalDiffusionProfile(DiffusionProfileSettings profile)
        {
            return m_InternalSSSAsset == profile;
        }

        readonly List<RenderPipelineMaterial> m_MaterialList = new List<RenderPipelineMaterial>();

        readonly DBufferManager m_DbufferManager = new DBufferManager();
        readonly SubsurfaceScatteringManager m_SSSBufferManager = new SubsurfaceScatteringManager();

        RTManager m_RTManager;
        readonly PostProcessSystem m_PostProcessSystem;

        public bool frameSettingsHistoryEnabled = false;

#if ENABLE_RAYTRACING
        public HDRaytracingManager m_RayTracingManager = new HDRaytracingManager();
        readonly HDRaytracingReflections m_RaytracingReflections = new HDRaytracingReflections();
        readonly HDRaytracingShadowManager m_RaytracingShadows = new HDRaytracingShadowManager();
        readonly HDRaytracingRenderer m_RaytracingRenderer = new HDRaytracingRenderer();
#endif

        // Renderer Bake configuration can vary depends on if shadow mask is enabled or no
        PerObjectData m_currentRendererConfigurationBakedLighting = HDUtils.k_RendererConfigurationBakedLighting;
        Material m_CopyStencil;
        MaterialPropertyBlock m_CopyDepthPropertyBlock = new MaterialPropertyBlock();
        Material m_CopyDepth;
        GPUCopy m_GPUCopy;
        MipGenerator m_MipGenerator;
        BlueNoise m_BlueNoise;

        IBLFilterBSDF[] m_IBLFilterArray = null;


        ComputeShader m_ScreenSpaceReflectionsCS { get { return m_Asset.renderPipelineResources.shaders.screenSpaceReflectionsCS; } }
        int m_SsrTracingKernel      = -1;
        int m_SsrReprojectionKernel = -1;

        ComputeShader m_applyDistortionCS { get { return m_Asset.renderPipelineResources.shaders.applyDistortionCS; } }
        int m_applyDistortionKernel;

        Material m_CameraMotionVectorsMaterial;
        Material m_DecalNormalBufferMaterial;

        // Debug material
        Material m_DebugViewMaterialGBuffer;
        Material m_DebugViewMaterialGBufferShadowMask;
        Material m_currentDebugViewMaterialGBuffer;
        Material m_DebugDisplayLatlong;
        Material m_DebugFullScreen;
        MaterialPropertyBlock m_DebugFullScreenPropertyBlock = new MaterialPropertyBlock();
        Material m_DebugColorPicker;
        Material m_ErrorMaterial;

        Material m_Blit;
        MaterialPropertyBlock m_BlitPropertyBlock = new MaterialPropertyBlock();

        RenderTargetIdentifier[] m_MRTCache2 = new RenderTargetIdentifier[2];

        // The current MSAA count
        MSAASamples m_MSAASamples;
        // MSAA resolve materials
        Material m_DepthResolveMaterial = null;
        Material m_ColorResolveMaterial = null;
        MaterialPropertyBlock m_MSAAPropertyBlock = new MaterialPropertyBlock();

        // The pass "SRPDefaultUnlit" is a fall back to legacy unlit rendering and is required to support unity 2d + unity UI that render in the scene.
        ShaderTagId[] m_ForwardAndForwardOnlyPassNames = { HDShaderPassNames.s_ForwardOnlyName, HDShaderPassNames.s_ForwardName, HDShaderPassNames.s_SRPDefaultUnlitName };
        ShaderTagId[] m_ForwardOnlyPassNames = { HDShaderPassNames.s_ForwardOnlyName, HDShaderPassNames.s_SRPDefaultUnlitName };

        ShaderTagId[] m_AllTransparentPassNames = {  HDShaderPassNames.s_TransparentBackfaceName,
                                                        HDShaderPassNames.s_ForwardOnlyName,
                                                        HDShaderPassNames.s_ForwardName,
                                                        HDShaderPassNames.s_SRPDefaultUnlitName };

        ShaderTagId[] m_TransparentNoBackfaceNames = {  HDShaderPassNames.s_ForwardOnlyName,
                                                        HDShaderPassNames.s_ForwardName,
                                                        HDShaderPassNames.s_SRPDefaultUnlitName };


        ShaderTagId[] m_AllForwardOpaquePassNames = {    HDShaderPassNames.s_ForwardOnlyName,
                                                            HDShaderPassNames.s_ForwardName,
                                                            HDShaderPassNames.s_SRPDefaultUnlitName };

        ShaderTagId[] m_DepthOnlyAndDepthForwardOnlyPassNames = { HDShaderPassNames.s_DepthForwardOnlyName, HDShaderPassNames.s_DepthOnlyName };
        ShaderTagId[] m_DepthForwardOnlyPassNames = { HDShaderPassNames.s_DepthForwardOnlyName };
        ShaderTagId[] m_DepthOnlyPassNames = { HDShaderPassNames.s_DepthOnlyName };
        ShaderTagId[] m_TransparentDepthPrepassNames = { HDShaderPassNames.s_TransparentDepthPrepassName };
        ShaderTagId[] m_TransparentDepthPostpassNames = { HDShaderPassNames.s_TransparentDepthPostpassName };
        ShaderTagId[] m_ForwardErrorPassNames = { HDShaderPassNames.s_AlwaysName, HDShaderPassNames.s_ForwardBaseName, HDShaderPassNames.s_DeferredName, HDShaderPassNames.s_PrepassBaseName, HDShaderPassNames.s_VertexName, HDShaderPassNames.s_VertexLMRGBMName, HDShaderPassNames.s_VertexLMName };
        ShaderTagId[] m_SinglePassName = new ShaderTagId[1];

        // Stencil usage in HDRenderPipeline.
        // Currently we use only 2 bits to identify the kind of lighting that is expected from the render pipeline
        // Usage is define in LightDefinitions.cs
        [Flags]
        public enum StencilBitMask
        {
            Clear                           = 0,    // 0x0
            LightingMask                    = 7,    // 0x7  - 3 bit
            Decals                          = 8,    // 0x8  - 1 bit
            DecalsForwardOutputNormalBuffer = 16,   // 0x10 - 1 bit
            DoesntReceiveSSR                = 32,   // 0x20 - 1 bit
            ObjectVelocity                  = 128,  // 0x80 - 1 bit
            All                             = 255   // 0xFF - 8 bit
        }

        RenderStateBlock m_DepthStateOpaque;

        // Detect when windows size is changing
        int m_CurrentWidth;
        int m_CurrentHeight;

        // Use to detect frame changes
        uint  m_FrameCount;
        float m_LastTime, m_Time;

        public int GetCurrentShadowCount() { return m_LightLoop.GetCurrentShadowCount(); }
        public int GetDecalAtlasMipCount()
        {
            int highestDim = Math.Max(renderPipelineSettings.decalSettings.atlasWidth, renderPipelineSettings.decalSettings.atlasHeight);
            return (int)Math.Log(highestDim, 2);
        }

        readonly SkyManager m_SkyManager = new SkyManager();
        readonly LightLoop m_LightLoop = new LightLoop();
        readonly VolumetricLightingSystem m_VolumetricLightingSystem = new VolumetricLightingSystem();
        readonly AmbientOcclusionSystem m_AmbientOcclusionSystem;

        // Debugging
        MaterialPropertyBlock m_SharedPropertyBlock = new MaterialPropertyBlock();
        DebugDisplaySettings m_DebugDisplaySettings = new DebugDisplaySettings();
        public DebugDisplaySettings debugDisplaySettings { get { return m_DebugDisplaySettings; } }
        static DebugDisplaySettings s_NeutralDebugDisplaySettings = new DebugDisplaySettings();
        DebugDisplaySettings m_CurrentDebugDisplaySettings;
        bool                            m_FullScreenDebugPushed;
        bool                            m_ValidAPI; // False by default mean we render normally, true mean we don't render anything
        bool                            m_IsDepthBufferCopyValid;
        RTHandleSystem.RTHandle         m_TemporaryTargetForCubemaps;
        Stack<Camera>                   m_ProbeCameraPool = new Stack<Camera>();

        RenderTargetIdentifier[] m_MRTWithSSS;
        string m_ForwardPassProfileName;

        Vector2Int m_PyramidSizeV2I = new Vector2Int();
        Vector4 m_PyramidSizeV4F = new Vector4();
        Vector4 m_PyramidScaleLod = new Vector4();
        Vector4 m_PyramidScale = new Vector4();

        public Material GetBlitMaterial() { return m_Blit; }

        ComputeBuffer m_DepthPyramidMipLevelOffsetsBuffer = null;


        ScriptableCullingParameters frozenCullingParams;
        bool frozenCullingParamAvailable = false;

        public bool showCascade
        {
            get => m_DebugDisplaySettings.GetDebugLightingMode() == DebugLightingMode.VisualizeCascade;
            set
            {
                if (value)
                    m_DebugDisplaySettings.SetDebugLightingMode(DebugLightingMode.VisualizeCascade);
                else
                    m_DebugDisplaySettings.SetDebugLightingMode(DebugLightingMode.None);
            }
        }

        protected virtual RTManager CreateRTManager()
        {
            return new RTManager();
        }

        RTHandleSystem.RTHandle GetRenderTarget(RT target)
        {
            return m_RTManager.GetRenderTarget(target);
        }

        public HDRenderPipeline(HDRenderPipelineAsset asset)
        {
            m_Asset = asset;
            HDProbeSystem.Parameters = asset.reflectionSystemParameters;

            DebugManager.instance.RefreshEditor();

            m_ValidAPI = true;

            if (!SetRenderingFeatures())
            {
                m_ValidAPI = false;

                return;
            }

#if UNITY_EDITOR
            // The first thing we need to do is to set the defines that depend on the render pipeline settings
            m_Asset.EvaluateSettings();

            // Check that the serialized Resources are not broken
            if ((GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineResources == null)
                (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineResources
                    = UnityEditor.AssetDatabase.LoadAssetAtPath<RenderPipelineResources>(HDUtils.GetHDRenderPipelinePath() + "Runtime/RenderPipelineResources/HDRenderPipelineResources.asset");
            if ((GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineEditorResources == null)
                (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineEditorResources
                    = UnityEditor.AssetDatabase.LoadAssetAtPath<HDRenderPipelineEditorResources>(HDUtils.GetHDRenderPipelinePath() + "Editor/RenderPipelineResources/HDRenderPipelineEditorResources.asset");
#endif

            // Upgrade the resources (re-import every references in RenderPipelineResources) if the resource version mismatches
            // It's done here because we know every HDRP assets have been imported before
            UpgradeResourcesIfNeeded();

            // Initial state of the RTHandle system.
            // Tells the system that we will require MSAA or not so that we can avoid wasteful render texture allocation.
            // TODO: Might want to initialize to at least the window resolution to avoid un-necessary re-alloc in the player
            RTHandles.Initialize(1, 1, m_Asset.renderPipelineSettings.supportMSAA, m_Asset.renderPipelineSettings.msaaSampleCount);

            m_GPUCopy = new GPUCopy(asset.renderPipelineResources.shaders.copyChannelCS);

            m_MipGenerator = new MipGenerator(m_Asset);
            m_BlueNoise = new BlueNoise(m_Asset);

            EncodeBC6H.DefaultInstance = EncodeBC6H.DefaultInstance ?? new EncodeBC6H(asset.renderPipelineResources.shaders.encodeBC6HCS);

            // Scan material list and assign it
            m_MaterialList = HDUtils.GetRenderPipelineMaterialList();

            m_SSSBufferManager.Build(asset);
            m_PostProcessSystem = new PostProcessSystem(asset);
            m_AmbientOcclusionSystem = new AmbientOcclusionSystem(asset);

            // Initialize various compute shader resources
            m_applyDistortionKernel = m_applyDistortionCS.FindKernel("KMain");
            m_SsrTracingKernel      = m_ScreenSpaceReflectionsCS.FindKernel("ScreenSpaceReflectionsTracing");
            m_SsrReprojectionKernel = m_ScreenSpaceReflectionsCS.FindKernel("ScreenSpaceReflectionsReprojection");

            // General material
            m_CopyStencil = CoreUtils.CreateEngineMaterial(asset.renderPipelineResources.shaders.copyStencilBufferPS);
            m_CameraMotionVectorsMaterial = CoreUtils.CreateEngineMaterial(asset.renderPipelineResources.shaders.cameraMotionVectorsPS);
            m_DecalNormalBufferMaterial = CoreUtils.CreateEngineMaterial(asset.renderPipelineResources.shaders.decalNormalBufferPS);
            m_CopyDepth = CoreUtils.CreateEngineMaterial(asset.renderPipelineResources.shaders.copyDepthBufferPS);

            InitializeDebugMaterials();

            m_MaterialList.ForEach(material => material.Build(asset));

            if(m_Asset.renderPipelineSettings.lightLoopSettings.supportFabricConvolution)
            {
                m_IBLFilterArray = new IBLFilterBSDF[2];
                m_IBLFilterArray[0] = new IBLFilterGGX(asset.renderPipelineResources, m_MipGenerator);
                m_IBLFilterArray[1] = new IBLFilterCharlie(asset.renderPipelineResources, m_MipGenerator);
            }
            else
            {
                m_IBLFilterArray = new IBLFilterBSDF[1];
                m_IBLFilterArray[0] = new IBLFilterGGX(asset.renderPipelineResources, m_MipGenerator);
            }

            m_LightLoop.Build(asset, m_IBLFilterArray);
            m_SkyManager.Build(asset, m_IBLFilterArray);
            m_VolumetricLightingSystem.Build(asset);

            m_DebugDisplaySettings.RegisterDebug();

#if UNITY_EDITOR
            // We don't need the debug of Scene View at runtime (each camera have its own debug settings)
            // All scene view will share the same FrameSettings for now as sometimes Dispose is called after
            // another instance of HDRenderPipeline constructor is called.

            Camera firstSceneViewCamera = UnityEditor.SceneView.sceneViews.Count > 0 ? (UnityEditor.SceneView.sceneViews[0] as UnityEditor.SceneView).camera : null;
            if (firstSceneViewCamera != null && !FrameSettingsHistory.isRegisteredSceneViewCamera(firstSceneViewCamera))
            {
                var history = FrameSettingsHistory.RegisterDebug(firstSceneViewCamera, null);
                DebugManager.instance.RegisterData(history);
            }
#endif

            m_DepthPyramidMipLevelOffsetsBuffer = new ComputeBuffer(15, sizeof(int) * 2);

            m_RTManager = CreateRTManager();
            m_RTManager.Initialize(m_Asset);

            // For debugging
            MousePositionDebug.instance.Build();

            InitializeRenderStateBlocks();

            // Keep track of the original msaa sample value
            m_MSAASamples = m_Asset ? m_Asset.renderPipelineSettings.msaaSampleCount : MSAASamples.None;
            m_DepthResolveMaterial = CoreUtils.CreateEngineMaterial(asset.renderPipelineResources.shaders.depthValuesPS);
            m_ColorResolveMaterial = CoreUtils.CreateEngineMaterial(asset.renderPipelineResources.shaders.colorResolvePS);

            // Propagate it to the debug menu
            m_DebugDisplaySettings.data.msaaSamples = m_MSAASamples;

            m_MRTWithSSS = new RenderTargetIdentifier[2 + SubsurfaceScatteringManager.sssBufferCount];
#if ENABLE_RAYTRACING
            m_RayTracingManager.Init(m_Asset.renderPipelineSettings, m_Asset.renderPipelineResources, m_BlueNoise);
            m_RaytracingReflections.Init(m_Asset, m_SkyManager, m_RayTracingManager, m_RTManager);
            m_RaytracingShadows.Init(m_Asset, m_RayTracingManager, m_RTManager, m_LightLoop);
            m_RaytracingRenderer.Init(m_Asset, m_SkyManager, m_RayTracingManager, m_RTManager);
            m_LightLoop.InitRaytracing(m_RayTracingManager);
            m_AmbientOcclusionSystem.InitRaytracing(m_RayTracingManager, m_RTManager);
#endif
        }

        void UpgradeResourcesIfNeeded()
        {
#if UNITY_EDITOR
            //possible because deserialized along the asset
            m_Asset.renderPipelineResources.UpgradeIfNeeded();
            //however it is not possible to call the same way the editor resources.
            //Reason: we pass here before the asset are accessible on disk.
            //Migration is done at deserialisation time for this scriptableobject
            //Note: renderPipelineResources can be migrated at deserialisation time too to have a more unified API
#endif
        }

        bool SetRenderingFeatures()
        {
            // Set sub-shader pipeline tag
            Shader.globalRenderPipeline = "HDRenderPipeline";

            // HD use specific GraphicsSettings
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;

            // TODO: Unity engine introduced a bug that breaks SRP batcher on metal :( disabling it for now.
            GraphicsSettings.useScriptableRenderPipelineBatching = m_Asset.enableSRPBatcher && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Metal;

            SupportedRenderingFeatures.active = new SupportedRenderingFeatures()
            {
                reflectionProbeModes = SupportedRenderingFeatures.ReflectionProbeModes.Rotation,
                defaultMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.IndirectOnly,
                mixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeModes.IndirectOnly | SupportedRenderingFeatures.LightmapMixedBakeModes.Shadowmask,
                lightmapBakeTypes = LightmapBakeType.Baked | LightmapBakeType.Mixed | LightmapBakeType.Realtime,
                lightmapsModes = LightmapsMode.NonDirectional | LightmapsMode.CombinedDirectional,
                lightProbeProxyVolumes = true,
                motionVectors = true,
                receiveShadows = false,
                reflectionProbes = true,
                rendererPriority = true,
                overridesEnvironmentLighting = true,
                overridesFog = true,
                overridesOtherLightingSettings = true,
                editableMaterialRenderQueue = false
            };

            Lightmapping.SetDelegate(GlobalIlluminationUtils.hdLightsDelegate);

#if UNITY_EDITOR
            SceneViewDrawMode.SetupDrawMode();

            if (UnityEditor.PlayerSettings.colorSpace == ColorSpace.Gamma)
            {
                Debug.LogError("High Definition Render Pipeline doesn't support Gamma mode, change to Linear mode");
            }
#endif

            GraphicsDeviceType unsupportedDeviceType;
            if (!IsSupportedPlatform(out unsupportedDeviceType))
            {
                CoreUtils.DisplayUnsupportedAPIMessage(unsupportedDeviceType.ToString());

                // Display more information to the users when it should have use Metal instead of OpenGL
                if (SystemInfo.graphicsDeviceType.ToString().StartsWith("OpenGL"))
                {
                    if (SystemInfo.operatingSystem.StartsWith("Mac"))
                        CoreUtils.DisplayUnsupportedMessage("Use Metal API instead.");
                    else if (SystemInfo.operatingSystem.StartsWith("Windows"))
                        CoreUtils.DisplayUnsupportedMessage("Use Vulkan API instead.");
                }

                return false;
            }

#if UNITY_EDITOR
            //force shadow mode to HardAndSoft while in HDRP context and save legacy settings
            // /!\ it is important to do it there as we only want to do it if we truly go in hdrp
            int currentQuality = QualitySettings.GetQualityLevel();
            int length = QualitySettings.names.Length;
            string[] oldShadowQuality = new string[length];
            for(int i = 0; i < QualitySettings.names.Length; ++i)
            {
                //the only way to get/set a shadow quality is to change the quality level first with current API
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                oldShadowQuality[i] = QualitySettings.shadows.ToString();
                QualitySettings.shadows = ShadowQuality.All;
            }
            QualitySettings.SetQualityLevel(currentQuality, applyExpensiveChanges: false);
            UnityEditor.EditorPrefs.SetString(k_OldQualityShadowKey, string.Join(",", oldShadowQuality));
#endif

            return true;
        }

        // Note: If you add new platform in this function, think about adding support when building the player to in HDRPCustomBuildProcessor.cs
        bool IsSupportedPlatform(out GraphicsDeviceType unsupportedGraphicDevice)
        {
            unsupportedGraphicDevice = SystemInfo.graphicsDeviceType;

            if (!SystemInfo.supportsComputeShaders)
                return false;

#if UNITY_EDITOR
            UnityEditor.BuildTarget activeBuildTarget = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
            // If the build target matches the operating system of the editor
            if (SystemInfo.operatingSystemFamily == HDUtils.BuildTargetToOperatingSystemFamily(activeBuildTarget))
            {
                bool autoAPI = UnityEditor.PlayerSettings.GetUseDefaultGraphicsAPIs(activeBuildTarget);

                // then, there is two configuration possible:
                if (autoAPI)
                {
                    // if the graphic api is chosen automatically, then only the system's graphic device type matters
                    if (!HDUtils.IsSupportedGraphicDevice(SystemInfo.graphicsDeviceType))
                        return false;
                }
                else
                {
                    // otherwise, we need to iterate over every graphic api available in the list to track every non-supported APIs
                    return HDUtils.AreGraphicsAPIsSupported(activeBuildTarget, out unsupportedGraphicDevice);
                }
            }
            else // if the build target does not match the editor OS, then we have to check using the graphic api list
            {
                return HDUtils.AreGraphicsAPIsSupported(activeBuildTarget, out unsupportedGraphicDevice);
            }

            if (!HDUtils.IsSupportedBuildTarget(activeBuildTarget))
                return false;
#else
            if (!HDUtils.IsSupportedGraphicDevice(SystemInfo.graphicsDeviceType))
                return false;
#endif

            if (!HDUtils.IsOperatingSystemSupported(SystemInfo.operatingSystem))
                return false;

            return true;
        }

        void UnsetRenderingFeatures()
        {
            Shader.globalRenderPipeline = "";

            SupportedRenderingFeatures.active = new SupportedRenderingFeatures();

            // Reset srp batcher state just in case
            GraphicsSettings.useScriptableRenderPipelineBatching = false;

            Lightmapping.ResetDelegate();
        }

        void InitializeDebugMaterials()
        {
            m_DebugViewMaterialGBuffer = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.shaders.debugViewMaterialGBufferPS);
            m_DebugViewMaterialGBufferShadowMask = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.shaders.debugViewMaterialGBufferPS);
            m_DebugViewMaterialGBufferShadowMask.EnableKeyword("SHADOWS_SHADOWMASK");
            m_DebugDisplayLatlong = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.shaders.debugDisplayLatlongPS);
            m_DebugFullScreen = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.shaders.debugFullScreenPS);
            m_DebugColorPicker = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.shaders.debugColorPickerPS);
            m_Blit = CoreUtils.CreateEngineMaterial(m_Asset.renderPipelineResources.shaders.blitPS);
            m_ErrorMaterial = CoreUtils.CreateEngineMaterial("Hidden/InternalErrorShader");
        }

        void InitializeRenderStateBlocks()
        {
            m_DepthStateOpaque = new RenderStateBlock
            {
                depthState = new DepthState(true, CompareFunction.LessEqual),
                mask = RenderStateMask.Depth
            };
        }

        protected override void Dispose(bool disposing)
        {
            while (m_ProbeCameraPool.Count > 0)
                CoreUtils.Destroy(m_ProbeCameraPool.Pop().gameObject);

            UnsetRenderingFeatures();

            if (!m_ValidAPI)
                return;

#if UNITY_EDITOR
            //restore shadow mode for all legacy quality settings
            if (UnityEditor.EditorPrefs.HasKey(k_OldQualityShadowKey))
            {
                int currentQuality = QualitySettings.GetQualityLevel();
                int length = QualitySettings.names.Length;
                string[] oldShadowQuality = UnityEditor.EditorPrefs.GetString(k_OldQualityShadowKey).Split(',');
                for (int i = 0; i < QualitySettings.names.Length; ++i)
                {
                    //the only way to get/set a shadow quality is to change the quality level first with current API
                    QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                    QualitySettings.shadows = (ShadowQuality)Enum.Parse(typeof(ShadowQuality), oldShadowQuality[i]);
                }
                QualitySettings.SetQualityLevel(currentQuality, applyExpensiveChanges: false);
                UnityEditor.EditorPrefs.DeleteKey(k_OldQualityShadowKey);
            }
#endif

            base.Dispose(disposing);

#if ENABLE_RAYTRACING
            m_RaytracingRenderer.Release();
            m_RaytracingShadows.Release();
            m_RaytracingReflections.Release();
            m_RayTracingManager.Release();
#endif
            m_DebugDisplaySettings.UnregisterDebug();

            m_LightLoop.Cleanup();

            // For debugging
            MousePositionDebug.instance.Cleanup();

            DecalSystem.instance.Cleanup();

            m_MaterialList.ForEach(material => material.Cleanup());

            CoreUtils.Destroy(m_CopyStencil);
            CoreUtils.Destroy(m_CameraMotionVectorsMaterial);
            CoreUtils.Destroy(m_DecalNormalBufferMaterial);

            CoreUtils.Destroy(m_DebugViewMaterialGBuffer);
            CoreUtils.Destroy(m_DebugViewMaterialGBufferShadowMask);
            CoreUtils.Destroy(m_DebugDisplayLatlong);
            CoreUtils.Destroy(m_DebugFullScreen);
            CoreUtils.Destroy(m_DebugColorPicker);
            CoreUtils.Destroy(m_Blit);
            CoreUtils.Destroy(m_CopyDepth);
            CoreUtils.Destroy(m_ErrorMaterial);

            m_SSSBufferManager.Cleanup();
            m_SkyManager.Cleanup();
            m_VolumetricLightingSystem.Cleanup();

            for(int bsdfIdx = 0; bsdfIdx < m_IBLFilterArray.Length; ++bsdfIdx)
            {
                m_IBLFilterArray[bsdfIdx].Cleanup();
            }

            m_PostProcessSystem.Cleanup();
            m_AmbientOcclusionSystem.Cleanup();
            m_BlueNoise.Cleanup();

            HDCamera.ClearAll();

            m_RTManager.Cleanup();
            m_MipGenerator.Release();
            HDCamera.ClearAll();

            CullingGroupManager.instance.Cleanup();

            CoreUtils.SafeRelease(m_DepthPyramidMipLevelOffsetsBuffer);

            CoreUtils.Destroy(m_DepthResolveMaterial);
            CoreUtils.Destroy(m_ColorResolveMaterial);

#if UNITY_EDITOR
            SceneViewDrawMode.ResetDrawMode();

            // Do not attempt to unregister SceneView FrameSettings. It is shared amongst every scene view and take only a little place.
            // For removing it, you should be sure that Dispose could never be called after the constructor of another instance of this SRP.
            // Also, at the moment, applying change to hdrpAsset cause the SRP to be Disposed and Constructed again.
            // Not always in that order.
#endif
        }

        void Resize(HDCamera hdCamera)
        {
            bool resolutionChanged = (hdCamera.actualWidth != m_CurrentWidth) || (hdCamera.actualHeight != m_CurrentHeight);

            if (resolutionChanged || m_LightLoop.NeedResize())
            {
                if (m_CurrentWidth > 0 && m_CurrentHeight > 0)
                    m_LightLoop.ReleaseResolutionDependentBuffers();

                m_LightLoop.AllocResolutionDependentBuffers((int)hdCamera.screenSize.x, (int)hdCamera.screenSize.y, hdCamera.camera.stereoEnabled);
            }

            // update recorded window resolution
            m_CurrentWidth = hdCamera.actualWidth;
            m_CurrentHeight = hdCamera.actualHeight;
        }

        public void PushGlobalParams(HDCamera hdCamera, CommandBuffer cmd, DiffusionProfileSettings sssParameters)
        {
            using (new ProfilingSample(cmd, "Push Global Parameters", CustomSamplerId.PushGlobalParameters.GetSampler()))
            {
                // Set up UnityPerFrame CBuffer.
                m_SSSBufferManager.PushGlobalParams(hdCamera, cmd, sssParameters);

                m_DbufferManager.PushGlobalParams(hdCamera, cmd, m_Asset.GetRenderPipelineSettings().decalSettings.perChannelMask, m_RTManager);

                m_VolumetricLightingSystem.PushGlobalParams(hdCamera, cmd, m_FrameCount);

                var ssRefraction = VolumeManager.instance.stack.GetComponent<ScreenSpaceRefraction>()
                    ?? ScreenSpaceRefraction.@default;
                ssRefraction.PushShaderParameters(cmd);

                // Set up UnityPerView CBuffer.
                hdCamera.SetupGlobalParams(cmd, m_Time, m_LastTime, m_FrameCount);

                cmd.SetGlobalVector(HDShaderIDs._IndirectLightingMultiplier, new Vector4(VolumeManager.instance.stack.GetComponent<IndirectLightingController>().indirectDiffuseIntensity, 0, 0, 0));

                if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.MotionVectors))
                {
                    var buf = m_RTManager.GetVelocityBuffer();

                    cmd.SetGlobalTexture(HDShaderIDs._CameraMotionVectorsTexture, buf);
                    cmd.SetGlobalVector( HDShaderIDs._CameraMotionVectorsSize, new Vector4(buf.referenceSize.x,
                                                                                           buf.referenceSize.y,
                                                                                           1.0f / buf.referenceSize.x,
                                                                                           1.0f / buf.referenceSize.y));
                    cmd.SetGlobalVector(HDShaderIDs._CameraMotionVectorsScale, new Vector4(buf.referenceSize.x / (float)buf.rt.width,
                                                                                           buf.referenceSize.y / (float)buf.rt.height));
                }
                else
                {
                    cmd.SetGlobalTexture(HDShaderIDs._CameraMotionVectorsTexture, Texture2D.blackTexture);
                }

                // Light loop stuff...
                if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.SSR))
                    cmd.SetGlobalTexture(HDShaderIDs._SsrLightingTexture, GetRenderTarget(RT.SSRLighting));
                else
                    cmd.SetGlobalTexture(HDShaderIDs._SsrLightingTexture, HDUtils.clearTexture);

                // Off screen rendering is disabled for most of the frame by default.
                cmd.SetGlobalInt(HDShaderIDs._OffScreenRendering, 0);
            }
        }

        bool NeedStencilBufferCopy()
        {
            // Currently, Unity does not offer a way to bind the stencil buffer as a texture in a compute shader.
            // Therefore, it's manually copied using a pixel shader.
            return m_LightLoop.GetFeatureVariantsEnabled();
        }

        void CopyDepthBufferIfNeeded(CommandBuffer cmd)
        {
            if (!m_IsDepthBufferCopyValid)
            {
                using (new ProfilingSample(cmd, "Copy depth buffer", CustomSamplerId.CopyDepthBuffer.GetSampler()))
                {
                    // TODO: maybe we don't actually need the top MIP level?
                    // That way we could avoid making the copy, and build the MIP hierarchy directly.
                    // The downside is that our SSR tracing accuracy would decrease a little bit.
                    // But since we never render SSR at full resolution, this may be acceptable.

                    // TODO: reading the depth buffer with a compute shader will cause it to decompress in place.
                    // On console, to preserve the depth test performance, we must NOT decompress the 'm_CameraDepthStencilBuffer' in place.
                    // We should call decompressDepthSurfaceToCopy() and decompress it to 'm_CameraDepthBufferMipChain'.
                    m_GPUCopy.SampleCopyChannel_xyzw2x(cmd, m_RTManager.GetDepthStencilBuffer(), m_RTManager.GetDepthTexture(), new RectInt(0, 0, m_CurrentWidth, m_CurrentHeight));
                }
                m_IsDepthBufferCopyValid = true;
            }
        }

        public void SetMicroShadowingSettings(CommandBuffer cmd)
        {
            MicroShadowing microShadowingSettings = VolumeManager.instance.stack.GetComponent<MicroShadowing>();
            cmd.SetGlobalFloat(HDShaderIDs._MicroShadowingOpacity, microShadowingSettings.enable ? microShadowingSettings.opacity : 0.0f);
        }

        public void ConfigureKeywords(bool enableBakeShadowMask, HDCamera hdCamera, CommandBuffer cmd)
        {
            // Globally enable (for GBuffer shader and forward lit (opaque and transparent) the keyword SHADOWS_SHADOWMASK
            CoreUtils.SetKeyword(cmd, "SHADOWS_SHADOWMASK", enableBakeShadowMask);
            // Configure material to use depends on shadow mask option
            m_currentRendererConfigurationBakedLighting = enableBakeShadowMask ? HDUtils.k_RendererConfigurationBakedLightingWithShadowMask : HDUtils.k_RendererConfigurationBakedLighting;
            m_currentDebugViewMaterialGBuffer = enableBakeShadowMask ? m_DebugViewMaterialGBufferShadowMask : m_DebugViewMaterialGBuffer;

            CoreUtils.SetKeyword(cmd, "LIGHT_LAYERS", hdCamera.frameSettings.IsEnabled(FrameSettingsField.LightLayers));
            cmd.SetGlobalInt(HDShaderIDs._EnableLightLayers, hdCamera.frameSettings.IsEnabled(FrameSettingsField.LightLayers) ? 1 : 0);

            // configure keyword for both decal.shader and material
            if (m_Asset.renderPipelineSettings.supportDecals)
            {
                CoreUtils.SetKeyword(cmd, "DECALS_OFF", false);
                CoreUtils.SetKeyword(cmd, "DECALS_3RT", !m_Asset.GetRenderPipelineSettings().decalSettings.perChannelMask);
                CoreUtils.SetKeyword(cmd, "DECALS_4RT", m_Asset.GetRenderPipelineSettings().decalSettings.perChannelMask);
            }
            else
            {
                CoreUtils.SetKeyword(cmd, "DECALS_OFF", true);
                CoreUtils.SetKeyword(cmd, "DECALS_3RT", false);
                CoreUtils.SetKeyword(cmd, "DECALS_4RT", false);
            }

            // Raise the normal buffer flag only if we are in forward rendering
            CoreUtils.SetKeyword(cmd, "WRITE_NORMAL_BUFFER", hdCamera.frameSettings.litShaderMode == LitShaderMode.Forward);

            // Raise or remove the depth msaa flag based on the frame setting
            CoreUtils.SetKeyword(cmd, "WRITE_MSAA_DEPTH", hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA));
        }

        struct RenderRequest
        {
            public struct Target
            {
                public RenderTargetIdentifier id;
                public CubemapFace face;
                public RenderTexture copyToTarget;
            }
            public HDCamera hdCamera;
            public bool destroyCamera;
            public Target target;
            public HDCullingResults cullingResults;
            public int index;
            // Indices of render request to render before this one
            public List<int> dependsOnRenderRequestIndices;
            public CameraSettings cameraSettings;
        }
        struct HDCullingResults
        {
            public CullingResults cullingResults;
            public HDProbeCullingResults hdProbeCullingResults;
            public DecalSystem.CullResult decalCullResults;
            // TODO: DecalCullResults

            internal void Reset()
            {
                hdProbeCullingResults.Reset();
                if (decalCullResults != null)
                    decalCullResults.Clear();
                else
                    decalCullResults = GenericPool<DecalSystem.CullResult>.Get();
            }
        }

        FrameSettings currentFrameSettings;
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            if (!m_ValidAPI || cameras.Length == 0)
                return;

            UnityEngine.Rendering.RenderPipeline.BeginFrameRendering(cameras);

            // Check if we can speed up FrameSettings process by skiping history
            // or go in detail if debug is activated. Done once for all renderer.
            frameSettingsHistoryEnabled = FrameSettingsHistory.enabled;

            {
                // SRP.Render() can be called several times per frame.
                // Also, most Time variables do not consistently update in the Scene View.
                // This makes reliable detection of the start of the new frame VERY hard.
                // One of the exceptions is 'Time.realtimeSinceStartup'.
                // Therefore, outside of the Play Mode we update the time at 60 fps,
                // and in the Play Mode we rely on 'Time.frameCount'.
                float t = Time.realtimeSinceStartup;
                uint c = (uint)Time.frameCount;

                bool newFrame;

                if (Application.isPlaying)
                {
                    newFrame = m_FrameCount != c;

                    m_FrameCount = c;
                }
                else
                {
                    // If we switch to other scene Time.realtimeSinceStartup is reset, so we need to
                    // reset also m_Time. Here we simply detect ill case to trigger the reset.
                    m_Time = m_Time > t ? 0.0f : m_Time;

                    newFrame = (t - m_Time) > 0.0166f;

                    if (newFrame)
                        m_FrameCount++;
                }

                if (newFrame)
                {
                    HDCamera.CleanUnused();

                    // Make sure both are never 0.
                    m_LastTime = (m_Time > 0) ? m_Time : t;
                    m_Time = t;
                }
            }

			// TODO: Check with Fred if it make sense to put that here now that we have refactor the loop
#if ENABLE_RAYTRACING
            m_RayTracingManager.UpdateAccelerationStructures();
#endif

            var dynResHandler = HDDynamicResolutionHandler.instance;
            dynResHandler.Update(m_Asset.renderPipelineSettings.dynamicResolutionSettings, () => { m_PostProcessSystem.ResetHistory(); });

            RTHandles.SetHardwareDynamicResolutionState(dynResHandler.HardwareDynamicResIsEnabled());

            using (ListPool<RenderRequest>.Get(out List<RenderRequest> renderRequests))
            using (ListPool<int>.Get(out List<int> rootRenderRequestIndices))
            using (DictionaryPool<HDProbe, List<int>>.Get(out Dictionary<HDProbe, List<int>> renderRequestIndicesWhereTheProbeIsVisible))
            using (ListPool<CameraSettings>.Get(out List<CameraSettings> cameraSettings))
            using (ListPool<CameraPositionSettings>.Get(out List<CameraPositionSettings> cameraPositionSettings))

            {
                // Culling loop
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
                    cameraSettings.Clear();
                    cameraPositionSettings.Clear();

                    var cullingResults = GenericPool<HDCullingResults>.Get();
                    cullingResults.Reset();

                    // Try to compute the parameters of the request or skip the request
                    var skipRequest = !(TryCalculateFrameParameters(
                            camera,
                            out var additionalCameraData,
                            out var hdCamera,
                            out var cullingParameters
                        )
                        // Note: In case of a custom render, we have false here and 'TryCull' is not executed
                        && TryCull(
                            camera, hdCamera, renderContext, cullingParameters,
                            ref cullingResults
                        ));

                    if (additionalCameraData != null && additionalCameraData.hasCustomRender)
                    {
                        skipRequest = true;
                        // Execute custom render
                        additionalCameraData.ExecuteCustomRender(renderContext, hdCamera);
                    }

                    if (skipRequest)
                    {
                        // Submit render context and free pooled resources for this request
                        renderContext.Submit();
                        GenericPool<HDCullingResults>.Release(cullingResults);
                        continue;
                    }

                    // Add render request
                    var request = new RenderRequest
                    {
                        hdCamera = hdCamera,
                        cullingResults = cullingResults,
                        target = new RenderRequest.Target
                        {
                            id = camera.targetTexture ?? new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget),
                            face = CubemapFace.Unknown
                        },
                        dependsOnRenderRequestIndices = ListPool<int>.Get(),
                        index = renderRequests.Count,
                        cameraSettings = CameraSettings.From(hdCamera)
                        // TODO: store DecalCullResult
                    };
                    renderRequests.Add(request);
                    // This is a root render request
                    rootRenderRequestIndices.Add(request.index);

                    // Add visible probes to list
                    for (var i = 0; i < cullingResults.cullingResults.visibleReflectionProbes.Length; ++i)
                    {
                        var visibleProbe = cullingResults.cullingResults.visibleReflectionProbes[i];

                        var additionalReflectionData =
                            visibleProbe.reflectionProbe.GetComponent<HDAdditionalReflectionData>()
                            ?? visibleProbe.reflectionProbe.gameObject.AddComponent<HDAdditionalReflectionData>();

                        AddVisibleProbeVisibleIndexIfUpdateIsRequired(additionalReflectionData, request.index);
                    }
                    for (var i = 0; i < cullingResults.hdProbeCullingResults.visibleProbes.Count; ++i)
                        AddVisibleProbeVisibleIndexIfUpdateIsRequired(cullingResults.hdProbeCullingResults.visibleProbes[i], request.index);

                    // local function to help insertion of visible probe
                    void AddVisibleProbeVisibleIndexIfUpdateIsRequired(HDProbe probe, int visibleInIndex)
                    {
                        // Don't add it if it has already been updated this frame or not a real time probe
                        // TODO: discard probes that are baked once per frame and already baked this frame
                        if (probe.mode != ProbeSettings.Mode.Realtime)
                            return;

                        if (!renderRequestIndicesWhereTheProbeIsVisible.TryGetValue(probe, out List<int> visibleInIndices))
                        {
                            visibleInIndices = ListPool<int>.Get();
                            renderRequestIndicesWhereTheProbeIsVisible.Add(probe, visibleInIndices);
                        }
                        if (!visibleInIndices.Contains(visibleInIndex))
                            visibleInIndices.Add(visibleInIndex);
                    }
                }

                foreach (var probeToRenderAndDependencies in renderRequestIndicesWhereTheProbeIsVisible)
                {
                    var visibleProbe = probeToRenderAndDependencies.Key;
                    var visibleInIndices = probeToRenderAndDependencies.Value;

                    // Two cases:
                    //   - If the probe is view independent, we add only one render request per face that is
                    //      a dependency for all its 'visibleIn' render requests
                    //   - If the probe is view dependent, we add one render request per face per 'visibleIn'
                    //      render requests
                    var isViewDependent = visibleProbe.type == ProbeSettings.ProbeType.PlanarProbe;

                    if (isViewDependent)
                    {
                        for (int i = 0; i < visibleInIndices.Count; ++i)
                    {
                            var visibleInIndex = visibleInIndices[i];
                            var visibleInRenderRequest = renderRequests[visibleInIndices[i]];
                            var viewerTransform = visibleInRenderRequest.hdCamera.camera.transform;

                            AddHDProbeRenderRequests(visibleProbe, viewerTransform, Enumerable.Repeat(visibleInIndex, 1));
                        }
                    }
                    else
                        AddHDProbeRenderRequests(visibleProbe, null, visibleInIndices);
                }
                foreach (var pair in renderRequestIndicesWhereTheProbeIsVisible)
                    ListPool<int>.Release(pair.Value);
                renderRequestIndicesWhereTheProbeIsVisible.Clear();

                // Local function to share common code between view dependent and view independent requests
                void AddHDProbeRenderRequests(
                    HDProbe visibleProbe,
                    Transform viewerTransform,
                    IEnumerable<int> visibleInIndices
                )
                {
                    var position = ProbeCapturePositionSettings.ComputeFrom(
                        visibleProbe,
                        viewerTransform
                    );
                    cameraSettings.Clear();
                    cameraPositionSettings.Clear();
                    HDRenderUtilities.GenerateRenderingSettingsFor(
                        visibleProbe.settings, position,
                        cameraSettings, cameraPositionSettings
                    );

                    switch (visibleProbe.type)
                    {
                        case ProbeSettings.ProbeType.ReflectionProbe:
                            int desiredProbeSize = (int)((HDRenderPipeline)RenderPipelineManager.currentPipeline).renderPipelineSettings.lightLoopSettings.reflectionCubemapSize;
                            if (visibleProbe.realtimeTexture == null || visibleProbe.realtimeTexture.width != desiredProbeSize)
                            {
                                visibleProbe.SetTexture(ProbeSettings.Mode.Realtime, HDRenderUtilities.CreateReflectionProbeRenderTarget(desiredProbeSize));
                            }
                            break;
                        case ProbeSettings.ProbeType.PlanarProbe:
                            int desiredPlanarProbeSize = (int)((HDRenderPipeline)RenderPipelineManager.currentPipeline).renderPipelineSettings.lightLoopSettings.planarReflectionTextureSize;
                            if (visibleProbe.realtimeTexture == null || visibleProbe.realtimeTexture.width != desiredPlanarProbeSize)
                            {
                                visibleProbe.SetTexture(ProbeSettings.Mode.Realtime, HDRenderUtilities.CreatePlanarProbeRenderTarget(desiredPlanarProbeSize));
                            }
                            break;
                    }

                    for (int j = 0; j < cameraSettings.Count; ++j)
                    {
                        Camera camera = (m_ProbeCameraPool.Count == 0)
                            ? new GameObject().AddComponent<Camera>()
                            : m_ProbeCameraPool.Pop();

                        camera.targetTexture = visibleProbe.realtimeTexture; // We need to set a targetTexture with the right otherwise when setting pixelRect, it will be rescaled internally to the size of the screen
                        camera.gameObject.hideFlags = HideFlags.HideAndDontSave;
                        camera.gameObject.SetActive(false);
                        camera.name = ComputeProbeCameraName(visibleProbe.name, j, viewerTransform?.name);
                        camera.ApplySettings(cameraSettings[j]);
                        camera.ApplySettings(cameraPositionSettings[j]);
                        camera.cameraType = CameraType.Reflection;
                        camera.pixelRect = new Rect(0, 0, visibleProbe.realtimeTexture.width, visibleProbe.realtimeTexture.height);

                        var _cullingResults = GenericPool<HDCullingResults>.Get();
                        _cullingResults.Reset();

                        if (!(TryCalculateFrameParameters(
                                camera,
                                out HDAdditionalCameraData additionalCameraData,
                                out HDCamera hdCamera,
                                out ScriptableCullingParameters cullingParameters
                            )
                            && TryCull(
                                camera, hdCamera, renderContext, cullingParameters,
                                ref _cullingResults
                            )))
                        {
                            // Skip request and free resources
                            Object.Destroy(camera);
                            GenericPool<HDCullingResults>.Release(_cullingResults);
                            continue;
                        }
                        camera.GetComponent<HDAdditionalCameraData>().flipYMode
                            = visibleProbe.type == ProbeSettings.ProbeType.ReflectionProbe
                                ? HDAdditionalCameraData.FlipYMode.ForceFlipY
                                : HDAdditionalCameraData.FlipYMode.Automatic;

                        if (!visibleProbe.realtimeTexture.IsCreated())
                            visibleProbe.realtimeTexture.Create();

                        visibleProbe.SetRenderData(ProbeSettings.Mode.Realtime, new HDProbe.RenderData
                        {
                            capturePosition = camera.transform.position,
                            projectionMatrix = camera.projectionMatrix,
                            worldToCameraRHS = camera.worldToCameraMatrix
                        });

                        // TODO: Assign the actual final target to render to.
                        //   Currently, we use a target for each probe, and then copy it into the cache before using it
                        //   during the lighting pass.
                        //   But what we actually want here, is to render directly into the cache (either CubeArray,
                        //   or Texture2DArray)
                        //   To do so, we need to first allocate in the cache the location of the target and then assign
                        //   it here.
                        var request = new RenderRequest
                        {
                            hdCamera = hdCamera,
                            cullingResults = _cullingResults,
                            destroyCamera = true,
                            dependsOnRenderRequestIndices = ListPool<int>.Get(),
                            index = renderRequests.Count,
                            cameraSettings = cameraSettings[j]
                            // TODO: store DecalCullResult
                        };

                        // As we render realtime texture on GPU side, we must tag the texture so our texture array cache detect that something have change
                        visibleProbe.realtimeTexture.IncrementUpdateCount();

                        if (cameraSettings.Count > 1)
                        {
                            var face = (CubemapFace)j;
                            request.target = new RenderRequest.Target
                            {
                                copyToTarget = visibleProbe.realtimeTexture,
                                face = face
                            };
                        }
                        else
                        {
                            request.target = new RenderRequest.Target
                            {
                                id = visibleProbe.realtimeTexture,
                                face = CubemapFace.Unknown
                            };
                        }
                        renderRequests.Add(request);


                        foreach (var visibleInIndex in visibleInIndices)
                            renderRequests[visibleInIndex].dependsOnRenderRequestIndices.Add(request.index);
                    }
                }

                // TODO: Refactor into a method. If possible remove the intermediate target
                // Find max size for Cubemap face targets and resize/allocate if required the intermediate render target
                {
                    var size = Vector2Int.zero;
                    for (int i = 0; i < renderRequests.Count; ++i)
                    {
                        var renderRequest = renderRequests[i];
                        var isCubemapFaceTarget = renderRequest.target.face != CubemapFace.Unknown;
                        if (!isCubemapFaceTarget)
                        continue;

                        var width = renderRequest.hdCamera.actualWidth;
                        var height = renderRequest.hdCamera.actualHeight;
                        size.x = Mathf.Max(width, size.x);
                        size.y = Mathf.Max(height, size.y);
                    }

                    if (size != Vector2.zero)
                    {
                        if (m_TemporaryTargetForCubemaps != null)
                        {
                            if (m_TemporaryTargetForCubemaps.rt.width != size.x
                                || m_TemporaryTargetForCubemaps.rt.height != size.y)
                            {
                                RTHandles.Release(m_TemporaryTargetForCubemaps);
                                m_TemporaryTargetForCubemaps = null;
                            }
                        }
                        if (m_TemporaryTargetForCubemaps == null)
                        {
                            m_TemporaryTargetForCubemaps = RTHandles.Alloc(
                                size.x, size.y, 1, colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                                autoGenerateMips: false,
                                useMipMap: false,
                                name: "Temporary Target For Cubemap Face",
                                useDynamicScale: false,
                                memoryTag: RTManager.k_RenderLoopMemoryTag);
                        }
                    }
                }

                using (ListPool<int>.Get(out List<int> renderRequestIndicesToRender))
                {
                    // Flatten the render requests graph in an array that guarantee dependency constraints
                    {
                        using (GenericPool<Stack<int>>.Get(out Stack<int> stack))
                        {
                            stack.Clear();
                            for (int i = 0; i < rootRenderRequestIndices.Count; ++i)
                            {
                                stack.Push(rootRenderRequestIndices[i]);
                                while (stack.Count > 0)
                                {
                                    var index = stack.Pop();
                                    if (!renderRequestIndicesToRender.Contains(index))
                                        renderRequestIndicesToRender.Add(index);

                                    var request = renderRequests[index];
                                    for (int j = 0; j < request.dependsOnRenderRequestIndices.Count; ++j)
                                        stack.Push(request.dependsOnRenderRequestIndices[j]);
                                }
                            }
                        }
                    }
                    // Execute render request graph, in reverse order
                    for (int i = renderRequestIndicesToRender.Count - 1; i >= 0; --i)
                    {
                        var renderRequestIndex = renderRequestIndicesToRender[i];
                        var renderRequest = renderRequests[renderRequestIndex];

                        var cmd = CommandBufferPool.Get("");

                        // TODO: Avoid the intermediate target and render directly into final target
                        //  CommandBuffer.Blit does not work on Cubemap faces
                        //  So we use an intermediate RT to perform a CommandBuffer.CopyTexture in the target Cubemap face
                        if (renderRequest.target.face != CubemapFace.Unknown)
                        {
                            if (!m_TemporaryTargetForCubemaps.rt.IsCreated())
                                m_TemporaryTargetForCubemaps.rt.Create();

                            var hdCamera = renderRequest.hdCamera;
                            ref var target = ref renderRequest.target;
                            target.id = m_TemporaryTargetForCubemaps;
                        }

                        using (new ProfilingSample(
                            cmd,
                            $"HDRenderPipeline::Render {renderRequest.hdCamera.camera.name}",
                            CustomSamplerId.HDRenderPipelineRender.GetSampler())
                        )
                        {
                            cmd.SetInvertCulling(renderRequest.cameraSettings.invertFaceCulling);
                            ExecuteRenderRequest(renderRequest, renderContext, cmd);
                            cmd.SetInvertCulling(false);

                            var target = renderRequest.target;
                            // Handle the copy if requested
                            if (target.copyToTarget != null)
                            {
                                cmd.CopyTexture(
                                    target.id, 0, 0, 0, 0, renderRequest.hdCamera.actualWidth, renderRequest.hdCamera.actualHeight,
                                    target.copyToTarget, (int)target.face, 0, 0, 0
                                );
                            }
                            // Destroy the camera if requested
                            if (renderRequest.destroyCamera)
                            {
                                renderRequest.hdCamera.camera.targetTexture = null; // release reference because the RenderTexture might be destroyed before camera
                                m_ProbeCameraPool.Push(renderRequest.hdCamera.camera);
                            }

                            ListPool<int>.Release(renderRequest.dependsOnRenderRequestIndices);
                            renderRequest.cullingResults.decalCullResults?.Clear();
                            GenericPool<HDCullingResults>.Release(renderRequest.cullingResults);
                        }

                        renderContext.ExecuteCommandBuffer(cmd);

                        CommandBufferPool.Release(cmd);
                        renderContext.Submit();
                    }
                }
            }
        }

        void ExecuteRenderRequest(RenderRequest renderRequest, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            InitializeGlobalResources(renderContext);

            var hdCamera = renderRequest.hdCamera;
            var camera = hdCamera.camera;
            var cullingResults = renderRequest.cullingResults.cullingResults;
            var hdProbeCullingResults = renderRequest.cullingResults.hdProbeCullingResults;
            var decalCullingResults = renderRequest.cullingResults.decalCullResults;
            var target = renderRequest.target;

            // If we render a reflection view or a preview we should not display any debug information
            // This need to be call before ApplyDebugDisplaySettings()
            if (camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview)
            {
                // Neutral allow to disable all debug settings
                m_CurrentDebugDisplaySettings = s_NeutralDebugDisplaySettings;
            }
            else
            {
                // Make sure we are in sync with the debug menu for the msaa count
                m_MSAASamples = m_DebugDisplaySettings.data.msaaSamples;
                m_DebugDisplaySettings.UpdateCameraFreezeOptions();
                m_CurrentDebugDisplaySettings = m_DebugDisplaySettings;
            }

            m_DbufferManager.enableDecals = false;
            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.Decals))
            {
                using (new ProfilingSample(null, "DBufferPrepareDrawData", CustomSamplerId.DBufferPrepareDrawData.GetSampler()))
                {
                    // TODO: update singleton with DecalCullResults
                    m_DbufferManager.enableDecals = true;              // mesh decals are renderers managed by c++ runtime and we have no way to query if any are visible, so set to true
                    DecalSystem.instance.LoadCullResults(decalCullingResults);
                    DecalSystem.instance.UpdateCachedMaterialData();    // textures, alpha or fade distances could've changed
                    DecalSystem.instance.CreateDrawData();              // prepare data is separate from draw
                    DecalSystem.instance.UpdateTextureAtlas(cmd);       // as this is only used for transparent pass, would've been nice not to have to do this if no transparent renderers are visible, needs to happen after CreateDrawData
                }
            }

            using (new ProfilingSample(cmd, "Volume Update", CustomSamplerId.VolumeUpdate.GetSampler()))
            {
                VolumeManager.instance.Update(hdCamera.volumeAnchor, hdCamera.volumeLayerMask);
            }

            // Do anything we need to do upon a new frame.
            // The NewFrame must be after the VolumeManager update and before Resize because it uses properties set in NewFrame
            m_LightLoop.NewFrame(hdCamera.frameSettings);

            Resize(hdCamera);
            m_PostProcessSystem.BeginFrame(cmd, hdCamera);

            ApplyDebugDisplaySettings(hdCamera, cmd);
            m_SkyManager.UpdateCurrentSkySettings(hdCamera);

            renderContext.SetupCameraProperties(camera, camera.stereoEnabled);

            PushGlobalParams(hdCamera, cmd, diffusionProfileSettings);

            // TODO: Find a correct place to bind these material textures
            // We have to bind the material specific global parameters in this mode
            m_MaterialList.ForEach(material => material.Bind(cmd));

            // Frustum cull density volumes on the CPU. Can be performed as soon as the camera is set up.
            DensityVolumeList densityVolumes = m_VolumetricLightingSystem.PrepareVisibleDensityVolumeList(hdCamera, cmd, m_Time);

            // Note: Legacy Unity behave like this for ShadowMask
            // When you select ShadowMask in Lighting panel it recompile shaders on the fly with the SHADOW_MASK keyword.
            // However there is no C# function that we can query to know what mode have been select in Lighting Panel and it will be wrong anyway. Lighting Panel setup what will be the next bake mode. But until light is bake, it is wrong.
            // Currently to know if you need shadow mask you need to go through all visible lights (of CullResult), check the LightBakingOutput struct and look at lightmapBakeType/mixedLightingMode. If one light have shadow mask bake mode, then you need shadow mask features (i.e extra Gbuffer).
            // It mean that when we build a standalone player, if we detect a light with bake shadow mask, we generate all shader variant (with and without shadow mask) and at runtime, when a bake shadow mask light is visible, we dynamically allocate an extra GBuffer and switch the shader.
            // So the first thing to do is to go through all the light: PrepareLightsForGPU
            bool enableBakeShadowMask;
            using (new ProfilingSample(cmd, "TP_PrepareLightsForGPU", CustomSamplerId.TPPrepareLightsForGPU.GetSampler()))
            {
                enableBakeShadowMask = m_LightLoop.PrepareLightsForGPU(cmd, hdCamera, cullingResults, hdProbeCullingResults, densityVolumes, m_DebugDisplaySettings);
            }
            // Configure all the keywords
            ConfigureKeywords(enableBakeShadowMask, hdCamera, cmd);

            StartStereoRendering(cmd, renderContext, camera);

            ClearBuffers(hdCamera, cmd);

            // TODO: Add stereo occlusion mask
            bool shouldRenderMotionVectorAfterGBuffer = RenderDepthPrepass(cullingResults, hdCamera, renderContext, cmd);

            if (!shouldRenderMotionVectorAfterGBuffer)
            {
                // If objects velocity if enabled, this will render the objects with motion vector into the target buffers (in addition to the depth)
                // Note: An object with motion vector must not be render in the prepass otherwise we can have motion vector write that should have been rejected
                RenderObjectsVelocity(cullingResults, hdCamera, renderContext, cmd);
            }

            // Now that all depths have been rendered, resolve the depth buffer
            ResolveMSAADepthNormal(cmd, hdCamera);

            // This will bind the depth buffer if needed for DBuffer)
            RenderDBuffer(hdCamera, cmd, renderContext, cullingResults);

            RenderGBuffer(cullingResults, hdCamera, renderContext, cmd);

            // We can now bind the normal buffer to be use by any effect
            cmd.SetGlobalTexture(HDShaderIDs._NormalBufferTexture, m_RTManager.GetNormalBuffer(false));

            if (m_DbufferManager.enableDecals && !hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA)) // MSAA not supported
            {
                using (new ProfilingSample(cmd, "DBuffer Normal (forward)", CustomSamplerId.DBufferNormal.GetSampler()))
                {
                    int stencilMask;
                    int stencilRef;
                    switch (hdCamera.frameSettings.litShaderMode)
                    {
                        case LitShaderMode.Forward:  // in forward rendering all pixels that decals wrote into have to be composited
                            stencilMask = (int)StencilBitMask.Decals;
                            stencilRef = (int)StencilBitMask.Decals;
                            break;
                        case LitShaderMode.Deferred: // in deferred rendering only pixels affected by both forward materials and decals need to be composited
                            stencilMask = (int)StencilBitMask.Decals | (int)StencilBitMask.DecalsForwardOutputNormalBuffer;
                            stencilRef = (int)StencilBitMask.Decals | (int)StencilBitMask.DecalsForwardOutputNormalBuffer;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("Unknown ShaderLitMode");
                    }

                    m_DecalNormalBufferMaterial.SetInt(HDShaderIDs._DecalNormalBufferStencilReadMask, stencilMask);
                    m_DecalNormalBufferMaterial.SetInt(HDShaderIDs._DecalNormalBufferStencilRef, stencilRef);

                    HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetDepthStencilBuffer());
                    cmd.SetRandomWriteTarget(1, m_RTManager.GetNormalBuffer());
                    cmd.DrawProcedural(Matrix4x4.identity, m_DecalNormalBufferMaterial, 0, MeshTopology.Triangles, 3, 1);
                    cmd.ClearRandomWriteTargets();
                }
            }

            // In both forward and deferred, everything opaque should have been rendered at this point so we can safely copy the depth buffer for later processing.
            GenerateDepthPyramid(hdCamera, cmd, FullScreenDebugMode.DepthPyramid);
            // Depth texture is now ready, bind it (Depth buffer could have been bind before if DBuffer is enable)
            cmd.SetGlobalTexture(HDShaderIDs._CameraDepthTexture, m_RTManager.GetDepthTexture());

            if (shouldRenderMotionVectorAfterGBuffer)
            {
                // See the call RenderObjectsVelocity() above and comment
                RenderObjectsVelocity(cullingResults, hdCamera, renderContext, cmd);
            }

            RenderCameraVelocity(cullingResults, hdCamera, renderContext, cmd);

            StopStereoRendering(cmd, renderContext, camera);
            // Caution: We require sun light here as some skies use the sun light to render, it means that UpdateSkyEnvironment must be called after PrepareLightsForGPU.
            // TODO: Try to arrange code so we can trigger this call earlier and use async compute here to run sky convolution during other passes (once we move convolution shader to compute).
            UpdateSkyEnvironment(hdCamera, cmd);


#if UNITY_EDITOR
            var showGizmos = camera.cameraType == CameraType.Game
                || camera.cameraType == CameraType.SceneView;
#endif
            if (m_CurrentDebugDisplaySettings.IsDebugMaterialDisplayEnabled() || m_CurrentDebugDisplaySettings.IsMaterialValidationEnabled())
            {
                StartStereoRendering(cmd, renderContext, camera);
                RenderDebugViewMaterial(cullingResults, hdCamera, renderContext, cmd);
                StopStereoRendering(cmd, renderContext, camera);
            }
            else
            {
                StartStereoRendering(cmd, renderContext, camera);

                if (!hdCamera.frameSettings.SSAORunsAsync())
                    m_AmbientOcclusionSystem.Render(cmd, hdCamera, m_RTManager, renderContext);

                // Clear and copy the stencil texture needs to be moved to before we invoke the async light list build,
                // otherwise the async compute queue can end up using that texture before the graphics queue is done with it.
                // TODO: Move this code inside LightLoop
                // For the SSR we need the lighting flags to be copied into the stencil texture (it is use to discard object that have no lighting)
                if (m_LightLoop.GetFeatureVariantsEnabled() || hdCamera.frameSettings.IsEnabled(FrameSettingsField.SSR))
                {
                    // For material classification we use compute shader and so can't read into the stencil, so prepare it.
                    using (new ProfilingSample(cmd, "Clear and copy stencil texture", CustomSamplerId.ClearAndCopyStencilTexture.GetSampler()))
                    {
#if UNITY_SWITCH
                        // Faster on Switch.
                        HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.StencilCopy), m_RTManager.GetDepthStencilBuffer(), ClearFlag.Color, CoreUtils.clearColorAllBlack);

                        m_CopyStencil.SetInt(HDShaderIDs._StencilRef, (int)StencilLightingUsage.NoLighting);
                        m_CopyStencil.SetInt(HDShaderIDs._StencilMask, (int)StencilBitMask.LightingMask);

                        // Use ShaderPassID 1 => "Pass 1 - Write 1 if value different from stencilRef to output"
                        CoreUtils.DrawFullScreen(cmd, m_CopyStencil, null, 1);
#else
                        HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.StencilCopy), ClearFlag.Color, CoreUtils.clearColorAllBlack);
                        HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetDepthStencilBuffer());
                        cmd.SetRandomWriteTarget(1, GetRenderTarget(RT.StencilCopy));

                        m_CopyStencil.SetInt(HDShaderIDs._StencilRef, (int)StencilLightingUsage.NoLighting);
                        m_CopyStencil.SetInt(HDShaderIDs._StencilMask, (int)StencilBitMask.LightingMask);

                        // Use ShaderPassID 3 => "Pass 3 - Initialize Stencil UAV copy with 1 if value different from stencilRef to output"
                        CoreUtils.DrawFullScreen(cmd, m_CopyStencil, null, 3);
                        cmd.ClearRandomWriteTargets();
#endif
                    }

                    if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.SSR))
                    {
                        using (new ProfilingSample(cmd, "Update stencil copy for SSR Exclusion", CustomSamplerId.UpdateStencilCopyForSSRExclusion.GetSampler()))
                        {
                            HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetDepthStencilBuffer());
                            cmd.SetRandomWriteTarget(1, GetRenderTarget(RT.StencilCopy));

                            m_CopyStencil.SetInt(HDShaderIDs._StencilRef, (int)StencilBitMask.DoesntReceiveSSR);
                            m_CopyStencil.SetInt(HDShaderIDs._StencilMask, (int)StencilBitMask.DoesntReceiveSSR);

                            // Pass 4 performs an OR between the already present content of the copy and the stencil ref, if stencil test passes.
                            CoreUtils.DrawFullScreen(cmd, m_CopyStencil, null, 4);
                            cmd.ClearRandomWriteTargets();
                        }
                    }
                }

                // When debug is enabled we need to clear otherwise we may see non-shadows areas with stale values.
                if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.ContactShadows) && m_CurrentDebugDisplaySettings.data.fullScreenDebugMode == FullScreenDebugMode.ContactShadows)
                {
                    HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.ScreenSpaceShadow), ClearFlag.Color, CoreUtils.clearColorAllBlack);
                }

                if (!hdCamera.frameSettings.ContactShadowsRunAsync())
                {
#if ENABLE_RAYTRACING
                    // Let's render the screen space area light shadows
                    bool areaShadowsRendered = m_RaytracingShadows.RenderAreaShadows(hdCamera, cmd, renderContext);
                    PushFullScreenDebugTexture(hdCamera, cmd, m_RaytracingShadows.GetShadowedIntegrationTexture(), FullScreenDebugMode.RaytracedAreaShadow);
                    if (areaShadowsRendered)
                    {
                        cmd.SetGlobalInt(HDShaderIDs._RaytracedAreaShadow, 1);
                    }
                    else
                    {
                        cmd.SetGlobalInt(HDShaderIDs._RaytracedAreaShadow, 0);
                    }
#endif

                    HDUtils.CheckRTCreated(GetRenderTarget(RT.ScreenSpaceShadow));

                    int firstMipOffsetY = m_RTManager.GetDepthBufferMipChainInfo().mipLevelOffsets[1].y;
                    m_LightLoop.RenderScreenSpaceShadows(hdCamera, GetRenderTarget(RT.ScreenSpaceShadow), hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA) ? GetRenderTarget(RT.DepthValuesMSAA) : m_RTManager.GetDepthTexture(), firstMipOffsetY, cmd);
                    m_LightLoop.SetScreenSpaceShadowsTexture(hdCamera, GetRenderTarget(RT.ScreenSpaceShadow), cmd);

                    PushFullScreenDebugTexture(hdCamera, cmd, GetRenderTarget(RT.ScreenSpaceShadow), FullScreenDebugMode.ContactShadows);
                }

                StopStereoRendering(cmd, renderContext, camera);

                var buildLightListTask = new HDGPUAsyncTask("Build light list", ComputeQueueType.Background);
                // It is important that this task is in the same queue as the build light list due to dependency it has on it. If really need to move it, put an extra fence to make sure buildLightListTask has finished.
                var volumeVoxelizationTask = new HDGPUAsyncTask("Volumetric voxelization", ComputeQueueType.Background);
                var SSRTask = new HDGPUAsyncTask("Screen Space Reflection", ComputeQueueType.Background);
                var SSAOTask = new HDGPUAsyncTask("SSAO", ComputeQueueType.Background);
                var contactShadowsTask = new HDGPUAsyncTask("Screen Space Shadows", ComputeQueueType.Background);

                var haveAsyncTaskWithShadows = false;
                if (hdCamera.frameSettings.BuildLightListRunsAsync())
                {
                    buildLightListTask.Start(cmd, renderContext, Callback, !haveAsyncTaskWithShadows);

                    haveAsyncTaskWithShadows = true;

                    void Callback(CommandBuffer asyncCmd)
                        => m_LightLoop.BuildGPULightListsCommon(hdCamera, asyncCmd, m_RTManager.GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA)), GetRenderTarget(RT.StencilCopy), m_SkyManager.IsLightingSkyValid());
                }

                if (hdCamera.frameSettings.VolumeVoxelizationRunsAsync())
                {
                    volumeVoxelizationTask.Start(cmd, renderContext, Callback, !haveAsyncTaskWithShadows);

                    haveAsyncTaskWithShadows = true;

                    void Callback(CommandBuffer asyncCmd)
                        => m_VolumetricLightingSystem.VolumeVoxelizationPass(hdCamera, asyncCmd, m_FrameCount, densityVolumes, m_LightLoop);
                }

                if (hdCamera.frameSettings.SSRRunsAsync())
                {
                    SSRTask.Start(cmd, renderContext, Callback, !haveAsyncTaskWithShadows);

                    haveAsyncTaskWithShadows = true;

                    void Callback(CommandBuffer asyncCmd)
                        => RenderSSR(hdCamera, asyncCmd, renderContext);
                }

                if (hdCamera.frameSettings.SSAORunsAsync())
                {
                    void AsyncSSAODispatch(CommandBuffer asyncCmd) => m_AmbientOcclusionSystem.Dispatch(asyncCmd, hdCamera, m_RTManager);
                    SSAOTask.Start(cmd, renderContext, AsyncSSAODispatch, !haveAsyncTaskWithShadows);
                    haveAsyncTaskWithShadows = true;
                }

                if (hdCamera.frameSettings.ContactShadowsRunAsync())
                {
                    contactShadowsTask.Start(cmd, renderContext, Callback, !haveAsyncTaskWithShadows);

                    haveAsyncTaskWithShadows = true;

                    void Callback(CommandBuffer asyncCmd)
                    {
                        var firstMipOffsetY = m_RTManager.GetDepthBufferMipChainInfo().mipLevelOffsets[1].y;
                        m_LightLoop.RenderScreenSpaceShadows(hdCamera, GetRenderTarget(RT.ScreenSpaceShadow), hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA) ? GetRenderTarget(RT.DepthValuesMSAA) : m_RTManager.GetDepthTexture(), firstMipOffsetY, asyncCmd);
                    }
                }


                using (new ProfilingSample(cmd, "Render shadows", CustomSamplerId.RenderShadows.GetSampler()))
                {
                    // This call overwrites camera properties passed to the shader system.
                    m_LightLoop.RenderShadows(renderContext, cmd, cullingResults, hdCamera);

                    // Overwrite camera properties set during the shadow pass with the original camera properties.
                    renderContext.SetupCameraProperties(camera, camera.stereoEnabled);
                    hdCamera.SetupGlobalParams(cmd, m_Time, m_LastTime, m_FrameCount);
                }

                if (!hdCamera.frameSettings.SSRRunsAsync())
                {
                    // Needs the depth pyramid and motion vectors, as well as the render of the previous frame.
                    RenderSSR(hdCamera, cmd, renderContext);
                }

                if (hdCamera.frameSettings.BuildLightListRunsAsync())
                {
                    buildLightListTask.EndWithPostWork(cmd, Callback);

                    void Callback() => m_LightLoop.PushGlobalParams(hdCamera, cmd);
                }
                else
                {
                    using (new ProfilingSample(cmd, "Build Light list", CustomSamplerId.BuildLightList.GetSampler()))
                    {
                        m_LightLoop.BuildGPULightLists(hdCamera, cmd, m_RTManager.GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA)), GetRenderTarget(RT.StencilCopy), m_SkyManager.IsLightingSkyValid());
                    }
                }

                {
                    // Set fog parameters for volumetric lighting.
                    var visualEnv = VolumeManager.instance.stack.GetComponent<VisualEnvironment>();
                    visualEnv.PushFogShaderParameters(hdCamera, cmd);
                }

                if (hdCamera.frameSettings.VolumeVoxelizationRunsAsync())
                {
                    volumeVoxelizationTask.End(cmd);
                }
                else
                {
                    // Perform the voxelization step which fills the density 3D texture.
                    m_VolumetricLightingSystem.VolumeVoxelizationPass(hdCamera, cmd, m_FrameCount, densityVolumes, m_LightLoop);
                }

                // Render the volumetric lighting.
                // The pass requires the volume properties, the light list and the shadows, and can run async.
                m_VolumetricLightingSystem.VolumetricLightingPass(hdCamera, cmd, m_FrameCount);

                SetMicroShadowingSettings(cmd);

                if (hdCamera.frameSettings.SSAORunsAsync())
                {
                    SSAOTask.EndWithPostWork(cmd, Callback);
                    void Callback() => m_AmbientOcclusionSystem.PostDispatchWork(cmd, hdCamera, m_RTManager);
                }

                if (hdCamera.frameSettings.ContactShadowsRunAsync())
                {
                    contactShadowsTask.EndWithPostWork(cmd, Callback);

                    void Callback()
                    {
                        m_LightLoop.SetScreenSpaceShadowsTexture(hdCamera, GetRenderTarget(RT.ScreenSpaceShadow), cmd);
                        PushFullScreenDebugTexture(hdCamera, cmd, GetRenderTarget(RT.ScreenSpaceShadow), FullScreenDebugMode.ContactShadows);
                    }
                }

                if (hdCamera.frameSettings.SSRRunsAsync())
                {
                    SSRTask.End(cmd);
                }

                // Might float this higher if we enable stereo w/ deferred
                StartStereoRendering(cmd, renderContext, camera);

                RenderDeferredLighting(hdCamera, cmd);

                RenderForward(cullingResults, hdCamera, renderContext, cmd, ForwardPass.Opaque);

                ResolveMSAAColor(cmd, hdCamera, GetRenderTarget(RT.SssDiffuseLightingMSAA), GetRenderTarget(RT.SssDiffuseLighting));
                ResolveMSAAColor(cmd, hdCamera, GetRenderTarget(RT.SSSMSAA0), GetRenderTarget(RT.SSS0));

                // SSS pass here handle both SSS material from deferred and forward
                m_SSSBufferManager.SubsurfaceScatteringPass(hdCamera, cmd, diffusionProfileSettings, m_RTManager);

                RenderSky(hdCamera, cmd);

                RenderTransparentDepthPrepass(cullingResults, hdCamera, renderContext, cmd);

                // Render pre refraction objects
                RenderForward(cullingResults, hdCamera, renderContext, cmd, ForwardPass.PreRefraction);

                if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.RoughRefraction))
                {
                    // First resolution of the color buffer for the color pyramid
                    ResolveMSAAColor(cmd, hdCamera, GetRenderTarget(RT.ColorMSAA), GetRenderTarget(RT.Color));

                    RenderColorPyramid(hdCamera, cmd, true);
                }

                // Bind current color pyramid for shader graph SceneColorNode on transparent objects
                var currentColorPyramid = hdCamera.GetCurrentFrameRT((int)HDCameraFrameHistoryType.ColorBufferMipChain);
                cmd.SetGlobalTexture(HDShaderIDs._ColorPyramidTexture, currentColorPyramid);

                // Render all type of transparent forward (unlit, lit, complex (hair...)) to keep the sorting between transparent objects.
                RenderForward(cullingResults, hdCamera, renderContext, cmd, ForwardPass.Transparent);

                // Second resolve the color buffer for finishing the frame
                ResolveMSAAColor(cmd, hdCamera, GetRenderTarget(RT.ColorMSAA), GetRenderTarget(RT.Color));

#if UNITY_EDITOR
                // Render gizmos that should be affected by post processes
                if (showGizmos)
                    RenderGizmos(cmd, camera, renderContext, GizmoSubset.PreImageEffects);
#endif

                // Render All forward error
                RenderForwardError(cullingResults, hdCamera, renderContext, cmd);

                // Fill depth buffer to reduce artifact for transparent object during postprocess
                RenderTransparentDepthPostpass(cullingResults, hdCamera, renderContext, cmd);

#if ENABLE_RAYTRACING
                m_RaytracingRenderer.Render(hdCamera, cmd, GetRenderTarget(RT.Color), renderContext, cullingResults);
#endif
                RenderColorPyramid(hdCamera, cmd, false);

                AccumulateDistortion(cullingResults, hdCamera, renderContext, cmd);
                RenderDistortion(hdCamera, cmd);

                StopStereoRendering(cmd, renderContext, camera);

                PushFullScreenDebugTexture(hdCamera, cmd, GetRenderTarget(RT.Color), FullScreenDebugMode.NanTracker);
                PushFullScreenLightingDebugTexture(hdCamera, cmd, GetRenderTarget(RT.Color));
            }

            // At this point, RTManager.GetRT(RT.Color) has been filled by either debug views are regular rendering so we can push it here.
            PushColorPickerDebugTexture(cmd, hdCamera, GetRenderTarget(RT.Color));

            RenderPostProcess(cullingResults, hdCamera, target.id, renderContext, cmd);

            // In developer build, we always render post process in m_AfterPostProcessBuffer at (0,0) in which we will then render debug.
            // Because of this, we need another blit here to the final render target at the right viewport.
            if (Debug.isDebugBuild)
            {
                RenderDebug(hdCamera, cmd, cullingResults);
                using (new ProfilingSample(cmd, "Final Blit (Dev Build Only)"))
                {
                    BlitFinalCameraTexture(cmd, hdCamera, target.id);
                }
            }

            // Pushes to XR headset and/or display mirror
            if (camera.stereoEnabled)
                renderContext.StereoEndRender(camera);

            // Due to our RT handle system we don't write into the backbuffer depth buffer (as our depth buffer can be bigger than the one provided)
            // So we need to do a copy of the corresponding part of RT depth buffer in the target depth buffer in various situation:
            // - RenderTexture (camera.targetTexture != null) has a depth buffer (camera.targetTexture.depth != 0)
            // - We are rendering into the main game view (i.e not a RenderTexture camera.cameraType == CameraType.Game && hdCamera.camera.targetTexture == null) in the editor for allowing usage of Debug.DrawLine and Debug.Ray.
            // - We draw Gizmo/Icons in the editor (hdCamera.camera.targetTexture != null && camera.targetTexture.depth != 0 - The Scene view has a targetTexture and a depth texture)
            // TODO: If at some point we get proper render target aliasing, we will be able to use the provided depth texture directly with our RT handle system
            // Note: Debug.DrawLine and Debug.Ray only work in editor, not in player
            var copyDepth = hdCamera.camera.targetTexture != null && hdCamera.camera.targetTexture.depth != 0;
#if UNITY_EDITOR
            copyDepth = copyDepth || hdCamera.isMainGameView; // Specific case of Debug.DrawLine and Debug.Ray
#endif
            if (copyDepth)
            {
                using (new ProfilingSample(cmd, "Copy Depth in Target Texture", CustomSamplerId.CopyDepth.GetSampler()))
                {
                    cmd.SetRenderTarget(target.id);
                    cmd.SetViewport(hdCamera.finalViewport);
                    m_CopyDepthPropertyBlock.SetTexture(HDShaderIDs._InputDepth, m_RTManager.GetDepthStencilBuffer());
                    // When we are Main Game View we need to flip the depth buffer ourselves as we are after postprocess / blit that have already flipped the screen
                    m_CopyDepthPropertyBlock.SetInt("_FlipY", hdCamera.isMainGameView ? 1 : 0);
                    CoreUtils.DrawFullScreen(cmd, m_CopyDepth, m_CopyDepthPropertyBlock);
                }
            }

#if UNITY_EDITOR
            // We need to make sure the viewport is correctly set for the editor rendering. It might have been changed by debug overlay rendering just before.
            cmd.SetViewport(hdCamera.finalViewport);

            // Render overlay Gizmos
            if (showGizmos)
                RenderGizmos(cmd, camera, renderContext, GizmoSubset.PostImageEffects);
#endif
        }

        void BlitFinalCameraTexture(CommandBuffer cmd, HDCamera hdCamera, RenderTargetIdentifier destination)
        {
            bool flip = hdCamera.flipYMode == HDAdditionalCameraData.FlipYMode.ForceFlipY || hdCamera.isMainGameView;
            // Here we can't use the viewport scale provided in hdCamera. The reason is that this scale is for internal rendering before post process with dynamic resolution factored in.
            // Here the input texture is already at the viewport size but may be smaller than the RT itself (because of the RTHandle system) so we compute the scale specifically here.
            var scaleBias = new Vector4((float)hdCamera.finalViewport.width / GetRenderTarget(RT.IntermediateAfterPostProcess).rt.width, (float)hdCamera.finalViewport.height / GetRenderTarget(RT.IntermediateAfterPostProcess).rt.height, 0.0f, 0.0f);
            var offset = Vector2.zero;

            if (flip)
            {
                scaleBias.w = scaleBias.y;
                scaleBias.y *= -1;
            }

            m_BlitPropertyBlock.SetTexture(HDShaderIDs._BlitTexture, GetRenderTarget(RT.IntermediateAfterPostProcess));
            m_BlitPropertyBlock.SetVector(HDShaderIDs._BlitScaleBias, scaleBias);
            m_BlitPropertyBlock.SetFloat(HDShaderIDs._BlitMipLevel, 0);
            HDUtils.DrawFullScreen(cmd, hdCamera.finalViewport, GetBlitMaterial(), destination, m_BlitPropertyBlock, 0);
        }

        void InitializeGlobalResources(ScriptableRenderContext renderContext)
        {
            // Global resources initialization
            {
                // This is the main command buffer used for the frame.
                var cmd = CommandBufferPool.Get("");
                using (new ProfilingSample(
                    cmd, "HDRenderPipeline::Render Initialize Materials",
                    CustomSamplerId.HDRenderPipelineRender.GetSampler())
                )
                {
                    // Init material if needed
                    for (int bsdfIdx = 0; bsdfIdx < m_IBLFilterArray.Length; ++bsdfIdx)
                    {
                        if (!m_IBLFilterArray[bsdfIdx].IsInitialized())
                            m_IBLFilterArray[bsdfIdx].Initialize(cmd);
                    }

                    foreach (var material in m_MaterialList)
                        material.RenderInit(cmd);
                }
                renderContext.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        // $"HDProbe RenderCamera ({probeName}: {face:00} for viewer '{viewerName}')"
        unsafe string ComputeProbeCameraName(string probeName, int face, string viewerName)
        {
            // Interpolate the camera name with as few allocation as possible
            const string pattern1 = "HDProbe RenderCamera (";
            const string pattern2 = ": ";
            const string pattern3 = " for viewer '";
            const string pattern4 = "')";
            const int maxCharCountPerName = 40;
            const int charCountPerNumber = 2;

            probeName = probeName ?? string.Empty;
            viewerName = viewerName ?? "null";

            var probeNameSize = Mathf.Min(probeName.Length, maxCharCountPerName);
            var viewerNameSize = Mathf.Min(viewerName.Length, maxCharCountPerName);
            int size = pattern1.Length + probeNameSize
                + pattern2.Length + charCountPerNumber
                + pattern3.Length + viewerNameSize
                + pattern4.Length;

            var buffer = stackalloc char[size];
            var p = buffer;
            int i, c, s = 0;
            for (i = 0; i < pattern1.Length; ++i, ++p)
                *p = pattern1[i];
            for (i = 0, c = Mathf.Min(probeName.Length, maxCharCountPerName); i < c; ++i, ++p)
                *p = probeName[i];
            s += c;
            for (i = 0; i < pattern2.Length; ++i, ++p)
                *p = pattern2[i];

            // Fast, no-GC index.ToString("2")
            var temp = (face * 205) >> 11;  // 205/2048 is nearly the same as /10
            *(p++) = (char)(temp + '0');
            *(p++) = (char)((face - temp * 10) + '0');
            s += charCountPerNumber;

            for (i = 0; i < pattern3.Length; ++i, ++p)
                *p = pattern3[i];
            for (i = 0, c = Mathf.Min(viewerName.Length, maxCharCountPerName); i < c; ++i, ++p)
                *p = viewerName[i];
            s += c;
            for (i = 0; i < pattern4.Length; ++i, ++p)
                *p = pattern4[i];

            s += pattern1.Length + pattern2.Length + pattern3.Length + pattern4.Length;
            return new string(buffer, 0, s);
        }

        bool TryCalculateFrameParameters(
            Camera camera,
            out HDAdditionalCameraData additionalCameraData,
            out HDCamera hdCamera,
            out ScriptableCullingParameters cullingParams
        )
        {
            // First, get aggregate of frame settings base on global settings, camera frame settings and debug settings
            // Note: the SceneView camera will never have additionalCameraData
            additionalCameraData = camera.GetComponent<HDAdditionalCameraData>();
            hdCamera = default;
            cullingParams = default;

            // Compute the FrameSettings actually used to draw the frame
            // FrameSettingsHistory do the same while keeping all step of FrameSettings aggregation in memory for DebugMenu
            if (frameSettingsHistoryEnabled)
                FrameSettingsHistory.AggregateFrameSettings(ref currentFrameSettings, camera, additionalCameraData, m_Asset);
            else
                FrameSettings.AggregateFrameSettings(ref currentFrameSettings, camera, additionalCameraData, m_Asset);

            // Specific pass to simply display the content of the camera buffer if users have fill it themselves (like video player)
            if (additionalCameraData && additionalCameraData.fullscreenPassthrough)
                return false;

            hdCamera = null;

            // Retrieve debug display settings to init FrameSettings, unless we are a reflection and in this case we don't have debug settings apply.
            DebugDisplaySettings debugDisplaySettings = (camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview) ? s_NeutralDebugDisplaySettings : m_DebugDisplaySettings;

            // Disable post process if we enable debug mode or if the post process layer is disabled
            if (debugDisplaySettings.IsDebugDisplayRemovePostprocess())
            {
                currentFrameSettings.SetEnabled(FrameSettingsField.Postprocess, false);
            }

            // Disable SSS if luxmeter is enabled
            if (debugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.LuxMeter)
            {
                currentFrameSettings.SetEnabled(FrameSettingsField.SubsurfaceScattering, false);
            }

            hdCamera = HDCamera.Get(camera) ?? HDCamera.Create(camera);
            // From this point, we should only use frame settings from the camera
            hdCamera.Update(currentFrameSettings, m_VolumetricLightingSystem, m_MSAASamples);

            // Custom Render requires a proper HDCamera, so we return after the HDCamera was setup
            if (additionalCameraData != null && additionalCameraData.hasCustomRender)
                return false;

            if (!camera.TryGetCullingParameters(
                camera.stereoEnabled,
                out cullingParams)) // Fixme remove stereo passdown?
                return false;

            if (m_DebugDisplaySettings.IsCameraFreezeEnabled())
            {
                bool cameraIsFrozen = camera.name.Equals(m_DebugDisplaySettings.GetFrozenCameraName());
                if (cameraIsFrozen)
                {
                    if (!frozenCullingParamAvailable)
                    {
                        frozenCullingParams = cullingParams;
                        frozenCullingParamAvailable = true;
                    }
                    cullingParams = frozenCullingParams;
                }
            }
            else
            {
                frozenCullingParamAvailable = false;
            }

            m_LightLoop.UpdateCullingParameters(ref cullingParams);
            hdCamera.UpdateStereoDependentState(ref cullingParams);
            return true;
        }

        static bool TryCull(
            Camera camera,
            HDCamera hdCamera,
            ScriptableRenderContext renderContext,
            ScriptableCullingParameters cullingParams,
            ref HDCullingResults cullingResults
        )
        {
#if UNITY_EDITOR
            // emit scene view UI
            if (camera.cameraType == CameraType.SceneView)
            {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            }
#endif

            DecalSystem.CullRequest decalCullRequest = null;
            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.Decals))
            {
                // decal system needs to be updated with current camera, it needs it to set up culling and light list generation parameters
                decalCullRequest = GenericPool<DecalSystem.CullRequest>.Get();
                DecalSystem.instance.CurrentCamera = camera;
                DecalSystem.instance.BeginCull(decalCullRequest);
            }

            // TODO: use a parameter to select probe types to cull depending on what is enabled in framesettings
            var hdProbeCullState = new HDProbeCullState();
            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.RealtimePlanarReflection))
                hdProbeCullState = HDProbeSystem.PrepareCull(camera);

            using (new ProfilingSample(null, "CullResults.Cull", CustomSamplerId.CullResultsCull.GetSampler()))
                cullingResults.cullingResults = renderContext.Cull(ref cullingParams);

            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.RealtimePlanarReflection))
                HDProbeSystem.QueryCullResults(hdProbeCullState, ref cullingResults.hdProbeCullingResults);
            else
                cullingResults.hdProbeCullingResults = default;

            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.Decals))
            {
                using (new ProfilingSample(null, "DBufferPrepareDrawData", CustomSamplerId.DBufferPrepareDrawData.GetSampler()))
                    DecalSystem.instance.EndCull(decalCullRequest, cullingResults.decalCullResults);
            }

            if (decalCullRequest != null)
            {
                decalCullRequest.Clear();
                GenericPool<DecalSystem.CullRequest>.Release(decalCullRequest);
            }

            return true;
        }

        void RenderGizmos(CommandBuffer cmd, Camera camera, ScriptableRenderContext renderContext, GizmoSubset gizmoSubset)
        {
#if UNITY_EDITOR
            if (UnityEditor.Handles.ShouldRenderGizmos())
            {
                bool renderPrePostprocessGizmos = (gizmoSubset == GizmoSubset.PreImageEffects);

                using (new ProfilingSample(cmd,
                    renderPrePostprocessGizmos ? "PrePostprocessGizmos" : "Gizmos",
                    renderPrePostprocessGizmos ? CustomSamplerId.GizmosPrePostprocess.GetSampler() : CustomSamplerId.Gizmos.GetSampler()))
                {
                    renderContext.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    renderContext.DrawGizmos(camera, gizmoSubset);
                }
            }
#endif
        }

        void RenderOpaqueRenderList(CullingResults cull,
            HDCamera hdCamera,
            ScriptableRenderContext renderContext,
            CommandBuffer cmd,
            ShaderTagId passName,
            PerObjectData rendererConfiguration = 0,
            RenderQueueRange? inRenderQueueRange = null,
            RenderStateBlock? stateBlock = null,
            Material overrideMaterial = null)
        {
            m_SinglePassName[0] = passName;
            RenderOpaqueRenderList(cull, hdCamera, renderContext, cmd, m_SinglePassName, rendererConfiguration, inRenderQueueRange, stateBlock, overrideMaterial);
        }

        void RenderOpaqueRenderList(CullingResults cull,
            HDCamera hdCamera,
            ScriptableRenderContext renderContext,
            CommandBuffer cmd,
            ShaderTagId[] passNames,
            PerObjectData rendererConfiguration = 0,
            RenderQueueRange? inRenderQueueRange = null,
            RenderStateBlock? stateBlock = null,
            Material overrideMaterial = null,
            bool excludeMotionVector = false
            )
        {
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.OpaqueObjects))
                return;

            // This is done here because DrawRenderers API lives outside command buffers so we need to make call this before doing any DrawRenders
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var sortingSettings = new SortingSettings(hdCamera.camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };

            var drawSettings = new DrawingSettings(HDShaderPassNames.s_EmptyName, sortingSettings)
            {
                perObjectData = rendererConfiguration
            };

            for (int i = 0; i < passNames.Length; ++i)
            {
                drawSettings.SetShaderPassName(i, passNames[i]);
            }

            if (overrideMaterial != null)
            {
                drawSettings.overrideMaterial = overrideMaterial;
                drawSettings.overrideMaterialPassIndex = 0;
            }

            var filterSettings = new FilteringSettings(inRenderQueueRange == null ? HDRenderQueue.k_RenderQueue_AllOpaque : inRenderQueueRange.Value)
            {
                excludeMotionVectorObjects = excludeMotionVector
            };

            if (stateBlock == null)
                renderContext.DrawRenderers(cull, ref drawSettings, ref filterSettings);
            else
            {
                var renderStateBlock = stateBlock.Value;
                renderContext.DrawRenderers(cull, ref drawSettings, ref filterSettings, ref renderStateBlock);
            }
        }

        void RenderTransparentRenderList(CullingResults cull,
            HDCamera hdCamera,
            ScriptableRenderContext renderContext,
            CommandBuffer cmd,
            ShaderTagId passName,
            PerObjectData rendererConfiguration = 0,
            RenderQueueRange? inRenderQueueRange = null,
            RenderStateBlock? stateBlock = null,
            Material overrideMaterial = null,
            bool excludeMotionVectorObjects = false
            )
        {
            m_SinglePassName[0] = passName;
            RenderTransparentRenderList(cull, hdCamera, renderContext, cmd, m_SinglePassName,
                rendererConfiguration, inRenderQueueRange, stateBlock, overrideMaterial);
        }

        void RenderTransparentRenderList(CullingResults cull,
            HDCamera hdCamera,
            ScriptableRenderContext renderContext,
            CommandBuffer cmd,
            ShaderTagId[] passNames,
            PerObjectData rendererConfiguration = 0,
            RenderQueueRange? inRenderQueueRange = null,
            RenderStateBlock? stateBlock = null,
            Material overrideMaterial = null,
            bool excludeMotionVectorObjects = false
            )
        {
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.TransparentObjects))
                return;

            // This is done here because DrawRenderers API lives outside command buffers so we need to make call this before doing any DrawRenders
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var sortingSettings = new SortingSettings(hdCamera.camera)
            {
                criteria = SortingCriteria.CommonTransparent | SortingCriteria.RendererPriority
            };

            var drawSettings = new DrawingSettings(HDShaderPassNames.s_EmptyName, sortingSettings)
            {
                perObjectData = rendererConfiguration
            };

            for (int i = 0; i < passNames.Length; ++i)
            {
                drawSettings.SetShaderPassName(i, passNames[i]);
            }

            if (overrideMaterial != null)
            {
                drawSettings.overrideMaterial = overrideMaterial;
                drawSettings.overrideMaterialPassIndex = 0;
            }

            var filterSettings = new FilteringSettings(inRenderQueueRange == null ? HDRenderQueue.k_RenderQueue_AllTransparent : inRenderQueueRange.Value)
            {
                excludeMotionVectorObjects = excludeMotionVectorObjects
            };

            if (stateBlock == null)
                renderContext.DrawRenderers(cull, ref drawSettings, ref filterSettings);
            else
            {
                var renderStateBlock = stateBlock.Value;
                renderContext.DrawRenderers(cull, ref drawSettings, ref filterSettings, ref renderStateBlock);
            }
        }

        void AccumulateDistortion(CullingResults cullResults, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.Distortion))
                return;

            using (new ProfilingSample(cmd, "Distortion", CustomSamplerId.Distortion.GetSampler()))
            {
                HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.Distortion), m_RTManager.GetDepthStencilBuffer(), ClearFlag.Color, Color.clear);

                // Only transparent object can render distortion vectors
                RenderTransparentRenderList(cullResults, hdCamera, renderContext, cmd, HDShaderPassNames.s_DistortionVectorsName);
            }
        }

        void RenderDistortion(HDCamera hdCamera, CommandBuffer cmd)
        {
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.Distortion))
                return;

            using (new ProfilingSample(cmd, "ApplyDistortion", CustomSamplerId.ApplyDistortion.GetSampler()))
            {
                var currentColorPyramid = hdCamera.GetCurrentFrameRT((int)HDCameraFrameHistoryType.ColorBufferMipChain);

                var size = new Vector4(hdCamera.actualWidth, hdCamera.actualHeight, 1f / hdCamera.actualWidth, 1f / hdCamera.actualHeight);
                uint x, y, z;
                m_applyDistortionCS.GetKernelThreadGroupSizes(m_applyDistortionKernel, out x, out y, out z);
                cmd.SetComputeTextureParam(m_applyDistortionCS, m_applyDistortionKernel, HDShaderIDs._DistortionTexture, GetRenderTarget(RT.Distortion));
                cmd.SetComputeTextureParam(m_applyDistortionCS, m_applyDistortionKernel, HDShaderIDs._ColorPyramidTexture, currentColorPyramid);
                cmd.SetComputeTextureParam(m_applyDistortionCS, m_applyDistortionKernel, HDShaderIDs._Destination, GetRenderTarget(RT.Color));
                cmd.SetComputeVectorParam(m_applyDistortionCS, HDShaderIDs._Size, size);

                cmd.DispatchCompute(m_applyDistortionCS, m_applyDistortionKernel, Mathf.CeilToInt(size.x / x), Mathf.CeilToInt(size.y / y), 1);
            }
        }

        // RenderDepthPrepass render both opaque and opaque alpha tested based on engine configuration.
        // Forward only renderer: We always render everything
        // Deferred renderer: We always render depth prepass for alpha tested (optimization), other object are render based on engine configuration.
        // Forward opaque with deferred renderer (DepthForwardOnly pass): We always render everything
        // True is return if motion vector must be render after GBuffer pass
        bool RenderDepthPrepass(CullingResults cull, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            // In case of deferred renderer, we can have forward opaque material. These materials need to be render in the depth buffer to correctly build the light list.
            // And they will tag the stencil to not be lit during the deferred lighting pass.

            // Guidelines: In deferred by default there is no opaque in forward. However it is possible to force an opaque material to render in forward
            // by using the pass "ForwardOnly". In this case the .shader should not have "Forward" but only a "ForwardOnly" pass.
            // It must also have a "DepthForwardOnly" and no "DepthOnly" pass as forward material (either deferred or forward only rendering) have always a depth pass.
            // If a forward material have no depth prepass, then lighting can be incorrect (deferred shadowing, SSAO), this may be acceptable depends on usage

            // Whatever the configuration we always render first the opaque object as opaque alpha tested are more costly to render and could be reject by early-z
            // (but not Hi-z as it is disable with clip instruction). This is handled automatically with the RenderQueue value (OpaqueAlphaTested have a different value and thus are sorted after Opaque)

            // Forward material always output normal buffer (unless they don't participate to shading)
            // Deferred material never output normal buffer

            // Note: Unlit object use a ForwardOnly pass and don't have normal, they will write 0 in the normal buffer. This should be safe
            // as they will not use the result of lighting anyway. However take care of effect that will try to filter normal buffer.
            // TODO: maybe we can use a stencil to tag when Forward unlit touch normal buffer

            // Additional guidelines when motion vector are enabled
            // To save drawcall we don't render in prepass the object that have object motion vector. We use the excludeMotion filter option of DrawRenderer to know that (only C++ can know if an object have object motion vector).
            // Thus during this prepass we will exclude all object that have object motion vector, mean that during the velocity pass they will also output normal buffer (like a regular prepass) if needed.
            // Combination of both depth prepass + motion vector pass provide the full depth buffer
            // Caution: When there is no depth prepass we must render the object motion vector after the GBuffer pass, otherwise it mean some object that hide the object with motion vector will overwrite depth buffer but not the
            // velocity buffer and we will get artifacts

            // In order to avoid rendering objects twice (once in the depth pre-pass and once in the motion vector pass, when the motion vector pass is enabled). We exclude the objects that have motion vectors.
            bool excludeMotion = hdCamera.frameSettings.IsEnabled(FrameSettingsField.ObjectMotionVectors);
            bool shouldRenderMotionVectorAfterGBuffer = false;

            switch (hdCamera.frameSettings.litShaderMode)
            {
                case LitShaderMode.Forward:
                    using (new ProfilingSample(cmd, "Depth Prepass (forward)", CustomSamplerId.DepthPrepass.GetSampler()))
                    {
                        HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetPrepassRTI(hdCamera.frameSettings), m_RTManager.GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA)));

                        XRUtils.DrawOcclusionMesh(cmd, hdCamera.camera, hdCamera.camera.stereoEnabled);

                        // Full forward: Output normal buffer for both forward and forwardOnly
                        // Exclude object that render velocity (if motion vector are enabled)
                        RenderOpaqueRenderList(cull, hdCamera, renderContext, cmd, m_DepthOnlyAndDepthForwardOnlyPassNames, 0, HDRenderQueue.k_RenderQueue_AllOpaque, excludeMotionVector: excludeMotion);
                    }
                    break;
                case LitShaderMode.Deferred:
                    // If we enable DBuffer, we need a full depth prepass
                    if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.DepthPrepassWithDeferredRendering) || m_DbufferManager.enableDecals)
                    {
                        using (new ProfilingSample(cmd, m_DbufferManager.enableDecals ? "Depth Prepass (deferred) force by Decals" : "Depth Prepass (deferred)", CustomSamplerId.DepthPrepass.GetSampler()))
                        {
                            HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetDepthStencilBuffer());

                            XRUtils.DrawOcclusionMesh(cmd, hdCamera.camera, hdCamera.camera.stereoEnabled);

                            // First deferred material
                            RenderOpaqueRenderList(cull, hdCamera, renderContext, cmd, m_DepthOnlyPassNames, 0, HDRenderQueue.k_RenderQueue_AllOpaque, excludeMotionVector: excludeMotion);

                            HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetPrepassRTI(hdCamera.frameSettings), m_RTManager.GetDepthStencilBuffer());

                            // Then forward only material that output normal buffer
                            RenderOpaqueRenderList(cull, hdCamera, renderContext, cmd, m_DepthForwardOnlyPassNames, 0, HDRenderQueue.k_RenderQueue_AllOpaque, excludeMotionVector: excludeMotion);
                        }
                    }
                    else // Deferred with partial depth prepass
                    {
                        using (new ProfilingSample(cmd, "Depth Prepass (deferred incomplete)", CustomSamplerId.DepthPrepass.GetSampler()))
                        {
                            HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetDepthStencilBuffer());

                            XRUtils.DrawOcclusionMesh(cmd, hdCamera.camera, hdCamera.camera.stereoEnabled);

                            // First deferred alpha tested materials. Alpha tested object have always a prepass even if enableDepthPrepassWithDeferredRendering is disabled
                            var renderQueueRange = new RenderQueueRange { lowerBound = (int)RenderQueue.AlphaTest, upperBound = (int)RenderQueue.GeometryLast - 1 };
                            RenderOpaqueRenderList(cull, hdCamera, renderContext, cmd, m_DepthOnlyPassNames, 0, renderQueueRange);

                            HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetPrepassRTI(hdCamera.frameSettings), m_RTManager.GetDepthStencilBuffer());

                            // Then forward only material that output normal buffer
                            RenderOpaqueRenderList(cull, hdCamera, renderContext, cmd, m_DepthForwardOnlyPassNames, 0, HDRenderQueue.k_RenderQueue_AllOpaque);
                        }

                        shouldRenderMotionVectorAfterGBuffer = true;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unknown ShaderLitMode");
            }

#if ENABLE_RAYTRACING
            // If there is a ray-tracing environment and the feature is enabled we want to push these objects to the prepass
            HDRaytracingEnvironment currentEnv = m_RayTracingManager.CurrentEnvironment();
            // We want the opaque objects to be in the prepass so that we avoid rendering uselessly the pixels before raytracing them
            if (currentEnv != null && currentEnv.raytracedObjects)
                RenderOpaqueRenderList(cull, hdCamera, renderContext, cmd, m_DepthOnlyAndDepthForwardOnlyPassNames, 0, HDRenderQueue.k_RenderQueue_AllOpaqueRaytracing);
#endif

            return shouldRenderMotionVectorAfterGBuffer;
        }

        // RenderGBuffer do the gbuffer pass. This is solely call with deferred. If we use a depth prepass, then the depth prepass will perform the alpha testing for opaque alpha tested and we don't need to do it anymore
        // during Gbuffer pass. This is handled in the shader and the depth test (equal and no depth write) is done here.
        void RenderGBuffer(CullingResults cull, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (hdCamera.frameSettings.litShaderMode != LitShaderMode.Deferred)
                return;

            using (new ProfilingSample(cmd, m_CurrentDebugDisplaySettings.IsDebugDisplayEnabled() ? "GBuffer Debug" : "GBuffer", CustomSamplerId.GBuffer.GetSampler()))
            {
                // setup GBuffer for rendering
                HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetGBufferRTI(hdCamera.frameSettings), m_RTManager.GetDepthStencilBuffer());
                RenderOpaqueRenderList(cull, hdCamera, renderContext, cmd, HDShaderPassNames.s_GBufferName, m_currentRendererConfigurationBakedLighting, HDRenderQueue.k_RenderQueue_AllOpaque);

                m_RTManager.BindGBufferTextures(cmd);
            }
        }

        void RenderDBuffer(HDCamera hdCamera, CommandBuffer cmd, ScriptableRenderContext renderContext, CullingResults cullResults)
        {
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.Decals))
                return;

            using (new ProfilingSample(cmd, "DBufferRender", CustomSamplerId.DBufferRender.GetSampler()))
            {
                // We need to copy depth buffer texture if we want to bind it at this stage
                CopyDepthBufferIfNeeded(cmd);

                bool rtCount4 = m_Asset.GetRenderPipelineSettings().decalSettings.perChannelMask;
                // Depth texture is now ready, bind it.
                cmd.SetGlobalTexture(HDShaderIDs._CameraDepthTexture, m_RTManager.GetDepthTexture());
                m_DbufferManager.ClearAndSetTargets(cmd, hdCamera, rtCount4, m_RTManager);
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortingSettings = new SortingSettings(hdCamera.camera)
                {
                    criteria = SortingCriteria.CommonOpaque
                };

                var drawSettings = new DrawingSettings(HDShaderPassNames.s_EmptyName, sortingSettings)
                {
                    perObjectData = PerObjectData.None
                };

                if (rtCount4)
                {
                    drawSettings.SetShaderPassName(0, HDShaderPassNames.s_MeshDecalsMName);
                    drawSettings.SetShaderPassName(1, HDShaderPassNames.s_MeshDecalsAOName);
                    drawSettings.SetShaderPassName(2, HDShaderPassNames.s_MeshDecalsMAOName);
                    drawSettings.SetShaderPassName(3, HDShaderPassNames.s_MeshDecalsSName);
                    drawSettings.SetShaderPassName(4, HDShaderPassNames.s_MeshDecalsMSName);
                    drawSettings.SetShaderPassName(5, HDShaderPassNames.s_MeshDecalsAOSName);
                    drawSettings.SetShaderPassName(6, HDShaderPassNames.s_MeshDecalsMAOSName);
                    drawSettings.SetShaderPassName(7, HDShaderPassNames.s_ShaderGraphMeshDecalsName4RT);
                }
                else
                {
                    drawSettings.SetShaderPassName(0, HDShaderPassNames.s_MeshDecals3RTName);
                    drawSettings.SetShaderPassName(1, HDShaderPassNames.s_ShaderGraphMeshDecalsName3RT);
                }

                FilteringSettings filterRenderersSettings = new FilteringSettings(HDRenderQueue.k_RenderQueue_AllOpaque);
                renderContext.DrawRenderers(cullResults, ref drawSettings, ref filterRenderersSettings);
                DecalSystem.instance.RenderIntoDBuffer(cmd);
                m_DbufferManager.UnSetHTile(cmd);
                m_DbufferManager.SetHTileTexture(cmd, m_RTManager);  // mask per 8x8 tile used for optimization when looking up dbuffer values
            }
        }

        void RenderDebugViewMaterial(CullingResults cull, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            using (new ProfilingSample(cmd, "DisplayDebug ViewMaterial", CustomSamplerId.DisplayDebugViewMaterial.GetSampler()))
            {
                if (m_CurrentDebugDisplaySettings.data.materialDebugSettings.IsDebugGBufferEnabled() && hdCamera.frameSettings.litShaderMode == LitShaderMode.Deferred)
                {
                    using (new ProfilingSample(cmd, "DebugViewMaterialGBuffer", CustomSamplerId.DebugViewMaterialGBuffer.GetSampler()))
                    {
                        HDUtils.DrawFullScreen(cmd, hdCamera, m_currentDebugViewMaterialGBuffer, GetRenderTarget(RT.Color));
                    }
                }
                else
                {
                    // When rendering debug material we shouldn't rely on a depth prepass for optimizing the alpha clip test. As it is control on the material inspector side
                    // we must override the state here.

                    HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.Color), m_RTManager.GetDepthStencilBuffer(), ClearFlag.All, CoreUtils.clearColorAllBlack);
                    // Render Opaque forward
                    RenderOpaqueRenderList(cull, hdCamera, renderContext, cmd, m_AllForwardOpaquePassNames, m_currentRendererConfigurationBakedLighting, stateBlock: m_DepthStateOpaque);

                    // Render forward transparent
                    RenderTransparentRenderList(cull, hdCamera, renderContext, cmd, m_AllTransparentPassNames, m_currentRendererConfigurationBakedLighting, stateBlock: m_DepthStateOpaque);
                }
            }
        }

        void RenderDeferredLighting(HDCamera hdCamera, CommandBuffer cmd)
        {
            if (hdCamera.frameSettings.litShaderMode != LitShaderMode.Deferred)
                return;

            m_MRTCache2[0] = GetRenderTarget(RT.Color);
            m_MRTCache2[1] = GetRenderTarget(RT.SssDiffuseLighting);
            var depthTexture = m_RTManager.GetDepthTexture();

            var options = new LightLoop.LightingPassOptions();

            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.SubsurfaceScattering))
            {
                // Output split lighting for materials asking for it (masked in the stencil buffer)
                options.outputSplitLighting = true;

                m_LightLoop.RenderDeferredLighting(hdCamera, cmd, m_CurrentDebugDisplaySettings, m_MRTCache2, m_RTManager.GetDepthStencilBuffer(), depthTexture, options);
            }

            // Output combined lighting for all the other materials.
            options.outputSplitLighting = false;

            m_LightLoop.RenderDeferredLighting(hdCamera, cmd, m_CurrentDebugDisplaySettings, m_MRTCache2, m_RTManager.GetDepthStencilBuffer(), depthTexture, options);
        }

        void UpdateSkyEnvironment(HDCamera hdCamera, CommandBuffer cmd)
        {
            m_SkyManager.UpdateEnvironment(hdCamera, m_LightLoop.GetCurrentSunLight(), cmd);
        }

        public void RequestSkyEnvironmentUpdate()
        {
            m_SkyManager.RequestEnvironmentUpdate();
        }

        void RenderSky(HDCamera hdCamera, CommandBuffer cmd)
        {
            var colorBuffer = hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA) ? GetRenderTarget(RT.ColorMSAA) : GetRenderTarget(RT.Color);
            var depthBuffer = m_RTManager.GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA));

            var visualEnv = VolumeManager.instance.stack.GetComponent<VisualEnvironment>();
            m_SkyManager.RenderSky(hdCamera, m_LightLoop.GetCurrentSunLight(), colorBuffer, depthBuffer, m_CurrentDebugDisplaySettings, cmd);

            if (visualEnv.fogType.value != FogType.None)
            {
                // TODO: compute only once.
#if UNITY_2019_1_OR_NEWER
                var pixelCoordToViewDirWS = HDUtils.ComputePixelCoordToWorldSpaceViewDirectionMatrix(hdCamera.camera.GetGateFittedFieldOfView() * Mathf.Deg2Rad, hdCamera.camera.GetGateFittedLensShift(), hdCamera.screenSize, hdCamera.viewMatrix, false);
#else
                var pixelCoordToViewDirWS = HDUtils.ComputePixelCoordToWorldSpaceViewDirectionMatrix(hdCamera.camera.fieldOfView * Mathf.Deg2Rad, Vector2.zero, hdCamera.screenSize, hdCamera.viewMatrix, false);
#endif
                m_SkyManager.RenderOpaqueAtmosphericScattering(cmd, hdCamera, colorBuffer, depthBuffer, pixelCoordToViewDirWS, hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA));
            }
        }

        public Texture2D ExportSkyToTexture()
        {
            return m_SkyManager.ExportSkyToTexture();
        }

        // Render forward is use for both transparent and opaque objects. In case of deferred we can still render opaque object in forward.
        void RenderForward(CullingResults cullResults, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd, ForwardPass pass)
        {
            // Guidelines: In deferred by default there is no opaque in forward. However it is possible to force an opaque material to render in forward
            // by using the pass "ForwardOnly". In this case the .shader should not have "Forward" but only a "ForwardOnly" pass.
            // It must also have a "DepthForwardOnly" and no "DepthOnly" pass as forward material (either deferred or forward only rendering) have always a depth pass.
            // The RenderForward pass will render the appropriate pass depends on the engine settings. In case of forward only rendering, both "Forward" pass and "ForwardOnly" pass
            // material will be render for both transparent and opaque. In case of deferred, both path are used for transparent but only "ForwardOnly" is use for opaque.
            // (Thus why "Forward" and "ForwardOnly" are exclusive, else they will render two times"

            // If rough refraction are turned off, we render all transparents in the Transparent pass and we skip the PreRefraction one.
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.RoughRefraction) && pass == ForwardPass.PreRefraction)
            {
                return;
            }

            if (m_CurrentDebugDisplaySettings.IsDebugDisplayEnabled())
            {
                m_ForwardPassProfileName = k_ForwardPassDebugName[(int)pass];
            }
            else
            {
                m_ForwardPassProfileName = k_ForwardPassName[(int)pass];
            }

            using (new ProfilingSample(cmd, m_ForwardPassProfileName, CustomSamplerId.ForwardPassName.GetSampler()))
            {
                var camera = hdCamera.camera;

                m_LightLoop.RenderForward(camera, cmd, pass == ForwardPass.Opaque);

                if (pass == ForwardPass.Opaque)
                {
                    // In case of forward SSS we will bind all the required target. It is up to the shader to write into it or not.
                    if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.SubsurfaceScattering))
                    {
                        if(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA))
                        {
                            m_MRTWithSSS[0] = GetRenderTarget(RT.ColorMSAA); // Store the specular color
                            m_MRTWithSSS[1] = GetRenderTarget(RT.SssDiffuseLightingMSAA);
                            for (int i = 0; i < SubsurfaceScatteringManager.sssBufferCount; ++i)
                            {
                                m_MRTWithSSS[i + 2] = m_RTManager.GetSSSBufferMSAA(i);
                            }
                        }
                        else
                        {
                            m_MRTWithSSS[0] = GetRenderTarget(RT.Color); // Store the specular color
                            m_MRTWithSSS[1] = GetRenderTarget(RT.SssDiffuseLighting);
                            for (int i = 0; i < SubsurfaceScatteringManager.sssBufferCount; ++i)
                            {
                                m_MRTWithSSS[i + 2] = m_RTManager.GetSSSBuffer(i);
                            }
                        }
                        HDUtils.SetRenderTarget(cmd, hdCamera, m_MRTWithSSS, m_RTManager.GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA)));
                    }
                    else
                    {
                        HDUtils.SetRenderTarget(cmd, hdCamera, hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA) ? GetRenderTarget(RT.ColorMSAA) : GetRenderTarget(RT.Color), m_RTManager.GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA)));
                    }

                    var passNames = hdCamera.frameSettings.litShaderMode == LitShaderMode.Forward
                        ? m_ForwardAndForwardOnlyPassNames
                        : m_ForwardOnlyPassNames;
                    RenderOpaqueRenderList(cullResults, hdCamera, renderContext, cmd, passNames, m_currentRendererConfigurationBakedLighting);
                }
                else
                {
                    HDUtils.SetRenderTarget(cmd, hdCamera, hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA) ? GetRenderTarget(RT.ColorMSAA) : GetRenderTarget(RT.Color), m_RTManager.GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA)));
                    if ((hdCamera.frameSettings.IsEnabled(FrameSettingsField.Decals)) && (DecalSystem.m_DecalDatasCount > 0)) // enable d-buffer flag value is being interpreted more like enable decals in general now that we have clustered
                                                                                                      // decal datas count is 0 if no decals affect transparency
                    {
                        DecalSystem.instance.SetAtlas(cmd); // for clustered decals
                    }

                    RenderQueueRange transparentRange = pass == ForwardPass.PreRefraction ? HDRenderQueue.k_RenderQueue_PreRefraction : HDRenderQueue.k_RenderQueue_Transparent;
                    if(!hdCamera.frameSettings.IsEnabled(FrameSettingsField.RoughRefraction))
                    {
                        transparentRange = HDRenderQueue.k_RenderQueue_AllTransparent;
                    }
                    RenderTransparentRenderList(cullResults, hdCamera, renderContext, cmd,  m_Asset.renderPipelineSettings.supportTransparentBackface ? m_AllTransparentPassNames : m_TransparentNoBackfaceNames, m_currentRendererConfigurationBakedLighting, transparentRange);
                }
            }
        }

        // This is use to Display legacy shader with an error shader
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        void RenderForwardError(CullingResults cullResults, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            using (new ProfilingSample(cmd, "Forward Error", CustomSamplerId.RenderForwardError.GetSampler()))
            {
                HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.Color), m_RTManager.GetDepthStencilBuffer());
                RenderOpaqueRenderList(cullResults, hdCamera, renderContext, cmd, m_ForwardErrorPassNames, 0, RenderQueueRange.all, null, m_ErrorMaterial);
            }
        }

        void RenderTransparentDepthPrepass(CullingResults cull, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.TransparentPrepass))
            {
                // Render transparent depth prepass after opaque one
                using (new ProfilingSample(cmd, "Transparent Depth Prepass", CustomSamplerId.TransparentDepthPrepass.GetSampler()))
                {
                    HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetDepthStencilBuffer());
                    RenderTransparentRenderList(cull, hdCamera, renderContext, cmd, m_TransparentDepthPrepassNames);
                }
            }
        }

        void RenderTransparentDepthPostpass(CullingResults cullResults, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.TransparentPostpass))
                return;

            using (new ProfilingSample(cmd, "Transparent Depth Post ", CustomSamplerId.TransparentDepthPostpass.GetSampler()))
            {
                HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetDepthStencilBuffer());
                RenderTransparentRenderList(cullResults, hdCamera, renderContext, cmd, m_TransparentDepthPostpassNames);
            }
        }

        void RenderObjectsVelocity(CullingResults cullResults, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.ObjectMotionVectors))
                return;

            using (new ProfilingSample(cmd, "Objects Velocity", CustomSamplerId.ObjectsVelocity.GetSampler()))
            {
                // These flags are still required in SRP or the engine won't compute previous model matrices...
                // If the flag hasn't been set yet on this camera, motion vectors will skip a frame.
                hdCamera.camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetVelocityPassRTI(hdCamera.frameSettings), m_RTManager.GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA)));
                RenderOpaqueRenderList(cullResults, hdCamera, renderContext, cmd, HDShaderPassNames.s_MotionVectorsName, PerObjectData.MotionVectors);
            }
        }

        void RenderCameraVelocity(CullingResults cullResults, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.MotionVectors))
                return;

            using (new ProfilingSample(cmd, "Camera Velocity", CustomSamplerId.CameraVelocity.GetSampler()))
            {
                // These flags are still required in SRP or the engine won't compute previous model matrices...
                // If the flag hasn't been set yet on this camera, motion vectors will skip a frame.
                hdCamera.camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                HDUtils.DrawFullScreen(cmd, hdCamera, m_CameraMotionVectorsMaterial, m_RTManager.GetVelocityBuffer(), m_RTManager.GetDepthStencilBuffer(), null, 0);
                PushFullScreenDebugTexture(hdCamera, cmd, m_RTManager.GetVelocityBuffer(), FullScreenDebugMode.MotionVectors);

#if UNITY_EDITOR

                // In scene view there is no motion vector, so we clear the RT to black
                if (hdCamera.camera.cameraType == CameraType.SceneView)
                {
                    HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetVelocityBuffer(), m_RTManager.GetDepthStencilBuffer(), ClearFlag.Color, CoreUtils.clearColorAllBlack);
                }
#endif
            }
        }

        void RenderSSR(HDCamera hdCamera, CommandBuffer cmd, ScriptableRenderContext renderContext)
        {
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.SSR))
                return;

#if ENABLE_RAYTRACING
            HDRaytracingEnvironment rtEnvironement = m_RayTracingManager.CurrentEnvironment();

            if (m_Asset.renderPipelineSettings.supportRayTracing && rtEnvironement != null && rtEnvironement.raytracedReflections)
            {
                m_RaytracingReflections.RenderReflections(hdCamera, cmd, m_RTManager.GetRenderTarget(RT.SSRLighting), renderContext);

                PushFullScreenDebugTexture(hdCamera, cmd, m_RaytracingReflections.m_LightCluster.m_DebugLightClusterTexture, FullScreenDebugMode.LightCluster);
            }
            else
#endif
            {
                var cs = m_ScreenSpaceReflectionsCS;

                var previousColorPyramid = hdCamera.GetPreviousFrameRT((int)HDCameraFrameHistoryType.ColorBufferMipChain);

                Vector2Int previousColorPyramidSize = new Vector2Int(previousColorPyramid.rt.width, previousColorPyramid.rt.height);

                int w = hdCamera.actualWidth;
                int h = hdCamera.actualHeight;

                using (new ProfilingSample(cmd, "SSR - Tracing", CustomSamplerId.SsrTracing.GetSampler()))
                {
                    var volumeSettings = VolumeManager.instance.stack.GetComponent<ScreenSpaceReflection>();

                    if (!volumeSettings) volumeSettings = ScreenSpaceReflection.@default;

                    int kernel = m_SsrTracingKernel;

                    float n = hdCamera.camera.nearClipPlane;
                    float f = hdCamera.camera.farClipPlane;

                    float thickness      = volumeSettings.depthBufferThickness;
                    float thicknessScale = 1.0f / (1.0f + thickness);
                    float thicknessBias  = -n / (f - n) * (thickness * thicknessScale);

                    HDUtils.PackedMipChainInfo info = m_RTManager.GetDepthBufferMipChainInfo();

                    float roughnessFadeStart             = 1 - volumeSettings.smoothnessFadeStart;
                    float roughnessFadeEnd               = 1 - volumeSettings.minSmoothness;
                    float roughnessFadeLength            = roughnessFadeEnd - roughnessFadeStart;
                    float roughnessFadeEndTimesRcpLength = (roughnessFadeLength != 0) ? (roughnessFadeEnd * (1.0f / roughnessFadeLength)) : 1;
                    float roughnessFadeRcpLength         = (roughnessFadeLength != 0) ? (1.0f / roughnessFadeLength) : 0;
                    float edgeFadeRcpLength              = Mathf.Min(1.0f / volumeSettings.screenFadeDistance, float.MaxValue);

                    cmd.SetComputeIntParam(  cs, HDShaderIDs._SsrIterLimit,                      volumeSettings.rayMaxIterations);
                    cmd.SetComputeFloatParam(cs, HDShaderIDs._SsrThicknessScale,                 thicknessScale);
                    cmd.SetComputeFloatParam(cs, HDShaderIDs._SsrThicknessBias,                  thicknessBias);
                    cmd.SetComputeFloatParam(cs, HDShaderIDs._SsrRoughnessFadeEnd,               roughnessFadeEnd);
                    cmd.SetComputeFloatParam(cs, HDShaderIDs._SsrRoughnessFadeRcpLength,         roughnessFadeRcpLength);
                    cmd.SetComputeFloatParam(cs, HDShaderIDs._SsrRoughnessFadeEndTimesRcpLength, roughnessFadeEndTimesRcpLength);
                    cmd.SetComputeIntParam(  cs, HDShaderIDs._SsrDepthPyramidMaxMip,             info.mipLevelCount);
                    cmd.SetComputeFloatParam(cs, HDShaderIDs._SsrEdgeFadeRcpLength,              edgeFadeRcpLength);
                    cmd.SetComputeIntParam(  cs, HDShaderIDs._SsrReflectsSky,                    volumeSettings.reflectSky ? 1 : 0);
                    cmd.SetComputeIntParam(  cs, HDShaderIDs._SsrStencilExclusionValue,          (int)StencilBitMask.DoesntReceiveSSR);
                    cmd.SetComputeVectorParam(cs, HDShaderIDs._ColorPyramidUvScaleAndLimitPrevFrame, HDUtils.ComputeUvScaleAndLimit(hdCamera.viewportSizePrevFrame, previousColorPyramidSize));

                    // cmd.SetComputeTextureParam(cs, kernel, "_SsrDebugTexture", RTManager.GetRT(RT.SSRDebug));
                    cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._SsrHitPointTexture, GetRenderTarget(RT.SSRHitPoint));
                    cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._StencilTexture, GetRenderTarget(RT.StencilCopy));

                    cmd.SetComputeBufferParam(cs, kernel, HDShaderIDs._SsrDepthPyramidMipOffsets, info.GetOffsetBufferData(m_DepthPyramidMipLevelOffsetsBuffer));

                    cmd.DispatchCompute(cs, kernel, HDUtils.DivRoundUp(w, 8), HDUtils.DivRoundUp(h, 8), 1);
                }

                using (new ProfilingSample(cmd, "SSR - Reprojection", CustomSamplerId.SsrReprojection.GetSampler()))
                {
                    int kernel = m_SsrReprojectionKernel;

                    // cmd.SetComputeTextureParam(cs, kernel, "_SsrDebugTexture", RTManager.GetRT(RT.SSRDebug));
                    cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._SsrHitPointTexture,   GetRenderTarget(RT.SSRHitPoint));
                    cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._SsrLightingTextureRW, GetRenderTarget(RT.SSRLighting));
                    cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._ColorPyramidTexture,  previousColorPyramid);

                    cmd.DispatchCompute(cs, kernel, HDUtils.DivRoundUp(w, 8), HDUtils.DivRoundUp(h, 8), 1);
                }

            	if (!hdCamera.colorPyramidHistoryIsValid)
            	{
                	cmd.SetGlobalTexture(HDShaderIDs._SsrLightingTexture, HDUtils.clearTexture);
                	hdCamera.colorPyramidHistoryIsValid = true; // For the next frame...
            	}
			}

            PushFullScreenDebugTexture(hdCamera, cmd, GetRenderTarget(RT.SSRLighting), FullScreenDebugMode.ScreenSpaceReflections);
        }

        void RenderColorPyramid(HDCamera hdCamera, CommandBuffer cmd, bool isPreRefraction)
        {
            if (isPreRefraction)
            {
                if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.RoughRefraction))
                    return;
            }
            else
            {
                // TODO: This final Gaussian pyramid can be reuse by Bloom and SSR in the future, so disable it only if there is no postprocess AND no distortion
                if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.Distortion) && !hdCamera.frameSettings.IsEnabled(FrameSettingsField.Postprocess) && !hdCamera.frameSettings.IsEnabled(FrameSettingsField.SSR))
                    return;
            }

            var currentColorPyramid = hdCamera.GetCurrentFrameRT((int)HDCameraFrameHistoryType.ColorBufferMipChain);

            int lodCount;

            using (new ProfilingSample(cmd, "Color Gaussian MIP Chain", CustomSamplerId.ColorPyramid.GetSampler()))
            {
                m_PyramidSizeV2I.Set(hdCamera.actualWidth, hdCamera.actualHeight);
                lodCount = m_MipGenerator.RenderColorGaussianPyramid(cmd, m_PyramidSizeV2I, GetRenderTarget(RT.Color), currentColorPyramid);
            }

            float scaleX = hdCamera.actualWidth / (float)currentColorPyramid.rt.width;
            float scaleY = hdCamera.actualHeight / (float)currentColorPyramid.rt.height;
            m_PyramidSizeV4F.Set(hdCamera.actualWidth, hdCamera.actualHeight, 1f / hdCamera.actualWidth, 1f / hdCamera.actualHeight);
            m_PyramidScaleLod.Set(scaleX, scaleY, lodCount, 0.0f);
            m_PyramidScale.Set(scaleX, scaleY, 0f, 0f);
            // Warning! Danger!
            // The color pyramid scale is only correct for the most detailed MIP level.
            // For the other MIP levels, due to truncation after division by 2, a row or
            // column of texels may be lost. Since this can happen to BOTH the texture
            // size AND the viewport, (uv * _ColorPyramidScale.xy) can be off by a texel
            // unless the scale is 1 (and it will not be 1 if the texture was resized
            // and is of greater size compared to the viewport).
            cmd.SetGlobalTexture(HDShaderIDs._ColorPyramidTexture, currentColorPyramid);
            cmd.SetGlobalVector(HDShaderIDs._ColorPyramidSize, m_PyramidSizeV4F);
            cmd.SetGlobalVector(HDShaderIDs._ColorPyramidScale, m_PyramidScaleLod);
            PushFullScreenDebugTextureMip(hdCamera, cmd, currentColorPyramid, lodCount, m_PyramidScale, isPreRefraction ? FullScreenDebugMode.PreRefractionColorPyramid : FullScreenDebugMode.FinalColorPyramid);
        }

        void GenerateDepthPyramid(HDCamera hdCamera, CommandBuffer cmd, FullScreenDebugMode debugMode)
        {
            CopyDepthBufferIfNeeded(cmd);

            int mipCount = m_RTManager.GetDepthBufferMipChainInfo().mipLevelCount;

            using (new ProfilingSample(cmd, "Generate Depth Buffer MIP Chain", CustomSamplerId.DepthPyramid.GetSampler()))
            {
                m_MipGenerator.RenderMinDepthPyramid(cmd, m_RTManager.GetDepthTexture(), m_RTManager.GetDepthBufferMipChainInfo());
            }

            float scaleX = hdCamera.actualWidth / (float)m_RTManager.GetDepthTexture().rt.width;
            float scaleY = hdCamera.actualHeight / (float)m_RTManager.GetDepthTexture().rt.height;
            m_PyramidSizeV4F.Set(hdCamera.actualWidth, hdCamera.actualHeight, 1f / hdCamera.actualWidth, 1f / hdCamera.actualHeight);
            m_PyramidScaleLod.Set(scaleX, scaleY, mipCount, 0.0f);
            m_PyramidScale.Set(scaleX, scaleY, 0f, 0f);
            cmd.SetGlobalTexture(HDShaderIDs._DepthPyramidTexture, m_RTManager.GetDepthTexture());
            cmd.SetGlobalVector(HDShaderIDs._DepthPyramidSize, m_PyramidSizeV4F);
            cmd.SetGlobalVector(HDShaderIDs._DepthPyramidScale, m_PyramidScaleLod);
            PushFullScreenDebugTextureMip(hdCamera, cmd, m_RTManager.GetDepthTexture(), mipCount, m_PyramidScale, debugMode);
        }

        public void ApplyDebugDisplaySettings(HDCamera hdCamera, CommandBuffer cmd)
        {
            // See ShaderPassForward.hlsl: for forward shaders, if DEBUG_DISPLAY is enabled and no DebugLightingMode or DebugMipMapMod
            // modes have been set, lighting is automatically skipped (To avoid some crashed due to lighting RT not set on console).
            // However debug mode like colorPickerModes and false color don't need DEBUG_DISPLAY and must work with the lighting.
            // So we will enabled DEBUG_DISPLAY independently

            // Enable globally the keyword DEBUG_DISPLAY on shader that support it with multi-compile
            CoreUtils.SetKeyword(cmd, "DEBUG_DISPLAY", m_CurrentDebugDisplaySettings.IsDebugDisplayEnabled());

            if (m_CurrentDebugDisplaySettings.IsDebugDisplayEnabled() ||
                m_CurrentDebugDisplaySettings.data.colorPickerDebugSettings.colorPickerMode != ColorPickerDebugMode.None)
            {
                // This is for texture streaming
                m_CurrentDebugDisplaySettings.UpdateMaterials();

                var lightingDebugSettings = m_CurrentDebugDisplaySettings.data.lightingDebugSettings;
                var materialDebugSettings = m_CurrentDebugDisplaySettings.data.materialDebugSettings;
                var debugAlbedo = new Vector4(lightingDebugSettings.overrideAlbedo ? 1.0f : 0.0f, lightingDebugSettings.overrideAlbedoValue.r, lightingDebugSettings.overrideAlbedoValue.g, lightingDebugSettings.overrideAlbedoValue.b);
                var debugSmoothness = new Vector4(lightingDebugSettings.overrideSmoothness ? 1.0f : 0.0f, lightingDebugSettings.overrideSmoothnessValue, 0.0f, 0.0f);
                var debugNormal = new Vector4(lightingDebugSettings.overrideNormal ? 1.0f : 0.0f, 0.0f, 0.0f, 0.0f);
                var debugSpecularColor = new Vector4(lightingDebugSettings.overrideSpecularColor ? 1.0f : 0.0f, lightingDebugSettings.overrideSpecularColorValue.r, lightingDebugSettings.overrideSpecularColorValue.g, lightingDebugSettings.overrideSpecularColorValue.b);
                var debugEmissiveColor = new Vector4(lightingDebugSettings.overrideEmissiveColor ? 1.0f : 0.0f, lightingDebugSettings.overrideEmissiveColorValue.r, lightingDebugSettings.overrideEmissiveColorValue.g, lightingDebugSettings.overrideEmissiveColorValue.b);
                var debugTrueMetalColor = new Vector4(materialDebugSettings.materialValidateTrueMetal ? 1.0f : 0.0f, materialDebugSettings.materialValidateTrueMetalColor.r, materialDebugSettings.materialValidateTrueMetalColor.g, materialDebugSettings.materialValidateTrueMetalColor.b);

                cmd.SetGlobalInt(HDShaderIDs._DebugViewMaterial, (int)m_CurrentDebugDisplaySettings.GetDebugMaterialIndex());
                cmd.SetGlobalInt(HDShaderIDs._DebugLightingMode, (int)m_CurrentDebugDisplaySettings.GetDebugLightingMode());
                cmd.SetGlobalInt(HDShaderIDs._DebugShadowMapMode, (int)m_CurrentDebugDisplaySettings.GetDebugShadowMapMode());
                cmd.SetGlobalInt(HDShaderIDs._DebugMipMapMode, (int)m_CurrentDebugDisplaySettings.GetDebugMipMapMode());
                cmd.SetGlobalInt(HDShaderIDs._DebugMipMapModeTerrainTexture, (int)m_CurrentDebugDisplaySettings.GetDebugMipMapModeTerrainTexture());
                cmd.SetGlobalInt(HDShaderIDs._ColorPickerMode, (int)m_CurrentDebugDisplaySettings.GetDebugColorPickerMode());
                cmd.SetGlobalInt(HDShaderIDs._DebugFullScreenMode, (int)m_CurrentDebugDisplaySettings.data.fullScreenDebugMode);

                cmd.SetGlobalVector(HDShaderIDs._DebugLightingAlbedo, debugAlbedo);
                cmd.SetGlobalVector(HDShaderIDs._DebugLightingSmoothness, debugSmoothness);
                cmd.SetGlobalVector(HDShaderIDs._DebugLightingNormal, debugNormal);
                cmd.SetGlobalVector(HDShaderIDs._DebugLightingSpecularColor, debugSpecularColor);
                cmd.SetGlobalVector(HDShaderIDs._DebugLightingEmissiveColor, debugEmissiveColor);
                cmd.SetGlobalColor(HDShaderIDs._DebugLightingMaterialValidateHighColor, materialDebugSettings.materialValidateHighColor);
                cmd.SetGlobalColor(HDShaderIDs._DebugLightingMaterialValidateLowColor, materialDebugSettings.materialValidateLowColor);
                cmd.SetGlobalColor(HDShaderIDs._DebugLightingMaterialValidatePureMetalColor, debugTrueMetalColor);

                cmd.SetGlobalVector(HDShaderIDs._MousePixelCoord, HDUtils.GetMouseCoordinates(hdCamera));
                cmd.SetGlobalVector(HDShaderIDs._MouseClickPixelCoord, HDUtils.GetMouseClickCoordinates(hdCamera));
                cmd.SetGlobalTexture(HDShaderIDs._DebugFont, m_Asset.renderPipelineResources.textures.debugFontTex);

                // The DebugNeedsExposure test allows us to set a neutral value if exposure is not needed. This way we don't need to make various tests inside shaders but only in this function.
                cmd.SetGlobalFloat(HDShaderIDs._DebugExposure, m_CurrentDebugDisplaySettings.DebugNeedsExposure() ? lightingDebugSettings.debugExposure : 0.0f);
            }
        }

        public void PushColorPickerDebugTexture(CommandBuffer cmd, HDCamera hdCamera, RTHandleSystem.RTHandle inputTexture)
        {
            if (m_CurrentDebugDisplaySettings.data.colorPickerDebugSettings.colorPickerMode != ColorPickerDebugMode.None || m_DebugDisplaySettings.data.falseColorDebugSettings.falseColor || m_DebugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.LuminanceMeter)
            {
                using (new ProfilingSample(cmd, "Push To Color Picker"))
                {
                    HDUtils.BlitCameraTexture(cmd, hdCamera, inputTexture, GetRenderTarget(RT.DebugColorPicker));
                }
            }
        }

        bool NeedsFullScreenDebugMode()
        {
            bool fullScreenDebugEnabled = m_CurrentDebugDisplaySettings.data.fullScreenDebugMode != FullScreenDebugMode.None;
            bool lightingDebugEnabled = m_CurrentDebugDisplaySettings.data.lightingDebugSettings.shadowDebugMode == ShadowMapDebugMode.SingleShadow;

            return fullScreenDebugEnabled || lightingDebugEnabled;
        }

        public void PushFullScreenLightingDebugTexture(HDCamera hdCamera, CommandBuffer cmd, RTHandleSystem.RTHandle textureID)
        {
            if (NeedsFullScreenDebugMode() && m_FullScreenDebugPushed == false)
            {
                m_FullScreenDebugPushed = true;
                HDUtils.BlitCameraTexture(cmd, hdCamera, textureID, GetRenderTarget(RT.DebugFullScreen));
            }
        }

        public void PushFullScreenDebugTexture(HDCamera hdCamera, CommandBuffer cmd, RTHandleSystem.RTHandle textureID, FullScreenDebugMode debugMode)
        {
            if (debugMode == m_CurrentDebugDisplaySettings.data.fullScreenDebugMode)
            {
                m_FullScreenDebugPushed = true; // We need this flag because otherwise if no full screen debug is pushed (like for example if the corresponding pass is disabled), when we render the result in RenderDebug m_DebugFullScreenTempBuffer will contain potential garbage
                HDUtils.BlitCameraTexture(cmd, hdCamera, textureID, GetRenderTarget(RT.DebugFullScreen));
            }
        }

        void PushFullScreenDebugTextureMip(HDCamera hdCamera, CommandBuffer cmd, RTHandleSystem.RTHandle texture, int lodCount, Vector4 scaleBias, FullScreenDebugMode debugMode)
        {
            if (debugMode == m_CurrentDebugDisplaySettings.data.fullScreenDebugMode)
            {
                var mipIndex = Mathf.FloorToInt(m_CurrentDebugDisplaySettings.data.fullscreenDebugMip * (lodCount));

                m_FullScreenDebugPushed = true; // We need this flag because otherwise if no full screen debug is pushed (like for example if the corresponding pass is disabled), when we render the result in RenderDebug m_DebugFullScreenTempBuffer will contain potential garbage
                HDUtils.BlitCameraTexture(cmd, hdCamera, texture, GetRenderTarget(RT.DebugFullScreen), scaleBias, mipIndex);
            }
        }

        void RenderDebug(HDCamera hdCamera, CommandBuffer cmd, CullingResults cullResults)
        {
            // We don't want any overlay for these kind of rendering
            if (hdCamera.camera.cameraType == CameraType.Reflection || hdCamera.camera.cameraType == CameraType.Preview)
                return;

            // Render Debug are only available in dev builds and we always render them in the same RT
            HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.IntermediateAfterPostProcess), m_RTManager.GetDepthStencilBuffer());

            using (new ProfilingSample(cmd, "Debug", CustomSamplerId.RenderDebug.GetSampler()))
            {
                // First render full screen debug texture
                if (NeedsFullScreenDebugMode() && m_FullScreenDebugPushed)
                {
                    m_FullScreenDebugPushed = false;
                    m_DebugFullScreenPropertyBlock.SetTexture(HDShaderIDs._DebugFullScreenTexture, GetRenderTarget(RT.DebugFullScreen));
                    m_DebugFullScreenPropertyBlock.SetFloat(HDShaderIDs._FullScreenDebugMode, (float)m_CurrentDebugDisplaySettings.data.fullScreenDebugMode);
                    HDUtils.PackedMipChainInfo info = m_RTManager.GetDepthBufferMipChainInfo();
                    m_DebugFullScreenPropertyBlock.SetInt(HDShaderIDs._DebugDepthPyramidMip, (int)(m_CurrentDebugDisplaySettings.data.fullscreenDebugMip * info.mipLevelCount));
                    m_DebugFullScreenPropertyBlock.SetBuffer(HDShaderIDs._DebugDepthPyramidOffsets, info.GetOffsetBufferData(m_DepthPyramidMipLevelOffsetsBuffer));

                    cmd.SetGlobalVector(HDShaderIDs._ScreenToTargetScale, hdCamera.doubleBufferedViewportScale);
                    HDUtils.DrawFullScreen(cmd, hdCamera, m_DebugFullScreen, GetRenderTarget(RT.IntermediateAfterPostProcess), m_DebugFullScreenPropertyBlock, 0);
                    PushColorPickerDebugTexture(cmd, hdCamera, GetRenderTarget(RT.IntermediateAfterPostProcess));
                }

                // Then debug color picking
                if (m_CurrentDebugDisplaySettings.data.colorPickerDebugSettings.colorPickerMode != ColorPickerDebugMode.None || m_CurrentDebugDisplaySettings.data.falseColorDebugSettings.falseColor || m_CurrentDebugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.LuminanceMeter)
                {
                    ColorPickerDebugSettings colorPickerDebugSettings = m_CurrentDebugDisplaySettings.data.colorPickerDebugSettings;
                    FalseColorDebugSettings falseColorDebugSettings = m_CurrentDebugDisplaySettings.data.falseColorDebugSettings;
                    var falseColorThresholds = new Vector4(falseColorDebugSettings.colorThreshold0, falseColorDebugSettings.colorThreshold1, falseColorDebugSettings.colorThreshold2, falseColorDebugSettings.colorThreshold3);

                    // Here we have three cases:
                    // - Material debug is enabled, this is the buffer we display
                    // - Otherwise we display the HDR buffer before postprocess and distortion
                    // - If fullscreen debug is enabled we always use it

                    m_DebugFullScreenPropertyBlock.SetTexture(HDShaderIDs._DebugColorPickerTexture, GetRenderTarget(RT.DebugColorPicker));
                    m_DebugFullScreenPropertyBlock.SetColor(HDShaderIDs._ColorPickerFontColor, colorPickerDebugSettings.fontColor);
                    m_DebugFullScreenPropertyBlock.SetInt(HDShaderIDs._FalseColorEnabled, falseColorDebugSettings.falseColor ? 1 : 0);
                    m_DebugFullScreenPropertyBlock.SetVector(HDShaderIDs._FalseColorThresholds, falseColorThresholds);
                    // The material display debug perform sRGBToLinear conversion as the final blit currently hardcodes a linearToSrgb conversion. As when we read with color picker this is not done,
                    // we perform it inside the color picker shader. But we shouldn't do it for HDR buffer.
                    m_DebugFullScreenPropertyBlock.SetFloat(HDShaderIDs._ApplyLinearToSRGB, m_CurrentDebugDisplaySettings.IsDebugMaterialDisplayEnabled() ? 1.0f : 0.0f);

                    cmd.SetGlobalVector(HDShaderIDs._ScreenToTargetScale, hdCamera.doubleBufferedViewportScale);
                    HDUtils.DrawFullScreen(cmd, hdCamera, m_DebugColorPicker, GetRenderTarget(RT.IntermediateAfterPostProcess), m_DebugFullScreenPropertyBlock, 0);
                }

                // Then overlays
                HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.IntermediateAfterPostProcess), m_RTManager.GetDepthStencilBuffer());

                HDUtils.ResetOverlay();
                float x = 0.0f;
                float overlayRatio = m_CurrentDebugDisplaySettings.data.debugOverlayRatio;
                float overlaySize = Math.Min(hdCamera.actualHeight, hdCamera.actualWidth) * overlayRatio;
                float y = hdCamera.actualHeight - overlaySize;

                var lightingDebug = m_CurrentDebugDisplaySettings.data.lightingDebugSettings;

                if (lightingDebug.displaySkyReflection)
                {
                    var skyReflection = m_SkyManager.skyReflection;
                    m_SharedPropertyBlock.SetTexture(HDShaderIDs._InputCubemap, skyReflection);
                    m_SharedPropertyBlock.SetFloat(HDShaderIDs._Mipmap, lightingDebug.skyReflectionMipmap);
                    m_SharedPropertyBlock.SetFloat(HDShaderIDs._DebugExposure, lightingDebug.debugExposure);
                    cmd.SetViewport(new Rect(x, y, overlaySize, overlaySize));
                    cmd.DrawProcedural(Matrix4x4.identity, m_DebugDisplayLatlong, 0, MeshTopology.Triangles, 3, 1, m_SharedPropertyBlock);
                    HDUtils.NextOverlayCoord(ref x, ref y, overlaySize, overlaySize, hdCamera);
                }

                m_LightLoop.RenderDebugOverlay(hdCamera, cmd, m_CurrentDebugDisplaySettings, ref x, ref y, overlaySize, hdCamera.actualWidth, cullResults, GetRenderTarget(RT.IntermediateAfterPostProcess), m_RTManager);
                DecalSystem.instance.RenderDebugOverlay(hdCamera, cmd, m_CurrentDebugDisplaySettings, ref x, ref y, overlaySize, hdCamera.actualWidth);
            }
        }

        void ClearBuffers(HDCamera hdCamera, CommandBuffer cmd)
        {
            bool msaa = hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA);

            using (new ProfilingSample(cmd, "ClearBuffers", CustomSamplerId.ClearBuffers.GetSampler()))
            {
                // We clear only the depth buffer, no need to clear the various color buffer as we overwrite them.
                // Clear depth/stencil and init buffers
                using (new ProfilingSample(cmd, "Clear Depth/Stencil", CustomSamplerId.ClearDepthStencil.GetSampler()))
                {
                    if (hdCamera.clearDepth)
                    {
                        HDUtils.SetRenderTarget(cmd, hdCamera, msaa ? GetRenderTarget(RT.ColorMSAA) : GetRenderTarget(RT.Color), m_RTManager.GetDepthStencilBuffer(msaa), ClearFlag.Depth);
                        if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA))
                        {
                            HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetDepthTexture(true), m_RTManager.GetDepthStencilBuffer(true), ClearFlag.Color, Color.black);
                        }
                    }
                    m_IsDepthBufferCopyValid = false;
                }

                // Clear the HDR target
                using (new ProfilingSample(cmd, "Clear HDR target", CustomSamplerId.ClearHDRTarget.GetSampler()))
                {
                    if (hdCamera.clearColorMode == HDAdditionalCameraData.ClearColorMode.BackgroundColor ||
                        // If the luxmeter is enabled, the sky isn't rendered so we clear the background color
                        m_DebugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.LuxMeter ||
                        // If we want the sky but the sky don't exist, still clear with background color
                        (hdCamera.clearColorMode == HDAdditionalCameraData.ClearColorMode.Sky && !m_SkyManager.IsVisualSkyValid()) ||
                        // Special handling for Preview we force to clear with background color (i.e black)
                        // Note that the sky use in this case is the last one setup. If there is no scene or game, there is no sky use as reflection in the preview
                        HDUtils.IsRegularPreviewCamera(hdCamera.camera)
                        )
                    {
                        Color clearColor = hdCamera.backgroundColorHDR;

                        // We set the background color to black when the luxmeter is enabled to avoid picking the sky color
                        if (m_DebugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.LuxMeter)
                            clearColor = Color.black;

                        HDUtils.SetRenderTarget(cmd, hdCamera, msaa ? GetRenderTarget(RT.ColorMSAA) : GetRenderTarget(RT.Color), m_RTManager.GetDepthStencilBuffer(msaa), ClearFlag.Color, clearColor);

                    }
                }

                if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.SubsurfaceScattering))
                {
                    using (new ProfilingSample(cmd, "Clear SSS Lighting Buffer", CustomSamplerId.ClearSssLightingBuffer.GetSampler()))
                    {
                        HDUtils.SetRenderTarget(cmd, hdCamera, msaa ? GetRenderTarget(RT.SssDiffuseLightingMSAA) : GetRenderTarget(RT.SssDiffuseLighting), ClearFlag.Color, CoreUtils.clearColorAllBlack);
                    }
                }

                if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.SSR))
                {
                    using (new ProfilingSample(cmd, "Clear SSR Buffers", CustomSamplerId.ClearSsrBuffers.GetSampler()))
                    {
                        // In practice, these textures are sparse (mostly black). Therefore, clearing them is fast (due to CMASK),
                        // and much faster than fully overwriting them from within SSR shaders.
                        // HDUtils.SetRenderTarget(cmd, hdCamera, RTManager.GetRT(RT.SSRDebug), ClearFlag.Color, CoreUtils.clearColorAllBlack);
                        HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.SSRHitPoint), ClearFlag.Color, CoreUtils.clearColorAllBlack);
                        HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.SSRLighting), ClearFlag.Color, CoreUtils.clearColorAllBlack);
                    }
                }

                // We don't need to clear the GBuffers as scene is rewrite and we are suppose to only access valid data (invalid data are tagged with stencil as StencilLightingUsage.NoLighting),
                // This is to save some performance
                if (hdCamera.frameSettings.litShaderMode == LitShaderMode.Deferred)
                {
                    // We still clear in case of debug mode
                    if (m_CurrentDebugDisplaySettings.IsDebugDisplayEnabled())
                    {
                        using (new ProfilingSample(cmd, "Clear GBuffer", CustomSamplerId.ClearGBuffer.GetSampler()))
                        {
                            HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetGBufferRTI(hdCamera.frameSettings), m_RTManager.GetDepthStencilBuffer(), ClearFlag.Color, CoreUtils.clearColorAllBlack);
                        }
                    }
                }
            }
        }

        void RenderPostProcess(CullingResults cullResults, HDCamera hdCamera, RenderTargetIdentifier finalRT, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            // Y-Flip needs to happen during the post process pass only if it's the final pass and is the regular game view
            // SceneView flip is handled by the editor internal code and GameView rendering into render textures should not be flipped in order to respect Unity texture coordinates convention
            bool flipInPostProcesses = HDUtils.PostProcessIsFinalPass() && (hdCamera.flipYMode == HDAdditionalCameraData.FlipYMode.ForceFlipY || hdCamera.isMainGameView);
            RenderTargetIdentifier destination = HDUtils.PostProcessIsFinalPass() ? finalRT : GetRenderTarget(RT.IntermediateAfterPostProcess);

            StartStereoRendering(cmd, renderContext, hdCamera.camera);

            // We render AfterPostProcess objects first into a separate buffer that will be composited in the final post process pass
            RenderAfterPostProcess(cullResults, hdCamera, renderContext, cmd);

            // Post-processes output straight to the backbuffer
            m_PostProcessSystem.Render(
                cmd: cmd,
                camera: hdCamera,
                blueNoise: m_BlueNoise,
                colorBuffer: GetRenderTarget(RT.Color),
                afterPostProcessTexture: GetRenderTarget(RT.AfterPostProcessOffscreen),
                lightingBuffer: null,
                finalRT: destination,
                flipY: flipInPostProcesses
            );

            StopStereoRendering(cmd, renderContext, hdCamera.camera);
        }

        void RenderAfterPostProcess(CullingResults cullResults, HDCamera hdCamera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (!hdCamera.frameSettings.IsEnabled(FrameSettingsField.AfterPostprocess))
                return;

            using (new ProfilingSample(cmd, "After Post-process", CustomSamplerId.AfterPostProcessing.GetSampler()))
            {
                // Here we share GBuffer albedo buffer since it's not needed anymore
                HDUtils.SetRenderTarget(cmd, hdCamera, GetRenderTarget(RT.AfterPostProcessOffscreen), m_RTManager.GetDepthStencilBuffer(), clearFlag: ClearFlag.Color, clearColor: Color.black);

                cmd.SetGlobalInt(HDShaderIDs._OffScreenRendering, 1);
                RenderOpaqueRenderList(cullResults, hdCamera, renderContext, cmd, HDShaderPassNames.s_ForwardOnlyName, 0, HDRenderQueue.k_RenderQueue_AfterPostProcessOpaque);
                // Setup off-screen transparency here
                RenderTransparentRenderList(cullResults, hdCamera, renderContext, cmd, HDShaderPassNames.s_ForwardOnlyName, 0, HDRenderQueue.k_RenderQueue_AfterPostProcessTransparent);
                cmd.SetGlobalInt(HDShaderIDs._OffScreenRendering, 0);
            }
        }

        void StartStereoRendering(CommandBuffer cmd, ScriptableRenderContext renderContext, Camera cam)
        {
            if (cam.stereoEnabled)
            {
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                renderContext.StartMultiEye(cam);
            }
        }

        void StopStereoRendering(CommandBuffer cmd, ScriptableRenderContext renderContext, Camera cam)
        {
            if (cam.stereoEnabled)
            {
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                renderContext.StopMultiEye(cam);
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

        void ResolveMSAADepthNormal(CommandBuffer cmd, HDCamera hdCamera)
        {
            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA))
            {
                using (new ProfilingSample(cmd, "ComputeDepthValues", CustomSamplerId.VolumeUpdate.GetSampler()))
                {
                    HDUtils.SetRenderTarget(cmd, hdCamera, m_RTManager.GetResolveMSAADepthNormalRTI(), GetRenderTarget(RT.DepthStencil));

                    // Set the input textures
                    Shader.SetGlobalTexture(HDShaderIDs._NormalTextureMS, GetRenderTarget(RT.NormalMSAA));
                    Shader.SetGlobalTexture(HDShaderIDs._DepthTextureMS, GetRenderTarget(RT.DepthAsColorMSAA));

                    // Resolve the depth and normal buffers
                    cmd.DrawProcedural(Matrix4x4.identity, m_DepthResolveMaterial, SampleCountToPassIndex(m_MSAASamples), MeshTopology.Triangles, 3, 1);
                }
            }
        }
        void ResolveMSAAColor(CommandBuffer cmd, HDCamera hdCamera, RTHandleSystem.RTHandle msaaTarget, RTHandleSystem.RTHandle simpleTarget)
        {
            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA))
            {
                using (new ProfilingSample(cmd, "ResolveColor", CustomSamplerId.VolumeUpdate.GetSampler()))
                {
                    // Grab the RTIs and set the output render targets
                    HDUtils.SetRenderTarget(cmd, hdCamera, simpleTarget);

                    // Set the input textures
                    m_MSAAPropertyBlock.SetTexture(HDShaderIDs._ColorTextureMS, msaaTarget);

                    // Resolve the depth and normal buffers
                    cmd.DrawProcedural(Matrix4x4.identity, m_ColorResolveMaterial, SampleCountToPassIndex(m_MSAASamples), MeshTopology.Triangles, 3, 1, m_MSAAPropertyBlock);
                }
            }
        }
    }
}
