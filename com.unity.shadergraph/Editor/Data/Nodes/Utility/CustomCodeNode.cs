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
    using Vector2Interface = SerializableSlotUtil.Vector2Interface;
    using Vector3Interface = SerializableSlotUtil.Vector3Interface;
    using Vector4Interface = SerializableSlotUtil.Vector4Interface;

    [Title("Custom Code")]
    class CustomCodeNode : AbstractMaterialNode
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
        SerializableSlot[] m_SerializableInputSlots = { new SerializableSlot(0, "In", SlotType.Input, SlotValueType.Vector1, 0) };

        [SerializeField]
		List<MaterialSlot> m_InputSlots;

        //[DynamicSlotListControl(SlotType.Input)]
		public List<MaterialSlot> inputSlots
		{
			get 
            { 
                if (m_SerializableInputSlots != null)
                {
                    m_InputSlots = new List<MaterialSlot>();
                    for(int i = 0; i < m_SerializableInputSlots.Length; i++)
                        m_InputSlots.Add(m_SerializableInputSlots[i].Deserialize());
                    m_SerializableInputSlots = null;
                }
                return m_InputSlots; 
            }
		}

        [SerializeField]
        SerializableSlot[] m_SerializableOutputSlots = { new SerializableSlot(1, "Out", SlotType.Output, SlotValueType.Vector1, 0) };

        [SerializeField]
		List<MaterialSlot> m_OutputSlots;
        
        //[DynamicSlotListControl(SlotType.Output)]
		public List<MaterialSlot> outputSlots
		{
			get 
            {
                if (m_SerializableOutputSlots != null)
                {
                    m_OutputSlots = new List<MaterialSlot>();
                    for(int i = 0; i < m_SerializableOutputSlots.Length; i++)
                        m_OutputSlots.Add(m_SerializableOutputSlots[i].Deserialize());
                    m_SerializableOutputSlots = null;
                }
                return m_OutputSlots; 
            }
		}

		[SerializeField]
		private string m_Code = "Out = In;";

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

        [SerializeField]
        private bool m_DisplayPreview = true;

        public override bool hasPreview { get { return true; } }
        public override bool activePreview { get { return m_DisplayPreview; } }

        public CustomCodeNode()
        {
            name = "Custom Code";
            UpdateNodeAfterDeserialization();
        }

        public sealed override void UpdateNodeAfterDeserialization()
        {
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

            RemoveSlotsNameNotMatching(validSlots);
        } 

        public override void OnBeforeSerialize()
        {
            base.OnBeforeSerialize();
            if (m_InputSlots != null)
            {
                m_SerializableInputSlots = new SerializableSlot[m_InputSlots.Count];
                for(int i = 0; i < m_InputSlots.Count; i++)
                    m_SerializableInputSlots[i] = m_InputSlots[i].Serialize();
            }
            if (m_OutputSlots != null)
            {
                m_SerializableOutputSlots = new SerializableSlot[m_OutputSlots.Count];
                for(int i = 0; i < m_OutputSlots.Count; i++)
                    m_SerializableOutputSlots[i] = m_OutputSlots[i].Serialize();
            }
        }

		string GetFunctionName()
        {
            return string.Format("Unity_CustomCode_{0}", precision);
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
            ps.Add(new DynamicSlotListView(this, inputSlots, SlotType.Input));
            ps.Add(new DynamicSlotListView(this, outputSlots, SlotType.Output));
            ps.Add(new PropertyRow(new Label("Enable Preview")), (row) =>
                {
                    row.Add(new Toggle(), (toggle) =>
                    {
                        toggle.value = m_DisplayPreview;
                        toggle.OnToggleChanged(ChangePreview);
                    });
                });
            return ps;
        }

        void ChangePreview(ChangeEvent<bool> evt)
        {
            owner.owner.RegisterCompleteObjectUndo("Change Preview state");
            m_DisplayPreview = evt.newValue;
            Dirty(ModificationScope.Graph);
        }
    }

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

    static class SerializableSlotUtil
    {
        public static SerializableSlot Serialize(this MaterialSlot slot)
        {
            int interfaceType = slot.GetInterfaceType();
            return new SerializableSlot(slot.id, slot.RawDisplayName(), slot.slotType, slot.valueType, interfaceType);
        }

        public static MaterialSlot Deserialize(this SerializableSlot slot)
        {
            var slotId = slot.id;
            var displayName = slot.name;
            var slotType = slot.slotType;
            var valueType = slot.valueType;
            var shaderOutputName = NodeUtils.GetHLSLSafeName(slot.name);
            var interfaceType = slot.interfaceType;
            var shaderStageCapability = ShaderStageCapability.All;

            if(slotType == SlotType.Input)
            {
                if(valueType == SlotValueType.Vector4)
                {
                    var i = (Vector4Interface)interfaceType;
                    switch(i)
                    {
                        case Vector4Interface.ColorRGBA:
                            return new ColorRGBAMaterialSlot(slotId, displayName, shaderOutputName, slotType, Vector4.zero, shaderStageCapability);
                        case Vector4Interface.ScreenPosition:
                            return new ScreenPositionMaterialSlot(slotId, displayName, shaderOutputName, ScreenSpaceType.Default, shaderStageCapability);
                        case Vector4Interface.VertexColor:
                            return new VertexColorMaterialSlot(slotId, displayName, shaderOutputName, shaderStageCapability);
                        default:
                            return MaterialSlot.CreateMaterialSlot(valueType, slotId, displayName, shaderOutputName, slotType, Vector4.zero, ShaderStageCapability.All);
                    }
                }
                else if(valueType == SlotValueType.Vector3)
                {
                    var i = (Vector3Interface)interfaceType;
                    switch(i)
                    {
                        case Vector3Interface.ColorRGB:
                            return new ColorRGBMaterialSlot(slotId, displayName, shaderOutputName, slotType, Vector4.zero, ColorMode.Default, shaderStageCapability);
                        case Vector3Interface.ObjectSpaceNormal:
                            return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability);
                        case Vector3Interface.ObjectSpaceTangent:
                            return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability);
                        case Vector3Interface.ObjectSpaceBitangent:
                            return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability);
                        case Vector3Interface.ObjectSpacePosition:
                            return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability);
                        case Vector3Interface.ViewSpaceNormal:
                            return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability);
                        case Vector3Interface.ViewSpaceTangent:
                            return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability);
                        case Vector3Interface.ViewSpaceBitangent:
                            return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability);
                        case Vector3Interface.ViewSpacePosition:
                            return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability);
                        case Vector3Interface.WorldSpaceNormal:
                            return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability);
                        case Vector3Interface.WorldSpaceTangent:
                            return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability);
                        case Vector3Interface.WorldSpaceBitangent:
                            return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability);
                        case Vector3Interface.WorldSpacePosition:
                            return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability);
                        case Vector3Interface.TangentSpaceNormal:
                            return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability);
                        case Vector3Interface.TangentSpaceTangent:
                            return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability);
                        case Vector3Interface.TangentSpaceBitangent:
                            return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability);
                        case Vector3Interface.TangentSpacePosition:
                            return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability);
                        case Vector3Interface.ObjectSpaceViewDirection:
                            return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability);
                        case Vector3Interface.ViewSpaceViewDirection:
                            return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability);
                        case Vector3Interface.WorldSpaceViewDirection:
                            return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability);
                        case Vector3Interface.TangentSpaceViewDirection:
                            return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability);
                        default:
                            return MaterialSlot.CreateMaterialSlot(valueType, slotId, displayName, shaderOutputName, slotType, Vector4.zero, ShaderStageCapability.All);
                    }
                }
                else if(valueType == SlotValueType.Vector2)
                {
                    var i = (Vector2Interface)interfaceType;
                    switch(i)
                    {
                        case Vector2Interface.MeshUV:
                            return new UVMaterialSlot(slotId, displayName, shaderOutputName, UVChannel.UV0, shaderStageCapability);
                        default:
                            return MaterialSlot.CreateMaterialSlot(valueType, slotId, displayName, shaderOutputName, slotType, Vector4.zero, ShaderStageCapability.All);
                    }
                }
            }

            return MaterialSlot.CreateMaterialSlot(valueType, slotId, displayName, shaderOutputName, slotType, Vector4.zero, shaderStageCapability);
        }

        public static int GetInterfaceType(this MaterialSlot slot)
        {
            int interfaceType = 0;

            if(TryGetInterfaceType<ColorRGBAMaterialSlot>(slot, out interfaceType))
                return interfaceType;
            else if(TryGetInterfaceType<ScreenPositionMaterialSlot>(slot, out interfaceType))
                return interfaceType;
            else if(TryGetInterfaceType<VertexColorMaterialSlot>(slot, out interfaceType))
                return interfaceType;
            else if(TryGetInterfaceType<ColorRGBMaterialSlot>(slot, out interfaceType))
                return interfaceType;
            else if(TryGetInterfaceType<UVMaterialSlot>(slot, out interfaceType))
                return interfaceType;

            else if(slot as NormalMaterialSlot != null)
            {
                NormalMaterialSlot normalMaterialSlot = slot as NormalMaterialSlot;
                switch(normalMaterialSlot.space)
                {
                    case CoordinateSpace.World:
                        return (int)Vector3Interface.WorldSpaceNormal;
                    case CoordinateSpace.Tangent:
                        return (int)Vector3Interface.TangentSpaceNormal;
                    case CoordinateSpace.View:
                        return (int)Vector3Interface.ViewSpaceNormal;
                    default:
                        return (int)Vector3Interface.ObjectSpaceNormal;
                }
            }

            else if(slot as TangentMaterialSlot != null)
            {
                TangentMaterialSlot tangentMaterialSlot = slot as TangentMaterialSlot;
                switch(tangentMaterialSlot.space)
                {
                    case CoordinateSpace.World:
                        return (int)Vector3Interface.WorldSpaceTangent;
                    case CoordinateSpace.Tangent:
                        return (int)Vector3Interface.TangentSpaceTangent;
                    case CoordinateSpace.View:
                        return (int)Vector3Interface.ViewSpaceTangent;
                    default:
                        return (int)Vector3Interface.ObjectSpaceTangent;
                }
            }

            else if(slot as BitangentMaterialSlot != null)
            {
                BitangentMaterialSlot bitangentMaterialSlot = slot as BitangentMaterialSlot;
                switch(bitangentMaterialSlot.space)
                {
                    case CoordinateSpace.World:
                        return (int)Vector3Interface.WorldSpaceBitangent;
                    case CoordinateSpace.Tangent:
                        return (int)Vector3Interface.TangentSpaceBitangent;
                    case CoordinateSpace.View:
                        return (int)Vector3Interface.ViewSpaceBitangent;
                    default:
                        return (int)Vector3Interface.ObjectSpaceBitangent;
                }
            }

            else if(slot as PositionMaterialSlot != null)
            {
                PositionMaterialSlot positionMaterialSlot = slot as PositionMaterialSlot;
                switch(positionMaterialSlot.space)
                {
                    case CoordinateSpace.World:
                        return (int)Vector3Interface.WorldSpacePosition;
                    case CoordinateSpace.Tangent:
                        return (int)Vector3Interface.TangentSpacePosition;
                    case CoordinateSpace.View:
                        return (int)Vector3Interface.ViewSpacePosition;
                    default:
                        return (int)Vector3Interface.ObjectSpacePosition;
                }
            }

            else if(slot as ViewDirectionMaterialSlot != null)
            {
                ViewDirectionMaterialSlot viewDirectionMaterialSlot = slot as ViewDirectionMaterialSlot;
                switch(viewDirectionMaterialSlot.space)
                {
                    case CoordinateSpace.World:
                        return (int)Vector3Interface.WorldSpaceViewDirection;
                    case CoordinateSpace.Tangent:
                        return (int)Vector3Interface.TangentSpaceViewDirection;
                    case CoordinateSpace.View:
                        return (int)Vector3Interface.ViewSpaceViewDirection;
                    default:
                        return (int)Vector3Interface.ObjectSpaceViewDirection;
                }
            }
                
            else
                return 0;
        }

        public static bool TryGetInterfaceType<T>(MaterialSlot slot, out int interfaceType) where T : MaterialSlot
        {
            interfaceType = 0;
            if(slot as T == null)
                return false;

            if(s_SlotInterfaceType.TryGetValue(typeof(T), out interfaceType))
                return true;
            else
                return false;
        }

        public static Dictionary<Type, int> s_SlotInterfaceType = new Dictionary<Type, int>()
        {
            { typeof(ColorRGBAMaterialSlot), (int)Vector4Interface.ColorRGBA },
            { typeof(ScreenPositionMaterialSlot), (int)Vector4Interface.ScreenPosition },
            { typeof(VertexColorMaterialSlot), (int)Vector4Interface.VertexColor },
            { typeof(ColorRGBMaterialSlot), (int)Vector3Interface.ColorRGB },
            /*{ typeof(NormalMaterialSlot), (int)Vector3Interface.ObjectSpaceNormal },
            { typeof(TangentMaterialSlot), (int)Vector3Interface.ObjectSpaceTangent },
            { typeof(BitangentMaterialSlot), (int)Vector3Interface.ObjectSpaceBitangent },
            { typeof(PositionMaterialSlot), (int)Vector3Interface.ObjectSpacePosition },
            { typeof(ViewDirectionMaterialSlot), (int)Vector3Interface.ObjectSpaceViewDirection },*/
            { typeof(UVMaterialSlot), (int)Vector2Interface.MeshUV },
        };

        public static Dictionary<SlotValueType, Type> s_InterfaceTypes = new Dictionary<SlotValueType, Type>()
        {
            { SlotValueType.Vector4, typeof(Vector4Interface) },
            { SlotValueType.Vector3, typeof(Vector3Interface) },
            { SlotValueType.Vector2, typeof(Vector2Interface) },
        };

        // Get all enum entries of an InterfaceType from its corresponding SlotValueType
        public static string[] GetEnumEntriesOfInterfaceType(SlotValueType slotValueType)
        {
            Type interfaceType;
            if(s_InterfaceTypes.TryGetValue(slotValueType, out interfaceType))
                return Enum.GetNames(Type.GetType(interfaceType.ToString()));
            
            return new string[] { "Default" };
        }

        public enum Vector4Interface
        {
            Default,
            ColorRGBA,
            ScreenPosition,
            VertexColor,
        }

        public enum Vector3Interface
        {
            Default,
            ColorRGB,
            ObjectSpaceNormal,
            ObjectSpaceTangent,
            ObjectSpaceBitangent,
            ObjectSpacePosition,
            ViewSpaceNormal,
            ViewSpaceTangent,
            ViewSpaceBitangent,
            ViewSpacePosition,
            WorldSpaceNormal,
            WorldSpaceTangent,
            WorldSpaceBitangent,
            WorldSpacePosition,
            TangentSpaceNormal,
            TangentSpaceTangent,
            TangentSpaceBitangent,
            TangentSpacePosition,
            ObjectSpaceViewDirection,
            ViewSpaceViewDirection,
            WorldSpaceViewDirection,
            TangentSpaceViewDirection,
        }

        public enum Vector2Interface
        {
            Default,
            MeshUV,
        }
    }
}

