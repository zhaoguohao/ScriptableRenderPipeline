using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph
{
    [Title("Utility", "Custom Function")]
    class CustomFunctionNode : AbstractMaterialNode
        , IHasSettings
        , IGeneratesBodyCode
        , IGeneratesFunction
        , IMayRequireNormal
        , IMayRequireTangent
        , IMayRequireBitangent
        , IMayRequireMeshUV
        , IMayRequireScreenPosition
        , IMayRequireViewDirection
        , IMayRequirePosition
        , IMayRequireVertexColor
    {
        [SerializeField]
		List<ReorderableSlot> m_InputSlots = new List<ReorderableSlot>() 
        {
            new ReorderableSlot(0, "In", SlotType.Input, SlotValueType.DynamicVector, 0, ShaderStageCapability.All)
        };

		public List<ReorderableSlot> inputSlots
		{
			get 
            { 
                if(m_InputSlots == null)
                    m_InputSlots = new List<ReorderableSlot>();

                return m_InputSlots; 
            }
		}

        [SerializeField]
		List<ReorderableSlot> m_OutputSlots = new List<ReorderableSlot>() 
        {
            new ReorderableSlot(1, "Out", SlotType.Output, SlotValueType.DynamicVector, 0, ShaderStageCapability.All)
        };
        
		public List<ReorderableSlot> outputSlots
		{
			get 
            {
                if(m_OutputSlots == null)
                    m_OutputSlots = new List<ReorderableSlot>();

                return m_OutputSlots; 
            }
		}

		[SerializeField]
		private string m_Code = "Out = In;";

		[TextFieldControl("", true)]
		public string code
		{
			get { return m_Code; }
			set { m_Code = value; }
		}

        ButtonConfig m_ButtonConfig;

        [ButtonControl]
        public ButtonConfig buttonConfig
        {
            get { return m_ButtonConfig; }
        }

        public override bool hasPreview { get { return true; } }

        public CustomFunctionNode()
        {
            name = "Custom Function";
            UpdateNodeAfterDeserialization();

            m_ButtonConfig = new ButtonConfig()
            {
                text = "Recompile",
                action = () =>
                {
                    Dirty(ModificationScope.Topological); 
                }
            };
        }

        public override void ValidateNode()
        {
            List<int> validSlots = new List<int>();
            UpdateSlotList(inputSlots, ref validSlots);
            UpdateSlotList(outputSlots, ref validSlots);
            RemoveSlotsNameNotMatching(validSlots);

            base.ValidateNode();
        }

        private void UpdateSlotList(List<ReorderableSlot> slots, ref List<int> validSlots)
        {
            foreach(ReorderableSlot entry in slots)
            {
                // If new Slot generate a valid ID and name
                if(entry.id == -1)
                {
                    entry.id = GetNewSlotID();
                    entry.name = GetNewSlotName();
                }

                // Add to Node
                AddSlot(entry.ToMaterialSlot());
                validSlots.Add(entry.id);
            }
        }

        private int GetNewSlotID()
        {
            // Track highest Slot ID
            int ceiling = -1;
            
            // Get all Slots from Node
            List<MaterialSlot> slots = new List<MaterialSlot>();
            GetSlots(slots);

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
            GetSlots(slots);

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

		string GetFunctionName()
        {
            return string.Format("Unity_CustomCode_{0}_{1}", GuidEncoder.Encode(guid), precision);
        }

		public void GenerateNodeCode(ShaderGenerator visitor, GraphContext context, GenerationMode generationMode)
        {
			var arguments = new List<string>();

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

        public NeededCoordinateSpace RequiresNormal(ShaderStageCapability stageCapability)
        {
            var binding = NeededCoordinateSpace.None;
            s_TempSlots.Clear();
            GetInputSlots(s_TempSlots);
            foreach (var slot in s_TempSlots)
                binding |= slot.RequiresNormal();
            return binding;
        }

        public NeededCoordinateSpace RequiresViewDirection(ShaderStageCapability stageCapability)
        {
            var binding = NeededCoordinateSpace.None;
            s_TempSlots.Clear();
            GetInputSlots(s_TempSlots);
            foreach (var slot in s_TempSlots)
                binding |= slot.RequiresViewDirection();
            return binding;
        }

        public NeededCoordinateSpace RequiresPosition(ShaderStageCapability stageCapability)
        {
            s_TempSlots.Clear();
            GetInputSlots(s_TempSlots);
            var binding = NeededCoordinateSpace.None;
            foreach (var slot in s_TempSlots)
                binding |= slot.RequiresPosition();
            return binding;
        }

        public NeededCoordinateSpace RequiresTangent(ShaderStageCapability stageCapability)
        {
            s_TempSlots.Clear();
            GetInputSlots(s_TempSlots);
            var binding = NeededCoordinateSpace.None;
            foreach (var slot in s_TempSlots)
                binding |= slot.RequiresTangent();
            return binding;
        }

        public NeededCoordinateSpace RequiresBitangent(ShaderStageCapability stageCapability)
        {
            s_TempSlots.Clear();
            GetInputSlots(s_TempSlots);
            var binding = NeededCoordinateSpace.None;
            foreach (var slot in s_TempSlots)
                binding |= slot.RequiresBitangent();
            return binding;
        }

        public bool RequiresMeshUV(UVChannel channel, ShaderStageCapability stageCapability)
        {
            s_TempSlots.Clear();
            GetInputSlots(s_TempSlots);
            foreach (var slot in s_TempSlots)
            {
                if (slot.RequiresMeshUV(channel))
                    return true;
            }
            return false;
        }

        public bool RequiresScreenPosition(ShaderStageCapability stageCapability)
        {
            s_TempSlots.Clear();
            GetInputSlots(s_TempSlots);
            foreach (var slot in s_TempSlots)
            {
                if (slot.RequiresScreenPosition())
                    return true;
            }
            return false;
        }

        public bool RequiresVertexColor(ShaderStageCapability stageCapability)
        {
            s_TempSlots.Clear();
            GetInputSlots(s_TempSlots);
            foreach (var slot in s_TempSlots)
            {
                if (slot.RequiresVertexColor())
                    return true;
            }
            return false;
        }

        public VisualElement CreateSettingsElement()
        {
            PropertySheet ps = new PropertySheet();
            ps.style.width = 500;
            ps.Add(new ReorderableSlotListView(this, inputSlots, SlotType.Input));
            ps.Add(new ReorderableSlotListView(this, outputSlots, SlotType.Output));
            return ps;
        }
    }
}