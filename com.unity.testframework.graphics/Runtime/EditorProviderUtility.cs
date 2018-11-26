#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.NRefactory.Ast;

namespace UnityEngine.TestTools.Graphics
{
    [InitializeOnLoad]
    public static class EditorProviderUtility
    {   
        static EditorProviderUtility()
        {
            Refresh();            
        }

        [MenuItem("Tests/Refresh TestCases")]
        public static void Refresh()
        {
            m_colorSpace = QualitySettings.activeColorSpace;
            m_platform = Application.platform;
            m_graphicsDeviceType = SystemInfo.graphicsDeviceType;
            m_buildSettingsScenes = EditorBuildSettings.scenes;
            
            if (m_testScenesPath == null) m_testScenesPath = new List<string>();
            m_testScenesPath.Clear();
            
            if (m_refImagesPerScenePath == null) m_refImagesPerScenePath = new Dictionary<string, Texture2D>();
            m_refImagesPerScenePath.Clear();
            
            for (var i = 0; i < m_buildSettingsScenes.Length; ++i)
            {
                var s = m_buildSettingsScenes[i];
                if (s.enabled)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path);
                    var labels = AssetDatabase.GetLabels(asset);
                    if (!labels.Contains("ExcludeGfxTests")) m_testScenesPath.Add(s.path);
                }

                string imagePath = $"{GetReferenceImagesPath()}{Path.GetFileNameWithoutExtension(m_buildSettingsScenes[i].path)}.png";
                
                m_refImagesPerScenePath.Add(
                    m_buildSettingsScenes[i].path,
                    AssetDatabase.LoadAssetAtPath<Texture2D>( imagePath )
                );
            }
        }

        static ColorSpace m_colorSpace;
        public static ColorSpace colorSpace => m_colorSpace;
        
        static RuntimePlatform m_platform;
        public static RuntimePlatform platform => m_platform;
        
        static GraphicsDeviceType m_graphicsDeviceType;
        public static GraphicsDeviceType graphicsDeviceType => m_graphicsDeviceType;
        
        static EditorBuildSettingsScene[] m_buildSettingsScenes;
        public static EditorBuildSettingsScene[] buildSettingsScenes => m_buildSettingsScenes;

        static List<string> m_testScenesPath;
        public static List<string> testScenesPath => m_testScenesPath;

        static Dictionary<string, Texture2D> m_refImagesPerScenePath;
        public static Dictionary<string, Texture2D> refImagesPerScenePath => m_refImagesPerScenePath;
        
        public static string ReferenceImagesRoot = "Assets/ReferenceImages";

        public static string GetReferenceImagesPath( string root = null)
        {
            return string.Format("{0}/{1}/{2}/{3}/", string.IsNullOrEmpty(root) ? ReferenceImagesRoot : root, colorSpace, platform, graphicsDeviceType);
        }

        public static Dictionary<string, string> CollectReferenceImagePathsFor(string referenceImageRoot)
        {
            return CollectReferenceImagePathsFor( referenceImageRoot, colorSpace, platform, graphicsDeviceType);
        }
        
        public static Dictionary<string, string> CollectReferenceImagePathsFor(string referenceImageRoot, ColorSpace colorSpace, RuntimePlatform runtimePlatform,
            GraphicsDeviceType graphicsApi)
        {
            var result = new Dictionary<string, string>();

            if (!Directory.Exists(referenceImageRoot))
                return result;

            var fullPathPrefix = string.Format("{0}/{1}/{2}/{3}/", referenceImageRoot, colorSpace, runtimePlatform, graphicsApi);

            foreach (var assetPath in AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith(fullPathPrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Count(ch => ch == '/')))
            {
                // Skip directories
                if (!File.Exists(assetPath))
                    continue;

                var fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (fileName == null)
                    continue;

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (!texture)
                    continue;

                result[fileName] = assetPath;
            }

            return result;
        }

    }
}

#endif