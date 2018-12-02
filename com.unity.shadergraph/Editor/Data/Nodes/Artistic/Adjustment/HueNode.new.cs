using System;
using System.Collections.Generic;

namespace UnityEditor.ShaderGraph
{
    sealed class NewHueNode : ShaderNodeType
    {
        InputPort m_InPort = new InputPort(0, "In", PortValue.Vector3());
        InputPort m_OffsetPort = new InputPort(1, "Offset", PortValue.Vector1(0.5f));
        OutputPort m_OutPort = new OutputPort(2, "Out", PortValueType.Vector3);

        Control m_offsetFactor = new Control(3, "Offset Factor", PortValueType.Vector1, Control.Slider(0.0f, 50.0f, 3.0f));
        
        public override void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Artistic/Adjustment",
                name = "New Hue",

                // defaultHlslFunction = new HlslFunctionDescriptor(...)
                //    then we don't need to implement OnNodeAdded, if it's always the same

                // TODO: build these lists via reflection of NewHueNode type, if these lists are not directly specified...
                inputs = new List<InputPort> { m_InPort, m_OffsetPort },
                outputs = new List<OutputPort> { m_OutPort },
                controls = new List<Control> { m_offsetFactor }
            };
            context.CreateNodeType(type);
        }

        public override void OnNodeAdded(NodeChangeContext context, NodeRef node)
        {
            context.SetHlslFunction(node,
               new HlslFunctionDescriptor
                {
                    source = HlslSource.File("Packages/com.unity.shadergraph/Editor/Data/Nodes/Artistic/Adjustment/HueNode.hlsl"),
                    name = "Unity_Hue",
                    arguments = new HlslArgumentList { m_InPort, m_OffsetPort, m_offsetFactor },
                    returnValue = m_OutPort,
//                    supportsPrecision = true      // indicates half/float variants exist in the HlslSource
                });
        }
    }

    [Serializable]
    class HueData
    {
        // nothing here now that Control can handle it's own serialization
        // this is only needed if script logic needs to serialize something outside of a control...
    }

// already defined in old HueNode file
//    enum HueMode
//    {
//        Degrees,
//        Normalized
//    }
}
