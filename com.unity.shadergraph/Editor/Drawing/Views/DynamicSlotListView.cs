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

        AbstractMaterialNode m_Node;
        SlotType m_Type;
        List<MaterialSlot> m_Slots;

		IMGUIContainer m_Container;
        ReorderableList m_ReorderableList;

        public DynamicSlotListView(AbstractMaterialNode node, List<MaterialSlot> slots, SlotType type)
        {
            styleSheets.Add(Resources.Load<StyleSheet>("Styles/DynamicSlotListView"));
            m_Node = node;
            m_Type = type;
            m_Slots = slots;

            m_Container = new IMGUIContainer(() => OnGUIHandler ()) { name = "ListContainer" };
            Add(m_Container);
        }

        void OnGUIHandler()
        {
            using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
            {
                var listTitle = string.Format("{0} Slots", m_Type.ToString());
                var entries = ConvertSlotsToEntries(m_Slots);
                m_ReorderableList = CreateReorderableList(entries, listTitle, true, true, true, true);
                m_ReorderableList.DoLayoutList();

                if (changeCheckScope.changed)
                {
                    UpdateSlots();
                    m_Node.Dirty(ModificationScope.Node);
                }
            }
        }

        private List<SlotEntry> ConvertSlotsToEntries(List<MaterialSlot> slots)
        {
            List<SlotEntry> entries = new List<SlotEntry>();
            foreach(MaterialSlot slot in slots)
            {
                entries.Add(new SlotEntry()
                {
                    id = slot.id,
                    name = slot.RawDisplayName(),
                    valueType = slot.valueType,
                });
            }
            return entries;
        }

        private ReorderableList CreateReorderableList(List<SlotEntry> list, string label, bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton) 
        {
            var reorderableList = new ReorderableList(list, typeof(SlotEntry), draggable, displayHeader, displayAddButton, displayRemoveButton);

            reorderableList.drawHeaderCallback = (Rect rect) => 
            {  
                var labelRect = new Rect(rect.x, rect.y, rect.width-10, rect.height);
                EditorGUI.LabelField(labelRect, label);
            };

            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => 
            {
                rect.y += 2;
                DrawEntry(list[index], new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight));
            };

            reorderableList.elementHeightCallback = (int indexer) => 
            {
                return reorderableList.elementHeight;
            };

            reorderableList.onAddCallback += AddEntry;
            reorderableList.onRemoveCallback += RemoveEntry;
            return reorderableList;
        }

        private void DrawEntry(SlotEntry entry, Rect rect)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            entry.name = EditorGUI.TextField( new Rect(rect.x, rect.y, rect.width / 2, EditorGUIUtility.singleLineHeight), entry.name, labelStyle);
            entry.valueType = (SlotValueType)EditorGUI.EnumPopup( new Rect(rect.x + rect.width / 2, rect.y, rect.width / 2, EditorGUIUtility.singleLineHeight), entry.valueType);
        }

        private void AddEntry(ReorderableList list)
        {
            list.list.Add(new SlotEntry() { id = -1, name = "New Slot", valueType = SlotValueType.Vector1 } );
            Redraw();
        }

        private void RemoveEntry(ReorderableList list)
        {
            list.list.RemoveAt(list.index);
            Redraw();
        }

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
            m_Slots.Clear();
            foreach(SlotEntry entry in m_ReorderableList.list)
            {
                if(entry.id == -1)
                    entry.id = GetNewSlotID();
                
                MaterialSlot slot = MaterialSlot.CreateMaterialSlot(entry.valueType, entry.id, entry.name, NodeUtils.GetHLSLSafeName(entry.name), 
                    m_Type, Vector4.zero, ShaderStageCapability.All);

                m_Slots.Add(slot);
            }

            m_Node.UpdateNodeAfterDeserialization();
            //m_Node.OnBeforeSerialize();
        }

        private int GetNewSlotID()
        {
            int ceiling = -1;
            
            List<MaterialSlot> slots = new List<MaterialSlot>();
            m_Node.GetSlots(slots);

            foreach(MaterialSlot slot in slots)
                ceiling = slot.id > ceiling ? slot.id : ceiling;

            return ceiling + 1;
        }

        // --------------------------------------------------
        // DATA

        public class SlotEntry
        {
            public int id;
            public string name;
            public SlotValueType valueType;
        }
    }
}
