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
            styleSheets.Add(Resources.Load<StyleSheet>("Styles/ShaderGraphBlackboard"));
            m_BlackboardField = blackboardField;
            m_Graph = graph;
            m_Input = input;

            Add(input.control.GetControl(new ShaderParameter(input)));
            
            //put the drawing of input binding options here based on slot type

//            AddRow("Type", new TextField());
//            AddRow("Exposed", new Toggle(null));
//            AddRow("Range", new Toggle(null));
//            AddRow("Default", new TextField());
//            AddRow("Tooltip", new TextField());


            AddToClassList("sgblackboardFieldInputView");

            
        }

        VisualElement CreateRow(string labelText, VisualElement control)
        {
            VisualElement rowView = new VisualElement();

            rowView.AddToClassList("rowView");

            Label label = new Label(labelText);

            label.AddToClassList("rowViewLabel");
            rowView.Add(label);

            control.AddToClassList("rowViewControl");
            rowView.Add(control);

            return rowView;
        }

        VisualElement AddRow(string labelText, VisualElement control)
        {
            VisualElement rowView = CreateRow(labelText, control);
            Add(rowView);
            return rowView;
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
