using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    sealed class NewFlipNode : ShaderNodeType
    {
        InputPort m_InPort = new InputPort(0, "In", PortValue.DynamicVector(0f));
        OutputPort m_OutPort = new OutputPort(1, "Out", PortValueType.DynamicVector);

        Control m_Red = new Control(2, "Red", PortValueType.Vector1, Control.Slider(0.0f, 0.0f, 1.0f));
        Control m_Green = new Control(3, "Green", PortValueType.Vector1, Control.Slider(0.0f, 0.0f, 1.0f));
        Control m_Blue = new Control(4, "Blue", PortValueType.Vector1, Control.Slider(0.0f, 0.0f, 1.0f));
        Control m_Alpha = new Control(5, "Alpha", PortValueType.Vector1, Control.Slider(0.0f, 0.0f, 1.0f));

        [NonSerialized] HlslValueRef m_FlipX;
        [NonSerialized] HlslValueRef m_FlipY;
        [NonSerialized] HlslValueRef m_FlipZ;
        [NonSerialized] HlslValueRef m_FlipW;
        [NonSerialized] int m_Dimension;

        public override void Setup(ref NodeSetupContext context)
        {
            var type = new NodeTypeDescriptor
            {
                path = "Channel/Flip",
                name = "New Flip",
                inputs = new List<InputPort> { m_InPort },
                outputs = new List<OutputPort> { m_OutPort },
                controls = new List<Control> { m_Red, m_Green, m_Blue, m_Alpha },
            };
            context.CreateNodeType(type);
        }

        HlslSource m_Source = HlslSource.File("Packages/com.unity.shadergraph/Editor/Data/Nodes/Channel/Channel.hlsl");

        void Setup(NodeChangeContext context, NodeRef node)
        {
            int dimension = context.GetPortDimension(m_InPort);
            //Debug.Log("Dimension: " + dimension);

            HlslArgumentList hlslArguments = new HlslArgumentList();
            hlslArguments.Add(m_InPort);

            m_FlipX = context.CreateHlslValue(context.GetControlValue(m_Red) != 0.0f ? -1.0f : 1.0f);
            m_FlipY = context.CreateHlslValue(context.GetControlValue(m_Green) != 0.0f ? -1.0f : 1.0f);
            m_FlipZ = context.CreateHlslValue(context.GetControlValue(m_Blue) != 0.0f ? -1.0f : 1.0f);
            m_FlipW = context.CreateHlslValue(context.GetControlValue(m_Alpha) != 0.0f ? -1.0f : 1.0f);

            if (dimension >= 1) hlslArguments.Add(m_FlipX);
            if (dimension >= 2) hlslArguments.Add(m_FlipY);
            if (dimension >= 3) hlslArguments.Add(m_FlipZ);
            if (dimension >= 4) hlslArguments.Add(m_FlipW);

            context.SetHlslFunction(
                node,
                new HlslFunctionDescriptor
                {
                    source = m_Source,
                    name = "Unity_Flip",
                    arguments = hlslArguments,
                    returnValue = m_OutPort
                }
            );
            m_Dimension = dimension;
        }

        public override void OnNodeAdded(NodeChangeContext context, NodeRef node)
        {
            Setup(context, node);
        }

        // Need a function for Ports Modified, separately from Node modified, since need to actually change shader code.
        public override void OnNodeModified(NodeChangeContext context, NodeRef node)
        {
            int dimension = context.GetPortDimension(m_InPort);
            if (dimension != m_Dimension)
            {
                Setup(context, node);
            }
            else
            {
                context.SetHlslValue(m_FlipX, context.GetControlValue(m_Red) != 0.0f ? -1.0f : 1.0f);
                context.SetHlslValue(m_FlipY, context.GetControlValue(m_Green) != 0.0f ? -1.0f : 1.0f);
                context.SetHlslValue(m_FlipZ, context.GetControlValue(m_Blue) != 0.0f ? -1.0f : 1.0f);
                context.SetHlslValue(m_FlipW, context.GetControlValue(m_Alpha) != 0.0f ? -1.0f : 1.0f);
            }
        }
    }
}
