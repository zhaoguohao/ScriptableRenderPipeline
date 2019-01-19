using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    [Title("Test")]
    class TestNode : AbstractMaterialNode, IGeneratesBodyCode, IGeneratesFunction
    {
        public TestNode()
        {
            name = "Test";
            UpdateNodeAfterDeserialization();
        }

        public sealed override void UpdateNodeAfterDeserialization()
        {
            AddSlot(new ShaderPort(0, "Slot A", Graphing.SlotType.Input, SlotValueType.Vector2, ShaderControlType.Vector2()));
            AddSlot(new ShaderPort(1, "Slot B", Graphing.SlotType.Input, SlotValueType.Vector3, ShaderControlType.Vector3(new Vector3(1, 0, 0))));
            AddSlot(new ShaderPort(2, "Slot C", Graphing.SlotType.Input, SlotValueType.Vector4, ShaderControlType.Vector4()));
        }

        public void GenerateNodeCode(ShaderGenerator visitor, GraphContext graphContext, GenerationMode generationMode)
        {

        }

        public void GenerateNodeFunction(FunctionRegistry registry, GraphContext graphContext, GenerationMode generationMode)
        {

        }
    }
}
