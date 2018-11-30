using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    //enum NewExponentialBase
    //{
    //    BaseE,
    //    Base2
    //};

    class NewExponentialNode : IShaderNodeType
    {
        InputPort m_InPort = new InputPort(0, "In", PortValue.DynamicVector(0f));
        OutputPort m_OutPort = new OutputPort(1, "Out", PortValueType.DynamicVector);

        public void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Math/Advanced",
                name = "New Exponential",
                inputs = new List<InputPort> { m_InPort },
                outputs = new List<OutputPort> { m_OutPort }
            };

            context.CreateNodeType(type);
        }

        HlslSourceRef m_Source;

        public void OnChange(ref NodeTypeChangeContext context)
        {
            if (!m_Source.isValid)
            {
                m_Source = context.CreateHlslSource("Packages/com.unity.shadergraph/Editor/Data/Nodes/Math/Advanced/Math_Advanced.hlsl");
            }

            foreach (var node in context.addedNodes)
            {
                context.SetHlslFunction(node, new HlslFunctionDescriptor
                {
                    source = m_Source,
                    name = "Unity_Exponential",
                    arguments = new HlslArgumentList { m_InPort },
                    returnValue = m_OutPort
                });
            }

            //foreach (var node in context.modifiedNodes)
            //{
            //    var data = (ExponentialData)context.GetData(node);
            //    if (context.WasControlModified(data.expBaseControl))
            //    {
            //        data.expBase = context.GetControlValue(data.expBaseControl);
            //        context.SetHlslValue(data.expBaseValue, data.expBase);
            //    }
            //}
        }

        //[Serializable]
        //class ExponentialData
        //{
        //    public NewExponentialBase expBase;

        //    [NonSerialized]
        //    public HlslValueRef expBaseValue;

        //    [NonSerialized]
        //    public ControlRef expBaseControl;
        //}

    }

}