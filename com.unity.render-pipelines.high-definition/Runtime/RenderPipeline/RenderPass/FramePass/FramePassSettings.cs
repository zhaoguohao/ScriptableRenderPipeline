using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Experimental.Rendering.HDPipeline.MaterialDebugSettings;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public enum MaterialProperty
    {
        None,
        Albedo,
        Normal,
        Smoothness,
        /// <summary>There is no equivalent for AxF shader.</summary>
        AmbientOcclusion,
        /// <summary>There is no equivalent for AxF, Fabric and Hair shaders.</summary>
        Metal,
        Specular,
        Alpha,

        //[Todo: see for particular properties like aniso...]
    }

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
            materialProperty = MaterialProperty.None,
            lightingProperty = LightingProperty.DiffuseOnlyDirectional | LightingProperty.DiffuseOnlyIndirect | LightingProperty.SpecularOnlyDirectional | LightingProperty.SpecularOnlyIndirect,
            debugFullScreen = DebugFullScreen.None
        };

        MaterialProperty materialProperty;
        LightingProperty lightingProperty;
        DebugFullScreen debugFullScreen;

        public FramePassSettings(FramePassSettings other)
        {
            InitMaterialPropertyMapIfNeeded();

            materialProperty = other.materialProperty;
            lightingProperty = other.lightingProperty;
            debugFullScreen = other.debugFullScreen;
        }

        /// <summary>State the property to render. In case of several SetFullscreenOutput chained call, only last will be used.</summary>
        public ref FramePassSettings SetFullscreenOutput(MaterialProperty materialProperty)
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


        static bool s_MaterialPropertyMapInitialized = false;
        static Dictionary<MaterialProperty, int[]> s_MaterialPropertyMap = new Dictionary<MaterialProperty, int[]>();

        static void InitMaterialPropertyMapIfNeeded()
        {
            if (s_MaterialPropertyMapInitialized)
                return;

            Dictionary<MaterialProperty, List<int>> materialPropertyMap = new Dictionary<MaterialProperty, List<int>>()
            {
                { MaterialProperty.Albedo, new List<int>() },
                { MaterialProperty.Normal, new List<int>() },
                { MaterialProperty.Smoothness, new List<int>() },
                { MaterialProperty.AmbientOcclusion, new List<int>() },
                { MaterialProperty.Metal, new List<int>() },
                { MaterialProperty.Specular, new List<int>() },
                { MaterialProperty.Alpha, new List<int>() },
            };

            // builtins parameters
            Type builtin = typeof(Builtin.BuiltinData);
            var attributes = builtin.GetCustomAttributes(true);
            var generateHLSLAttribute = attributes[0] as GenerateHLSL;
            int materialStartIndex = generateHLSLAttribute.paramDefinesStart;

            int localIndex = 0;
            foreach (var field in typeof(Builtin.BuiltinData).GetFields())
            {
                if (Attribute.IsDefined(field, typeof(FramePassMaterialMappingAttribute)))
                {
                    var propertyAttr = (FramePassMaterialMappingAttribute[])field.GetCustomAttributes(typeof(FramePassMaterialMappingAttribute), false);
                    materialPropertyMap[propertyAttr[0].property].Add(materialStartIndex + localIndex);
                }
                var surfaceAttributes = (SurfaceDataAttributes[])field.GetCustomAttributes(typeof(SurfaceDataAttributes), false);
                if (surfaceAttributes.Length > 0)
                    localIndex += surfaceAttributes[0].displayNames.Length;
            }

            // specific shader parameters
            List<MaterialItem> materialItems = GetAllMaterialDatas();

            foreach (MaterialItem materialItem in materialItems)
            {
                attributes = materialItem.surfaceDataType.GetCustomAttributes(true);
                generateHLSLAttribute = attributes[0] as GenerateHLSL;
                materialStartIndex = generateHLSLAttribute.paramDefinesStart;

                if (!generateHLSLAttribute.needParamDebug)
                    continue;

                var fields = materialItem.surfaceDataType.GetFields();

                localIndex = 0;
                foreach (var field in fields)
                {
                    if (Attribute.IsDefined(field, typeof(FramePassMaterialMappingAttribute)))
                    {
                        var propertyAttr = (FramePassMaterialMappingAttribute[])field.GetCustomAttributes(typeof(FramePassMaterialMappingAttribute), false);
                        materialPropertyMap[propertyAttr[0].property].Add(materialStartIndex + localIndex);
                    }
                    var surfaceAttributes = (SurfaceDataAttributes[])field.GetCustomAttributes(typeof(SurfaceDataAttributes), false);
                    if (surfaceAttributes.Length > 0)
                        localIndex += surfaceAttributes[0].displayNames.Length;
                }

                if (materialItem.bsdfDataType == null)
                    continue;

                attributes = materialItem.bsdfDataType.GetCustomAttributes(true);
                generateHLSLAttribute = attributes[0] as GenerateHLSL;
                materialStartIndex = generateHLSLAttribute.paramDefinesStart;

                if (!generateHLSLAttribute.needParamDebug)
                    continue;

                fields = materialItem.bsdfDataType.GetFields();

                localIndex = 0;
                foreach (var field in fields)
                {
                    if (Attribute.IsDefined(field, typeof(FramePassMaterialMappingAttribute)))
                    {
                        var propertyAttr = (FramePassMaterialMappingAttribute[])field.GetCustomAttributes(typeof(FramePassMaterialMappingAttribute), false);
                        materialPropertyMap[propertyAttr[0].property].Add(materialStartIndex + localIndex++);
                    }
                    var surfaceAttributes = (SurfaceDataAttributes[])field.GetCustomAttributes(typeof(SurfaceDataAttributes), false);
                    if (surfaceAttributes.Length > 0)
                        localIndex += surfaceAttributes[0].displayNames.Length;
                }
            }

            foreach (var key in materialPropertyMap.Keys)
            {
                s_MaterialPropertyMap[key] = materialPropertyMap[key].ToArray();
            }

            s_MaterialPropertyMapInitialized = true;
        }

        // to development only [TODO: remove this]
        public void TEST(DebugDisplaySettings.DebugData data) => FillDebugData(data);

        // Usage example:
        // (new FramePassSettings(FramePassSettings.@default)).SetFullscreenOutput(prop).FillDebugData((RenderPipelineManager.currentPipeline as HDRenderPipeline).debugDisplaySettings.data);
        internal void FillDebugData(DebugDisplaySettings.DebugData data)
        {
            data.materialDebugSettings.debugViewMaterial = materialProperty == MaterialProperty.None ? new int[0] : s_MaterialPropertyMap[materialProperty];

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

    public class FramePassMaterialMappingAttribute : Attribute
    {
        public readonly MaterialProperty property;

        public FramePassMaterialMappingAttribute(MaterialProperty property)
            => this.property = property;
    }
}

