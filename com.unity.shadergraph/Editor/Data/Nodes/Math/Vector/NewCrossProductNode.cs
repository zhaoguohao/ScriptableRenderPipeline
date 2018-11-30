using System.Collections.Generic;

namespace UnityEditor.ShaderGraph
{
    class NewCrossProductNode : IShaderNodeType
    {
        InputPortRef m_InPortA;
        InputPortRef m_InPortB;
        InputPortRef m_OffsetPort;
        OutputPortRef m_OutPort;

        public void Setup(ref NodeSetupContext context)
        {
            m_InPortA = context.CreateInputPort(0, "InA", PortValue.Vector3());
            m_InPortB = context.CreateInputPort(1, "InB", PortValue.Vector3());
            m_OutPort = context.CreateOutputPort(2, "Out", PortValueType.Vector3);
            var type = new NodeTypeDescriptor
            {
                path = "Math/Vector",
                name = "New Cross Product",
                inputs = new List<InputPortRef> { m_InPortA, m_InPortB },
                outputs = new List<OutputPortRef> { m_OutPort }
            };
            context.CreateType(type);
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
                    name = "Unity_CrossProduct",
                    arguments = new HlslArgumentList { m_InPortA, m_InPortB },
                    returnValue = m_OutPort
                });
            }
        }
    }
}
