using UnityEngine;

namespace UnityEngine.TestTools.Graphics
{
    /// <summary>
    /// Represents one automatically-generated graphics test case.
    /// </summary>
    public class GraphicsTestCase
    {
        private readonly string _scenePath;
        private readonly Texture2D _referenceImage;
        private readonly string _referenceImagePath;

        public GraphicsTestCase(string scenePath, Texture2D referenceImage, string referenceImagePath)
        {
            _scenePath = scenePath;
            _referenceImage = referenceImage;
            _referenceImagePath = referenceImagePath;
        }

        /// <summary>
        /// The path to the scene to be used for this test case.
        /// </summary>
        public string ScenePath { get { return _scenePath; } }

        /// <summary>
        /// The reference image that represents the expected output for this test case.
        /// </summary>
        public Texture2D ReferenceImage {  get { return _referenceImage; } }

        /// <summary>
        /// The path to the reference image.
        /// </summary>
        public string ReferenceImagePath { get { return _referenceImagePath; } }
    }
}
