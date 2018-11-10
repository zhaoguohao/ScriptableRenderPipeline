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
        public static TestPipelineAsset CreateAsset()
        {
            var asset = CreateInstance<TestPipelineAsset>();
            AssetDatabase.CreateAsset(asset, "Assets/New Test Pipeline Asset.asset");
            return asset;
        }

        public override Shader defaultShader
        {
            get
            {
                return Shader.Find("ShaderGraph/Tests/Default-Unlit");
            }
        }

        protected override RenderPipeline CreatePipeline()
        {
            return new TestPipeline();
        }
    }
}