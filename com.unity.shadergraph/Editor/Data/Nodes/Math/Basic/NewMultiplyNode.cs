using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    sealed class NewMultiplyNode : ShaderNodeType
    {
        InputPort m_aPort = new InputPort(0, "A", PortValue.DynamicVector(0f));
        InputPort m_bPort = new InputPort(1, "B", PortValue.DynamicVector(2f));
        OutputPort m_OutPort = new OutputPort(2, "Out", PortValueType.DynamicVector);

        public override void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Math/Basic",
                name = "New Multiply",
                inputs = new List<InputPort> { m_aPort, m_bPort },
                outputs = new List<OutputPort> { m_OutPort }
            };

            context.CreateNodeType(type);
        }

        HlslSource m_Source = HlslSource.File("Packages/com.unity.shadergraph/Editor/Data/Nodes/Math/Basic/Math_Basic.hlsl");

        public override void OnNodeAdded(NodeChangeContext context, NodeRef node)
        {
            context.SetHlslFunction(node, new HlslFunctionDescriptor
            {
                source = m_Source,
                name = "Unity_Multiply",
                arguments = new HlslArgumentList { m_aPort, m_bPort },
                returnValue = m_OutPort
            });
        }
    }
}
