using System;

namespace UnityEditor.ShaderGraph
{
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
