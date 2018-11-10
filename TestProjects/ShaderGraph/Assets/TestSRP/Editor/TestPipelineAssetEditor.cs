using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.ShaderGraph.Tests;

namespace UnityEditor.Rendering.ShaderGraph.Editor.Tests
{
    [CustomEditor(typeof(TestPipelineAsset))]
    public class TestPipelineAssetEditor : UnityEditor.Editor
    {
        [MenuItem("Assets/Create/Rendering/Test Pipeline Asset")]
        static void CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<TestPipelineAsset>();
            AssetDatabase.CreateAsset(asset, "Assets/New TestPipelineAsset.asset");
            Selection.activeObject = asset;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }
    }
}