using System;
using System.Linq;
using UnityEditor.Graphing;

namespace UnityEditor.ShaderGraph
{
    internal static class InputDescriptorExtensions
    {
        internal static IShaderProperty InputToShaderProperty(this InputDescriptor input)
        {
            var propType = input.valueType.ToPropertyType();
            var propName = input.name;
            var propGuid = input.guid;
            var propValue = input.defaultValue;
            
            switch (propType)
            {
                case PropertyType.Vector1:
                    var vector1Prop = new Vector1ShaderProperty();
                    vector1Prop.displayName = propName;
                    vector1Prop.value = propValue.vectorValue.x;
                    return vector1Prop;
                case PropertyType.Vector2:
                    var vector2Prop = new Vector2ShaderProperty();
                    vector2Prop.displayName = propName;
                    vector2Prop.value = propValue.vectorValue;
                    return vector2Prop;
                case PropertyType.Vector3:
                    var vector3Prop = new Vector3ShaderProperty();
                    vector3Prop.displayName = propName;
                    vector3Prop.value = propValue.vectorValue;
                    return vector3Prop;
                case PropertyType.Vector4:
                    var vector4Prop = new Vector4ShaderProperty();
                    vector4Prop.displayName = propName;
                    vector4Prop.value = propValue.vectorValue;
                    return vector4Prop;
                default:
                    throw new ArgumentOutOfRangeException();
            }           
            
        }
    }
}
