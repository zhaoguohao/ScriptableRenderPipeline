using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEditorInternal;

namespace UnityEditor.ShaderGraph.Drawing
{
    class DynamicSlotListView : VisualElement
    {
        // --------------------------------------------------
        // VIEW

        AbstractMaterialNode m_Node;
        SlotType m_Type;
        List<MaterialSlot> m_Slots;

		IMGUIContainer m_Container;

        [SerializeField]
        ReorderableList m_ReorderableList;

        int m_SelectedIndex = -1;

        public DynamicSlotListView(AbstractMaterialNode node, List<MaterialSlot> slots, SlotType type)
        {
            styleSheets.Add(Resources.Load<StyleSheet>("Styles/DynamicSlotListView"));
            m_Node = node;
            m_Type = type;
            m_Slots = slots;

            m_Container = new IMGUIContainer(() => OnGUIHandler ()) { name = "ListContainer" };
            Add(m_Container);
        }

        void OnGUIHandler()
        {
            using (var changeCheckScope = new EditorGUI.ChangeCheckScope())
            {
                var listTitle = string.Format("{0} Slots", m_Type.ToString());
                var entries = ConvertSlotsToEntries(m_Slots);
                m_ReorderableList = CreateReorderableList(entries, listTitle, true, true, true, true);
                m_ReorderableList.index = m_SelectedIndex;
                m_ReorderableList.DoLayoutList();

                if (changeCheckScope.changed)
                    m_Node.Dirty(ModificationScope.Node);
            }
        }

        private List<SlotEntry> ConvertSlotsToEntries(List<MaterialSlot> slots)
        {
            List<SlotEntry> entries = new List<SlotEntry>();
            foreach(MaterialSlot slot in slots)
            {
                entries.Add(new SlotEntry()
                {
                    id = slot.id,
                    name = slot.RawDisplayName(),
                    valueType = slot.valueType,
                });
            }
            return entries;
        }

        private ReorderableList CreateReorderableList(List<SlotEntry> list, string label, bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton) 
        {
            var reorderableList = new ReorderableList(list, typeof(SlotEntry), draggable, displayHeader, displayAddButton, displayRemoveButton);

            reorderableList.drawHeaderCallback = (Rect rect) => 
            {  
                var labelRect = new Rect(rect.x, rect.y, rect.width-10, rect.height);
                EditorGUI.LabelField(labelRect, label);
            };

            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => 
            {
                rect.y += 2;
                DrawEntry(reorderableList, index, new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight));
            };

            reorderableList.elementHeightCallback = (int indexer) => 
            {
                return reorderableList.elementHeight;
            };

            reorderableList.onSelectCallback += SelectEntry;
            reorderableList.onAddCallback += AddEntry;
            reorderableList.onRemoveCallback += RemoveEntry;
            return reorderableList;
        }

        private void DrawEntry(ReorderableList list, int index, Rect rect)
        {
            GUIStyle labelStyle = new GUIStyle();

            labelStyle.normal.textColor = Color.white;

            SlotEntry entry = list.list[index] as SlotEntry;
            EditorGUI.BeginChangeCheck();
            entry.name = EditorGUI.DelayedTextField( new Rect(rect.x, rect.y, rect.width / 3, EditorGUIUtility.singleLineHeight), entry.name, labelStyle);            
            entry.valueType = (SlotValueType)EditorGUI.EnumPopup( new Rect(rect.x + rect.width / 3, rect.y, rect.width / 3, EditorGUIUtility.singleLineHeight), entry.valueType);
            entry.interfaceType = EditorGUI.Popup( new Rect(rect.x + (rect.width / 3) * 2, rect.y, rect.width / 3, EditorGUIUtility.singleLineHeight), entry.interfaceType, GetEnumEntries(entry.valueType) );
            if(EditorGUI.EndChangeCheck())
            {
                UpdateSlots();
            }
        }

        private void SelectEntry(ReorderableList list)
        {
            m_SelectedIndex = list.index;
            Redraw();
        }

        private void AddEntry(ReorderableList list)
        {
            list.list.Add(new SlotEntry() { id = -1, name = "New Slot", valueType = SlotValueType.Vector1 } );
            m_SelectedIndex = m_Slots.Count - 1;
            UpdateSlots();
        }

        private void RemoveEntry(ReorderableList list)
        {
            list.index = m_SelectedIndex;
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
            m_SelectedIndex = list.index;
            UpdateSlots();
        }

        void Redraw()
        {
            Remove(m_Container);
            m_Container = new IMGUIContainer(() => OnGUIHandler ()) { name = "ListContainer" };
            Add(m_Container);
        }

        // --------------------------------------------------
        // MODEL

        public void UpdateSlots()
        {
            m_Slots.Clear();
            foreach(SlotEntry entry in m_ReorderableList.list)
            {
                if(entry.id == -1)
                    entry.id = GetNewSlotID();

                m_Slots.Add(GetMaterialSlot(entry));
            }

            m_Node.UpdateNodeAfterDeserialization();
        }

        private MaterialSlot GetMaterialSlot(SlotEntry entry)
        {
            var slotId = entry.id;
            var displayName = entry.name;
            var shaderOutputName = NodeUtils.GetHLSLSafeName(entry.name);
            var shaderStageCapability = ShaderStageCapability.All;

            if(m_Type == SlotType.Input)
            {
                if(entry.valueType == SlotValueType.Vector4)
                {
                    var i = (Vector4Interface)entry.interfaceType;
                    switch(i)
                    {
                        case Vector4Interface.ColorRGBA:
                            return new ColorRGBAMaterialSlot(slotId, displayName, shaderOutputName, m_Type, Vector4.zero, shaderStageCapability);
                        case Vector4Interface.ScreenPosition:
                            return new ScreenPositionMaterialSlot(slotId, displayName, shaderOutputName, ScreenSpaceType.Default, shaderStageCapability);
                        case Vector4Interface.VertexColor:
                            return new VertexColorMaterialSlot(slotId, displayName, shaderOutputName, shaderStageCapability);
                        default:
                            return MaterialSlot.CreateMaterialSlot(entry.valueType, slotId, displayName, shaderOutputName, m_Type, Vector4.zero, ShaderStageCapability.All);
                    }
                }
                else if(entry.valueType == SlotValueType.Vector3)
                {
                    var i = (Vector3Interface)entry.interfaceType;
                    switch(i)
                    {
                        case Vector3Interface.ColorRGB:
                            return new ColorRGBMaterialSlot(slotId, displayName, shaderOutputName, m_Type, Vector4.zero, ColorMode.Default, shaderStageCapability);
                        case Vector3Interface.ObjectSpaceNormal:
                            return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability);
                        case Vector3Interface.ObjectSpaceTangent:
                            return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability);
                        case Vector3Interface.ObjectSpaceBitangent:
                            return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability);
                        case Vector3Interface.ObjectSpacePosition:
                            return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability);
                        case Vector3Interface.ViewSpaceNormal:
                            return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability);
                        case Vector3Interface.ViewSpaceTangent:
                            return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability);
                        case Vector3Interface.ViewSpaceBitangent:
                            return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability);
                        case Vector3Interface.ViewSpacePosition:
                            return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability);
                        case Vector3Interface.WorldSpaceNormal:
                            return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability);
                        case Vector3Interface.WorldSpaceTangent:
                            return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability);
                        case Vector3Interface.WorldSpaceBitangent:
                            return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability);
                        case Vector3Interface.WorldSpacePosition:
                            return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability);
                        case Vector3Interface.TangentSpaceNormal:
                            return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability);
                        case Vector3Interface.TangentSpaceTangent:
                            return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability);
                        case Vector3Interface.TangentSpaceBitangent:
                            return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability);
                        case Vector3Interface.TangentSpacePosition:
                            return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability);
                        case Vector3Interface.ObjectSpaceViewDirection:
                            return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability);
                        case Vector3Interface.ViewSpaceViewDirection:
                            return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability);
                        case Vector3Interface.WorldSpaceViewDirection:
                            return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability);
                        case Vector3Interface.TangentSpaceViewDirection:
                            return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability);
                        default:
                            return MaterialSlot.CreateMaterialSlot(entry.valueType, slotId, displayName, shaderOutputName, m_Type, Vector4.zero, ShaderStageCapability.All);
                    }
                }
                else if(entry.valueType == SlotValueType.Vector2)
                {
                    var i = (Vector2Interface)entry.interfaceType;
                    switch(i)
                    {
                        case Vector2Interface.MeshUV:
                            return new UVMaterialSlot(slotId, displayName, shaderOutputName, UVChannel.UV0, shaderStageCapability);
                        default:
                            return MaterialSlot.CreateMaterialSlot(entry.valueType, slotId, displayName, shaderOutputName, m_Type, Vector4.zero, ShaderStageCapability.All);
                    }
                }
            }

            return MaterialSlot.CreateMaterialSlot(entry.valueType, slotId, displayName, shaderOutputName, m_Type, Vector4.zero, shaderStageCapability);
        }

        private int GetNewSlotID()
        {
            int ceiling = -1;
            
            List<MaterialSlot> slots = new List<MaterialSlot>();
            m_Node.GetSlots(slots);

            foreach(MaterialSlot slot in slots)
                ceiling = slot.id > ceiling ? slot.id : ceiling;

            return ceiling + 1;
        }

        // --------------------------------------------------
        // DATA

        public class SlotEntry
        {
            public int id;
            public string name;
            public SlotValueType valueType;
            public int interfaceType;
        }

        protected enum Vector4Interface
        {
            Default,
            ColorRGBA,
            ScreenPosition,
            VertexColor,
        }

        protected enum Vector3Interface
        {
            Default,
            ColorRGB,
            ObjectSpaceNormal,
            ObjectSpaceTangent,
            ObjectSpaceBitangent,
            ObjectSpacePosition,
            ViewSpaceNormal,
            ViewSpaceTangent,
            ViewSpaceBitangent,
            ViewSpacePosition,
            WorldSpaceNormal,
            WorldSpaceTangent,
            WorldSpaceBitangent,
            WorldSpacePosition,
            TangentSpaceNormal,
            TangentSpaceTangent,
            TangentSpaceBitangent,
            TangentSpacePosition,
            ObjectSpaceViewDirection,
            ViewSpaceViewDirection,
            WorldSpaceViewDirection,
            TangentSpaceViewDirection,
        }

        protected enum Vector2Interface
        {
            Default,
            MeshUV,
        }

        private static Dictionary<SlotValueType, Type> s_InterfaceTypes = new Dictionary<SlotValueType, Type>()
        {
            { SlotValueType.Vector4, typeof(Vector4Interface) },
            { SlotValueType.Vector3, typeof(Vector3Interface) },
            { SlotValueType.Vector2, typeof(Vector2Interface) },
        };

        private string[] GetEnumEntries(SlotValueType slotValueType)
        {
            Type interfaceType;
            if(s_InterfaceTypes.TryGetValue(slotValueType, out interfaceType))
                return Enum.GetNames(Type.GetType(interfaceType.ToString()));
            
            return new string[] { "Default" };
        }
    }
}
