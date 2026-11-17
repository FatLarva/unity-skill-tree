using System;
using System.Collections.Generic;
using Game.SkillTree.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.SkillTree.Editor
{
    [InitializeOnLoad]
    internal sealed class SkillTreeAssetEditor : ISkillTreeAssetEditor
    {
        private static readonly SkillTreeAssetEditor Instance;
        private readonly List<TrackedAsset> _editedAssets = new();
        private SkillTreeInspectorContext _context;

        static SkillTreeAssetEditor()
        {
            Instance = new SkillTreeAssetEditor();
            SkillTreeAssetEditing.Register(Instance);
            Undo.undoRedoPerformed += Instance.OnUndoRedo;
            Selection.selectionChanged += Instance.OnSelectionChanged;
            EditorApplication.update += Instance.UpdateContext;
            EditorApplication.playModeStateChanged += Instance.OnPlayModeChanged;
            EditorSceneManager.sceneClosing += Instance.OnSceneClosing;
            AssemblyReloadEvents.beforeAssemblyReload += Instance.Dispose;
        }

        public bool OpenNode(SkillNodeConfig node)
        {
            if (node == null)
            {
                return false;
            }
            CloseContext(null);
            Selection.activeObject = node;
            EditorGUIUtility.PingObject(node);
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            return true;
        }

        public bool OpenProperties(SkillTreeConfig tree, int nodeId, SkillTreeGraph graph)
        {
            if (tree == null || graph == null || tree.Layout == null)
            {
                return false;
            }
            var node = tree.FindNode(nodeId);
            var layout = tree.Layout.Find(nodeId);
            if (node == null || layout == null)
            {
                return false;
            }
            CloseContext(null);
            var context = ScriptableObject.CreateInstance<SkillTreeNodeInspectorContext>();
            context.Initialize(tree, graph);
            context.SetNode(node, layout);
            _context = context;
            graph.Selected += OnGraphSelected;
            ShowContext();
            return true;
        }

        public bool OpenArrangement(SkillTreeConfig tree, SkillTreeGraph graph)
        {
            if (tree == null || graph == null || tree.Layout == null)
            {
                return false;
            }
            CloseContext(null);
            var context = ScriptableObject.CreateInstance<SkillTreeArrangementContext>();
            context.Initialize(tree, graph);
            context.CaptureBaseline();
            _context = context;
            ShowContext();
            return true;
        }

        public void CloseContext(SkillTreeGraph graph)
        {
            if (_context == null || graph != null && _context.Graph != graph)
            {
                return;
            }
            var context = _context;
            _context = null;
            if (context is SkillTreeNodeInspectorContext && context.Graph != null)
            {
                context.Graph.Selected -= OnGraphSelected;
            }
            context.Release();
            Undo.ClearUndo(context);
            UnityEngine.Object.DestroyImmediate(context);
        }

        public bool IsPreviewing(SkillTreeGraph graph)
        {
            return _context is SkillTreeArrangementContext arrangement &&
                   arrangement.Graph == graph && arrangement.IsPreviewing;
        }

        public bool SaveLayout(SkillTreeLayoutConfig layout, IReadOnlyList<SkillTreeNode> nodes, string undoName)
        {
            if (layout == null || nodes == null || !AssetDatabase.Contains(layout))
            {
                return false;
            }
            var changed = new List<SkillNodeLayoutConfig>();
            var values = new List<SkillTreeNodeLayout>();
            foreach (var node in nodes)
            {
                if (node == null)
                {
                    return false;
                }
                var asset = layout.Find(node.Id);
                if (asset == null || !AssetDatabase.Contains(asset))
                {
                    return false;
                }
                if (!asset.Matches(node))
                {
                    var properties = new SkillTreeNodeLayout(node);
                    if (!properties.Validate(out string error))
                    {
                        Debug.LogError($"Cannot save skill tree layout: {error}", layout);
                        return false;
                    }
                    changed.Add(asset);
                    values.Add(properties);
                }
            }
            if (changed.Count == 0)
            {
                return true;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            var targets = new UnityEngine.Object[changed.Count];
            for (int index = 0; index < targets.Length; index++)
            {
                targets[index] = changed[index];
            }
            Undo.RegisterCompleteObjectUndo(targets, undoName);
            for (int index = 0; index < changed.Count; index++)
            {
                changed[index].Configure(values[index]);
            }
            Undo.FlushUndoRecordObjects();
            foreach (var asset in changed)
            {
                MarkEditedAsset(asset);
            }
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
            return true;
        }

        public bool UndoLastEdit()
        {
            Undo.PerformUndo();
            return true;
        }

        public bool RedoLastEdit()
        {
            Undo.PerformRedo();
            return true;
        }

        internal static void SaveEditedAsset(SkillTreeConfigAsset asset)
        {
            if (asset == null || !AssetDatabase.Contains(asset))
            {
                return;
            }
            MarkEditedAsset(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        internal static void MarkEditedAsset(SkillTreeConfigAsset asset)
        {
            Instance.Track(asset);
            asset.NotifyChanged();
            EditorUtility.SetDirty(asset);
        }

        internal static SkillTreeGraph FindGraph(SkillNodeLayoutConfig layout)
        {
            return Instance._context is SkillTreeNodeInspectorContext context && context.Layout == layout
                ? context.Graph
                : null;
        }

        private void ShowContext()
        {
            Selection.activeObject = _context;
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }

        private void OnGraphSelected(SkillTreeNode selected)
        {
            if (_context is not SkillTreeNodeInspectorContext context || context.Tree == null) return;
            int id = selected?.Id ?? 0;
            context.SetNode(id == 0 ? null : context.Tree.FindNode(id),
                id == 0 ? null : context.Tree.Layout?.Find(id));
        }

        private void OnSelectionChanged()
        {
            if (_context != null && Selection.activeObject != _context)
            {
                CloseContext(null);
            }
        }

        private void UpdateContext()
        {
            if (_context != null && (_context.Tree == null || _context.Graph == null || !_context.Graph.Editable))
            {
                CloseContext(null);
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode)
            {
                CloseContext(null);
            }
        }

        private void OnSceneClosing(Scene scene, bool removingScene)
        {
            if (_context != null && _context.Graph != null && _context.Graph.gameObject.scene == scene)
            {
                CloseContext(null);
            }
        }

        private void Track(SkillTreeConfigAsset asset)
        {
            for (var index = _editedAssets.Count - 1; index >= 0; index--)
            {
                var tracked = _editedAssets[index];
                if (!tracked.Reference.TryGetTarget(out var current) || current == null)
                {
                    _editedAssets.RemoveAt(index);
                }
                else if (current == asset)
                {
                    tracked.Snapshot = EditorJsonUtility.ToJson(asset);
                    return;
                }
            }
            _editedAssets.Add(new TrackedAsset(asset));
        }

        private void OnUndoRedo()
        {
            if (_context is SkillTreeArrangementContext arrangement) arrangement.CancelEdit(false);
            bool changed = false;
            for (var index = _editedAssets.Count - 1; index >= 0; index--)
            {
                var tracked = _editedAssets[index];
                if (!tracked.Reference.TryGetTarget(out var asset) || asset == null)
                {
                    _editedAssets.RemoveAt(index);
                    continue;
                }
                string snapshot = EditorJsonUtility.ToJson(asset);
                if (snapshot == tracked.Snapshot)
                {
                    continue;
                }
                tracked.Snapshot = snapshot;
                asset.NotifyChanged();
                EditorUtility.SetDirty(asset);
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssets();
        }

        private void Dispose()
        {
            CloseContext(null);
            Undo.undoRedoPerformed -= OnUndoRedo;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= UpdateContext;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            SkillTreeAssetEditing.Register(null);
            _editedAssets.Clear();
        }

        private sealed class TrackedAsset
        {
            public WeakReference<SkillTreeConfigAsset> Reference { get; }
            public string Snapshot { get; set; }

            public TrackedAsset(SkillTreeConfigAsset asset)
            {
                Reference = new WeakReference<SkillTreeConfigAsset>(asset);
                Snapshot = EditorJsonUtility.ToJson(asset);
            }
        }
    }
}
