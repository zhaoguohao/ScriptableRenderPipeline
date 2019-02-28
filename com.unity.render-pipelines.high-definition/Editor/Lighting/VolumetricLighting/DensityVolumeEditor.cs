using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;
using System.Collections.Generic;
using UnityEditorInternal;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(DensityVolume))]
    class DensityVolumeEditor : Editor
    {
        internal const EditMode.SceneViewEditMode k_EditShape = EditMode.SceneViewEditMode.ReflectionProbeBox;
        internal const EditMode.SceneViewEditMode k_EditBlend = EditMode.SceneViewEditMode.GridBox;

        static HierarchicalBox s_ShapeBox;
        internal static HierarchicalBox s_BlendBox;

        SerializedDensityVolume m_SerializedDensityVolume;
        
        void OnEnable()
        {
            m_SerializedDensityVolume = new SerializedDensityVolume(serializedObject);

            if (s_ShapeBox == null || s_ShapeBox.Equals(null))
            {
                s_ShapeBox = new HierarchicalBox(DensityVolumeUI.Styles.k_GizmoColorBase, DensityVolumeUI.Styles.k_BaseHandlesColor);
                s_ShapeBox.monoHandle = false;
            }
            if (s_BlendBox == null || s_BlendBox.Equals(null))
            {
                s_BlendBox = new HierarchicalBox(DensityVolumeUI.Styles.k_GizmoColorBase, InfluenceVolumeUI.k_HandlesColor, parent: s_ShapeBox);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DensityVolumeUI.Inspector.Draw(m_SerializedDensityVolume, this);

            m_SerializedDensityVolume.Apply();
        }

        static Vector3 CenterBlendLocalPosition(DensityVolume densityVolume)
        {
            if (densityVolume.parameters.editorAdvancedFade)
            {
                Vector3 size = densityVolume.parameters.size;
                Vector3 posBlend = densityVolume.parameters.editorPositiveFade;
                posBlend.x *= size.x;
                posBlend.y *= size.y;
                posBlend.z *= size.z;
                Vector3 negBlend = densityVolume.parameters.editorNegativeFade;
                negBlend.x *= size.x;
                negBlend.y *= size.y;
                negBlend.z *= size.z;
                Vector3 localPosition = (negBlend - posBlend) * 0.5f;
                return localPosition;
            }
            else
                return Vector3.zero;
        }

        static Vector3 BlendSize(DensityVolume densityVolume)
        {
            Vector3 size = densityVolume.parameters.size;
            if (densityVolume.parameters.editorAdvancedFade)
            {
                Vector3 blendSize = (Vector3.one - densityVolume.parameters.editorPositiveFade - densityVolume.parameters.editorNegativeFade);
                blendSize.x *= size.x;
                blendSize.y *= size.y;
                blendSize.z *= size.z;
                return blendSize;
            }
            else
                return size - densityVolume.parameters.editorUniformFade * 2f * Vector3.one;
        }
        
        [DrawGizmo(GizmoType.Selected|GizmoType.Active)]
        static void DrawGizmosSelected(DensityVolume densityVolume, GizmoType gizmoType)
        {
            using (new Handles.DrawingScope(Matrix4x4.TRS(densityVolume.transform.position, densityVolume.transform.rotation, Vector3.one)))
            {
                // Blend box
                s_BlendBox.center = CenterBlendLocalPosition(densityVolume);
                s_BlendBox.size = BlendSize(densityVolume);
                Color baseColor = densityVolume.parameters.albedo;
                baseColor.a = 8/255f;
                s_BlendBox.baseColor = baseColor;
                s_BlendBox.DrawHull(EditMode.editMode == k_EditBlend);
                
                // Bounding box.
                s_ShapeBox.center = Vector3.zero;
                s_ShapeBox.size = densityVolume.parameters.size;
                s_ShapeBox.DrawHull(EditMode.editMode == k_EditShape);
            }
        }

        void OnSceneGUI()
        {
            DensityVolume densityVolume = target as DensityVolume;

            switch (EditMode.editMode)
            {
                case k_EditBlend:
                    using (new Handles.DrawingScope(Matrix4x4.TRS(densityVolume.transform.position, densityVolume.transform.rotation, Vector3.one)))
                    {
                        //contained must be initialized in all case
                        s_ShapeBox.center = Vector3.zero;
                        s_ShapeBox.size = densityVolume.parameters.size;

                        Color baseColor = densityVolume.parameters.albedo;
                        baseColor.a = 8 / 255f;
                        s_BlendBox.baseColor = baseColor;
                        s_BlendBox.monoHandle = !m_SerializedDensityVolume.editorAdvancedFade.boolValue;
                        s_BlendBox.center = CenterBlendLocalPosition(densityVolume);
                        s_BlendBox.size = BlendSize(densityVolume);
                        EditorGUI.BeginChangeCheck();
                        s_BlendBox.DrawHandle();
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(densityVolume, "Change Density Volume Blend");

                            if (m_SerializedDensityVolume.editorAdvancedFade.boolValue)
                            {
                                //work in local space to compute the change on positiveFade and negativeFade
                                Vector3 newCenterBlendLocalPosition = s_BlendBox.center;
                                Vector3 halfSize = s_BlendBox.size * 0.5f;
                                Vector3 size = densityVolume.parameters.size;
                                Vector3 posFade = newCenterBlendLocalPosition + halfSize;
                                posFade.x = 0.5f - posFade.x / size.x;
                                posFade.y = 0.5f - posFade.y / size.y;
                                posFade.z = 0.5f - posFade.z / size.z;
                                Vector3 negFade = newCenterBlendLocalPosition - halfSize;
                                negFade.x = 0.5f + negFade.x / size.x;
                                negFade.y = 0.5f + negFade.y / size.y;
                                negFade.z = 0.5f + negFade.z / size.z;
                                m_SerializedDensityVolume.editorPositiveFade.vector3Value = posFade;
                                m_SerializedDensityVolume.editorNegativeFade.vector3Value = negFade;
                            }
                            else
                            {
                                float uniformDistance = (s_ShapeBox.size.x - s_BlendBox.size.x) * 0.5f;
                                float max = Mathf.Min(s_ShapeBox.size.x, s_ShapeBox.size.y, s_ShapeBox.size.z) * 0.5f;
                                m_SerializedDensityVolume.editorUniformFade.floatValue = Mathf.Clamp(uniformDistance, 0f, max);
                            }
                            m_SerializedDensityVolume.Apply();
                            //densityVolume.parameters.positiveFade = posFade;
                            //densityVolume.parameters.negativeFade = negFade;
                        }
                    }
                    break;
                case k_EditShape:
                    //important: if the origin of the handle's space move along the handle,
                    //handles displacement will appears as moving two time faster.
                    using (new Handles.DrawingScope(Matrix4x4.TRS(Vector3.zero, densityVolume.transform.rotation, Vector3.one)))
                    {
                        //contained must be initialized in all case
                        s_ShapeBox.center = Quaternion.Inverse(densityVolume.transform.rotation) * densityVolume.transform.position;
                        s_ShapeBox.size = densityVolume.parameters.size;

                        Vector3 previousSize = densityVolume.parameters.size;
                        Vector3 previousPositiveFade = densityVolume.parameters.editorPositiveFade;
                        Vector3 previousNegativeFade = densityVolume.parameters.editorNegativeFade;
                        
                        EditorGUI.BeginChangeCheck();
                        s_ShapeBox.DrawHandle();
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObjects(new Object[] { densityVolume, densityVolume.transform }, "ChangeDensity Volume Bounding Box");

                            m_SerializedDensityVolume.size.vector3Value = s_ShapeBox.size;
                            DensityVolumeUI.UpdateBlendToKeepDistances(previousSize, previousPositiveFade, previousNegativeFade, m_SerializedDensityVolume, this);
                            m_SerializedDensityVolume.Apply();

                            Vector3 delta = densityVolume.transform.rotation * s_ShapeBox.center - densityVolume.transform.position;
                            densityVolume.transform.position += delta;
                        }
                    }
                    break;
            }
        }
    }
}
