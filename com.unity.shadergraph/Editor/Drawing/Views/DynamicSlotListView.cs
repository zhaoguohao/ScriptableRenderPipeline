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
        // --------------------------------------------------
        // VIEW

        // Node data
        AbstractMaterialNode m_Node;
        SlotType m_Type;
        List<MaterialSlot> m_Slots;

        // GUI data
		IMGUIContainer m_Container;

        // Reorderable List data
        [SerializeField] ReorderableList m_ReorderableList;
        int m_SelectedIndex = -1;

        public DynamicSlotListView(AbstractMaterialNode node, List<MaterialSlot> slots, SlotType type)
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
                
                // Create Reorderable List data from Node Slot data
                List<SerializableSlot> serializableSlots = new List<SerializableSlot>();
                foreach(MaterialSlot slot in m_Slots)
                    serializableSlots.Add(slot.Serialize());

                // Create Reorderable List
                m_ReorderableList = CreateReorderableList(serializableSlots, listTitle, true, true, true, true);
                m_ReorderableList.index = m_SelectedIndex;
                m_ReorderableList.DoLayoutList();

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
            int elementCount = m_Type == SlotType.Input ? 3 : 2;
            EditorGUI.BeginChangeCheck();
            serializableSlot.name = EditorGUI.DelayedTextField( new Rect(rect.x, rect.y, rect.width / elementCount, EditorGUIUtility.singleLineHeight), serializableSlot.name, labelStyle);       
            serializableSlot.valueType = (SlotValueType)EditorGUI.EnumPopup( new Rect(rect.x + rect.width / elementCount, rect.y, rect.width / elementCount, EditorGUIUtility.singleLineHeight), serializableSlot.valueType);
            
            if(m_Type == SlotType.Input)
                serializableSlot.interfaceType = EditorGUI.Popup( new Rect(rect.x + (rect.width / elementCount) * 2, rect.y, rect.width / elementCount, EditorGUIUtility.singleLineHeight), serializableSlot.interfaceType, DynamicSlotUtil.GetEnumEntriesOfInterfaceType(serializableSlot.valueType) );
            
            // Update Slots if changed
            if(EditorGUI.EndChangeCheck())
                UpdateSlots();
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
                name = GetNewSlotName(),
                slotType = m_Type, 
                valueType = SlotValueType.Vector1,
                interfaceType = 0 
            });
            m_SelectedIndex = m_Slots.Count - 1;
            UpdateSlots();
        }

        // Remove element callback
        private void RemoveEntry(ReorderableList list)
        {
            list.index = m_SelectedIndex;
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
            m_SelectedIndex = list.index;
            UpdateSlots();
        }

        // Rebuild UI for redraw
        void Redraw()
        {
            Remove(m_Container);
            m_Container = new IMGUIContainer(() => OnGUIHandler ()) { name = "ListContainer" };
            Add(m_Container);
        }

        // --------------------------------------------------
        // MODEL

        public void UpdateSlots()
        {
            // Clear all Slots on Node
            m_Slots.Clear();

            // Recreate all Slots from Reorderable List
            foreach(SerializableSlot entry in m_ReorderableList.list)
            {
                // If new Slot generate a valid ID
                if(entry.id == -1)
                    entry.id = GetNewSlotID();

                // Add to Node
                m_Slots.Add(entry.Deserialize());
            }

            // Update Node
            m_Node.UpdateNodeAfterDeserialization();
        }

        private int GetNewSlotID()
        {
            // Track highest Slot ID
            int ceiling = -1;
            
            // Get all Slots from Node
            List<MaterialSlot> slots = new List<MaterialSlot>();
            m_Node.GetSlots(slots);

            // Increment highest Slot ID from Slots on Node
            foreach(MaterialSlot slot in slots)
                ceiling = slot.id > ceiling ? slot.id : ceiling;

            return ceiling + 1;
        }

        private string GetNewSlotName()
        {
            // Track highest number of unnamed Slots
            int ceiling = 0;

            // Get all Slots from Node
            List<MaterialSlot> slots = new List<MaterialSlot>();
            m_Node.GetSlots(slots);

            // Increment highest Slot number from Slots on Node
            foreach(MaterialSlot slot in slots)
            {
                if(slot.displayName.StartsWith("New Slot"))
                    ceiling++;
            }

            if(ceiling > 0)
                return string.Format("New Slot ({0})", ceiling);
            return "New Slot";
        }
    }
}
