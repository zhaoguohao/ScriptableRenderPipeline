using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Graphing;

namespace UnityEditor.ShaderGraph
{
    [Title("Utility", "Sub-graph")]
    class SubGraphNode : AbstractMaterialNode
        , IGeneratesBodyCode
        , IOnAssetEnabled
        , IGeneratesFunction
        , IMayRequireNormal
        , IMayRequireTangent
        , IMayRequireBitangent
        , IMayRequireMeshUV
        , IMayRequireScreenPosition
        , IMayRequireViewDirection
        , IMayRequirePosition
        , IMayRequireVertexColor
        , IMayRequireTime
    {
        [SerializeField]
        private string m_SerializedSubGraph = string.Empty;

        [NonSerialized]
        SubGraphAsset m_SubGraph;

        [NonSerialized]
        SubGraphData m_SubGraphData = default;

        [Serializable]
        private class SubGraphHelper
        {
            public SubGraphAsset subGraph;
        }

        [Serializable]
        class SubGraphAssetReference
        {
            public AssetReference subGraph = default;

            public override string ToString()
            {
                return $"subGraph={subGraph}";
            }
        }

        [Serializable]
        class AssetReference
        {
            public long fileID = default;
            public string guid = default;
            public int type = default;

            public override string ToString()
            {
                return $"fileID={fileID}, guid={guid}, type={type}";
            }
        }

        public string subGraphGuid
        {
            get
            {
                var assetReference = JsonUtility.FromJson<SubGraphAssetReference>(m_SerializedSubGraph);
                return assetReference.subGraph.guid;
            }
        }

        void LoadSubGraph()
        {
            if (m_SubGraph == null)
            {
                if (string.IsNullOrEmpty(m_SerializedSubGraph))
                {
                    return;
                }
                
                var helper = new SubGraphHelper();
                EditorJsonUtility.FromJsonOverwrite(m_SerializedSubGraph, helper);
                m_SubGraph = helper.subGraph;
                foreach (var subGraphData in SubGraphDatabase.instance.subGraphs)
                {
                    if (subGraphData.assetGuid == subGraphGuid)
                    {
                        m_SubGraphData = subGraphData;
                        break;
                    }
                }
            }
        }

        public SubGraphData subGraphData
        {
            get
            {
                LoadSubGraph();
                return m_SubGraphData;
            }
        }

        public SubGraphAsset subGraphAsset
        {
            get
            {
                LoadSubGraph();
                return m_SubGraph;
            }
            set
            {
                if (subGraphAsset == value)
                    return;

                var helper = new SubGraphHelper();
                helper.subGraph = value;
                m_SerializedSubGraph = EditorJsonUtility.ToJson(helper, true);
                m_SubGraph = null;
                m_SubGraphData = null;
                UpdateSlots();

                Dirty(ModificationScope.Topological);
            }
        }

        public override bool hasPreview
        {
            get { return subGraphData != null; }
        }

        public override PreviewMode previewMode
        {
            get
            {
                if (subGraphData == null)
                    return PreviewMode.Preview2D;

                return PreviewMode.Preview3D;
            }
        }

        public SubGraphNode()
        {
            name = "Sub Graph";
        }

        public override bool allowedInSubGraph
        {
            get { return true; }
        }


        public void GenerateNodeCode(ShaderGenerator visitor, GraphContext graphContext, GenerationMode generationMode)
        {
            if (subGraphData == null)
                return;
            
            var sb = new ShaderStringBuilder();
            var inputVariableName = $"bindings_{subGraphData.hlslName}_{subGraphData.assetGuid}";
            
            GraphUtil.GenerateSurfaceInputTransferCode(sb, subGraphData.requirements, subGraphData.inputStructName, inputVariableName);
            
            visitor.AddShaderChunk(sb.ToString());

            foreach (var outSlot in subGraphData.outputs)
                visitor.AddShaderChunk(string.Format("{0} {1};", NodeUtils.ConvertConcreteSlotValueTypeToString(precision, outSlot.concreteValueType), GetVariableNameForSlot(outSlot.id)), true);

            var arguments = new List<string>();
            foreach (var prop in subGraphData.inputs)
            {
                var inSlotId = prop.guid.GetHashCode();

                if (prop is TextureShaderProperty)
                    arguments.Add(string.Format("TEXTURE2D_ARGS({0}, sampler{0})", GetSlotValue(inSlotId, generationMode)));
                else if (prop is Texture2DArrayShaderProperty)
                    arguments.Add(string.Format("TEXTURE2D_ARRAY_ARGS({0}, sampler{0})", GetSlotValue(inSlotId, generationMode)));
                else if (prop is Texture3DShaderProperty)
                    arguments.Add(string.Format("TEXTURE3D_ARGS({0}, sampler{0})", GetSlotValue(inSlotId, generationMode)));
                else if (prop is CubemapShaderProperty)
                    arguments.Add(string.Format("TEXTURECUBE_ARGS({0}, sampler{0})", GetSlotValue(inSlotId, generationMode)));
                else
                    arguments.Add(GetSlotValue(inSlotId, generationMode));
            }

            // pass surface inputs through
            arguments.Add(inputVariableName);

            foreach (var outSlot in subGraphData.outputs)
                arguments.Add(GetVariableNameForSlot(outSlot.id));

            visitor.AddShaderChunk(
                string.Format("{0}({1});"
                    , subGraphData.functionName
                    , arguments.Aggregate((current, next) => string.Format("{0}, {1}", current, next))));
        }

        public void OnEnable()
        {
            UpdateSlots();
        }

        public virtual void UpdateSlots()
        {
            var validNames = new List<int>();
            if (subGraphData == null)
            {
                RemoveSlotsNameNotMatching(validNames, true);
                return;
            }

            var props = subGraphData.inputs;
            foreach (var prop in props)
            {
                var propType = prop.propertyType;
                SlotValueType slotType;

                switch (propType)
                {
                    case PropertyType.Color:
                        slotType = SlotValueType.Vector4;
                        break;
                    case PropertyType.Texture2D:
                        slotType = SlotValueType.Texture2D;
                        break;
                    case PropertyType.Texture2DArray:
                        slotType = SlotValueType.Texture2DArray;
                        break;
                    case PropertyType.Texture3D:
                        slotType = SlotValueType.Texture3D;
                        break;
                    case PropertyType.Cubemap:
                        slotType = SlotValueType.Cubemap;
                        break;
                    case PropertyType.Gradient:
                        slotType = SlotValueType.Gradient;
                        break;
                    case PropertyType.Vector1:
                        slotType = SlotValueType.Vector1;
                        break;
                    case PropertyType.Vector2:
                        slotType = SlotValueType.Vector2;
                        break;
                    case PropertyType.Vector3:
                        slotType = SlotValueType.Vector3;
                        break;
                    case PropertyType.Vector4:
                        slotType = SlotValueType.Vector4;
                        break;
                    case PropertyType.Boolean:
                        slotType = SlotValueType.Boolean;
                        break;
                    case PropertyType.Matrix2:
                        slotType = SlotValueType.Matrix2;
                        break;
                    case PropertyType.Matrix3:
                        slotType = SlotValueType.Matrix3;
                        break;
                    case PropertyType.Matrix4:
                        slotType = SlotValueType.Matrix4;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var id = prop.guid.GetHashCode();
                MaterialSlot slot = MaterialSlot.CreateMaterialSlot(slotType, id, prop.displayName, prop.referenceName, SlotType.Input, prop.defaultValue, ShaderStageCapability.All);
                // copy default for texture for niceness
                if (slotType == SlotValueType.Texture2D && propType == PropertyType.Texture2D)
                {
                    var tSlot = slot as Texture2DInputMaterialSlot;
                    var tProp = prop as TextureShaderProperty;
                    if (tSlot != null && tProp != null)
                        tSlot.texture = tProp.value.texture;
                }
                // copy default for texture array for niceness
                else if (slotType == SlotValueType.Texture2DArray && propType == PropertyType.Texture2DArray)
                {
                    var tSlot = slot as Texture2DArrayInputMaterialSlot;
                    var tProp = prop as Texture2DArrayShaderProperty;
                    if (tSlot != null && tProp != null)
                        tSlot.textureArray = tProp.value.textureArray;
                }
                // copy default for texture 3d for niceness
                else if (slotType == SlotValueType.Texture3D && propType == PropertyType.Texture3D)
                {
                    var tSlot = slot as Texture3DInputMaterialSlot;
                    var tProp = prop as Texture3DShaderProperty;
                    if (tSlot != null && tProp != null)
                        tSlot.texture = tProp.value.texture;
                }
                // copy default for cubemap for niceness
                else if (slotType == SlotValueType.Cubemap && propType == PropertyType.Cubemap)
                {
                    var tSlot = slot as CubemapInputMaterialSlot;
                    var tProp = prop as CubemapShaderProperty;
                    if (tSlot != null && tProp != null)
                        tSlot.cubemap = tProp.value.cubemap;
                }
                AddSlot(slot);
                validNames.Add(id);
            }

            var outputStage = subGraphData.effectiveShaderStage;

            foreach (var slot in subGraphData.outputs)
            {
                AddSlot(MaterialSlot.CreateMaterialSlot(slot.valueType, slot.id, slot.RawDisplayName(), 
                    slot.shaderOutputName, SlotType.Output, Vector4.zero, outputStage));
                validNames.Add(slot.id);
            }

            RemoveSlotsNameNotMatching(validNames, true);
        }

        private void ValidateShaderStage()
        {
            List<MaterialSlot> slots = new List<MaterialSlot>();
            GetInputSlots(slots);
            GetOutputSlots(slots);

            var outputStage = subGraphData.effectiveShaderStage;
            foreach(MaterialSlot slot in slots)
                slot.stageCapability = outputStage;
        }

        public override void ValidateNode()
        {
            if (subGraphData == null || !subGraphData.isValid)
            {
                owner.AddValidationError(tempId, "Sub Graph failed to import");
            }

            ValidateShaderStage();

            base.ValidateNode();
        }

        public override void CollectShaderProperties(PropertyCollector visitor, GenerationMode generationMode)
        {
            base.CollectShaderProperties(visitor, generationMode);

            if (subGraphData == null)
                return;

            foreach (var property in subGraphData.properties)
            {
                visitor.AddShaderProperty(property);
            }
        }

        public override void CollectPreviewMaterialProperties(List<PreviewProperty> properties)
        {
            base.CollectPreviewMaterialProperties(properties);
            
            if (subGraphData == null)
                return;

            foreach (var property in subGraphData.properties)
            {
                properties.Add(property.GetPreviewMaterialProperty());
            }
        }

        public virtual void GenerateNodeFunction(FunctionRegistry registry, GraphContext graphContext, GenerationMode generationMode)
        {
            if (subGraphData == null)
                return;

            var database = SubGraphDatabase.instance;
            
            foreach (var functionName in subGraphData.functionNames)
            {
                registry.ProvideFunction(functionName, s =>
                {
                    var functionSource = database.functionSources[database.functionNames.BinarySearch(functionName)];
                    s.AppendLines(functionSource);
                });
            }
        }

        public NeededCoordinateSpace RequiresNormal(ShaderStageCapability stageCapability)
        {
            if (subGraphData == null)
                return NeededCoordinateSpace.None;

            return subGraphData.requirements.requiresNormal;
        }

        public bool RequiresMeshUV(UVChannel channel, ShaderStageCapability stageCapability)
        {
            if (subGraphData == null)
                return false;

            return subGraphData.requirements.requiresMeshUVs.Contains(channel);
        }

        public bool RequiresScreenPosition(ShaderStageCapability stageCapability)
        {
            if (subGraphData == null)
                return false;

            return subGraphData.requirements.requiresScreenPosition;
        }

        public NeededCoordinateSpace RequiresViewDirection(ShaderStageCapability stageCapability)
        {
            if (subGraphData == null)
                return NeededCoordinateSpace.None;

            return subGraphData.requirements.requiresViewDir;
        }

        public NeededCoordinateSpace RequiresPosition(ShaderStageCapability stageCapability)
        {
            if (subGraphData == null)
                return NeededCoordinateSpace.None;

            return subGraphData.requirements.requiresPosition;
        }

        public NeededCoordinateSpace RequiresTangent(ShaderStageCapability stageCapability)
        {
            if (subGraphData == null)
                return NeededCoordinateSpace.None;

            return subGraphData.requirements.requiresTangent;
        }

        public bool RequiresTime()
        {
            if (subGraphData == null)
                return false;

            return subGraphData.requirements.requiresTime;
        }

        public NeededCoordinateSpace RequiresBitangent(ShaderStageCapability stageCapability)
        {
            if (subGraphData == null)
                return NeededCoordinateSpace.None;

            return subGraphData.requirements.requiresBitangent;
        }

        public bool RequiresVertexColor(ShaderStageCapability stageCapability)
        {
            if (subGraphData == null)
                return false;

            return subGraphData.requirements.requiresVertexColor;
        }

        public override void GetSourceAssetDependencies(List<string> paths)
        {
            base.GetSourceAssetDependencies(paths);
            if (subGraphData != null)
            {
                paths.Add(AssetDatabase.GetAssetPath(subGraphAsset));
                foreach (var graphGuid in subGraphData.subGraphGuids)
                {
                    paths.Add(AssetDatabase.GUIDToAssetPath(graphGuid));
                }
            }
        }
    }
}
