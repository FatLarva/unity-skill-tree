using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree.Data
{
    [Serializable]
    public struct SkillTreeNodeLayout
    {
        [SerializeField] private int _nodeId;
        [SerializeField] private int _parentId;
        [SerializeField] private Vector2 _position;
        [SerializeField] private Color _color;
        [SerializeField] private SkillTreeIconMode _iconMode;
        [SerializeField] private SkillTreeNodeIconRenderer _iconRenderer;
        [SerializeField] private Sprite _iconSprite;
        [SerializeField, Min(0.1f)] private float _iconScale;
        [SerializeField] private float _iconRotation;
        [SerializeField] private SkillTreeConnectionStyle _connection;
        [SerializeField] private SkillTreeVisualConnection[] _additionalParents;

        public int NodeId => _nodeId;
        public int ParentId => _parentId;
        public Vector2 Position => _position;
        public Color Color => _color;
        public SkillTreeIconMode IconMode => _iconMode;
        public SkillTreeNodeIconRenderer IconRenderer => _iconRenderer;
        public Sprite IconSprite => _iconSprite;
        public float IconScale => _iconScale;
        public float IconRotation => _iconRotation;
        public SkillTreeConnectionStyle Connection => _connection;
        public IReadOnlyList<SkillTreeVisualConnection> AdditionalParents => _additionalParents ?? Array.Empty<SkillTreeVisualConnection>();

        public SkillTreeNodeLayout(int nodeId, int parentId, Vector2 position, Color color,
            SkillTreeNodeIconRenderer iconRenderer = null, float iconScale = 1f, float iconRotation = 0f,
            SkillTreeConnectionStyle? connection = null, IReadOnlyList<SkillTreeVisualConnection> additionalParents = null,
            Sprite iconSprite = null, SkillTreeIconMode iconMode = SkillTreeIconMode.GraphicIcon)
        {
            _nodeId = nodeId;
            _parentId = parentId;
            _position = position;
            _color = color;
            _iconMode = iconMode;
            _iconRenderer = iconRenderer;
            _iconSprite = iconSprite;
            _iconScale = iconScale;
            _iconRotation = iconRotation;
            _connection = connection ?? SkillTreeConnectionStyle.Default;
            _additionalParents = CopyConnections(additionalParents);
        }

        public SkillTreeNodeLayout(SkillTreeNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            _nodeId = node.Id;
            _parentId = node.ParentId;
            _position = node.Position;
            _color = node.Color;
            _iconMode = node.IconMode;
            _iconRenderer = node.IconRenderer;
            _iconSprite = node.IconSprite;
            _iconScale = node.IconScale;
            _iconRotation = node.IconRotation;
            _connection = node.Connection;
            _additionalParents = CopyConnections(node.AdditionalParents);
        }

        public static SkillTreeNodeLayout CreateDefault()
        {
            return new SkillTreeNodeLayout(0, 0, Vector2.zero, Color.white);
        }

        public bool Validate(out string error) => SkillTreeConfigValidation.ValidateNodeLayout(this, out error);

        public void ApplyTo(SkillTreeNode node)
        {
            if (node == null || node.Id != _nodeId)
            {
                throw new ArgumentException("The layout must be applied to its matching node.", nameof(node));
            }

            node.ParentId = _parentId;
            node.Position = _position;
            node.Color = _color;
            node.IconMode = _iconMode;
            node.IconRenderer = _iconRenderer;
            node.IconSprite = _iconSprite;
            node.IconScale = _iconScale;
            node.IconRotation = _iconRotation;
            node.Connection = _connection;
            node.AdditionalParents = CopyConnections(_additionalParents);
        }

        public bool Matches(SkillTreeNode node)
        {
            return node != null && _nodeId == node.Id &&
                   _parentId == node.ParentId &&
                   _position == node.Position && _color == node.Color &&
                   _iconMode == node.IconMode && _iconRenderer == node.IconRenderer &&
                   _iconSprite == node.IconSprite && _iconScale == node.IconScale &&
                   _iconRotation == node.IconRotation && ConnectionsMatch(_connection, node.Connection) && AdditionalConnectionsMatch(node.AdditionalParents);
        }

        public SkillTreeNodeLayout Copy() => new(_nodeId, _parentId, _position, _color, _iconRenderer, _iconScale, _iconRotation, _connection, _additionalParents, _iconSprite, _iconMode);

        private bool AdditionalConnectionsMatch(IReadOnlyList<SkillTreeVisualConnection> connections)
        {
            if (AdditionalParents.Count != (connections?.Count ?? 0)) return false;
            for (int index = 0; index < AdditionalParents.Count; index++)
            {
                if (!AdditionalParents[index].Equals(connections[index])) return false;
            }
            return true;
        }

        private static SkillTreeVisualConnection[] CopyConnections(IReadOnlyList<SkillTreeVisualConnection> connections)
        {
            if (connections == null || connections.Count == 0) return Array.Empty<SkillTreeVisualConnection>();
            var copy = new SkillTreeVisualConnection[connections.Count];
            for (int index = 0; index < copy.Length; index++) copy[index] = connections[index];
            return copy;
        }

        private static bool ConnectionsMatch(SkillTreeConnectionStyle first, SkillTreeConnectionStyle second)
        {
            return first.Mode == second.Mode && first.StartHandle == second.StartHandle &&
                   first.EndHandle == second.EndHandle && first.UseNodeColors == second.UseNodeColors &&
                   first.StartColor == second.StartColor && first.EndColor == second.EndColor &&
                   first.Width == second.Width;
        }
    }
}
