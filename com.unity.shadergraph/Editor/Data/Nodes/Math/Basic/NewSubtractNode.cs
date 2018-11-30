using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    sealed class NewSubtractNode : ShaderNodeType
    {
        InputPort m_APort = new InputPort(0, "A", PortValue.DynamicVector(1f));
        InputPort m_BPort = new InputPort(1, "B", PortValue.DynamicVector(1f));
        OutputPort m_OutPort = new OutputPort(2, "Out", PortValueType.DynamicVector);
        HlslSource m_Source = HlslSource.File("Packages/com.unity.shadergraph/Editor/Data/Nodes/Math/Basic/Math_Basic.hlsl");

        public override void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Math/Basic",
                name = "New Subtract",
                inputs = new List<InputPort> { m_APort, m_BPort },
                outputs = new List<OutputPort> { m_OutPort }
            };
            context.CreateNodeType(type);
        }

        public override void OnNodeAdded(NodeChangeContext context, NodeRef node)
        {
            context.SetHlslFunction(node, new HlslFunctionDescriptor
            {
                source = m_Source,
                name = "Unity_Subtract",
                arguments = new HlslArgumentList { m_APort, m_BPort },
                returnValue = m_OutPort
            });
        }
    }
}
