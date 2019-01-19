using System;
using System.Collections.Generic;
using UnityEditor.Graphing;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.ShaderGraph.Drawing.Slots;

namespace UnityEditor.ShaderGraph
{
    class VectorControlType : ShaderControlType
    {
        static readonly string[] k_VectorComponentLabels = { "X", "Y", "Z", "W" };
        public Vector4 defaultValue;

        public override VisualElement GetControl(ShaderPort port)
        {
            var labels = k_VectorComponentLabels.Take(port.concreteValueType.GetChannelCount()).ToArray();
            return new MultiFloatSlotControlView(port.owner, labels, () => port.vectorValue, (newValue) => port.vectorValue = newValue);
        }

        public static ShaderControlType Vector2(this ShaderControlType ex)
        {
            return new VectorControlType() { defaultValue = UnityEngine.Vector4.zero };
        }

        public static ShaderControlType Vector2(Vector2 defaultValue)
        {
            return new VectorControlType() { defaultValue = new Vector4(defaultValue.x, defaultValue.y, 0, 0) };
        }

        public static ShaderControlType Vector3()
        {
            return new VectorControlType() { defaultValue = UnityEngine.Vector4.zero };
        }

        public static ShaderControlType Vector3(Vector3 defaultValue)
        {
            return new VectorControlType() { defaultValue = new Vector4(defaultValue.x, defaultValue.y, defaultValue.z, 0) };
        }

        public static ShaderControlType Vector4()
        {
            return new VectorControlType() { defaultValue = UnityEngine.Vector4.zero };
        }

        public static ShaderControlType Vector4(Vector4 defaultValue)
        {
            return new VectorControlType() { defaultValue = defaultValue };
        }
    }

    abstract class ShaderControlType
    {
        public abstract VisualElement GetControl(ShaderPort port);
    }

    [Serializable]
    class ShaderPort : MaterialSlot
    {
        [SerializeField]
        private SlotValueType m_ValueType = SlotValueType.Vector1;

        [SerializeField]
        private Vector4 m_DefaultVectorValue = Vector4.zero;

        [SerializeField]
        private Vector4 m_VectorValue = Vector4.zero;

        [SerializeField]
        ShaderControlType m_ControlType;        

        public ShaderPort(
            int slotId,
            string displayName,
            SlotType slotType,
            SlotValueType valueType,
            ShaderControlType controlType,
            ShaderStageCapability stageCapability = ShaderStageCapability.All)
            : base(slotId, displayName, displayName, slotType, stageCapability, false)
        {
            m_ValueType = valueType;
            m_ControlType = controlType;
        }

        public override SlotValueType valueType
        {
            get { return m_ValueType; }
        }

        public Vector4 defaultVectorValue
        {
            get { return m_DefaultVectorValue; }
            set { m_DefaultVectorValue = value; }
        }

        public Vector4 vectorValue
        {
            get { return m_VectorValue; }
            set { m_VectorValue = value; }
        }

        public override ConcreteSlotValueType concreteValueType
        {
            get
            {
                switch(m_ValueType)
                {
                    case SlotValueType.Vector4:
                        return ConcreteSlotValueType.Vector4;
                    case SlotValueType.Vector3:
                        return ConcreteSlotValueType.Vector3;
                    case SlotValueType.Vector2:
                        return ConcreteSlotValueType.Vector2;
                    case SlotValueType.Vector1:
                        return ConcreteSlotValueType.Vector1;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public override VisualElement InstantiateControl()
        {
            return m_ControlType.GetControl(this);
        }

        public override void GetPreviewProperties(List<PreviewProperty> properties, string name)
        {
            var propType = ConvertConcreteSlotValueTypeToPropertyType(concreteValueType);
            var pp = new PreviewProperty(propType) { name = name };

            switch(propType)
            {
                case PropertyType.Vector4:
                case PropertyType.Vector3:
                case PropertyType.Vector2:
                    pp.vector4Value = new Vector4(vectorValue.x, vectorValue.y, vectorValue.z, vectorValue.w);
                    break;
                case PropertyType.Vector1:
                    pp.floatValue = vectorValue.x;
                    break;
            }
                
            properties.Add(pp);
        }

        protected override string ConcreteSlotValueAsVariable(AbstractMaterialNode.OutputPrecision precision)
        {
            var channelCount = SlotValueHelper.GetChannelCount(concreteValueType);
            switch(concreteValueType)
            {
                case ConcreteSlotValueType.Vector1:
                    return NodeUtils.FloatToShaderValue(vectorValue.x);
                case ConcreteSlotValueType.Vector4:
                case ConcreteSlotValueType.Vector3:
                case ConcreteSlotValueType.Vector2:
                {
                    string values = NodeUtils.FloatToShaderValue(vectorValue.x);
                    for (var i = 1; i < channelCount; i++)
                        values += ", " + NodeUtils.FloatToShaderValue(vectorValue[i]);
                    return string.Format("{0}{1}({2})", precision, channelCount, values);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void AddDefaultProperty(PropertyCollector properties, GenerationMode generationMode)
        {
            if (!generationMode.IsPreview())
                return;

            var matOwner = owner as AbstractMaterialNode;
            if (matOwner == null)
                throw new Exception(string.Format("Slot {0} either has no owner, or the owner is not a {1}", this, typeof(AbstractMaterialNode)));

            IShaderProperty property;
            switch (concreteValueType)
            {
                case ConcreteSlotValueType.Vector4:
                    property = new Vector4ShaderProperty();
                    break;
                case ConcreteSlotValueType.Vector3:
                    property = new Vector3ShaderProperty();
                    break;
                case ConcreteSlotValueType.Vector2:
                    property = new Vector2ShaderProperty();
                    break;
                case ConcreteSlotValueType.Vector1:
                    property = new Vector1ShaderProperty();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            property.overrideReferenceName = matOwner.GetVariableNameForSlot(id);
            property.generatePropertyBlock = false;
            properties.AddShaderProperty(property);
        }

        public override void CopyValuesFrom(MaterialSlot foundSlot)
        {
            var slot = foundSlot as ShaderPort;
            if (slot != null)
            {
                vectorValue = slot.vectorValue;
            }
        }
    }
}

