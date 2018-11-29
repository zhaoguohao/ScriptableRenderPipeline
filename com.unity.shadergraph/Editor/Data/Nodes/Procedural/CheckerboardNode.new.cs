using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewCheckerboardNode : IShaderNodeType
    {
        InputPortRef m_inPortUV;
        InputPortRef m_inPortColorA;
        InputPortRef m_inPortColorB;
        InputPortRef m_inPortFrequency;
        OutputPortRef m_outPortResult;

        public void Setup(ref NodeSetupContext context)
        {
            m_inPortUV = context.CreateInputPort(0, "UV", PortValue.Vector2(Vector2.zero));
            m_inPortColorA = context.CreateInputPort(1, "ColorA", PortValue.Vector3(Vector3.zero));
            m_inPortColorB = context.CreateInputPort(2, "ColorB", PortValue.Vector3(Vector3.one));
            m_inPortFrequency = context.CreateInputPort(3, "Frequency", PortValue.Vector2(Vector2.one));
            m_outPortResult = context.CreateOutputPort(4, "Out", PortValueType.Vector3);
            var type = new NodeTypeDescriptor
            {
                path = "Procedural/Checkerboard",
                name = "New Checkerboard",
                inputs = new List<InputPortRef> { m_inPortUV, m_inPortColorA, m_inPortColorB, m_inPortFrequency },
                outputs = new List<OutputPortRef> { m_outPortResult }
            };
            context.CreateType(type);
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
