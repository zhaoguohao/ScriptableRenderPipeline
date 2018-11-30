using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewSaturateNode : IShaderNodeType
    {
        InputPortRef m_InPort;
        OutputPortRef m_OutPort;

        public void Setup(ref NodeSetupContext context)
        {
            m_InPort = context.CreateInputPort(0, "In", PortValue.DynamicVector(.5f));
            m_OutPort = context.CreateOutputPort(1, "Out", PortValueType.DynamicVector);

            var type = new NodeTypeDescriptor
            {
                path = "Math/Range",
                name = "New Saturate",
                inputs = new List<InputPortRef> { m_InPort },
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
                    name = "Unity_Saturate",
                    arguments = new HlslArgumentList { m_InPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
