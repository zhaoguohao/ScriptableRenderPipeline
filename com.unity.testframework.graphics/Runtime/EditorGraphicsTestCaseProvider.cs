#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.TestTools.Graphics;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.TestTools.Graphics
{
    internal class EditorGraphicsTestCaseProvider : IGraphicsTestCaseProvider
    {
        
        string m_ReferenceImagePath = string.Empty;

        public EditorGraphicsTestCaseProvider()
        {
        }

        public EditorGraphicsTestCaseProvider(string referenceImagePath)
        {
            m_ReferenceImagePath = referenceImagePath;
        }

        public static IEnumerable<string> GetTestScenePaths()
        {
            return EditorProviderUtility.testScenesPath;
        }

        public IEnumerable<GraphicsTestCase> GetTestCases()
        {
            var scenes = GetTestScenePaths();
            foreach (var scenePath in scenes)
            {
                Texture2D referenceImage = null;

                referenceImage = EditorProviderUtility.refImagesPerScenePath[scenePath];

                yield return new GraphicsTestCase(scenePath, referenceImage);
            }
        }

        public GraphicsTestCase GetTestCaseFromPath(string scenePath)
        {
            GraphicsTestCase output = null;

            Texture2D referenceImage = null;

            referenceImage = EditorProviderUtility.refImagesPerScenePath[scenePath];

            output = new GraphicsTestCase(scenePath, referenceImage);

            return output;
        }
    }
}
#endif