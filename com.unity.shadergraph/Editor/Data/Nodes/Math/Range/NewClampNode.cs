using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewClampNode : IShaderNodeType
    {
        InputPort m_InPort = new InputPort(0, "In", PortValue.DynamicVector(.5f));
        InputPort m_MinPort = new InputPort(1, "Min", PortValue.DynamicVector(0));
        InputPort m_MaxPort = new InputPort(2, "Max", PortValue.DynamicVector(1));
        OutputPort m_OutPort = new OutputPort(3, "Out", PortValueType.DynamicVector);

        public void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Math/Range",
                name = "New Clamp",
                inputs = new List<InputPort> { m_InPort, m_MinPort, m_MaxPort },
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
                    name = "Unity_Clamp",
                    arguments = new HlslArgumentList { m_InPort, m_MinPort, m_MaxPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
