using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewTruncateNode : IShaderNodeType
    {
        InputPortRef m_InPort;
        OutputPortRef m_OutPort;
        HlslSourceRef m_Source;

        public void Setup(ref NodeSetupContext context)
        {
            m_InPort = context.CreateInputPort(0, "In", PortValue.DynamicVector(0f));
            m_OutPort = context.CreateOutputPort(1, "Out", PortValueType.DynamicVector);

            var type = new NodeTypeDescriptor
            {
                path = "Math/Round",
                name = "New Truncate",
                inputs = new List<InputPortRef> { m_InPort },
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
                    name = "Unity_Truncate",
                    arguments = new HlslArgumentList { m_InPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
