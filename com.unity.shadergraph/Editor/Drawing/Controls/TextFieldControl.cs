using System;
using System.Reflection;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Color = UnityEditor.ShaderGraph.ColorNode.Color;

namespace UnityEditor.ShaderGraph.Drawing.Controls
{
    [AttributeUsage(AttributeTargets.Property)]
    class TextFieldControlAttribute : Attribute, IControlAttribute
    {
        string m_Label;
        bool m_Multiline;

        public TextFieldControlAttribute(string label = null, bool multiline = false)
        {
            m_Label = label;
            m_Multiline = multiline;
        }

        public VisualElement InstantiateControl(AbstractMaterialNode node, PropertyInfo propertyInfo)
        {
            return new TextFieldControlView(m_Label, m_Multiline, node, propertyInfo);
        }
    }

    class TextFieldControlView : VisualElement
    {
        AbstractMaterialNode m_Node;
        PropertyInfo m_PropertyInfo;

        string m_String;
        TextField m_TextField;

        public TextFieldControlView(string label, bool multiLine, AbstractMaterialNode node, PropertyInfo propertyInfo)
        {
            m_Node = node;
            m_PropertyInfo = propertyInfo;
            styleSheets.Add(Resources.Load<StyleSheet>("Styles/Controls/TextFieldControlView"));
            if (propertyInfo.PropertyType != typeof(string))
                throw new ArgumentException("Property must be of type string.", "propertyInfo");
            label = label ?? ObjectNames.NicifyVariableName(propertyInfo.Name);

            m_String = (string)m_PropertyInfo.GetValue(m_Node, null);

            if (!string.IsNullOrEmpty(label))
                Add(new Label(label));

            m_TextField = new TextField { value = m_String, multiline = multiLine };
            m_TextField.RegisterValueChangedCallback(OnChange);
            Add(m_TextField);
        }

        void OnChange(ChangeEvent<string> evt)
        {
            m_Node.owner.owner.RegisterCompleteObjectUndo("Text Change");
            m_String = evt.newValue;
            m_PropertyInfo.SetValue(m_Node, m_String, null);
            this.MarkDirtyRepaint();
        }
    }
}
