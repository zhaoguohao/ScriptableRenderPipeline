/*
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewRemapNode : IShaderNodeType
    {
        InputPort m_InPort = new InputPort(0, "In", PortValue.DynamicVector(.5f));
        InputPort m_InMinMaxPort = new InputPort(1, "In Min Max", PortValue.Vector2(new Vector2(0, 1)));
        InputPort m_OutMinMaxPort = new InputPort(2, "Out Min Max", PortValue.Vector2(new Vector2(0, 1)));
        OutputPort m_OutPort = new OutputPort(3, "Out", PortValueType.DynamicVector);

        public void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Math/Range",
                name = "New Remap",
                inputs = new List<InputPort> { m_InPort, m_InMinMaxPort, m_OutMinMaxPort },
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
                    name = "Unity_Remap",
                    arguments = new HlslArgumentList { m_InPort, m_InMinMaxPort, m_OutMinMaxPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
*/