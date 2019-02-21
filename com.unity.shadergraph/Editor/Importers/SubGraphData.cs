using System;
using System.Collections.Generic;
using UnityEditor.Graphing;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    [Serializable]
    class SubGraphData : ISerializationCallbackReceiver
    {
        public bool isValid;
        
        public long processedAt;

        public string functionName;

        public string inputStructName;

        public string hlslName;

        public string assetGuid;

        public ShaderGraphRequirements requirements;

        public string path;

        public List<string> functionNames = new List<string>();

        [NonSerialized]
        public List<AbstractShaderProperty> inputs = new List<AbstractShaderProperty>();
        
        [SerializeField]
        List<SerializationHelper.JSONSerializedElement> m_SerializedInputs = new List<SerializationHelper.JSONSerializedElement>();
        
        [NonSerialized]
        public List<AbstractShaderProperty> properties = new List<AbstractShaderProperty>();
        
        [SerializeField]
        List<SerializationHelper.JSONSerializedElement> m_SerializedProperties = new List<SerializationHelper.JSONSerializedElement>();
        
        [NonSerialized]
        public List<MaterialSlot> outputs = new List<MaterialSlot>();
        
        [SerializeField]
        List<SerializationHelper.JSONSerializedElement> m_SerializedOutputs = new List<SerializationHelper.JSONSerializedElement>();

        public List<string> subGraphGuids = new List<string>();

        public ShaderStageCapability effectiveShaderStage;
        
        public void OnBeforeSerialize()
        {
            m_SerializedInputs = SerializationHelper.Serialize<AbstractShaderProperty>(inputs);
            m_SerializedProperties = SerializationHelper.Serialize<AbstractShaderProperty>(properties);
            m_SerializedOutputs = SerializationHelper.Serialize<MaterialSlot>(outputs);
        }

        public void OnAfterDeserialize()
        {
            var typeSerializationInfos = GraphUtil.GetLegacyTypeRemapping();
            inputs = SerializationHelper.Deserialize<AbstractShaderProperty>(m_SerializedInputs, typeSerializationInfos);
            properties = SerializationHelper.Deserialize<AbstractShaderProperty>(m_SerializedProperties, typeSerializationInfos);
            outputs = SerializationHelper.Deserialize<MaterialSlot>(m_SerializedOutputs, typeSerializationInfos);
        }
    }
}
