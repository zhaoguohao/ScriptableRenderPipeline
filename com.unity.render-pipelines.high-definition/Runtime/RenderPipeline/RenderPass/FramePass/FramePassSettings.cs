using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline.Attributes;
using static UnityEngine.Experimental.Rendering.HDPipeline.MaterialDebugSettings;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    [Flags]
    public enum LightingProperty
    {
        DiffuseOnlyDirectional = 1 << 0,
        DiffuseOnlyIndirect = 1 << 1,
        SpecularOnlyDirectional = 1 << 2,
        SpecularOnlyIndirect = 1 << 3
    }
    
    public enum DebugFullScreen
    {
        None,
        Depth,
        ScreanSpaceAmbientOcclusion,
        MotionVectors
    }

    public unsafe struct FramePassSettings
    {
        public static FramePassSettings @default = new FramePassSettings
        {
            materialProperty = MaterialSharedProperty.None,
            lightingProperty = LightingProperty.DiffuseOnlyDirectional | LightingProperty.DiffuseOnlyIndirect | LightingProperty.SpecularOnlyDirectional | LightingProperty.SpecularOnlyIndirect,
            debugFullScreen = DebugFullScreen.None
        };

        MaterialSharedProperty materialProperty;
        LightingProperty lightingProperty;
        DebugFullScreen debugFullScreen;

        public FramePassSettings(FramePassSettings other)
        {
            materialProperty = other.materialProperty;
            lightingProperty = other.lightingProperty;
            debugFullScreen = other.debugFullScreen;
        }

        /// <summary>State the property to render. In case of several SetFullscreenOutput chained call, only last will be used.</summary>
        public ref FramePassSettings SetFullscreenOutput(MaterialSharedProperty materialProperty)
        {
            this.materialProperty = materialProperty;
            return ref *ThisPtr;
        }

        /// <summary>State the property to render. In case of several SetFullscreenOutput chained call, only last will be used.</summary>
        public ref FramePassSettings SetFullscreenOutput(LightingProperty lightingProperty)
        {
            this.lightingProperty = lightingProperty;
            return ref *ThisPtr;
        }

        /// <summary>State the property to render. In case of several SetFullscreenOutput chained call, only last will be used.</summary>
        public ref FramePassSettings SetFullscreenOutput(DebugFullScreen debugFullScreen)
        {
            this.debugFullScreen = debugFullScreen;
            return ref *ThisPtr;
        }

        FramePassSettings* ThisPtr
        {
            get
            {
                fixed (FramePassSettings* pThis = &this)
                    return pThis;
            }
        }
        
        // to development only [TODO: remove this]
        public void TEST(DebugDisplaySettings.DebugData data) => FillDebugData(data);

        // Usage example:
        // (new FramePassSettings(FramePassSettings.@default)).SetFullscreenOutput(prop).FillDebugData((RenderPipelineManager.currentPipeline as HDRenderPipeline).debugDisplaySettings.data);
        internal void FillDebugData(DebugDisplaySettings.DebugData data)
        {
            data.materialDebugSettings.SetDebugViewCommonMaterialProperty(materialProperty);

            if (lightingProperty == (LightingProperty.DiffuseOnlyDirectional | LightingProperty.DiffuseOnlyIndirect | LightingProperty.SpecularOnlyDirectional | LightingProperty.SpecularOnlyIndirect))
                data.lightingDebugSettings.debugLightingMode = DebugLightingMode.None;
            else if (lightingProperty == (LightingProperty.DiffuseOnlyDirectional | LightingProperty.DiffuseOnlyIndirect))
                data.lightingDebugSettings.debugLightingMode = DebugLightingMode.DiffuseLighting;
            else if (lightingProperty == (LightingProperty.SpecularOnlyDirectional | LightingProperty.SpecularOnlyIndirect))
                data.lightingDebugSettings.debugLightingMode = DebugLightingMode.SpecularLighting;
            else
                throw new NotImplementedException();

            switch (debugFullScreen)
            {
                case DebugFullScreen.None:
                    data.fullScreenDebugMode = FullScreenDebugMode.None;
                    break;
                case DebugFullScreen.Depth:
                    data.fullScreenDebugMode = FullScreenDebugMode.DepthPyramid;
                    break;
                case DebugFullScreen.ScreanSpaceAmbientOcclusion:
                    data.fullScreenDebugMode = FullScreenDebugMode.SSAO;
                    break;
                case DebugFullScreen.MotionVectors:
                    data.fullScreenDebugMode = FullScreenDebugMode.MotionVectors;
                    break;
                default:
                    throw new ArgumentException("Unknown DebugFullScreen");
            }
        }
    }
}

