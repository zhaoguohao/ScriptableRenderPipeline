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
		List<MaterialSlot> m_InputSlots;

        //[DynamicSlotListControl(SlotType.Input)]
		public List<MaterialSlot> inputSlots
		{
			get 
            { 
                if(m_InputSlots == null)
                    m_InputSlots = new List<MaterialSlot>();
                return m_InputSlots; 
            }
		}

        [SerializeField]
		List<MaterialSlot> m_OutputSlots;
        
        //[DynamicSlotListControl(SlotType.Output)]
		public List<MaterialSlot> outputSlots
		{
			get 
            {
                if(m_OutputSlots == null)
                    m_OutputSlots = new List<MaterialSlot>(); 
                return m_OutputSlots; 
            }
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
            UpdateNodeAfterDeserialization();
        }

        public sealed override void UpdateNodeAfterDeserialization()
        {
            Debug.Log(string.Format("Deserializing {0} input slots", inputSlots.Count));
            List<int> validSlots = new List<int>();

            foreach(MaterialSlot slot in inputSlots)
            {
                AddSlot(slot);
                validSlots.Add(slot.id);
            }
                
            foreach(MaterialSlot slot in outputSlots)
            {
                AddSlot(slot);
                validSlots.Add(slot.id);
            }

            //RemoveSlotsNameNotMatching(validSlots);
        } 

		string GetFunctionName()
        {
            return string.Format("Unity_CustomCode_{0}", precision);
        }

		public void GenerateNodeCode(ShaderGenerator visitor, GraphContext context, GenerationMode generationMode)
        {
            Debug.Log(string.Format("Generating from {0} input slots", inputSlots.Count));
			var arguments = new List<string>();

            //Debug.Log(inputSlots.Count);

            for(int i = 0; i < inputSlots.Count; i++)
                arguments.Add(GetSlotValue(inputSlots[i].id, generationMode));

			for(int i = 0; i < outputSlots.Count; i++)
                arguments.Add(GetVariableNameForSlot(outputSlots[i].id));

            if(arguments.Count == 0)
                return;

            for(int i = 0; i < outputSlots.Count; i++)
            {
                visitor.AddShaderChunk(string.Format("{0} {1};", 
                    FindOutputSlot<MaterialSlot>(outputSlots[i].id).concreteValueType.ToString(precision), 
                    GetVariableNameForSlot(outputSlots[i].id)), false);
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
            
			for(int i = 0; i < inputSlots.Count; i++)
			{
                MaterialSlot slot = FindInputSlot<MaterialSlot>(inputSlots[i].id);
				arguments.Add(string.Format("{0} {1}", slot.concreteValueType.ToString(precision), slot.shaderOutputName));
			}

			for(int i = 0; i < outputSlots.Count; i++)
			{
                MaterialSlot slot = FindOutputSlot<MaterialSlot>(outputSlots[i].id);
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
            container.Add(new DynamicSlotListView(this, inputSlots, SlotType.Input));
            container.Add(new DynamicSlotListView(this, outputSlots, SlotType.Output));
            return container;
        }
    }
}

