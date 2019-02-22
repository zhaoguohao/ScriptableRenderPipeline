using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Graphing;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph.Drawing
{
    class BlackboardProviderSubgraph : BlackboardProvider
    {
        readonly GraphData m_Graph;
        readonly Dictionary<int, BlackboardRow> m_SubgraphInputRows;
        readonly BlackboardSection m_Section;
        //WindowDraggable m_WindowDraggable;
        //ResizeBorderFrame m_ResizeBorderFrame;
        public override Blackboard blackboard { get; set; }
        Label m_PathLabel;
        TextField m_PathLabelTextField;
        bool m_EditPathCancelled = false;
        List<MaterialNodeView> m_SelectedNodes = new List<MaterialNodeView>();

        //public Action onDragFinished
        //{
        //    get { return m_WindowDraggable.OnDragFinished; }
        //    set { m_WindowDraggable.OnDragFinished = value; }
        //}

        //public Action onResizeFinished
        //{
        //    get { return m_ResizeBorderFrame.OnResizeFinished; }
        //    set { m_ResizeBorderFrame.OnResizeFinished = value; }
        //}

        Dictionary<MaterialSlot, bool> m_ExpandedSubgraphInputs = new Dictionary<MaterialSlot, bool>();

        public Dictionary<MaterialSlot, bool> expandedSubgraphInputs
        {
            get { return m_ExpandedSubgraphInputs; }
        }

        public string assetName
        {
            get { return blackboard.title; }
            set
            {
                blackboard.title = value;
            }
        }

        public BlackboardProviderSubgraph(GraphData graph)
        {
            m_Graph = graph;

            m_SubgraphInputRows = new Dictionary<int, BlackboardRow>();
            
            blackboard = new Blackboard()
            {
                scrollable = true,
                subTitle = FormatPath(graph.path),
                editTextRequested = EditTextRequested,
                addItemRequested = AddItemRequested,
                moveItemRequested = MoveItemRequested
            };

            m_PathLabel = blackboard.hierarchy.ElementAt(0).Q<Label>("subTitleLabel");
            m_PathLabel.RegisterCallback<MouseDownEvent>(OnMouseDownEvent);

            m_PathLabelTextField = new TextField { visible = false };
            m_PathLabelTextField.Q("unity-text-input").RegisterCallback<FocusOutEvent>(e => { OnEditPathTextFinished(); });
            m_PathLabelTextField.Q("unity-text-input").RegisterCallback<KeyDownEvent>(OnPathTextFieldKeyPressed);
            blackboard.hierarchy.Add(m_PathLabelTextField);

            // m_WindowDraggable = new WindowDraggable(blackboard.shadow.Children().First().Q("header"));
            // blackboard.AddManipulator(m_WindowDraggable);

            // m_ResizeBorderFrame = new ResizeBorderFrame(blackboard) { name = "resizeBorderFrame" };
            // blackboard.shadow.Add(m_ResizeBorderFrame);

            m_Section = new BlackboardSection { headerVisible = false };

            foreach (var subgraphInput in graph.subgraphInputs)
                AddSubgraphInput(subgraphInput);
            
            blackboard.Add(m_Section);
        }

        void OnDragUpdatedEvent(DragUpdatedEvent evt)
        {
            if (m_SelectedNodes.Any())
            {
                foreach (var node in m_SelectedNodes)
                {
                    node.RemoveFromClassList("hovered");
                }
                m_SelectedNodes.Clear();
            }
        }

        void OnMouseDownEvent(MouseDownEvent evt)
        {
            if (evt.clickCount == 2 && evt.button == (int)MouseButton.LeftMouse)
            {
                StartEditingPath();
                evt.PreventDefault();
            }
        }

        void StartEditingPath()
        {
            m_PathLabelTextField.visible = true;

            m_PathLabelTextField.value = m_PathLabel.text;
            m_PathLabelTextField.style.position = Position.Absolute;
            var rect = m_PathLabel.ChangeCoordinatesTo(blackboard, new Rect(Vector2.zero, m_PathLabel.layout.size));
            m_PathLabelTextField.style.left = rect.xMin;
            m_PathLabelTextField.style.top = rect.yMin;
            m_PathLabelTextField.style.width = rect.width;
            m_PathLabelTextField.style.fontSize = 11;
            m_PathLabelTextField.style.marginLeft = 0;
            m_PathLabelTextField.style.marginRight = 0;
            m_PathLabelTextField.style.marginTop = 0;
            m_PathLabelTextField.style.marginBottom = 0;

            m_PathLabel.visible = false;

            m_PathLabelTextField.Q("unity-text-input").Focus();
            m_PathLabelTextField.SelectAll();
        }

        void OnPathTextFieldKeyPressed(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    m_EditPathCancelled = true;
                    m_PathLabelTextField.Q("unity-text-input").Blur();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    m_PathLabelTextField.Q("unity-text-input").Blur();
                    break;
                default:
                    break;
            }
        }

        void OnEditPathTextFinished()
        {
            m_PathLabel.visible = true;
            m_PathLabelTextField.visible = false;

            var newPath = m_PathLabelTextField.text;
            if (!m_EditPathCancelled && (newPath != m_PathLabel.text))
            {
                newPath = SanitizePath(newPath);
            }

            m_Graph.path = newPath;
            m_PathLabel.text = FormatPath(newPath);
            m_EditPathCancelled = false;
        }

        static string FormatPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "—";
            return path;
        }

        static string SanitizePath(string path)
        {
            var splitString = path.Split('/');
            List<string> newStrings = new List<string>();
            foreach (string s in splitString)
            {
                var str = s.Trim();
                if (!string.IsNullOrEmpty(str))
                {
                    newStrings.Add(str);
                }
            }

            return string.Join("/", newStrings.ToArray());
        }

        void MoveItemRequested(Blackboard blackboard, int newIndex, VisualElement visualElement)
        {
            var subgraphInput = visualElement.userData as MaterialSlot;

            if (subgraphInput == null)
                return;

            m_Graph.owner.RegisterCompleteObjectUndo("Move Subgraph Input");
            m_Graph.MoveSubgraphInput(subgraphInput, newIndex);
        }

        void AddItemRequested(Blackboard blackboard)
        {
            var gm = new GenericMenu();
            gm.AddItem(new GUIContent("Vector1"), false, () => AddSubgraphInput(new Vector1MaterialSlot(), true));
            gm.ShowAsContext();
        }

        void EditTextRequested(Blackboard blackboard, VisualElement visualElement, string newText)
        {   
            var field = (BlackboardField)visualElement;
            var subgraphInput = (MaterialSlot)field.userData;
            if (!string.IsNullOrEmpty(newText) && newText != subgraphInput.RawDisplayName())
            {
                m_Graph.owner.RegisterCompleteObjectUndo("Edit Subgraph Input Name");
                newText = m_Graph.SanitizeSubgraphInputName(newText, subgraphInput.id);
                subgraphInput.displayName = newText;
                field.text = newText;
                DirtyNodes();
            }
        }

        public override void HandleGraphChanges()
        {
            foreach (var subgraphInputId in m_Graph.removedSubgraphInputs)
            {
                BlackboardRow row;
                if (m_SubgraphInputRows.TryGetValue(subgraphInputId, out row))
                {
                    row.RemoveFromHierarchy();
                    m_SubgraphInputRows.Remove(subgraphInputId);
                }
            }

            foreach (var subgraphInput in m_Graph.addedSubgraphInputs)
                AddSubgraphInput(subgraphInput, index: m_Graph.GetSubgraphInputIndex(subgraphInput));

            foreach (var subgraphInputDict in expandedSubgraphInputs)
            {
                SessionState.SetBool(subgraphInputDict.Key.id.ToString(), subgraphInputDict.Value);
            }

            if (m_Graph.movedSubgraphInputs.Any())
            {
                foreach (var row in m_SubgraphInputRows.Values)
                    row.RemoveFromHierarchy();

                foreach (var subgraphInput in m_Graph.subgraphInputs)
                    m_Section.Add(m_SubgraphInputRows[subgraphInput.id]);
            }
            m_ExpandedSubgraphInputs.Clear();
        }

        void AddSubgraphInput(MaterialSlot subgraphInput, bool create = false, int index = -1)
        {
            if (m_SubgraphInputRows.ContainsKey(subgraphInput.id))
                return;

            if (create)
                subgraphInput.displayName = m_Graph.SanitizePropertyName(subgraphInput.RawDisplayName());

            var field = new BlackboardField(null, subgraphInput.RawDisplayName(), subgraphInput.concreteValueType.ToString()) { userData = subgraphInput };

            var propertyView = new BlackboardFieldSubgraphInputView(field, m_Graph, subgraphInput);
            var row = new BlackboardRow(field, propertyView);
            var pill = row.Q<Pill>();
            pill.RegisterCallback<MouseEnterEvent>(evt => OnMouseHoverSubgraph(evt, subgraphInput));
            pill.RegisterCallback<MouseLeaveEvent>(evt => OnMouseHoverSubgraph(evt, subgraphInput));
            pill.RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);

            var expandButton = row.Q<Button>("expandButton");
            expandButton.RegisterCallback<MouseDownEvent>(evt => OnExpandedSubgraph(evt, subgraphInput), TrickleDown.TrickleDown);

            row.userData = subgraphInput;
            if (index < 0)
                index = m_SubgraphInputRows.Count;
            if (index == m_SubgraphInputRows.Count)
                m_Section.Add(row);
            else
                m_Section.Insert(index, row);
            m_SubgraphInputRows[subgraphInput.id] = row;

            m_SubgraphInputRows[subgraphInput.id].expanded = SessionState.GetBool(subgraphInput.id.ToString(), true);

            if (create)
            {
                row.expanded = true;
                m_Graph.owner.RegisterCompleteObjectUndo("Create Subgraph Input");
                m_Graph.AddSubgraphInput(subgraphInput);
                field.OpenTextEditor();
            }
        }

        void OnExpandedSubgraph(MouseDownEvent evt, MaterialSlot subgraphInput)
        {
            m_ExpandedSubgraphInputs[subgraphInput] = !m_SubgraphInputRows[subgraphInput.id].expanded;
        }

        void DirtyNodes()
        {
            foreach (var node in m_Graph.GetNodes<PropertyNode>())
            {
                node.OnEnable();
                node.Dirty(ModificationScope.Node);
            }
        }

        public BlackboardRow GetBlackboardRowSubgraph(int id)
        {
            return m_SubgraphInputRows[id];
        }

        void OnMouseHoverSubgraph(EventBase evt, MaterialSlot subgraphInput)
        {
            var graphView = blackboard.GetFirstAncestorOfType<MaterialGraphView>();
            if (evt.eventTypeId == MouseEnterEvent.TypeId())
            {
                foreach (var node in graphView.nodes.ToList().OfType<MaterialNodeView>())
                {
                    if (node.node is PropertyNode propertyNode)
                    {
                        if (propertyNode.subgraphInputId == subgraphInput.id)
                        {
                            m_SelectedNodes.Add(node);
                            node.AddToClassList("hovered");
                        }
                    }
                }
            }
            else if (evt.eventTypeId == MouseLeaveEvent.TypeId() && m_SelectedNodes.Any())
            {
                foreach (var node in m_SelectedNodes)
                {
                    node.RemoveFromClassList("hovered");
                }
                m_SelectedNodes.Clear();
            }
        }
    }
}
