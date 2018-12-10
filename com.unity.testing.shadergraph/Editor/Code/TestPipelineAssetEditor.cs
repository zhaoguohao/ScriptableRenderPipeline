using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.ShaderGraph.Tests;

namespace UnityEditor.Rendering.ShaderGraph.Editor.Tests
{
    [CustomEditor(typeof(TestPipelineAsset))]
    public class TestPipelineAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty m_defaultMaterial;

        [MenuItem("Assets/Create/Rendering/Test Pipeline Asset")]
        static void CreateAsset()
        {
            var asset = ScriptableObject.CreateInstance<TestPipelineAsset>();
            AssetDatabase.CreateAsset(asset, "Assets/New TestPipelineAsset.asset");
            Selection.activeObject = asset;
        }

        public override void OnInspectorGUI()
        {
            if(m_defaultMaterial == null)
            {
                m_defaultMaterial = serializedObject.FindProperty("m_defaultMaterial");
            }

            EditorGUILayout.PropertyField(m_defaultMaterial);
        }
    }
}