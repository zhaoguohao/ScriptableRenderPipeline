using System;
using UnityEngine;

#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif


namespace UnityEditor.ShaderGraph.Drawing.Slots
{
    public class LabelSlotControlView : VisualElement
    {
        public LabelSlotControlView(string label)
        {
            var labelField = new Label(label);
            Add(labelField);
        }
    }
}
