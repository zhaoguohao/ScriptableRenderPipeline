using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewSquareRootNode : IShaderNodeType
    {
        InputPort m_InPort = new InputPort(0, "In", PortValue.DynamicVector(0f));
        OutputPort m_OutPort = new OutputPort(1, "Out", PortValueType.DynamicVector);
        HlslSourceRef m_Source;

        public void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Math/Basic",
                name = "New Square Root",
                inputs = new List<InputPort> { m_InPort },
                outputs = new List<OutputPort> { m_OutPort }
            };
            context.CreateNodeType(type);
        }

        public void OnChange(ref NodeTypeChangeContext context)
        {
            if (!m_Source.isValid)
            {
                m_Source = context.CreateHlslSource("Packages/com.unity.shadergraph/Editor/Data/Nodes/Math/Basic/Math_Basic.hlsl");
            }

            foreach (var node in context.addedNodes)
            {
                context.SetHlslFunction(node, new HlslFunctionDescriptor
                {
                    source = m_Source,
                    name = "Unity_SquareRoot",
                    arguments = new HlslArgumentList { m_InPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
