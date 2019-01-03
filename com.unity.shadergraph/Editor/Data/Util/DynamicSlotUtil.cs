using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;

namespace UnityEditor.ShaderGraph
{
    static class DynamicSlotUtil
    {
        public static SerializableSlot Serialize(this MaterialSlot slot)
        {
            int interfaceType = slot.GetInterfaceType();
            return new SerializableSlot(slot.id, slot.RawDisplayName(), slot.slotType, slot.valueType, interfaceType, slot.stageCapability);
        }

        public static MaterialSlot Deserialize(this SerializableSlot slot)
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
