/*
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewMaximumNode : IShaderNodeType
    {
        InputPort m_APort = new InputPort(0, "A", PortValue.DynamicVector(.5f));
        InputPort m_BPort = new InputPort(1, "B", PortValue.DynamicVector(.5f));
        OutputPort m_OutPort = new OutputPort(2, "Out", PortValueType.DynamicVector);

        public void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Math/Range",
                name = "New Maximum",
                inputs = new List<InputPort> { m_APort, m_BPort },
                outputs = new List<OutputPort> { m_OutPort }
            };

            context.CreateNodeType(type);
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
                    name = "Unity_Maximum",
                    arguments = new HlslArgumentList { m_APort, m_BPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
*/