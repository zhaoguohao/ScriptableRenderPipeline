using System.Collections.Generic;

namespace UnityEditor.ShaderGraph
{
    class NewCrossProductNode : IShaderNodeType
    {
        InputPort m_InPortA = new InputPort(0, "InA", PortValue.Vector3());
        InputPort m_InPortB = new InputPort(1, "InB", PortValue.Vector3());
        OutputPort m_OutPort = new OutputPort(2, "Out", PortValueType.Vector3);

        public void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Math/Vector",
                name = "New Cross Product",
                inputs = new List<InputPort> { m_InPortA, m_InPortB },
                outputs = new List<OutputPort> { m_OutPort }
            };
            context.CreateNodeType(type);
        }

        HlslSourceRef m_Source;

        public void OnChange(ref NodeTypeChangeContext context)
        {
            if (!m_Source.isValid)
            {
                m_Source = context.CreateHlslSource("Packages/com.unity.shadergraph/Editor/Data/Nodes/Math/Vector/Math_Vector.hlsl");
            }

            foreach (var node in context.addedNodes)
            {
                context.SetHlslFunction(node, new HlslFunctionDescriptor
                {
                    source = m_Source,
                    name = "CrossProduct",
                    arguments = new HlslArgumentList { m_InPortA, m_InPortB },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
