//using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    sealed class NewCheckerboardNode : ShaderNodeType
    {
        InputPort m_inPortUV = new InputPort(0, "UV", PortValue.Vector2(Vector2.zero));
        InputPort m_inPortColorA = new InputPort(1, "ColorA", PortValue.Vector3(Vector3.zero));
        InputPort m_inPortColorB = new InputPort(2, "ColorB", PortValue.Vector3(Vector3.one));
        InputPort m_inPortFrequency = new InputPort(3, "Frequency", PortValue.Vector2(Vector2.one));
        OutputPort m_outPortResult = new OutputPort(4, "Out", PortValueType.Vector3);

        public override void Setup(ref NodeSetupContext context)
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

        HlslSource m_Source = HlslSource.File("Packages/com.unity.shadergraph/Editor/Data/Nodes/Procedural/CheckerboardNode.hlsl");


        public override void OnNodeAdded(NodeChangeContext context, NodeRef node)
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
    }
}
