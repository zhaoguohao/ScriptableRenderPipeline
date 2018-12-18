using System.Collections.Generic;
using UnityEngine;
using UnityEditorInternal;

namespace UnityEditor.ShaderGraph
{
    static class DynamicSlotUtils
    {
        public static ReorderableList CreateReorderableList(DynamicSlotList list, string label, bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton) 
        {
            var reorderableList = new ReorderableList(list.slots, typeof(DynamicSlotList.Slot), draggable, displayHeader, displayAddButton, displayRemoveButton);

            reorderableList.drawHeaderCallback = (Rect rect) => 
            {  
                var labelRect = new Rect(rect.x, rect.y, rect.width-10, rect.height);
                EditorGUI.LabelField(labelRect, label);
            };

            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => 
            {
                var element = list.slots[index];
                rect.y += 2;
                CreateEntry(list.slots, index, element, new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight));
            };

            reorderableList.elementHeightCallback = (int indexer) => 
            {
                return reorderableList.elementHeight;
            };

            reorderableList.onAddCallback += AddItem;
            reorderableList.onRemoveCallback += RemoveItem;
            return reorderableList;
        }

        private static void CreateEntry(List<DynamicSlotList.Slot> list, int index, DynamicSlotList.Slot entry, Rect rect)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            list[index].name = EditorGUI.TextField( new Rect(rect.x, rect.y, rect.width / 2, EditorGUIUtility.singleLineHeight), entry.name, labelStyle);
            list[index].type = (SlotValueType)EditorGUI.EnumPopup( new Rect(rect.x + rect.width / 2, rect.y, rect.width / 2, EditorGUIUtility.singleLineHeight), entry.type);
        }

        private static void AddItem(ReorderableList list)
        {
            list.list.Add(new DynamicSlotList.Slot());
        }

        private static void RemoveItem(ReorderableList list)
        {
            list.list.RemoveAt(list.index);
        }
    }
}
