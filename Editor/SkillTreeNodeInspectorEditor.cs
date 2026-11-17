using System;
using Game.SkillTree.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Game.SkillTree.Editor
{
    [CustomEditor(typeof(SkillTreeNodeInspectorContext))]
    public sealed class SkillTreeNodeInspectorEditor : SkillTreeInspectorBase
    {
        private UnityEditor.Editor _gameplayEditor;
        private UnityEditor.Editor _appearanceEditor;
        private Action _refreshPanes;

        private void OnEnable()
        {
            if (target is SkillTreeNodeInspectorContext context) context.NodeChanged += OnNodeChanged;
        }

        protected override void OnDisable()
        {
            if (target is SkillTreeNodeInspectorContext context) context.NodeChanged -= OnNodeChanged;
            _refreshPanes = null;
            base.OnDisable();
            ReleasePaneEditors();
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = BeginInspector();
            var context = (SkillTreeNodeInspectorContext)target;
            root.Add(new HelpBox("Edits apply immediately to the two config assets. Native Inspector fields support color picking and dragging numeric labels.", HelpBoxMessageType.Info));
            var noSelection = new HelpBox("Select a node with gameplay and appearance configs to edit its properties.", HelpBoxMessageType.Info);
            root.Add(noSelection);
            var columns = new VisualElement();
            columns.AddToClassList("skill-tree-columns");
            columns.RegisterCallback<GeometryChangedEvent>(change => columns.EnableInClassList("narrow", change.newRect.width < 650f));
            var gameplay = new Foldout { text = "Gameplay", value = true, name = "gameplay" };
            gameplay.AddToClassList("skill-tree-column");
            var appearance = new Foldout { text = "Appearance", value = true, name = "appearance" };
            appearance.AddToClassList("skill-tree-column");
            columns.Add(gameplay);
            columns.Add(appearance);
            root.Add(columns);

            var error = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            error.AddToClassList("skill-tree-hidden");
            root.Add(error);
            root.schedule.Execute(() =>
            {
                if (context == null || context.Tree == null)
                {
                    return;
                }
                bool valid = context.Tree.Validate(out string message);
                error.EnableInClassList("skill-tree-hidden", valid);
                error.text = valid ? string.Empty : message;
            }).Every(200);

            var actions = new VisualElement();
            actions.AddToClassList("skill-tree-actions");
            actions.Add(new Button(() =>
            {
                if (context.Graph != null && context.Node != null && context.Layout != null)
                {
                    context.Graph.SetNodeColor(context.Node.Id, context.Layout.Color, true);
                }
            }) { text = "Color descendants", name = "colorDescendants" });
            actions.Add(new Button(() => Selection.activeObject = context.Node) { text = "Node asset", name = "nodeAsset" });
            actions.Add(new Button(() => Selection.activeObject = context.Layout) { text = "Appearance asset", name = "layoutAsset" });
            root.Add(actions);

            _refreshPanes = () =>
            {
                gameplay.Clear();
                appearance.Clear();
                bool hasSelection = context.Node != null && context.Layout != null;
                noSelection.EnableInClassList("skill-tree-hidden", hasSelection);
                columns.EnableInClassList("skill-tree-hidden", !hasSelection);
                actions.EnableInClassList("skill-tree-hidden", !hasSelection);
                if (!hasSelection)
                {
                    ReleasePaneEditors();
                    return;
                }

                UnityEditor.Editor.CreateCachedEditor(context.Node, null, ref _gameplayEditor);
                UnityEditor.Editor.CreateCachedEditor(context.Layout, null, ref _appearanceEditor);
                gameplay.Add(CreatePane(_gameplayEditor));
                appearance.Add(CreatePane(_appearanceEditor));
            };
            _refreshPanes();
            return root;
        }

        private void OnNodeChanged() => _refreshPanes?.Invoke();

        private void ReleasePaneEditors()
        {
            if (_gameplayEditor != null) DestroyImmediate(_gameplayEditor);
            if (_appearanceEditor != null) DestroyImmediate(_appearanceEditor);
            _gameplayEditor = null;
            _appearanceEditor = null;
        }

        private static InspectorElement CreatePane(UnityEditor.Editor editor)
        {
            var inspector = new InspectorElement(editor);
            // Native field alignment must use this pane's width, not the outer Inspector width.
            inspector.AddToClassList("unity-inspector-main-container");
            return inspector;
        }
    }
}
