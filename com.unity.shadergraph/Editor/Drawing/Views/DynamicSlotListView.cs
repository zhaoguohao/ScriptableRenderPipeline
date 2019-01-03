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
        // Node data
        AbstractMaterialNode m_Node;
        SlotType m_Type;
        List<SerializableSlot> m_Slots;

        // GUI data
		IMGUIContainer m_Container;
        int m_SelectedIndex = -1;

        public DynamicSlotListView(AbstractMaterialNode node, List<SerializableSlot> slots, SlotType type)
        {
            // Styling
            styleSheets.Add(Resources.Load<StyleSheet>("Styles/DynamicSlotListView"));

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
                var reorderableList = CreateReorderableList(m_Slots, listTitle, true, true, true, true);
                reorderableList.index = m_SelectedIndex;
                reorderableList.DoLayoutList();

                // If changed repaint
                if (changeCheckScope.changed)
                    m_Node.Dirty(ModificationScope.Node);
            }
        }

        private ReorderableList CreateReorderableList(List<SerializableSlot> list, string label, bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton) 
        {
            // Create Reorderable List
            var reorderableList = new ReorderableList(list, typeof(SerializableSlot), draggable, displayHeader, displayAddButton, displayRemoveButton);

            // Draw Header
            reorderableList.drawHeaderCallback = (Rect rect) => 
            {  
                var labelRect = new Rect(rect.x, rect.y, rect.width-10, rect.height);
                EditorGUI.LabelField(labelRect, label);
            };

            // Draw Element
            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => 
            {
                rect.y += 2;
                DrawEntry(reorderableList, index, new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight));
            };

            // Element height
            reorderableList.elementHeightCallback = (int indexer) => 
            {
                return reorderableList.elementHeight;
            };
            
            // Add callback delegates
            reorderableList.onSelectCallback += SelectEntry;
            reorderableList.onAddCallback += AddEntry;
            reorderableList.onRemoveCallback += RemoveEntry;
            return reorderableList;
        }

        private void DrawEntry(ReorderableList list, int index, Rect rect)
        {
            // Label style
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;

            // Get Slot data
            SerializableSlot serializableSlot = list.list[index] as SerializableSlot;

            // Draw element GUI
            int elementCount = m_Type == SlotType.Input ? 4 : 3;
            EditorGUI.BeginChangeCheck();
            serializableSlot.name = EditorGUI.DelayedTextField( new Rect(rect.x, rect.y, rect.width / elementCount, EditorGUIUtility.singleLineHeight), serializableSlot.name, labelStyle);       
            serializableSlot.valueType = (SlotValueType)EditorGUI.EnumPopup( new Rect(rect.x + rect.width / elementCount, rect.y, rect.width / elementCount, EditorGUIUtility.singleLineHeight), serializableSlot.valueType);
            
            if(m_Type == SlotType.Input)
                serializableSlot.interfaceType = EditorGUI.Popup( new Rect(rect.x + (rect.width / elementCount) * 2, rect.y, rect.width / elementCount, EditorGUIUtility.singleLineHeight), serializableSlot.interfaceType, DynamicSlotUtil.GetEnumEntriesOfInterfaceType(serializableSlot.valueType) );

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
            list.list.Add(new SerializableSlot() 
            { 
                id = -1, 
                name = "",
                slotType = m_Type, 
                valueType = SlotValueType.Vector1,
                interfaceType = 0,
                stageCapability = ShaderStageCapability.All, 
            });
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

        // Rebuild UI for redraw
        void Redraw()
        {
            Remove(m_Container);
            m_Container = new IMGUIContainer(() => OnGUIHandler ()) { name = "ListContainer" };
            Add(m_Container);
        }
    }
}
