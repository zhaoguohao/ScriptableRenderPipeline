using System;
using System.Collections.Generic;

namespace UnityEditor.ShaderGraph
{
    sealed class NewHueNode : ShaderNodeType
    {
        InputPort m_InPort = new InputPort(0, "In", PortValue.Vector3());
        InputPort m_OffsetPort = new InputPort(1, "Offset", PortValue.Vector1(0.5f));
        OutputPort m_OutPort = new OutputPort(2, "Out", PortValueType.Vector3);

        public override void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Artistic/Adjustment",
                name = "New Hue",
                inputs = new List<InputPort> { m_InPort, m_OffsetPort },
                outputs = new List<OutputPort> { m_OutPort }
            };
            context.CreateNodeType(type);
        }

        public override void OnNodeAdded(NodeChangeContext context, NodeRef node)
        {
            var data = (HueData) context.GetData(node);
            if (data == null)
            {
                data = new HueData { offsetFactor = 1f };
                context.SetData(node, data);
            }

            data.offsetFactorControl = context.CreateControl(node, "Offset Factor", data.offsetFactor);
            data.offsetFactorValue = context.CreateHlslValue(data.offsetFactor);

            context.SetHlslFunction(node, new HlslFunctionDescriptor
            {
                source = HlslSource.File("Packages/com.unity.shadergraph/Editor/Data/Nodes/Artistic/Adjustment/HueNode.hlsl"),
                name = "Unity_Hue",
                arguments = new HlslArgumentList { m_InPort, m_OffsetPort, data.offsetFactorValue },
                returnValue = m_OutPort
            });
        }

        public override void OnNodeModified(NodeChangeContext context, NodeRef node)
        {
            var data = (HueData) context.GetData(node);
            if (context.WasControlModified(data.offsetFactorControl))
            {
                data.offsetFactor = context.GetControlValue(data.offsetFactorControl);
                context.SetHlslValue(data.offsetFactorValue, data.offsetFactor);
            }
        }

//        float GetOffsetFactor(HueData data)
//        {
//            return data.mode == HueMode.Degrees ? 1 / 360f : 1;
//        }
    }

    [Serializable]
    class HueData
    {
//        public HueMode mode;
        public float offsetFactor;

        [NonSerialized]
//        public HlslValueRef modeValue;
        public HlslValueRef offsetFactorValue;

        [NonSerialized]
        public ControlRef offsetFactorControl;
    }

// already defined in old HueNode file
//    enum HueMode
//    {
//        Degrees,
//        Normalized
//    }
}
