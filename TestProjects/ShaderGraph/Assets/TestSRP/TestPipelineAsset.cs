using System;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

namespace UnityEngine.Rendering.ShaderGraph.Tests
{
    public class TestPipelineAsset : RenderPipelineAsset
    {
        [SerializeField]
        private Material m_defaultMaterial = null;
        [SerializeField]
        private Shader m_defaultShader = null;

        public static TestPipelineAsset CreateAsset()
        {
            var asset = CreateInstance<TestPipelineAsset>();
            AssetDatabase.CreateAsset(asset, "Assets/New Test Pipeline Asset.asset");
            return asset;
        }

        public override Shader defaultShader
        {
            get { return m_defaultShader; }
        }

        public override Material defaultMaterial
        {
            get { return m_defaultMaterial; }
        }

        protected override RenderPipeline CreatePipeline()
        {
            return new TestPipeline();
        }
    }
}