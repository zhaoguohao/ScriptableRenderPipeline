using System;
using System.Linq;
using System.Globalization;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Toggle = UnityEngine.UIElements.Toggle;
using UnityEditor.Experimental.GraphView;

namespace UnityEditor.ShaderGraph.Drawing
{
    class BlackboardFieldInputView : VisualElement
    {
        readonly BlackboardField m_BlackboardField;
        readonly AbstractMaterialGraph m_Graph;

        InputDescriptor m_Input;

        static Type s_ContextualMenuManipulator = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypesOrNothing()).FirstOrDefault(t => t.FullName == "UnityEngine.UIElements.ContextualMenuManipulator");
        
        public BlackboardFieldInputView(BlackboardField blackboardField, AbstractMaterialGraph graph, InputDescriptor input)
        {
            styleSheets.Add(Resources.Load<StyleSheet>("Styles/SubGraphBlackboard"));
            m_BlackboardField = blackboardField;
            m_Graph = graph;
            m_Input = input;

            Add(GetControlForShaderParameter(new ShaderValue(input, graph)));
            AddToClassList("sgblackboardFieldInputView");
        }

        private VisualElement GetControlForShaderParameter(ShaderValue parameter)
        {
            var container = new VisualElement { name = "Container" };
            container.style.marginLeft = 6;
            container.style.marginRight = 6;
            container.style.flexDirection = FlexDirection.Row;

            var labelContainer = new VisualElement { name = "LabelContainer" };
            labelContainer.style.width = 60;
            var label = new Label("Default");
            label.style.paddingTop = 4;
            labelContainer.Add(label);
            container.Add(labelContainer);

            var valueContainer = new VisualElement { name = "ValueContainer" };
            valueContainer.Add(parameter.control.GetControl(parameter));
            valueContainer.style.flexGrow = 1;
            valueContainer.style.flexDirection = FlexDirection.Row;
            container.Add(valueContainer);
            return container;
        }
        void RemoveElements(VisualElement[] elements)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i].parent == this)
                    Remove(elements[i]);
            }
        }

        void DirtyNodes(ModificationScope modificationScope = ModificationScope.Node)
        {
            foreach (var node in m_Graph.GetNodes<PropertyNode>())
                node.Dirty(modificationScope);
        }
    }
}
