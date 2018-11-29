using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewPowerNode : IShaderNodeType
    {
        InputPortRef m_APort;
        InputPortRef m_BPort;
        OutputPortRef m_OutPort;
        HlslSourceRef m_Source;

        public void Setup(ref NodeSetupContext context)
        {
            m_APort = context.CreateInputPort(0, "A", PortValue.DynamicVector(0f));
            m_BPort = context.CreateInputPort(1, "B", PortValue.DynamicVector(2f));
            m_OutPort = context.CreateOutputPort(2, "Out", PortValueType.DynamicVector);

            var type = new NodeTypeDescriptor
            {
                path = "Math/Basic",
                name = "New Power",
                inputs = new List<InputPortRef> { m_APort, m_BPort },
                outputs = new List<OutputPortRef> { m_OutPort }
            };
            context.CreateType(type);
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
                    name = "Unity_Power",
                    arguments = new HlslArgumentList { m_APort, m_BPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
