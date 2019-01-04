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
    class ReorderableSlotListView : VisualElement
    {
        // Node data
        AbstractMaterialNode m_Node;
        SlotType m_Type;
        List<ReorderableSlot> m_Slots;

        // GUI data
		IMGUIContainer m_Container;
        ReorderableList m_ReorderableList;
        int m_SelectedIndex = -1;

        public ReorderableSlotListView(AbstractMaterialNode node, List<ReorderableSlot> slots, SlotType type)
        {
            // Styling
            styleSheets.Add(Resources.Load<StyleSheet>("Styles/ReorderableSlotListView"));

            // Set Node data
            m_Node = node;
            m_Type = type;
            m_Slots = slots;

            // Generate GUI
            m_Container = new IMGUIContainer(() => OnGUIHandler ()) { name = "ListContainer" };
            Add(m_Container);
        }

        void OnGUIHandler()
        {
            // If changed
            using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
            {
                // Get GUI elements
                var listTitle = string.Format("{0} Slots", m_Type.ToString());

                // Create Reorderable List
                m_ReorderableList = CreateReorderableList(m_Slots, listTitle, true, true, true, true);
                m_ReorderableList.index = m_SelectedIndex;
                m_ReorderableList.DoLayoutList();

                // If changed repaint
                if (changeCheckScope.changed)
                    m_Node.Dirty(ModificationScope.Node);
            }
        }

        private ReorderableList CreateReorderableList(List<ReorderableSlot> list, string label, bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton) 
        {
            // Create Reorderable List
            if(m_ReorderableList == null)
                m_ReorderableList = new ReorderableList(list, typeof(ReorderableSlot), draggable, displayHeader, displayAddButton, displayRemoveButton);

            // Draw Header
            m_ReorderableList.drawHeaderCallback = (Rect rect) => 
            {  
                var labelRect = new Rect(rect.x, rect.y, rect.width-10, rect.height);
                EditorGUI.LabelField(labelRect, label);
            };

            // Draw Element
            m_ReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => 
            {
                rect.y += 2;
                DrawEntry(m_ReorderableList, index, new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight));
            };

            // Element height
            m_ReorderableList.elementHeightCallback = (int indexer) => 
            {
                return m_ReorderableList.elementHeight;
            };
            
            // Add callback delegates
            m_ReorderableList.onSelectCallback += SelectEntry;
            m_ReorderableList.onAddCallback += AddEntry;
            m_ReorderableList.onRemoveCallback += RemoveEntry;
            return m_ReorderableList;
        }

        private void DrawEntry(ReorderableList list, int index, Rect rect)
        {
            // Label style
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;

            // Get Slot data
            ReorderableSlot serializableSlot = list.list[index] as ReorderableSlot;

            // Draw element GUI
            int elementCount = m_Type == SlotType.Input ? 4 : 3;
            EditorGUI.BeginChangeCheck();
            serializableSlot.name = EditorGUI.DelayedTextField( new Rect(rect.x, rect.y, rect.width / elementCount, EditorGUIUtility.singleLineHeight), serializableSlot.name, labelStyle);       
            serializableSlot.valueType = (SlotValueType)EditorGUI.EnumPopup( new Rect(rect.x + rect.width / elementCount, rect.y, rect.width / elementCount, EditorGUIUtility.singleLineHeight), serializableSlot.valueType);
            
            if(m_Type == SlotType.Input)
                serializableSlot.interfaceType = EditorGUI.Popup( new Rect(rect.x + (rect.width / elementCount) * 2, rect.y, rect.width / elementCount, EditorGUIUtility.singleLineHeight), serializableSlot.interfaceType, ReorderableSlotListUtil.GetEnumEntriesOfInterfaceType(serializableSlot.valueType) );

            serializableSlot.stageCapability = (ShaderStageCapability)EditorGUI.EnumPopup( new Rect(rect.x + (rect.width / elementCount) * (elementCount-1), rect.y, rect.width / elementCount, EditorGUIUtility.singleLineHeight), serializableSlot.stageCapability);
            
            // Update Slots if changed
            if(EditorGUI.EndChangeCheck())
                m_Node.ValidateNode();
        }

        // Select element callback
        private void SelectEntry(ReorderableList list)
        {
            m_SelectedIndex = list.index;
            Redraw();
        }

        // Add element callback
        private void AddEntry(ReorderableList list)
        {
            list.list.Add(new ReorderableSlot(-1, "", m_Type, SlotValueType.Vector1));
            m_SelectedIndex = m_Slots.Count - 1;
            m_Node.ValidateNode();
        }

        // Remove element callback
        private void RemoveEntry(ReorderableList list)
        {
            list.index = m_SelectedIndex;
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
            m_SelectedIndex = list.index;
            m_Node.ValidateNode();
        }

        private void ReorderEntries(ReorderableList list)
        {
            m_Node.ValidateNode();
        }

        // Rebuild UI for redraw
        void Redraw()
        {
            Remove(m_Container);
            m_Container = new IMGUIContainer(() => OnGUIHandler ()) { name = "ListContainer" };
            Add(m_Container);
        }
    }
}
