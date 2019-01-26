using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Graphing;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph.Drawing
{
    class BlackboardSubgraphProvider : BlackboardProvider
    {
        readonly AbstractMaterialGraph m_Graph; //specific to subgraph
        public static readonly Texture2D exposedIcon = Resources.Load<Texture2D>("GraphView/Nodes/BlackboardFieldExposed");
        readonly Dictionary<int, BlackboardRow> m_InputRows; //specific to subgraph
        readonly BlackboardSection m_Section;
        Label m_PathLabel;
        TextField m_PathLabelTextField;
        bool m_EditPathCancelled = false;
        List<MaterialNodeView> m_SelectedNodes = new List<MaterialNodeView>();
        SubGraph subgraph => (SubGraph)m_Graph; //specific to subgraph

        Dictionary<InputDescriptor, bool> m_ExpandedDescriptors = new Dictionary<InputDescriptor, bool>(); //specific to subgraph

        public Dictionary<InputDescriptor, bool> expandedDescriptors
        {
            get { return m_ExpandedDescriptors; }
        }

        public BlackboardSubgraphProvider(SubGraph graph)
        {
            m_Graph = graph;
            m_InputRows = new Dictionary<int, BlackboardRow>();

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

            m_Section = new BlackboardSection { headerVisible = false };
            foreach (var input in graph.inputs)
                AddInput(input);
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

        //input specific actions vs property actions start here

        void MoveItemRequested(Blackboard blackboard, int newIndex, VisualElement visualElement)
        {
            var input = visualElement.userData as IShaderProperty;
            if (input == null)
                return;
            m_Graph.owner.RegisterCompleteObjectUndo("Move input");
            m_Graph.MoveShaderProperty(input, newIndex);
        }

        void AddItemRequested(Blackboard blackboard)
        {
            var gm = new GenericMenu();
            gm.AddItem(new GUIContent("Vector1"), false, () => AddInput(new InputDescriptor(0, "Vector1", ConcreteSlotValueType.Vector1), true));
            gm.AddItem(new GUIContent("Vector2"), false, () => AddInput(new InputDescriptor(1, "Vector2", ConcreteSlotValueType.Vector2), true));
            gm.AddItem(new GUIContent("Vector3"), false, () => AddInput(new InputDescriptor(2, "Vector3", ConcreteSlotValueType.Vector3), true));
            gm.AddItem(new GUIContent("Gradient"), false, () => AddInput(new InputDescriptor(3, "Gradient", ConcreteSlotValueType.Gradient), true));
            gm.AddItem(new GUIContent("Texture2D"), false, () => AddInput(new InputDescriptor(4, "Texture2D", ConcreteSlotValueType.Texture2D), true));
            gm.ShowAsContext();
        }

        void EditTextRequested(Blackboard blackboard, VisualElement visualElement, string newText)
        {
            var field = (BlackboardField)visualElement;
            var input = (InputDescriptor)field.userData;
            if (!string.IsNullOrEmpty(newText) && newText != input.name)
            {
                m_Graph.owner.RegisterCompleteObjectUndo("Edit input Name");
                newText = m_Graph.SanitizePropertyName(newText, input.id);
                input.name = newText;
                field.text = newText;
                DirtyNodes();
            }
        }

        new public void HandleGraphChanges()
        {
            foreach (var inputID in subgraph.removedInputs)
            {
                BlackboardRow row;
                if (m_InputRows.TryGetValue(inputID, out row))
                {
                    row.RemoveFromHierarchy();
                    m_InputRows.Remove(inputID);
                }
            }

            foreach (var inputs in subgraph.addedInputs)
                AddInput(inputs, index: inputs.id);

            foreach (var inputDict in expandedDescriptors)
            {
                SessionState.SetBool(inputDict.Key.id.ToString(), inputDict.Value);
            }

            if (subgraph.movedInputs.Any())
            {
                foreach (var row in m_InputRows.Values)
                    row.RemoveFromHierarchy();

                foreach (var input in subgraph.inputs)
                    m_Section.Add(m_InputRows[input.id]);
            }
            expandedProperties.Clear();
        }

        void AddInput(InputDescriptor input, bool create = false, int index = -1)
        {
            if (m_InputRows.ContainsKey(input.id))
                return;

            var icon = exposedIcon;
            var field = new BlackboardField(icon, input.name, input.valueType.ToString()) { userData = input };

            var inputView = new BlackboardFieldInputView(field, m_Graph, input);
            var row = new BlackboardRow(field, inputView);
            var pill = row.Q<Pill>();
            //pill.RegisterCallback<MouseEnterEvent>(evt => OnMouseHover(evt, input));
            //pill.RegisterCallback<MouseLeaveEvent>(evt => OnMouseHover(evt, input));
            pill.RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);

            var expandButton = row.Q<Button>("expandButton");
            expandButton.RegisterCallback<MouseDownEvent>(evt => OnExpanded(evt, input), TrickleDown.TrickleDown);

            row.userData = input;
            if (index < 0)
                index = m_InputRows.Count;
            if (index == m_InputRows.Count)
                m_Section.Add(row);
            else
                m_Section.Insert(index, row);
            m_InputRows[input.id] = row;

            m_InputRows[input.id].expanded = SessionState.GetBool(input.id.ToString(), true);

            if (create)
            {
                row.expanded = true;
                m_Graph.owner.RegisterCompleteObjectUndo("Create input");
                field.OpenTextEditor();
            }
        }

        void OnExpanded(MouseDownEvent evt, InputDescriptor input)
        {
            expandedDescriptors[input] = !m_InputRows[input.id].expanded;
        }

        void DirtyNodes()
        {
            foreach (var node in m_Graph.GetNodes<PropertyNode>())
            {
                node.OnEnable();
                node.Dirty(ModificationScope.Node);
            }
        }

        public BlackboardRow GetBlackboardRow(int guid)
        {
            return m_InputRows[guid];
        }

        
    }
}