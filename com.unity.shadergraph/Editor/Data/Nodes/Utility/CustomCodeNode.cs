using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph
{
    [Title("Custom Code")]
    class CustomCodeNode : AbstractMaterialNode, IGeneratesFunction, IGeneratesBodyCode, IHasSettings
    {
        [SerializeField]
		DynamicSlotList m_InputSlotList;

        //[DynamicSlotListControl(SlotType.Input)]
		public DynamicSlotList inputSlotList
		{
			get { return m_InputSlotList; }
			set { m_InputSlotList = value; }
		}

        [SerializeField]
		DynamicSlotList m_OutputSlotList;
        
        //[DynamicSlotListControl(SlotType.Output)]
		public DynamicSlotList outputSlotList
		{
			get { return m_OutputSlotList; }
			set { m_OutputSlotList = value; }
		}

		[SerializeField]
		private string m_Code;

		[TextFieldControl("", true)]
		public string code
		{
			get { return m_Code; }
			set 
			{ 
				m_Code = value;
				Dirty(ModificationScope.Topological); 
			}
		}

        public override bool hasPreview { get { return true; } }

        public CustomCodeNode()
        {
            name = "Custom Code";
            m_InputSlotList = new DynamicSlotList(this, SlotType.Input);
            m_OutputSlotList = new DynamicSlotList(this, SlotType.Output);
            UpdateNodeAfterDeserialization();
        }

		string GetFunctionName()
        {
            return string.Format("Unity_CustomCode_{0}", precision);
        }

		public void GenerateNodeCode(ShaderGenerator visitor, GraphContext context, GenerationMode generationMode)
        {
			var arguments = new List<string>();

            for(int i = 0; i < inputSlotList.list.Count; i++)
                arguments.Add(GetSlotValue(inputSlotList.list[i].slotId, generationMode));

			for(int i = 0; i < outputSlotList.list.Count; i++)
                arguments.Add(GetVariableNameForSlot(outputSlotList.list[i].slotId));

            if(arguments.Count == 0)
                return;

            for(int i = 0; i < outputSlotList.list.Count; i++)
            {
                visitor.AddShaderChunk(string.Format("{0} {1};", 
                    FindOutputSlot<MaterialSlot>(outputSlotList.list[i].slotId).concreteValueType.ToString(precision), 
                    GetVariableNameForSlot(outputSlotList.list[i].slotId)), false);
            }

			visitor.AddShaderChunk(
                string.Format("{0}({1});"
                    , GetFunctionName()
                    , arguments.Aggregate((current, next) => string.Format("{0}, {1}", current, next)))
                , false);
        }

		public void GenerateNodeFunction(FunctionRegistry registry, GraphContext graphContext, GenerationMode generationMode)
        {
			var arguments = new List<string>();
            
			for(int i = 0; i < inputSlotList.list.Count; i++)
			{
                MaterialSlot slot = FindInputSlot<MaterialSlot>(inputSlotList.list[i].slotId);
				arguments.Add(string.Format("{0} {1}", slot.concreteValueType.ToString(precision), slot.shaderOutputName));
			}

			for(int i = 0; i < outputSlotList.list.Count; i++)
			{
                MaterialSlot slot = FindOutputSlot<MaterialSlot>(outputSlotList.list[i].slotId);
				arguments.Add(string.Format("out {0} {1}", slot.concreteValueType.ToString(precision), slot.shaderOutputName));
			}

            if(arguments.Count == 0)
                return;

			var argumentString = string.Format("{0}({1})"
                    , GetFunctionName()
                    , arguments.Aggregate((current, next) => string.Format("{0}, {1}", current, next)));

            registry.ProvideFunction(GetFunctionName(), s =>
                {
                    s.AppendLine("void {0}", argumentString);
                    using (s.BlockScope())
                    {
                        s.AppendLine(code);
                    }
                });
        }

        public VisualElement CreateSettingsElement()
        {
            VisualElement container = new VisualElement();
            container.Add(inputSlotList.CreateSettingsElement());
            container.Add(outputSlotList.CreateSettingsElement());
            return container;
        }
    }
}

