using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEditorInternal;

namespace UnityEditor.ShaderGraph.Drawing
{
    class DynamicSlotListView : VisualElement
    {
        AbstractMaterialNode m_Node;
        DynamicSlotList m_DynamicSlotList;
		IMGUIContainer m_Container;
        ReorderableList m_ReorderableList;

        public DynamicSlotListView(AbstractMaterialNode node, DynamicSlotList slotList)
        {
            styleSheets.Add(Resources.Load<StyleSheet>("Styles/DynamicSlotListView"));
            m_Node = node;
            m_DynamicSlotList = slotList;

            m_Container = new IMGUIContainer(() => CreateReorderableList ()) { name = "ListContainer" };
            Add(m_Container);
        }

        void CreateReorderableList()
        {
            using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
            {
                var listTitle = string.Format("{0} Slots", m_DynamicSlotList.slotType.ToString());
                m_ReorderableList = DynamicSlotUtils.CreateReorderableList(m_DynamicSlotList, listTitle, true, true, true, true);
                m_ReorderableList.onAddCallback += Redraw;
                m_ReorderableList.DoLayoutList();

                if (changeCheckScope.changed)
                {
                    m_DynamicSlotList.UpdateSlots();
                    m_Node.Dirty(ModificationScope.Node);
                }
            }
        }

        void Redraw(ReorderableList list)
        {
            Remove(m_Container);
            m_Container = new IMGUIContainer(() => CreateReorderableList ()) { name = "ListContainer" };
            Add(m_Container);
        }
    }
}
