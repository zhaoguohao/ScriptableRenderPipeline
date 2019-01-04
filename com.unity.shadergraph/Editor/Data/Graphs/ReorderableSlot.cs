using System;
using UnityEngine;
using UnityEditor.Graphing;

namespace UnityEditor.ShaderGraph
{
    [Serializable]
    class ReorderableSlot
    {
        [SerializeField] public int id;
        [SerializeField] public string name;
        [SerializeField] public SlotType slotType;
        [SerializeField] public SlotValueType valueType;
        [SerializeField] public int interfaceType;
        [SerializeField] public ShaderStageCapability stageCapability;

        public ReorderableSlot(int id, string name, SlotType slotType, SlotValueType valueType, 
            int interfaceType = 0, ShaderStageCapability stageCapability = ShaderStageCapability.All)
        {
            this.id = id;
            this.name = name;
            this.slotType = slotType;
            this.valueType = valueType;
            this.interfaceType = interfaceType;
            this.stageCapability = stageCapability;
        }
    }
}
