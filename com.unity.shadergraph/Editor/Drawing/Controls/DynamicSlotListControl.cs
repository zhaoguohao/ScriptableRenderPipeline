using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditorInternal;

namespace UnityEditor.ShaderGraph.Drawing.Controls
{
    [AttributeUsage(AttributeTargets.Property)]
    class DynamicSlotListControlAttribute : Attribute, IControlAttribute
    {
        SlotType m_Type;

        public DynamicSlotListControlAttribute(SlotType type)
        {
            m_Type = type;
        }

        public VisualElement InstantiateControl(AbstractMaterialNode node, PropertyInfo propertyInfo)
        {
            return new DynamicSlotListControlView(node, propertyInfo, m_Type);
        }
    }

    class DynamicSlotListControlView : VisualElement, INodeModificationListener
    {
        AbstractMaterialNode m_Node;
        PropertyInfo m_PropertyInfo;
        DynamicSlotList m_DynamicSlotList;
        IMGUIContainer m_Container;
        ReorderableList m_ReorderableList;

        public DynamicSlotListControlView(AbstractMaterialNode node, PropertyInfo propertyInfo, SlotType type)
        {
            styleSheets.Add(Resources.Load<StyleSheet>("Styles/Controls/DynamicSlotListControlView"));
            m_Node = node;
            m_PropertyInfo = propertyInfo;
            m_DynamicSlotList = (DynamicSlotList)m_PropertyInfo.GetValue(m_Node, null);

            if (propertyInfo.PropertyType != typeof(DynamicSlotList))
                throw new ArgumentException("Property must be an DynamicSlotList.", "propertyInfo");

            m_Container = new IMGUIContainer(() => CreateReorderableList ()) { name = "ListContainer" };
            Add(m_Container);
        }

        void CreateReorderableList()
        {
            using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
            {
                m_DynamicSlotList = (DynamicSlotList)m_PropertyInfo.GetValue(m_Node, null);
                var listTitle = string.Format("{0} Slots", m_DynamicSlotList.type.ToString());
                m_ReorderableList = DynamicSlotUtils.CreateReorderableList(m_DynamicSlotList, listTitle, true, true, true, true);
                
                m_ReorderableList.onAddCallback += Redraw;
                m_ReorderableList.onRemoveCallback += Redraw;
                m_ReorderableList.onSelectCallback += SelectItem;// Redraw;
                m_ReorderableList.onReorderCallback += SelectItem;

                m_ReorderableList.DoLayoutList();

                if (changeCheckScope.changed)
                {
                    m_Node.owner.owner.RegisterCompleteObjectUndo("Change " + m_Node.name);
                    m_DynamicSlotList.list = m_DynamicSlotList.list;
                    m_PropertyInfo.SetValue(m_Node, m_DynamicSlotList, null);
                }
            }
        }

        public void OnNodeModified(ModificationScope scope)
        {
            if (scope == ModificationScope.Node)
                Redraw(null);
        }

        private void SelectItem(ReorderableList list)
        {
            Repaint(list);
            m_Container.Focus();
        }

        void Repaint(ReorderableList list)
        {
            m_Container.MarkDirtyRepaint();
        }

        void Redraw(ReorderableList list)
        {
            Remove(m_Container);
            m_Container = new IMGUIContainer(() => CreateReorderableList ()) { name = "ListContainer" };
            Add(m_Container);
        }
    }
}
