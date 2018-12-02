using System;

namespace UnityEditor.ShaderGraph
{
    public interface INodeControlType
    {
        void InstantiateControl(int id);
    }

    // this serves as the 'template' for a slider control -- it can instantiate the actual control on a node
    public class NodeSliderControlType : INodeControlType
    {
        internal float minValue;
        internal float maxValue;
        internal float defaultValue;

        public NodeSliderControlType(float minValue, float maxValue, float defaultValue)
        {
            this.minValue = minValue;
            this.maxValue = maxValue;
            this.defaultValue = defaultValue;
        }

        void INodeControlType.InstantiateControl(int id)
        {
            // ... build the actual control on the actual node
            // 
        }
    }

    // This should probably be replaced by INodeControlType
    // or something similar... so it can be customized
    public class ControlType
    {
        // stuff goes here
        public float defaultValue;
    };

    public class Control
    {
        internal int id;
        internal string displayName;
        internal PortValueType valueType;
        internal INodeControlType controlType;
        internal int controlIndex;      // registered index

        public Control(int id, string displayName, PortValueType valueType, INodeControlType controlType)
        {
            this.id = id;
            this.displayName = displayName;
            this.valueType = valueType;
            this.controlType = controlType;
            this.controlIndex = -1;     // not registered yet, invalid index
        }

        public static INodeControlType Slider(float defaultValue, float minValue, float maxValue)
        {
            return new NodeSliderControlType(minValue, maxValue, defaultValue);
        }
    };

    public class InputPort
    {
        internal int id;
        internal string displayName;
        internal PortValue defaultValue;
        internal InputPortRef inputPortRef;

        public InputPort(int id, string displayName, PortValue defaultValue)
        {
            this.id = id;
            this.displayName = displayName;
            this.defaultValue = defaultValue;
            this.inputPortRef = new InputPortRef(0);     // set invalid (not registered yet)
        }
    }

    public class OutputPort
    {
        internal int id;
        internal string displayName;
        internal PortValueType portType;
        internal OutputPortRef outputPortRef;

        public OutputPort(int id, string displayName, PortValueType portType)
        {
            this.id = id;
            this.displayName = displayName;
            this.portType = portType;
            this.outputPortRef = new OutputPortRef(0);     // set invalid (not registered yet)
        }
    }

    [Serializable]
    public struct InputPortRef      // TODO: not public, or merge with InputPort?
    {
        internal int value { get; }
        internal int index => value - 1;

        internal InputPortRef(int value)
        {
            this.value = value;
        }

        internal bool isValid => value > 0;
    }

    [Serializable]
    public struct OutputPortRef     // TODO: not public, or merge with OutputPort?
    {
        internal int value { get; }
        internal int index => value - 1;

        internal OutputPortRef(int value)
        {
            this.value = value;
        }

        internal bool isValid => value > 0;
    }
}
