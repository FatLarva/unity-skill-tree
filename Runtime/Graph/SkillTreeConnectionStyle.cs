using System;
using UnityEngine;

namespace Game.SkillTree
{
    public enum SkillTreeConnectionMode
    {
        Auto,
        Mirrored,
        Custom
    }

    [Serializable]
    public struct SkillTreeConnectionStyle : IEquatable<SkillTreeConnectionStyle>
    {
        [SerializeField] private SkillTreeConnectionMode _mode;
        [SerializeField] private Vector2 _startHandle;
        [SerializeField] private Vector2 _endHandle;
        [SerializeField] private bool _useNodeColors;
        [SerializeField] private Color _startColor;
        [SerializeField] private Color _endColor;
        [SerializeField, Min(0.1f)] private float _width;

        public static SkillTreeConnectionStyle Default => new()
        {
            Mode = SkillTreeConnectionMode.Auto,
            UseNodeColors = true,
            StartColor = Color.white,
            EndColor = Color.white,
            Width = 1.2f
        };

        public SkillTreeConnectionMode Mode { get => _mode; set => _mode = value; }
        public bool Automatic { get => _mode == SkillTreeConnectionMode.Auto; set => _mode = value ? SkillTreeConnectionMode.Auto : SkillTreeConnectionMode.Custom; }
        public Vector2 StartHandle { get => _startHandle; set => _startHandle = value; }
        public Vector2 EndHandle { get => _endHandle; set => _endHandle = value; }
        public bool UseNodeColors { get => _useNodeColors; set => _useNodeColors = value; }
        public Color StartColor { get => _startColor; set => _startColor = value; }
        public Color EndColor { get => _endColor; set => _endColor = value; }
        public float Width { get => _width; set => _width = value; }

        public SkillTreeConnectionStyle Clone() => this;

        public bool Equals(SkillTreeConnectionStyle other)
        {
            return _mode == other._mode && _startHandle.Equals(other._startHandle) && _endHandle.Equals(other._endHandle) &&
                   _useNodeColors == other._useNodeColors && _startColor.Equals(other._startColor) &&
                   _endColor.Equals(other._endColor) && _width.Equals(other._width);
        }

        public override bool Equals(object obj) => obj is SkillTreeConnectionStyle other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_mode, _startHandle, _endHandle, _useNodeColors, _startColor, _endColor, _width);
    }
}
