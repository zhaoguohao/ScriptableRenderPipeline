using System;
using UnityEngine;
using UnityEditor.Graphing;

namespace UnityEditor.ShaderGraph
{
    [Serializable]
    class SerializableSlot
    {
        [SerializeField] public int id;
        [SerializeField] public string name;
        [SerializeField] public SlotType slotType;
        [SerializeField] public SlotValueType valueType;
        [SerializeField] public int interfaceType;

        public SerializableSlot()
        {
        }

        public SerializableSlot(int id, string name, SlotType slotType, SlotValueType valueType, int interfaceType)
        {
            this.id = id;
            this.name = name;
            this.slotType = slotType;
            this.valueType = valueType;
            this.interfaceType = interfaceType;
        }
    }
}
