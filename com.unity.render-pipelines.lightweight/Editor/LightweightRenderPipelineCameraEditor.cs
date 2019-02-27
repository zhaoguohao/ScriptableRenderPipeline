using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AnimatedValues;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;
using UnityEngine.Rendering.PostProcessing;

namespace UnityEditor.Rendering.LWRP
{
    [CustomEditorForRenderPipeline(typeof(Camera), typeof(LightweightRenderPipelineAsset))]
    [CanEditMultipleObjects]
    class LightweightRenderPipelineCameraEditor : CameraEditor
    {
        internal enum BackgroundType
        {
            Skybox = 0,
            SolidColor,
            DontCare,
        }

        internal class Styles
        {
            public static GUIContent backgroundType = EditorGUIUtility.TrTextContent("Background Type", "Controls how to initialize the Camera's background.\n\nSkybox initializes camera with Skybox, defaulting to a background color if no skybox is found.\n\nSolid Color initializes background with the background color.\n\nDon't care have undefined values for camera background. Use this only if you are rendering all pixels in the Camera's view.");
            public static GUIContent cameraType = EditorGUIUtility.TrTextContent("Camera Type", "Controls which type of camera this is.");
            public static GUIContent renderingShadows = EditorGUIUtility.TrTextContent("Render Shadows", "Enable this to make this camera render shadows.");
            public static GUIContent requireDepthTexture = EditorGUIUtility.TrTextContent("Depth Texture", "On makes this camera create a _CameraDepthTexture, which is a copy of the rendered depth values.\nOff makes the camera not create a depth texture.\nUse Pipeline Settings applies settings from the Render Pipeline Asset.");
            public static GUIContent requireOpaqueTexture = EditorGUIUtility.TrTextContent("Opaque Texture", "On makes this camera create a _CameraOpaqueTexture, which is a copy of the rendered view.\nOff makes the camera does not create an opaque texture.\nUse Pipeline Settings applies settings from the Render Pipeline Asset.");
            public static GUIContent allowMSAA = EditorGUIUtility.TrTextContent("MSAA", "Use Multi Sample Anti-Aliasing to reduce aliasing.");
            public static GUIContent allowHDR = EditorGUIUtility.TrTextContent("HDR", "High Dynamic Range gives you a wider range of light intensities, so your lighting looks more realistic. With it, you can still see details and experience less saturation even with bright light.", (Texture) null);

            public static GUIContent rendererType = EditorGUIUtility.TrTextContent("Renderer Type", "Controls which renderer this camera uses.");
            public static GUIContent rendererData = EditorGUIUtility.TrTextContent("Renderer Data", "Required by a custom Renderer. If none is assigned this camera uses the one assigned in the Pipeline Settings.");

            public readonly GUIContent[] renderingPathOptions = { EditorGUIUtility.TrTextContent("Forward") };
            public readonly string hdrDisabledWarning = "HDR rendering is disabled in the Lightweight Render Pipeline asset.";
            public readonly string mssaDisabledWarning = "Anti-aliasing is disabled in the Lightweight Render Pipeline asset.";

            public static GUIContent[] displayedRendererTypeOverride =
            {
                new GUIContent("Custom"),
                new GUIContent("Use Pipeline Settings"),
            };

            public static int[] rendererTypeOptions = Enum.GetValues(typeof(RendererOverrideOption)) as int[];
            public static GUIContent[] cameraBackgroundType =
            {
                new GUIContent("Skybox"),
                new GUIContent("Solid Color"),
                new GUIContent("Don't Care"),
            };

            public static int[] cameraBackgroundValues = { 0, 1, 2};

            // This is for adding more data like Pipeline Asset option
            public static GUIContent[] displayedAdditionalDataOptions =
            {
                new GUIContent("Off"),
                new GUIContent("On"),
                new GUIContent("Use Pipeline Settings"),
            };

            public static GUIContent[] displayedDepthTextureOverride =
            {
                new GUIContent("On (Forced due to Post Processing)"),
            };

            public static int[] additionalDataOptions = Enum.GetValues(typeof(CameraOverrideOption)) as int[];

            // Using the pipeline Settings
            public static GUIContent[] displayedCameraOptions =
            {
                new GUIContent("Off"),
                new GUIContent("Use Pipeline Settings"),
            };
            public static int[] cameraOptions = { 0, 1 };

            // Camera Types
            public static List<GUIContent> m_CameraTypeNames = null;
            public static readonly string[] cameraTypeNames = Enum.GetNames(typeof(LWRPCameraType));
            public static int[] additionalDataCameraTypeOptions = Enum.GetValues(typeof(LWRPCameraType)) as int[];
        };


        ReorderableList m_LayerList;

        public Camera camera { get { return target as Camera; } }

        static List<Camera> k_Cameras;
        List<Camera> validCameras = new List<Camera>();
        // Animation Properties
        public bool isSameClearFlags { get { return !settings.clearFlags.hasMultipleDifferentValues; } }
        public bool isSameOrthographic { get { return !settings.orthographic.hasMultipleDifferentValues; } }

        static readonly int[] s_RenderingPathValues = {0};
        static Styles s_Styles;
        LightweightRenderPipelineAsset m_LightweightRenderPipeline;
        LWRPAdditionalCameraData m_AdditionalCameraData;
        SerializedObject m_AdditionalCameraDataSO;

        readonly AnimBool m_ShowBGColorAnim = new AnimBool();
        readonly AnimBool m_ShowOrthoAnim = new AnimBool();
        readonly AnimBool m_ShowTargetEyeAnim = new AnimBool();

        SerializedProperty m_AdditionalCameraDataRenderShadowsProp;
        SerializedProperty m_AdditionalCameraDataRenderDepthProp;
        SerializedProperty m_AdditionalCameraDataRenderOpaqueProp;
        SerializedProperty m_AdditionalCameraDataRendererProp;
        SerializedProperty m_AdditionalCameraDataRendererDataProp;
        SerializedProperty m_AdditionalCameraDataCameraTypeProp;

        SerializedProperty m_AdditionalCameraDataCameras;

        void SetAnimationTarget(AnimBool anim, bool initialize, bool targetValue)
        {
            if (initialize)
            {
                anim.value = targetValue;
                anim.valueChanged.AddListener(Repaint);
            }
            else
            {
                anim.target = targetValue;
            }
        }

        void UpdateAnimationValues(bool initialize)
        {
            SetAnimationTarget(m_ShowBGColorAnim, initialize, isSameClearFlags && (camera.clearFlags == CameraClearFlags.SolidColor || camera.clearFlags == CameraClearFlags.Skybox));
            SetAnimationTarget(m_ShowOrthoAnim, initialize, isSameOrthographic && camera.orthographic);
            SetAnimationTarget(m_ShowTargetEyeAnim, initialize, settings.targetEye.intValue != (int)StereoTargetEyeMask.Both || PlayerSettings.virtualRealitySupported);
        }

        void UpdateCameraTypeIntPopupData()
        {
            if (Styles.m_CameraTypeNames == null)
            {
                Styles.m_CameraTypeNames = new List<GUIContent>();
                foreach (string typeName in Styles.cameraTypeNames)
                {
                    Styles.m_CameraTypeNames.Add(new GUIContent(typeName));
                }
            }
        }

        public new void OnEnable()
        {
            m_LightweightRenderPipeline = GraphicsSettings.renderPipelineAsset as LightweightRenderPipelineAsset;
            settings.OnEnable();

            // Additional Camera Data
            m_AdditionalCameraData = camera.gameObject.GetComponent<LWRPAdditionalCameraData>();
            if (m_AdditionalCameraData == null)
            {
                m_AdditionalCameraData = camera.gameObject.AddComponent<LWRPAdditionalCameraData>();
            }
            init(m_AdditionalCameraData);

            UpdateAnimationValues(true);
            UpdateCameraTypeIntPopupData();
            UpdateCameras();
        }

        void UpdateCameras()
        {
            var o = new PropertyFetcher<LWRPAdditionalCameraData>(m_AdditionalCameraDataSO);
            m_AdditionalCameraDataCameras = o.Find(x => x.cameras);

            var camType = (LWRPCameraType)m_AdditionalCameraDataCameraTypeProp.intValue;
            if (camType == LWRPCameraType.Game)
            {
                m_LayerList = new ReorderableList(m_AdditionalCameraDataSO, m_AdditionalCameraDataCameras, true, true, true, true);
                m_LayerList.drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "Muppets"); };
                m_LayerList.drawElementCallback += DrawElementCallback;

                m_LayerList.onRemoveCallback = list =>
                {
                    m_AdditionalCameraDataCameras.DeleteArrayElementAtIndex(list.index);
                    ReorderableList.defaultBehaviours.DoRemoveButton(list);
                    m_AdditionalCameraDataSO.ApplyModifiedProperties();
                };

                m_LayerList.onAddDropdownCallback = (rect, list) => AddCameraToCameraList(rect, list);
            }
        }

        private void DrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            rect.height = EditorGUIUtility.singleLineHeight;
            rect.y += 1;

            var element = m_AdditionalCameraDataCameras.GetArrayElementAtIndex(index);

            var cam = element.objectReferenceValue as Camera;
            if (cam != null)
            {
                var type = cam.gameObject.GetComponent<LWRPAdditionalCameraData>().cameraType;

                EditorGUI.TextField(rect, cam.name, type.ToString());
            }
        }

        void AddCameraToCameraList(Rect rect, ReorderableList list)
        {
            Camera[] allCameras = new Camera[Camera.allCamerasCount];
            Camera.GetAllCameras(allCameras);
            foreach (var camera in allCameras)
            {
                if (camera.gameObject.GetComponent<LWRPAdditionalCameraData>().cameraType == LWRPCameraType.Overlay)
                {
                    validCameras.Add(camera);
                }
            }

            var names = new GUIContent[validCameras.Count];

            for(int i = 0; i < validCameras.Count; ++i)
            {
                names[i] = new GUIContent( validCameras[i].name );
            }

            EditorUtility.DisplayCustomMenu(rect, names, -1, AddCameraToCameraListMenuSelected, null);
        }

        void AddCameraToCameraListMenuSelected(object userData, string[] options, int selected)
        {
            var length = m_AdditionalCameraDataCameras.arraySize;
            ++m_AdditionalCameraDataCameras.arraySize;
            m_AdditionalCameraDataCameras.serializedObject.ApplyModifiedProperties();
            m_AdditionalCameraDataCameras.GetArrayElementAtIndex(length).objectReferenceValue = validCameras[selected];
            m_AdditionalCameraDataCameras.serializedObject.ApplyModifiedProperties();
        }

        void init(LWRPAdditionalCameraData additionalCameraData)
        {
            m_AdditionalCameraDataSO = new SerializedObject(additionalCameraData);
            m_AdditionalCameraDataRenderShadowsProp = m_AdditionalCameraDataSO.FindProperty("m_RenderShadows");
            m_AdditionalCameraDataRenderDepthProp = m_AdditionalCameraDataSO.FindProperty("m_RequiresDepthTextureOption");
            m_AdditionalCameraDataRenderOpaqueProp = m_AdditionalCameraDataSO.FindProperty("m_RequiresOpaqueTextureOption");
            m_AdditionalCameraDataRendererProp = m_AdditionalCameraDataSO.FindProperty("m_RendererOverrideOption");
            m_AdditionalCameraDataRendererDataProp = m_AdditionalCameraDataSO.FindProperty("m_RendererData");
            m_AdditionalCameraDataCameraTypeProp = m_AdditionalCameraDataSO.FindProperty("m_CameraType");

            m_AdditionalCameraDataCameras = m_AdditionalCameraDataSO.FindProperty("m_Cameras");
        }

        public void OnDisable()
        {
            m_ShowBGColorAnim.valueChanged.RemoveListener(Repaint);
            m_ShowOrthoAnim.valueChanged.RemoveListener(Repaint);
            m_ShowTargetEyeAnim.valueChanged.RemoveListener(Repaint);

            m_LightweightRenderPipeline = null;
        }

        public override void OnInspectorGUI()
        {
            if (s_Styles == null)
                s_Styles = new Styles();

            settings.Update();
            UpdateAnimationValues(false);

            DrawCameraType();
            EditorGUILayout.Space();

            var camType = (LWRPCameraType)m_AdditionalCameraDataCameraTypeProp.intValue;
            // Offscreen Camera
            if (camType == LWRPCameraType.Offscreen)
            {
                DrawClearFlags();
                // Will split this and make a small warning that you do not have a sky box material assigned
                using (var group = new EditorGUILayout.FadeGroupScope(m_ShowBGColorAnim.faded))
                    if (group.visible)
                        settings.DrawBackgroundColor();

                settings.DrawCullingMask();

                settings.DrawProjection();
                settings.DrawClippingPlanes();

                DrawTargetTexture();
                settings.DrawOcclusionCulling();

                settings.DrawDynamicResolution();

                DrawRendererType();
                DrawDepthTexture();
            }

            // Game Camera
            if (camType == LWRPCameraType.Game)
            {

                m_LayerList.DoLayoutList();
                m_AdditionalCameraDataSO.ApplyModifiedProperties();

                DrawClearFlags();
                using (var group = new EditorGUILayout.FadeGroupScope(m_ShowBGColorAnim.faded))
                    if (group.visible)
                        settings.DrawBackgroundColor();
                settings.DrawCullingMask();

                settings.DrawProjection();
                settings.DrawClippingPlanes();

                settings.DrawNormalizedViewPort();
                settings.DrawDepth();

                settings.DrawOcclusionCulling();
                DrawHDR();
                DrawMSAA();

                settings.DrawDynamicResolution();

                // Maybe this one needs to be split.
                DrawRendererType();
                DrawDepthTexture();
                DrawOpaqueTexture();
                DrawRenderShadows();

                DrawVRSettings();

                settings.DrawMultiDisplay();
            }

            // Overlay Camera
            if (camType == LWRPCameraType.Overlay)
            {
                settings.DrawCullingMask();

                settings.DrawProjection();
                settings.DrawClippingPlanes();

                settings.DrawOcclusionCulling();
            }
            // UI Camera
            if (camType == LWRPCameraType.UI)
            {
                settings.DrawCullingMask();
                settings.DrawProjection();
                settings.DrawClippingPlanes();
            }

        settings.ApplyModifiedProperties();
        }

        BackgroundType GetBackgroundType(CameraClearFlags clearFlags)
        {
            switch (clearFlags)
            {
                case CameraClearFlags.Skybox:
                    return BackgroundType.Skybox;
                case CameraClearFlags.Nothing:
                    return BackgroundType.DontCare;

                // DepthOnly is not supported by design in LWRP. We upgrade it to SolidColor
                default:
                    return BackgroundType.SolidColor;
            }
        }

        void DrawCameraType()
        {
            LWRPCameraType selectedCameraType;
            selectedCameraType = (LWRPCameraType)m_AdditionalCameraDataCameraTypeProp.intValue;

            EditorGUI.BeginChangeCheck();
            int selCameraType = EditorGUILayout.IntPopup(Styles.cameraType, (int)selectedCameraType, Styles.m_CameraTypeNames.ToArray(), Styles.additionalDataCameraTypeOptions);
            if (EditorGUI.EndChangeCheck())
            {
                m_AdditionalCameraDataCameraTypeProp.intValue = selCameraType;
                m_AdditionalCameraDataSO.ApplyModifiedProperties();
                UpdateCameras();
            }
        }

        void DrawClearFlags()
        {
            // Converts between ClearFlags and Background Type.
            BackgroundType backgroundType = GetBackgroundType((CameraClearFlags) settings.clearFlags.intValue);

            EditorGUI.BeginChangeCheck();
            BackgroundType selectedType = (BackgroundType)EditorGUILayout.IntPopup(Styles.backgroundType, (int)backgroundType,
                Styles.cameraBackgroundType, Styles.cameraBackgroundValues);

            if (EditorGUI.EndChangeCheck())
            {
                CameraClearFlags selectedClearFlags;
                switch (selectedType)
                {
                    case BackgroundType.Skybox:
                        selectedClearFlags = CameraClearFlags.Skybox;
                        break;

                    case BackgroundType.DontCare:
                        selectedClearFlags = CameraClearFlags.Nothing;
                        break;

                    default:
                        selectedClearFlags = CameraClearFlags.SolidColor;
                        break;
                }

                settings.clearFlags.intValue = (int) selectedClearFlags;
            }
        }

        void DrawHDR()
        {
            Rect controlRect = EditorGUILayout.GetControlRect(true);
            EditorGUI.BeginProperty(controlRect, Styles.allowHDR, settings.HDR);
            int selectedValue = !settings.HDR.boolValue ? 0 : 1;
            settings.HDR.boolValue = EditorGUI.IntPopup(controlRect, Styles.allowHDR, selectedValue, Styles.displayedCameraOptions, Styles.cameraOptions) == 1;
            EditorGUI.EndProperty();
        }

        void DrawMSAA()
        {
            Rect controlRect = EditorGUILayout.GetControlRect(true);
            EditorGUI.BeginProperty(controlRect, Styles.allowMSAA, settings.allowMSAA);
            int selectedValue = !settings.allowMSAA.boolValue ? 0 : 1;
            settings.allowMSAA.boolValue = EditorGUI.IntPopup(controlRect, Styles.allowMSAA, selectedValue, Styles.displayedCameraOptions, Styles.cameraOptions) == 1;
            EditorGUI.EndProperty();
        }

        void DrawTargetTexture()
        {
            EditorGUILayout.PropertyField(settings.targetTexture);

            if (!settings.targetTexture.hasMultipleDifferentValues)
            {
                var texture = settings.targetTexture.objectReferenceValue as RenderTexture;
                int pipelineSamplesCount = m_LightweightRenderPipeline.msaaSampleCount;

                if (texture && texture.antiAliasing > pipelineSamplesCount)
                {
                    string pipelineMSAACaps = (pipelineSamplesCount > 1)
                        ? String.Format("is set to support {0}x", pipelineSamplesCount)
                        : "has MSAA disabled";
                    EditorGUILayout.HelpBox(String.Format("Camera target texture requires {0}x MSAA. Lightweight pipeline {1}.", texture.antiAliasing, pipelineMSAACaps),
                        MessageType.Warning, true);
                }
            }
        }

        void DrawRendererType()
        {
            RendererOverrideOption selectedRendererOption;
            m_AdditionalCameraDataSO.Update();
            selectedRendererOption = (RendererOverrideOption) m_AdditionalCameraDataRendererProp.intValue;

            Rect controlRectRendererType = EditorGUILayout.GetControlRect(true);

            EditorGUI.BeginProperty(controlRectRendererType, Styles.rendererType, m_AdditionalCameraDataRendererProp);
            EditorGUI.BeginChangeCheck();
            selectedRendererOption = (RendererOverrideOption)EditorGUI.IntPopup(controlRectRendererType, Styles.rendererType, (int)selectedRendererOption, Styles.displayedRendererTypeOverride, Styles.rendererTypeOptions);
            if (EditorGUI.EndChangeCheck())
            {
                m_AdditionalCameraDataRendererProp.intValue = (int)selectedRendererOption;
                m_AdditionalCameraDataSO.ApplyModifiedProperties();
            }
            EditorGUI.EndProperty();
        }

        void DrawDepthTexture()
        {
            CameraOverrideOption selectedDepthOption;
            m_AdditionalCameraDataSO.Update();
            selectedDepthOption = (CameraOverrideOption)m_AdditionalCameraDataRenderDepthProp.intValue;
            Rect controlRectDepth = EditorGUILayout.GetControlRect(true);
            // Need to check if post processing is added and active.
            // If it is we will set the int pop to be 1 which is ON and gray it out
            bool defaultDrawOfDepthTextureUI = true;
            PostProcessLayer ppl = camera.GetComponent<PostProcessLayer>();
            var propValue = (int)selectedDepthOption;
            if (ppl != null && ppl.isActiveAndEnabled)
            {
                if ((propValue == 2 && !m_LightweightRenderPipeline.supportsCameraDepthTexture) || propValue == 0)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.IntPopup(controlRectDepth, Styles.requireDepthTexture, 0, Styles.displayedDepthTextureOverride, Styles.additionalDataOptions);
                    EditorGUI.EndDisabledGroup();
                    defaultDrawOfDepthTextureUI = false;
                }
            }
            if(defaultDrawOfDepthTextureUI)
            {
                EditorGUI.BeginProperty(controlRectDepth, Styles.requireDepthTexture, m_AdditionalCameraDataRenderDepthProp);
                EditorGUI.BeginChangeCheck();

                selectedDepthOption = (CameraOverrideOption)EditorGUI.IntPopup(controlRectDepth, Styles.requireDepthTexture, (int)selectedDepthOption, Styles.displayedAdditionalDataOptions, Styles.additionalDataOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    m_AdditionalCameraDataRenderDepthProp.intValue = (int)selectedDepthOption;
                    m_AdditionalCameraDataSO.ApplyModifiedProperties();
                }
                EditorGUI.EndProperty();
            }
        }

        void DrawOpaqueTexture()
        {
            CameraOverrideOption selectedOpaqueOption;
            m_AdditionalCameraDataSO.Update();
            selectedOpaqueOption =(CameraOverrideOption)m_AdditionalCameraDataRenderOpaqueProp.intValue;

            Rect controlRectColor = EditorGUILayout.GetControlRect(true);

            EditorGUI.BeginProperty(controlRectColor, Styles.requireOpaqueTexture, m_AdditionalCameraDataRenderOpaqueProp);
            EditorGUI.BeginChangeCheck();
            selectedOpaqueOption = (CameraOverrideOption)EditorGUI.IntPopup(controlRectColor, Styles.requireOpaqueTexture, (int)selectedOpaqueOption, Styles.displayedAdditionalDataOptions, Styles.additionalDataOptions);
            if (EditorGUI.EndChangeCheck())
            {
                m_AdditionalCameraDataRenderOpaqueProp.intValue = (int)selectedOpaqueOption;
                m_AdditionalCameraDataSO.ApplyModifiedProperties();
            }
            EditorGUI.EndProperty();
        }

        void DrawRenderShadows()
        {
            bool selectedValueShadows;
            m_AdditionalCameraDataSO.Update();
            selectedValueShadows = m_AdditionalCameraData.renderShadows;

            Rect controlRectShadows = EditorGUILayout.GetControlRect(true);

            EditorGUI.BeginProperty(controlRectShadows, Styles.renderingShadows, m_AdditionalCameraDataRenderShadowsProp);
            EditorGUI.BeginChangeCheck();

            selectedValueShadows = EditorGUI.Toggle(controlRectShadows, Styles.renderingShadows, selectedValueShadows);
            if (EditorGUI.EndChangeCheck())
            {
                m_AdditionalCameraDataRenderShadowsProp.boolValue = selectedValueShadows;
                m_AdditionalCameraDataSO.ApplyModifiedProperties();
            }
            EditorGUI.EndProperty();
        }

        void DrawVRSettings()
        {
            settings.DrawVR();
            using (var group = new EditorGUILayout.FadeGroupScope(m_ShowTargetEyeAnim.faded))
                if (group.visible)
                    settings.DrawTargetEye();
        }
    }
}
