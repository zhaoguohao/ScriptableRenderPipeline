using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewStepNode : IShaderNodeType
    {
        InputPortRef m_APort;
        InputPortRef m_BPort;
        OutputPortRef m_OutPort;
        HlslSourceRef m_Source;

        public void Setup(ref NodeSetupContext context)
        {
            m_APort = context.CreateInputPort(0, "Edge", PortValue.DynamicVector(1f));
            m_BPort = context.CreateInputPort(1, "In", PortValue.DynamicVector(0f));
            m_OutPort = context.CreateOutputPort(2, "Out", PortValueType.DynamicVector);

            var type = new NodeTypeDescriptor
            {
                path = "Math/Round",
                name = "New Step",
                inputs = new List<InputPortRef> { m_APort, m_BPort },
                outputs = new List<OutputPortRef> { m_OutPort }
            };
            context.CreateType(type);
        }

        public void OnChange(ref NodeTypeChangeContext context)
        {
            if (!m_Source.isValid)
            {
                m_Source = context.CreateHlslSource("Packages/com.unity.shadergraph/Editor/Data/Nodes/Math/Round/Math_Round.hlsl");
            }

            foreach (var node in context.addedNodes)
            {
                context.SetHlslFunction(node, new HlslFunctionDescriptor
                {
                    source = m_Source,
                    name = "Unity_Step",
                    arguments = new HlslArgumentList { m_APort, m_BPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
