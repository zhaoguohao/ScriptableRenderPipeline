using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    class NewDivideNode : IShaderNodeType
    {
        InputPort m_APort = new InputPort(0, "A", PortValue.DynamicVector(0f));
        InputPort m_BPort = new InputPort(1, "B", PortValue.DynamicVector(2f));
        OutputPort m_OutPort = new OutputPort(2, "Out", PortValueType.DynamicVector);
        HlslSourceRef m_Source;

        public void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Math/Basic",
                name = "New Divide",
                inputs = new List<InputPort> { m_APort, m_BPort },
                outputs = new List<OutputPort> { m_OutPort }
            };
            context.CreateNodeType(type);
        }

        public void OnChange(ref NodeTypeChangeContext context)
        {
            if (!m_Source.isValid)
            {
                m_Source = context.CreateHlslSource("Packages/com.unity.shadergraph/Editor/Data/Nodes/Math/Basic/Math_Basic.hlsl");
            }

            foreach (var node in context.addedNodes)
            {
                context.SetHlslFunction(node, new HlslFunctionDescriptor
                {
                    source = m_Source,
                    name = "Unity_Divide",
                    arguments = new HlslArgumentList { m_APort, m_BPort },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
