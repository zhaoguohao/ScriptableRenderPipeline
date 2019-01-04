using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;

namespace UnityEditor.ShaderGraph
{
    static class ReorderableSlotListUtil
    {
        public static void UpdateSlotList(INode node, List<ReorderableSlot> slots, ref List<int> validSlots)
        {
            foreach(ReorderableSlot entry in slots)
            {
                // If new Slot generate a valid ID and name
                if(entry.id == -1)
                {
                    entry.id = GetNewSlotID(node);
                    entry.name = GetNewSlotName(node);
                }

                // Add to Node
                node.AddSlot(entry.ToMaterialSlot());
                validSlots.Add(entry.id);
            }
        }

        private static int GetNewSlotID(INode node)
        {
            // Track highest Slot ID
            int ceiling = -1;
            
            // Get all Slots from Node
            List<MaterialSlot> slots = new List<MaterialSlot>();
            node.GetSlots(slots);

            // Increment highest Slot ID from Slots on Node
            foreach(MaterialSlot slot in slots)
                ceiling = slot.id > ceiling ? slot.id : ceiling;

            return ceiling + 1;
        }

        private static string GetNewSlotName(INode node)
        {
            // Track highest number of unnamed Slots
            int ceiling = 0;

            // Get all Slots from Node
            List<MaterialSlot> slots = new List<MaterialSlot>();
            node.GetSlots(slots);

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

        public static MaterialSlot ToMaterialSlot(this ReorderableSlot slot)
        {
            var slotId = slot.id;
            var displayName = slot.name;
            var slotType = slot.slotType;
            var valueType = slot.valueType;
            var shaderOutputName = NodeUtils.GetHLSLSafeName(slot.name);
            var interfaceType = slot.interfaceType;
            var shaderStageCapability = slot.stageCapability;

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

        public static string[] GetEnumEntriesOfInterfaceType(SlotValueType slotValueType)
        {
            Type interfaceType;
            if(s_InterfaceTypes.TryGetValue(slotValueType, out interfaceType))
                return Enum.GetNames(Type.GetType(interfaceType.ToString()));
            
            return new string[] { "Default" };
        }

        public static Dictionary<SlotValueType, Type> s_InterfaceTypes = new Dictionary<SlotValueType, Type>()
        {
            { SlotValueType.Vector4, typeof(Vector4Interface) },
            { SlotValueType.Vector3, typeof(Vector3Interface) },
            { SlotValueType.Vector2, typeof(Vector2Interface) },
        };
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
