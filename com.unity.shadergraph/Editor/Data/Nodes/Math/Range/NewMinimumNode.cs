using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewMinimumNode : IShaderNodeType
    {
        InputPortRef m_APort;
        InputPortRef m_BPort;
        OutputPortRef m_OutPort;

        public void Setup(ref NodeSetupContext context)
        {
            m_APort = context.CreateInputPort(0, "A", PortValue.DynamicVector(.5f));
            m_BPort = context.CreateInputPort(1, "B", PortValue.DynamicVector(.5f));
            m_OutPort = context.CreateOutputPort(2, "Out", PortValueType.DynamicVector);

            var type = new NodeTypeDescriptor
            {
                path = "Math/Range",
                name = "New Minimum",
                inputs = new List<InputPortRef> { m_APort, m_BPort },
                outputs = new List<OutputPortRef> { m_OutPort }
            };

            context.CreateType(type);
        }

        HlslSourceRef m_Source;

        public void OnChange(ref NodeTypeChangeContext context)
        {
            // TODO: Figure out what should cause the user to create the hlsl source
            // TODO: How does sharing files between multiple node types work?
            if (!m_Source.isValid)
            {
                m_Source = context.CreateHlslSource("Packages/com.unity.shadergraph/Editor/Data/Nodes/Math/Range/Math_Range.hlsl");
            }

            foreach (var node in context.addedNodes)
            {
                context.SetHlslFunction(node, new HlslFunctionDescriptor
                {
                    source = m_Source,
                    name = "Unity_Minimum",
                    arguments = new HlslArgumentList { m_APort, m_BPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
