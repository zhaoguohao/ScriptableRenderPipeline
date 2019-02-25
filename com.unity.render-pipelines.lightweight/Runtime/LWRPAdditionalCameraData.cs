using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Experimental.Rendering.LWRP;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.LWRP
{
    public enum CameraOverrideOption
    {
        Off,
        On,
        UsePipelineSettings,
    }

    public enum RendererOverrideOption
    {
        Custom,
        UsePipelineSettings,
    }

    public enum LWRPCameraType
    {
        Offscreen,
        Game,
        Overlay,
        UI,
    }


    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    public class LWRPAdditionalCameraData : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Tooltip("If enabled shadows will render for this camera.")]
        [FormerlySerializedAs("renderShadows"), SerializeField]
        bool m_RenderShadows = true;

        [Tooltip("If enabled depth texture will render for this camera bound as _CameraDepthTexture.")]
        [SerializeField]
        CameraOverrideOption m_RequiresDepthTextureOption = CameraOverrideOption.UsePipelineSettings;

        [Tooltip("If enabled opaque color texture will render for this camera and bound as _CameraOpaqueTexture.")]
        [SerializeField]
        CameraOverrideOption m_RequiresOpaqueTextureOption = CameraOverrideOption.UsePipelineSettings;

        [SerializeField]
        RendererOverrideOption m_RendererOverrideOption = RendererOverrideOption.UsePipelineSettings;

        [SerializeField]
        IRendererData m_RendererData = null;

        IRendererSetup m_RendererSetup = null;

        [SerializeField]
        LWRPCameraType m_CameraType = LWRPCameraType.Game;

        [SerializeField]
        List<Camera> m_Cameras = new List<Camera>();

        // Deprecated:
        [FormerlySerializedAs("requiresDepthTexture"), SerializeField]
        bool m_RequiresDepthTexture = false;

        [FormerlySerializedAs("requiresColorTexture"), SerializeField]
        bool m_RequiresColorTexture = false;

        [HideInInspector] [SerializeField] float m_Version = 2;

        public float version => m_Version;

        public bool renderShadows
        {
            get => m_RenderShadows;
            set => m_RenderShadows = value;
        }

        public CameraOverrideOption requiresDepthOption
        {
            get  => m_RequiresDepthTextureOption;
            set => m_RequiresDepthTextureOption = value;
        }

        public CameraOverrideOption requiresColorOption
        {
            get => m_RequiresOpaqueTextureOption;
            set => m_RequiresOpaqueTextureOption = value;
        }

        public LWRPCameraType cameraType
        {
            get => m_CameraType;
            set => m_CameraType = value;
        }

//        public static List<Camera> getCameras
//        {
//            get => m_Cameras;
//        }

        public void AddCamera(Camera camera)
        {
            m_Cameras.Add(camera);
        }

        public bool requiresDepthTexture
        {
            get
            {
                if (m_RequiresDepthTextureOption == CameraOverrideOption.UsePipelineSettings)
                {
                    LightweightRenderPipelineAsset asset = GraphicsSettings.renderPipelineAsset as LightweightRenderPipelineAsset;
                    return asset.supportsCameraDepthTexture;
                }
                else
                {
                    return m_RequiresDepthTextureOption == CameraOverrideOption.On;
                }
            }
            set { m_RequiresDepthTextureOption = (value) ? CameraOverrideOption.On : CameraOverrideOption.Off; }
        }

        public bool requiresColorTexture
        {
            get
            {
                if (m_RequiresOpaqueTextureOption == CameraOverrideOption.UsePipelineSettings)
                {
                    LightweightRenderPipelineAsset asset = GraphicsSettings.renderPipelineAsset as LightweightRenderPipelineAsset;
                    return asset.supportsCameraOpaqueTexture;
                }
                else
                {
                    return m_RequiresOpaqueTextureOption == CameraOverrideOption.On;
                }
            }
            set { m_RequiresOpaqueTextureOption = (value) ? CameraOverrideOption.On : CameraOverrideOption.Off; }
        }

        public IRendererSetup rendererSetup
        {
            get
            {
                if (m_RendererOverrideOption == RendererOverrideOption.UsePipelineSettings || m_RendererData == null)
                    return LightweightRenderPipeline.asset.rendererSetup;

                if (m_RendererSetup == null)
                    m_RendererSetup = m_RendererData.Create();

                return m_RendererSetup;
            }
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (version <= 1)
            {
                m_RequiresDepthTextureOption = (m_RequiresDepthTexture) ? CameraOverrideOption.On : CameraOverrideOption.Off;
                m_RequiresOpaqueTextureOption = (m_RequiresColorTexture) ? CameraOverrideOption.On : CameraOverrideOption.Off;
            }
        }

        public void OnDrawGizmos()
        {
            string gizmoName;
            Color tint = Color.white;
            if (m_CameraType == LWRPCameraType.Game)
            {
                gizmoName = "Camera_Base";
            }
            else if (m_CameraType == LWRPCameraType.Overlay)
            {
                gizmoName = "Camera_Overlay";
            }
            else if (m_CameraType == LWRPCameraType.Offscreen)
            {
                gizmoName = "Camera_Offscreen";
            }
            else
            {
                gizmoName = "Camera_UI";
            }

            if (Selection.activeObject == this.gameObject)
            {
                tint = new Color(1f,0.5f,0,0.5f);
                // Get the preferences selection color
            }
            Gizmos.DrawIcon(transform.position, gizmoName, true, tint);
        }
    }
}
