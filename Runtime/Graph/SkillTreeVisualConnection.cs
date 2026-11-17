using System;
using UnityEngine;

namespace Game.SkillTree
{
    [Serializable]
    public struct SkillTreeVisualConnection : IEquatable<SkillTreeVisualConnection>
    {
        [SerializeField] private int _parentId;
        [SerializeField] private SkillTreeConnectionStyle _connection;

        public int ParentId => _parentId;
        public SkillTreeConnectionStyle Connection => _connection;

        public SkillTreeVisualConnection(int parentId, SkillTreeConnectionStyle? connection = null)
        {
            _parentId = parentId;
            _connection = connection ?? SkillTreeConnectionStyle.Default;
        }

        public bool Equals(SkillTreeVisualConnection other) => _parentId == other._parentId && _connection.Equals(other._connection);
        public override bool Equals(object obj) => obj is SkillTreeVisualConnection other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_parentId, _connection);
    }
}
