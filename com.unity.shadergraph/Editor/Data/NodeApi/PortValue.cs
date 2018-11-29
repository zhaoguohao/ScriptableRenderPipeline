using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    [StructLayout(LayoutKind.Explicit)]
    public struct PortValue
    {
        [FieldOffset(0)]
        PortValueType m_Type;

        [FieldOffset(4)]
        Vector4 m_Vector;

        [FieldOffset(8)]
        Matrix4x4 m_Matrix;

        [FieldOffset(12)]
        Texture m_Texture;

        PortValue(PortValueType type)
            : this()
        {
            m_Type = type;
        }

        public static PortValue Vector1(float value) => new PortValue(PortValueType.Vector1) { m_Vector = new Vector4(value, 0) };

        public static PortValue Vector2(Vector2 value) => new PortValue(PortValueType.Vector2) { m_Vector = value };

        public static PortValue Vector3() => new PortValue(PortValueType.Vector3) { m_Vector = UnityEngine.Vector3.zero };

        public static PortValue Vector3(Vector3 value) => new PortValue(PortValueType.Vector3) { m_Vector = value };

        public static PortValue Vector4(Vector4 value) => new PortValue(PortValueType.Vector4) { m_Vector = value };
        
        public static PortValue DynamicVector(float value) => new PortValue(PortValueType.DynamicVector) { m_Vector = new Vector4(value, 0) };

        public static PortValue Matrix2x2() => new PortValue(PortValueType.Matrix2x2) { m_Matrix = UnityEngine.Matrix4x4.identity };

        public static PortValue Matrix3x3() => new PortValue(PortValueType.Matrix3x3) { m_Matrix = UnityEngine.Matrix4x4.identity };

        public static PortValue Matrix4x4() => new PortValue(PortValueType.Matrix4x4) { m_Matrix = UnityEngine.Matrix4x4.identity };

        public static PortValue DynamicMatrix() => new PortValue(PortValueType.DynamicMatrix) { m_Matrix = UnityEngine.Matrix4x4.identity };

        public static PortValue DynamicValue(float value) => new PortValue(PortValueType.DynamicValue) { m_Vector = new Vector4(value, 0) };

        public static PortValue Texture2D(Texture2D texture) => new PortValue(PortValueType.Texture2D) { m_Texture = texture };

        public static PortValue Texture3D(Texture3D texture) => new PortValue(PortValueType.Texture3D) { m_Texture = texture };

        public static PortValue Texture2DArray(Texture2DArray texture) => new PortValue(PortValueType.Texture2DArray) { m_Texture = texture };

        public static PortValue Cubemap(Cubemap texture) => new PortValue(PortValueType.Cubemap) { m_Texture = texture };

        public static PortValue SamplerState() => new PortValue(PortValueType.SamplerState);

        public PortValueType type => m_Type;

        public float vector1Value => m_Vector.x;

        public Vector2 vector2Value => m_Vector;

        public Vector3 vector3Value => m_Vector;

        public Vector4 vector4Value => m_Vector;

        public Matrix4x4 matrix4x4Value => m_Matrix;

        public Texture2D texture2DValue => (Texture2D)m_Texture;

        public Texture3D texture3DValue => (Texture3D)m_Texture;

        public Texture2DArray texture2DArrayValue => (Texture2DArray)m_Texture;

        public Cubemap cubemapValue => (Cubemap)m_Texture;

        public override string ToString()
        {
            string value;
            switch (type)
            {
                case PortValueType.Vector1:
                    value = vector1Value.ToString();
                    break;
                case PortValueType.Vector2:
                    value = vector2Value.ToString();
                    break;
                case PortValueType.Vector3:
                    value = vector3Value.ToString();
                    break;
                case PortValueType.Vector4:
                    value = vector4Value.ToString();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return $"type={type}, value={value}";
        }
    }
}
