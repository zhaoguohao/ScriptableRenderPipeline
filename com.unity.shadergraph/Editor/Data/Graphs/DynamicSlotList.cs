using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEngine.UIElements;
using UnityEditor.ShaderGraph.Drawing;

namespace UnityEditor.ShaderGraph
{
	// ----------------------------------------------------------------------------------------------------
    // Class
    // ----------------------------------------------------------------------------------------------------

	[Serializable]
	class DynamicSlotList
	{
        [Serializable]
		public class Slot
		{
            public int slotId;
			public string name;
			public SlotValueType type;

            public Slot()
            {
                slotId = -1;
                name = "New Slot";
                type = SlotValueType.Vector1;
            }
		}

		public DynamicSlotList(AbstractMaterialNode node, SlotType type)
		{
			m_Node = node;
			m_SlotType = type;

            m_Slots = new List<Slot>();
            m_ValidSlots = new List<int>();
		}

		private AbstractMaterialNode m_Node;

        [SerializeField]
		private SlotType m_SlotType;

		public SlotType slotType
		{
			get { return m_SlotType; }
		}

        [SerializeField]
		private List<Slot> m_Slots;

		public List<Slot> slots
		{
			get { return m_Slots; }
		}

        [SerializeField]
        private List<int> m_ValidSlots;

        public List<int> validSlots
        {
            get { return m_ValidSlots; }
        }

        public void UpdateSlots()
        {
            validSlots.Clear();

            for(int i = 0; i < slots.Count; i++)
            {
                if(slots[i].slotId == -1)
                    slots[i].slotId = GetNewSlotID();
                
                MaterialSlot slot = MaterialSlot.CreateMaterialSlot(slots[i].type, slots[i].slotId, slots[i].name, slots[i].name, 
                    m_SlotType, Vector4.zero, ShaderStageCapability.All);

                m_Node.AddSlot(slot);
                validSlots.Add(slot.id);
            }
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

            var listView = new DynamicSlotListView(m_Node, this);
            if (listView != null)
                container.Add(listView);

            return container;
		}
	}
}
