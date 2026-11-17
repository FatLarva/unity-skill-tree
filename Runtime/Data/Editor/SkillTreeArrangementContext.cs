using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.SkillTree.Data
{
    public sealed class SkillTreeArrangementContext : SkillTreeInspectorContext
    {
        [SerializeField] private SkillTreeLayoutPreset _preset;
        [SerializeField] private SkillTreeArrangementScope _scope;
        [SerializeField, Min(10)] private float _horizontalSpacing = 160f;
        [SerializeField, Min(10)] private float _verticalSpacing = 150f;
        [SerializeField, Range(0, 360)] private float _branchAngle = 55f;
        [SerializeField, Range(0.1f, 1f)] private float _lengthScale = 0.75f;
        [SerializeField, Min(20)] private float _radius = 300f;
        [SerializeField] private float _rotation;

        private bool _released;
        private List<SkillTreeNode> _beforeEdit;
        private List<SkillTreeNode> _preview;
        private int _selectedId;
        private string _settingsBeforeEdit;

        public bool HasSelection => Graph != null && !(Graph.SelectedId == 0);
        public bool IsPreviewing => _beforeEdit != null;
        public string Error { get; private set; }
        public SkillTreeLayoutPreset Preset => _preset;
        public SkillTreeArrangementScope Scope => _scope;

        public bool UsesHorizontalSpacing
        {
            get
            {
                if (_preset == SkillTreeLayoutPreset.TreeDown || _preset == SkillTreeLayoutPreset.TreeUp)
                {
                    return true;
                }
                if (_preset != SkillTreeLayoutPreset.Fractal || _scope != SkillTreeArrangementScope.All || Graph == null)
                {
                    return false;
                }
                int roots = 0;
                foreach (var node in Graph.Nodes)
                {
                    if ((node.ParentId == 0) && ++roots > 1) return true;
                }
                return false;
            }
        }

        public void CaptureBaseline()
        {
            name = "Skill tree arrangement";
            _released = false;
            Error = null;
        }

        public void BeginEdit()
        {
            if (IsPreviewing || Graph == null || Tree == null || _released) return;
            _beforeEdit = CopyNodes(Graph.Nodes);
            _selectedId = Graph.SelectedId;
            _settingsBeforeEdit = EditorJsonUtility.ToJson(this);
            Error = null;
        }

        public bool Preview()
        {
            if (!IsPreviewing || Graph == null || Tree == null || _released)
            {
                return false;
            }
            try
            {
                _branchAngle = ClampBranchAngle(_branchAngle);
                var settings = new SkillTreeArrangementSettings
                {
                    Preset = _preset,
                    Scope = _scope,
                    HorizontalSpacing = _horizontalSpacing,
                    VerticalSpacing = _verticalSpacing,
                    BranchAngle = _branchAngle,
                    LengthScale = _lengthScale,
                    Radius = _radius,
                    Rotation = _rotation
                };
                var incoming = CopyNodes(_beforeEdit);
                SkillTreeAutoLayout.Apply(incoming, settings, _selectedId);
                Graph.UpdateNodes(incoming);
                _preview = incoming;
                Error = null;
                return true;
            }
            catch (ArgumentException exception)
            {
                Error = exception.Message;
            }
            catch (InvalidOperationException exception)
            {
                Error = exception.Message;
            }
            return false;
        }

        public bool CommitEdit()
        {
            if (!IsPreviewing) return true;
            if (!string.IsNullOrEmpty(Error))
            {
                CancelEdit();
                return false;
            }
            if (_preview != null)
            {
                const string undoName = "Arrange skill tree";
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                if (!SkillTreeAssetEditing.SaveLayout(Tree.Layout, _preview, undoName))
                {
                    Error = "Could not save this arrangement. Check the node layout configs.";
                    CancelEdit();
                    return false;
                }
                string settingsAfterEdit = EditorJsonUtility.ToJson(this);
                if (settingsAfterEdit != _settingsBeforeEdit)
                {
                    // Record the pre-gesture Inspector values in the same operation as
                    // its layout assets, without adding Undo records during preview.
                    EditorJsonUtility.FromJsonOverwrite(_settingsBeforeEdit, this);
                    Undo.RegisterCompleteObjectUndo(this, undoName);
                    EditorJsonUtility.FromJsonOverwrite(settingsAfterEdit, this);
                    Undo.FlushUndoRecordObjects();
                }
                Undo.CollapseUndoOperations(group);
                Undo.IncrementCurrentGroup();
            }
            ClearEdit();
            return true;
        }

        public void CancelEdit(bool restoreSettings = true)
        {
            if (IsPreviewing && Graph != null) Graph.UpdateNodes(_beforeEdit);
            if (restoreSettings && _settingsBeforeEdit != null)
                EditorJsonUtility.FromJsonOverwrite(_settingsBeforeEdit, this);
            ClearEdit();
        }

        public bool Apply()
        {
            if (!CommitEdit()) return false;
            BeginEdit();
            if (Preview()) return CommitEdit();
            CancelEdit();
            return false;
        }

        public override void Release()
        {
            CancelEdit();
            _released = true;
        }

        private void ClearEdit()
        {
            _beforeEdit = null;
            _preview = null;
            _selectedId = 0;
            _settingsBeforeEdit = null;
        }

        private static List<SkillTreeNode> CopyNodes(IReadOnlyList<SkillTreeNode> nodes)
        {
            var copy = new List<SkillTreeNode>(nodes.Count);
            foreach (var node in nodes) copy.Add(node.Clone());
            return copy;
        }

        internal static float ClampBranchAngle(float value) => float.IsNaN(value) ? 0f : Mathf.Clamp(value, 0f, 360f);
    }
}
