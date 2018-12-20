using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    class DecalGUI : ExpandableAreaMaterial
    {
        [Flags]
        enum Expandable : uint
        {
            Input = 1 << 0
        }

        protected static class Styles
        {
            public static string InputsText = "Inputs";
            public static GUIContent drawOrderText = new GUIContent("Draw order", "Controls draw order of decal projectors");
        }

        protected override uint defaultExpandedState { get { return (uint)Expandable.Input; } }

        protected MaterialProperty drawOrder = new MaterialProperty();
        protected const string kDrawOrder = "_DrawOrder";

        protected MaterialEditor m_MaterialEditor;

        void FindMaterialProperties(MaterialProperty[] props)
        {
            drawOrder = FindProperty(kDrawOrder, props);

            // always instanced
            SerializedProperty instancing = m_MaterialEditor.serializedObject.FindProperty("m_EnableInstancingVariants");
            instancing.boolValue = true;
        }


        public void ShaderPropertiesGUI(Material material)
        {
            // Use default labelWidth
            EditorGUIUtility.labelWidth = 0f;

            using (var header = new HeaderScope(Styles.InputsText, (uint)Expandable.Input, this))
            {
                if (header.expanded)
                {
                    // Detect any changes to the material
                    EditorGUI.BeginChangeCheck();
                    {
                        m_MaterialEditor.ShaderProperty(drawOrder, Styles.drawOrderText);
                    }

                    EditorGUI.EndChangeCheck();
                }
            }
        }


        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            m_MaterialEditor = materialEditor;

            // We should always register the key used to keep collapsable state
            InitExpandableState(materialEditor);

            // We should always do this call at the beginning
            m_MaterialEditor.serializedObject.Update();

            FindMaterialProperties(props);

            Material material = materialEditor.target as Material;
            ShaderPropertiesGUI(material);

            // We should always do this call at the end
            m_MaterialEditor.serializedObject.ApplyModifiedProperties();
        }
    }
}
