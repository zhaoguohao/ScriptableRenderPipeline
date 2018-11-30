using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewCheckerboardNode : IShaderNodeType
    {
        InputPort m_inPortUV = new InputPort(0, "UV", PortValue.Vector2(Vector2.zero));
        InputPort m_inPortColorA = new InputPort(1, "ColorA", PortValue.Vector3(Vector3.zero));
        InputPort m_inPortColorB = new InputPort(2, "ColorB", PortValue.Vector3(Vector3.one));
        InputPort m_inPortFrequency = new InputPort(3, "Frequency", PortValue.Vector2(Vector2.one));
        OutputPort m_outPortResult = new OutputPort(4, "Out", PortValueType.Vector3);

        public void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Procedural/Checkerboard",
                name = "New Checkerboard",
                inputs = new List<InputPort> { m_inPortUV, m_inPortColorA, m_inPortColorB, m_inPortFrequency },
                outputs = new List<OutputPort> { m_outPortResult }
            };
            context.CreateNodeType(type);
        }

        HlslSourceRef m_Source;
        public void OnChange(ref NodeTypeChangeContext context)
        {
            if (!m_Source.isValid)
            {
                m_Source = context.CreateHlslSource("Packages/com.unity.shadergraph/Editor/Data/Nodes/Procedural/CheckerboardNode.hlsl");
            }

            // process newly created nodes
            foreach (NodeRef node in context.addedNodes)
            {
                context.SetHlslFunction(
                    node,
                    new HlslFunctionDescriptor
                    {
                        source = m_Source,
                        name = "Unity_Checkerboard",
                        arguments = new HlslArgumentList
                        {
                            m_inPortUV,
                            m_inPortColorA,
                            m_inPortColorB,
                            m_inPortFrequency
                        },
                        returnValue = m_outPortResult
                    }
                );
            }

            foreach (var node in context.modifiedNodes)
            {
                // .... no controls
            }
        }
    }
}
