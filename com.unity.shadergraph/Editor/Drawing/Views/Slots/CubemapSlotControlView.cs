using System;
using UnityEditor.Graphing;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_2019_1_OR_NEWER
using UnityEditor.UIElements;
using UnityEngine.UIElements;
#else
using UnityEditor.Experimental.UIElements;
using UnityEngine.Experimental.UIElements;
#endif

namespace UnityEditor.ShaderGraph.Drawing.Slots
{
    public class CubemapSlotControlView : VisualElement
    {
        CubemapInputMaterialSlot m_Slot;

        public CubemapSlotControlView(CubemapInputMaterialSlot slot)
        {
            AddStyleSheetPath("Styles/Controls/CubemapSlotControlView");
            m_Slot = slot;
            var objectField = new ObjectField { objectType = typeof(Cubemap), value = m_Slot.cubemap };
            objectField.OnValueChanged(OnValueChanged);
            Add(objectField);
        }

        void OnValueChanged(ChangeEvent<Object> evt)
        {
            var cubemap = evt.newValue as Cubemap;
            if (cubemap != m_Slot.cubemap)
            {
                m_Slot.owner.owner.owner.RegisterCompleteObjectUndo("Change Cubemap");
                m_Slot.cubemap = cubemap;
                m_Slot.owner.Dirty(ModificationScope.Node);
            }
        }
    }
}
