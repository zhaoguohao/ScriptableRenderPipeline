using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Graphing;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    // Currently most of Shader Graph relies on AbstractMaterialNode as an abstraction, so it's a bit of a mouthful to
    // remove it just like that. Therefore we have this class that represents an IShaderNode as a AbstractMaterialNode.
    [Serializable]
    sealed class ProxyShaderNode : AbstractMaterialNode, IGeneratesBodyCode, IGeneratesFunction
    {
        [NonSerialized]
        object m_Data;

        [SerializeField]
        SerializationHelper.JSONSerializedElement m_SerializedData;

        [SerializeField]
        string m_ShaderNodeTypeName;

        NodeTypeState m_TypeState;

        public HlslFunctionDescriptor function { get; set; }

        public NodeTypeState typeState
        {
            get => m_TypeState;
            set
            {
                m_TypeState = value;
                m_ShaderNodeTypeName = value?.baseNodeType.GetType().FullName;
            }
        }

        public bool isNew { get; set; }

        public object data
        {
            get => m_Data;
            set => m_Data = value;
        }

        public string shaderNodeTypeName => m_ShaderNodeTypeName;

        public override bool hasPreview => true;

        public ProxyShaderNode()
        {
        }

        // This one is only really used in SearchWindowProvider, as we need a dummy node with slots for the code there.
        // Eventually we can make the code in SWP nicer, and remove this constructor.
        public ProxyShaderNode(NodeTypeState typeState)
        {
            this.typeState = typeState;
            name = typeState.type.name;
            isNew = true;

            UpdateSlots();
        }

        public override void ValidateNode()
        {
            base.ValidateNode();

            var errorDetected = true;
            if (owner == null)
            {
                Debug.LogError($"{name} ({guid}) has a null owner.");
            }
            else if (typeState == null)
            {
                Debug.LogError($"{name} ({guid}) has a null state.");
            }
            else if (typeState.owner != owner)
            {
                Debug.LogError($"{name} ({guid}) has an invalid state.");
            }
            else
            {
                errorDetected = false;
            }

            hasError |= errorDetected;
        }

        public override void OnBeforeSerialize()
        {
            base.OnBeforeSerialize();
            if (data != null)
            {
                m_SerializedData = SerializationHelper.Serialize(data);
            }

            if (typeState != null)
            {
                m_ShaderNodeTypeName = typeState.baseNodeType.GetType().FullName;
            }
        }

        public override void UpdateNodeAfterDeserialization()
        {
            if (m_SerializedData.typeInfo.IsValid())
            {
                m_Data = SerializationHelper.Deserialize<object>(m_SerializedData, GraphUtil.GetLegacyTypeRemapping());
                m_SerializedData = default;
            }

            UpdateStateReference();
        }

        public void UpdateStateReference()
        {
            var materialOwner = (AbstractMaterialGraph)owner;
            typeState = materialOwner.nodeTypeStates.FirstOrDefault(x => x.baseNodeType.GetType().FullName == shaderNodeTypeName);
            if (typeState == null)
            {
                throw new InvalidOperationException($"Cannot find an {nameof(ShaderNodeType)} with type name {shaderNodeTypeName}");
            }
            UpdateSlots();
        }

        public void InstantiateControls(NodeRef nodeRef, List<ControlRef> createdControls)
        {
            foreach(ControlDescriptor control in typeState.controlDescs)
            {
                // TODO: this is a hack
                // really, we should use pass the interface down to whoever really builds the control
                if (control.controlType.GetType() == typeof(NodeSliderControlType))
                {
                    NodeSliderControlType sliderControl = control.controlType as NodeSliderControlType;
                    var controlState = new ControlState
                    {
                        nodeId = nodeRef.node.tempId,
                        label = control.displayName,
                        value = sliderControl.defaultValue,
                        controlId = control.id
                    };

                    var controlRef = new ControlRef(typeState.controls.Count);
                    typeState.controls.Add(controlState);
                    createdControls.Add(controlRef);
                }
            }
        }

        void UpdateSlots()
        {
            var validSlotIds = new List<int>();

            // TODO: Properly handle shaderOutputName (i.e.
            foreach (InputPort iport in typeState.type.inputs)
            {
                // find matching port in registered values
                var port = typeState.inputPorts[iport.inputPortRef.index];
                var displayName = $"{NodeUtils.GetHLSLSafeName(port.displayName)}{port.id}";
                switch (port.value.type)
                {
                    case PortValueType.Vector1:
                        AddSlot(new Vector1MaterialSlot(port.id, port.displayName, displayName, SlotType.Input, port.value.vector1Value));
                        break;
                    case PortValueType.Vector2:
                        AddSlot(new Vector2MaterialSlot(port.id, port.displayName, displayName, SlotType.Input, port.value.vector2Value));
                        break;
                    case PortValueType.Vector3:
                        AddSlot(new Vector3MaterialSlot(port.id, port.displayName, displayName, SlotType.Input, port.value.vector3Value));
                        break;
                    case PortValueType.Vector4:
                        AddSlot(new Vector4MaterialSlot(port.id, port.displayName, displayName, SlotType.Input, port.value.vector4Value));
                        break;
                    case PortValueType.DynamicVector:
                        AddSlot(new DynamicVectorMaterialSlot(port.id, port.displayName, displayName, SlotType.Input, port.value.vector4Value));
                        break;
                    case PortValueType.Matrix2x2:
                        AddSlot(new Matrix2MaterialSlot(port.id, port.displayName, displayName, SlotType.Input));
                        break;
                    case PortValueType.Matrix3x3:
                        AddSlot(new Matrix3MaterialSlot(port.id, port.displayName, displayName, SlotType.Input));
                        break;
                    case PortValueType.Matrix4x4:
                        AddSlot(new Matrix4MaterialSlot(port.id, port.displayName, displayName, SlotType.Input));
                        break;
                    case PortValueType.DynamicMatrix:
                        AddSlot(new DynamicMatrixMaterialSlot(port.id, port.displayName, displayName, SlotType.Input));
                        break;
                    case PortValueType.DynamicValue:
                        AddSlot(new DynamicValueMaterialSlot(port.id, port.displayName, displayName, SlotType.Input, Matrix4x4.identity));
                        break;
                    case PortValueType.Texture2D:
                        AddSlot(new Texture2DInputMaterialSlot(port.id, port.displayName, displayName));
                        break;
                    case PortValueType.Texture3D:
                        AddSlot(new Texture3DInputMaterialSlot(port.id, port.displayName, displayName));
                        break;
                    case PortValueType.Texture2DArray:
                        AddSlot(new Texture2DArrayInputMaterialSlot(port.id, port.displayName, displayName));
                        break;
                    case PortValueType.Cubemap:
                        AddSlot(new CubemapInputMaterialSlot(port.id, port.displayName, displayName));
                        break;
                    case PortValueType.SamplerState:
                        AddSlot(new SamplerStateMaterialSlot(port.id, port.displayName, displayName, SlotType.Input));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                validSlotIds.Add(port.id);
            }

            foreach (var oport in typeState.type.outputs)
            {
                // find matching port in registered values
                var port = typeState.outputPorts[oport.outputPortRef.index];
                var displayName = $"{NodeUtils.GetHLSLSafeName(port.displayName)}{port.id}";
                switch (port.type)
                {
                    case PortValueType.Vector1:
                        AddSlot(new Vector1MaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.Vector2:
                        AddSlot(new Vector2MaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.Vector3:
                        AddSlot(new Vector3MaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.Vector4:
                        AddSlot(new Vector4MaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.DynamicVector:
                        AddSlot(new DynamicVectorMaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.Matrix2x2:
                        AddSlot(new Matrix2MaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.Matrix3x3:
                        AddSlot(new Matrix3MaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.Matrix4x4:
                        AddSlot(new Matrix4MaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.DynamicMatrix:
                        AddSlot(new DynamicMatrixMaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.DynamicValue:
                        AddSlot(new DynamicValueMaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.Texture2D:
                        AddSlot(new Texture2DMaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.Texture3D:
                        AddSlot(new Texture3DMaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.Texture2DArray:
                        AddSlot(new Texture2DArrayMaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.Cubemap:
                        AddSlot(new CubemapMaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    case PortValueType.SamplerState:
                        AddSlot(new SamplerStateMaterialSlot(port.id, port.displayName, displayName, SlotType.Output, default));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                validSlotIds.Add(port.id);
            }

            RemoveSlotsNameNotMatching(validSlotIds, true);
        }

        public void GenerateNodeCode(ShaderGenerator visitor, GraphContext graphContext, GenerationMode generationMode)
        {
            var builder = new ShaderStringBuilder();

            // Declare variables for output ports.
            foreach (HlslArgument argument in function.arguments)
            {
                if (argument.type != HlslArgumentType.OutputPort)
                {
                    continue;
                }

                var slotId = argument.outputPortID;
                var slot = FindSlot<MaterialSlot>(slotId);
                var typeStr = NodeUtils.ConvertConcreteSlotValueTypeToString(precision, slot.concreteValueType);
                var variableStr = GetVariableNameForSlot(slotId);
                builder.Append($"{typeStr} {variableStr};");
            }

            // Declare variable for return value, and set it to the return value from the following function call.
            if (function.returnValue.outputPortRef.isValid)
            {
                var slotId = function.returnValue.id;
                var slot = FindSlot<MaterialSlot>(slotId);
                var typeStr = NodeUtils.ConvertConcreteSlotValueTypeToString(precision, slot.concreteValueType);
                builder.Append($"{typeStr} {GetVariableNameForSlot(slotId)} = ");
            }

            // Build up the function call.
            builder.Append($"{function.name}(");

            // Add in function arguments.
            var first = true;
            foreach (var argument in function.arguments)
            {
                if (!first)
                {
                    builder.Append(", ");
                }
                first = false;

                switch (argument.type)
                {
                    case HlslArgumentType.Control:
                        if (generationMode == GenerationMode.Preview)
                        {
                            builder.Append($"{GetVariableNameForNode()}_c{argument.controlID}");
                        }
                        else
                        {
                            var controlState = GetControlState(argument.controlID);
                            builder.Append(NodeUtils.FloatToShaderValue(controlState.value));
                        }
                        break;
                    case HlslArgumentType.InputPort:
                        var inputSlotId = argument.inputPortID;
                        builder.Append(GetSlotValue(inputSlotId, generationMode));
                        break;
                    case HlslArgumentType.OutputPort:
                        var outputSlotId = argument.outputPortID;
                        builder.Append(GetVariableNameForSlot(outputSlotId));
                        break;
                    case HlslArgumentType.Vector1:
                        builder.Append(NodeUtils.FloatToShaderValue(argument.vector1Value));
                        break;
                    case HlslArgumentType.Value:
                        if (generationMode == GenerationMode.Preview)
                        {
                            builder.Append($"{GetVariableNameForNode()}_v{argument.valueRef.index}");
                        }
                        else
                        {
                            var hlslValue = typeState.hlslValues[argument.valueRef.index];
                            builder.Append(NodeUtils.FloatToShaderValue(hlslValue.value));
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            builder.Append(");");

            visitor.AddShaderChunk(builder.ToString());
        }

        // TODO: This should be inserted at a higher level, but it will do for now
        public void GenerateNodeFunction(FunctionRegistry registry, GraphContext graphContext, GenerationMode generationMode)
        {
            registry.ProvideFunction(function.source.value, builder =>
            {
                switch (function.source.type)
                {
                    case HlslSourceType.File:
                        builder.AppendLine($"#include \"{function.source.value}\"");
                        break;
                    case HlslSourceType.String:
                        builder.AppendLines(function.source.value);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });
        }

        public override void GetSourceAssetDependencies(List<string> paths)
        {
            foreach (var source in typeState.hlslSources)
            {
                if (source.type == HlslSourceType.File)
                {
                    paths.Add(source.value);
                }
            }
        }

        public override void CollectShaderProperties(PropertyCollector properties, GenerationMode generationMode)
        {
            base.CollectShaderProperties(properties, generationMode);

            if (generationMode != GenerationMode.Preview)
            {
                return;
            }

            foreach (var argument in function.arguments)
            {
                if (argument.type == HlslArgumentType.Value)
                {
                    properties.AddShaderProperty(new Vector1ShaderProperty
                    {
                        // TODO: valueRef.index is per node instance, so this is different for every node... bug!
                        overrideReferenceName = $"{GetVariableNameForNode()}_v{argument.valueRef.index}",
                        generatePropertyBlock = false
                    });
                }
                else if (argument.type == HlslArgumentType.Control)
                {
                    properties.AddShaderProperty(new Vector1ShaderProperty
                    {
                        overrideReferenceName = $"{GetVariableNameForNode()}_c{argument.controlID}",
                        generatePropertyBlock = false
                    });
                }
            }
        }

        ControlState GetControlState(int controlID)
        {
            ControlState controlState = typeState.controls.Find(x => x.nodeId == tempId && x.controlId == controlID);
            return controlState;
        }

        public override void CollectPreviewMaterialProperties(List<PreviewProperty> properties)
        {
            base.CollectPreviewMaterialProperties(properties);

            foreach (var argument in function.arguments)
            {
                if (argument.type == HlslArgumentType.Value)
                {
                    var hlslValue = typeState.hlslValues[argument.valueRef.index];
                    properties.Add(new PreviewProperty(PropertyType.Vector1)
                    {
                        // TODO: valueRef.index is per node instance, so this is different for every node... bug!
                        name = $"{GetVariableNameForNode()}_v{argument.valueRef.index}",
                        floatValue = hlslValue.value
                    });
                }
                else if (argument.type == HlslArgumentType.Control)
                {
                    // find the control on this node that represents this control
                    var controlState = GetControlState(argument.controlID);
                    properties.Add(new PreviewProperty(PropertyType.Vector1)
                    {
                        name = $"{GetVariableNameForNode()}_c{argument.controlID}",
                        floatValue = controlState.value
                    });
                }
                else
                    continue;
            }
        }
    }
}
