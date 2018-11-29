using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewRemapNode : IShaderNodeType
    {
        InputPortRef m_InPort;
        InputPortRef m_InMinMaxPort;
        InputPortRef m_OutMinMaxPort;
        OutputPortRef m_OutPort;

        public void Setup(ref NodeSetupContext context)
        {
            m_InPort = context.CreateInputPort(0, "In", PortValue.DynamicVector(.5f));
            m_InMinMaxPort = context.CreateInputPort(1, "In Min Max", PortValue.DynamicVector(0));
            m_OutMinMaxPort = context.CreateInputPort(2, "Out Min Max", PortValue.DynamicVector(1));
            m_OutPort = context.CreateOutputPort(3, "Out", PortValueType.DynamicVector);

            var type = new NodeTypeDescriptor
            {
                path = "Math/Range",
                name = "New Remap",
                inputs = new List<InputPortRef> { m_InPort, m_InMinMaxPort, m_OutMinMaxPort },
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
                    name = "Unity_Remap",
                    arguments = new HlslArgumentList { m_InPort, m_InMinMaxPort, m_OutMinMaxPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
