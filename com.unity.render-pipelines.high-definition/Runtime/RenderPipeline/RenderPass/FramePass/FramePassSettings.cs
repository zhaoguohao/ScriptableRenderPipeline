using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Experimental.Rendering.HDPipeline.MaterialDebugSettings;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public enum MaterialProperty
    {
        All,
        Albedo,
        Normal,
        Smoothness,
        /// <summary>There is no equivalent for AxF shader. It will be rendered black.</summary>
        AmbientOcclusion,
        /// <summary>There is no equivalent for AxF, Fabric and Hair shaders. They will be rendered black.</summary>
        Metal,
        Specular,
        Alpha, //[TODO]

        //[Todo: see for particular properties like aniso...]
    }

    public enum LightingProperty
    {
        All,
        DiffuseOnly,
        DiffuseOnlyDirectional,
        DiffuseOnlyIndirectional,
        SpecularOnly,
        SpecularOnlyDirectional,
        SpecularOnlyIndirectional,
        // ...
    }

    public unsafe struct FramePassSettings
    {
        public static FramePassSettings @default = new FramePassSettings
        {
            materialProperty = MaterialProperty.All,
            lightingProperty = LightingProperty.All
        };

        MaterialProperty materialProperty;
        LightingProperty lightingProperty;
        
        public FramePassSettings(FramePassSettings other)
        {
            InitMaterialPropertyMapIfNeeded();

            materialProperty = other.materialProperty;
            lightingProperty = other.lightingProperty;
        }

        /// <summary>
        /// State the property to render. In case of several SetFullscreenOutput chained call, only last will be used.
        /// </summary>
        /// <param name="mat"></param>
        /// <returns></returns>
        public ref FramePassSettings SetFullscreenOutput(MaterialProperty mat)
        {
            return ref *ThisPtr;
        }

        /// <summary>
        /// State the property to render. In case of several SetFullscreenOutput chained call, only last will be used.
        /// </summary>
        /// <param name="mat"></param>
        /// <returns></returns>
        public ref FramePassSettings SetFullscreenOutput(LightingProperty mat)
        {
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

            List<MaterialItem> materialItems = GetAllMaterialDatas();

            foreach (MaterialItem materialItem in materialItems)
            {
                var attributes = materialItem.surfaceDataType.GetCustomAttributes(true);
                var generateHLSLAttribute = attributes[0] as GenerateHLSL;
                int materialStartIndex = generateHLSLAttribute.paramDefinesStart;

                if (!generateHLSLAttribute.needParamDebug)
                {
                    return;
                }

                var fields = materialItem.surfaceDataType.GetFields();

                int localIndex = 0;
                foreach (var field in fields)
                {
                    if (Attribute.IsDefined(field, typeof(FramePassMaterialMappingAttribute)))
                    {
                        var propertyAttr = (FramePassMaterialMappingAttribute[])field.GetCustomAttributes(typeof(FramePassMaterialMappingAttribute), false);
                        materialPropertyMap[propertyAttr[0].property].Add(materialStartIndex + localIndex++);
                    }
                }

                if (materialItem.bsdfDataType == null)
                    continue;

                attributes = materialItem.bsdfDataType.GetCustomAttributes(true);
                generateHLSLAttribute = attributes[0] as GenerateHLSL;
                materialStartIndex = generateHLSLAttribute.paramDefinesStart;

                if (!generateHLSLAttribute.needParamDebug)
                {
                    return;
                }

                fields = materialItem.surfaceDataType.GetFields();

                localIndex = 0;
                foreach (var field in fields)
                {
                    if (Attribute.IsDefined(field, typeof(FramePassMaterialMappingAttribute)))
                    {
                        var propertyAttr = (FramePassMaterialMappingAttribute[])field.GetCustomAttributes(typeof(FramePassMaterialMappingAttribute), false);
                        materialPropertyMap[propertyAttr[0].property].Add(materialStartIndex + localIndex++);
                    }
                }
            }

            foreach (var key in materialPropertyMap.Keys)
            {
                s_MaterialPropertyMap[key] = materialPropertyMap[key].ToArray();
            }

            s_MaterialPropertyMapInitialized = true;
        }


        internal void FillDebugData(DebugDisplaySettings.DebugData data)
        {
            data.materialDebugSettings.debugViewMaterial = materialProperty == MaterialProperty.All ? new int[0] : s_MaterialPropertyMap[materialProperty];
            //[TODO: Add lighting settings too]
        }
    }

    public class FramePassMaterialMappingAttribute : Attribute
    {
        public readonly MaterialProperty property;

        public FramePassMaterialMappingAttribute(MaterialProperty property)
            => this.property = property;
    }
}
