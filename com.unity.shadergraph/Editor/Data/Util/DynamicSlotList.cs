using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Drawing.Controls;

namespace UnityEditor.ShaderGraph
{
	// ----------------------------------------------------------------------------------------------------
    // Class
    // ----------------------------------------------------------------------------------------------------

	[Serializable]
	class DynamicSlotList
	{
        [Serializable]
		public class Entry
		{
            public int slotId;
			public string name = "New Slot";
			public SlotValueType type = SlotValueType.Vector1;

            public Entry()
            {
                slotId = -1;
            }
		}

		public DynamicSlotList(AbstractMaterialNode node, SlotType type)
		{
			m_Node = node;
			m_Type = type;
		}

		private AbstractMaterialNode m_Node;
		private SlotType m_Type;
		public SlotType type
		{
			get { return m_Type; }
		}

		public List<Entry> m_List = new List<Entry>();

		public List<Entry> list
		{
			get { return m_List; }
			set 
			{ 
				m_List = value;
				UpdateSlots();
				m_Node.Dirty(ModificationScope.Topological); 
			}
		}

        private List<int> m_ActiveSlots = new List<int>();

        public List<int> activeSlots
        {
            get { return m_ActiveSlots; }
            set { m_ActiveSlots = value; }
        }

        public void UpdateSlots()
        {
            List<int> validIDs = new List<int>();
            List<MaterialSlot> slots = new List<MaterialSlot>();
            m_Node.GetSlots(slots);

            // TODO - This tries to add slots from other sources (other dynamic lists) into the valid list
            // Otherwise this slot list will destroy all other slots
            var otherValidSlots = slots.Where(x => !list.Any(y => y.slotId == x.id)).ToArray();
            var otherValidIDs = new int[otherValidSlots.Length];
            for(int i = 0; i < otherValidIDs.Length; i++)
                otherValidIDs[i] = otherValidSlots[i].id; 

            for(int i = 0; i < list.Count; i++)
            {
                if(list[i].slotId == -1)
                    list[i].slotId = GetNewSlotID();
                
                MaterialSlot slot = MaterialSlot.CreateMaterialSlot(list[i].type, list[i].slotId, list[i].name, list[i].name, 
                    m_Type, Vector4.zero, ShaderStageCapability.All);

                m_Node.AddSlot(slot);
                validIDs.Add(list[i].slotId);
            }

            validIDs.AddRange(otherValidIDs);
            m_Node.RemoveSlotsNameNotMatching(validIDs);
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

		public VisualElement CreateSettingsElement()
		{
			var container = new VisualElement();

            var inputListElement = new DynamicSlotListView(m_Node, this);
            if (inputListElement != null)
                container.Add(inputListElement);

            return container;
		}
	}

	// ----------------------------------------------------------------------------------------------------
    // Utils
    // ----------------------------------------------------------------------------------------------------

    static class DynamicSlotUtils
    {
        public static ReorderableList CreateReorderableList(DynamicSlotList list, string label, bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton) 
        {
            var reorderableList = new ReorderableList(list.list, typeof(DynamicSlotList.Entry), draggable, displayHeader, displayAddButton, displayRemoveButton);

            reorderableList.drawHeaderCallback = (Rect rect) => 
            {  
                var labelRect = new Rect(rect.x, rect.y, rect.width-10, rect.height);
                EditorGUI.LabelField(labelRect, label);
            };

            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => 
            {
                var element = list.list[index];
                rect.y += 2;
                CreateEntry(list.list, index, element, new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight));
            };

            reorderableList.elementHeightCallback = (int indexer) => 
            {
                return reorderableList.elementHeight;
            };

            reorderableList.onAddCallback += AddItem;
            reorderableList.onRemoveCallback += RemoveItem;
            return reorderableList;
        }

        private static void CreateEntry(List<DynamicSlotList.Entry> list, int index, DynamicSlotList.Entry entry, Rect rect)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            list[index].name = EditorGUI.TextField( new Rect(rect.x, rect.y, rect.width / 2, EditorGUIUtility.singleLineHeight), entry.name, labelStyle);
            list[index].type = (SlotValueType)EditorGUI.EnumPopup( new Rect(rect.x + rect.width / 2, rect.y, rect.width / 2, EditorGUIUtility.singleLineHeight), entry.type);
        }

        private static void AddItem(ReorderableList list)
        {
            list.list.Add(new DynamicSlotList.Entry());
        }

        private static void RemoveItem(ReorderableList list)
        {
            list.list.RemoveAt(list.index);
        }
    }
}
