using UnityEngine;

namespace Game.SkillTree
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class SkillTreeGeometryPage : UnityEngine.UI.MaskableGraphic
    {
        internal enum LayerKind { Base, PurchaseGlow, Overlay }

        private readonly UnityEngine.UI.VertexHelper _buffer = new();
        private Mesh _mesh;

        internal UnityEngine.UI.VertexHelper Buffer => _buffer;
        internal LayerKind Layer { get; private set; }
        internal bool Overlay => Layer == LayerKind.Overlay;

        internal void Begin(LayerKind layer, float opacity)
        {
            Layer = layer;
            canvasRenderer.SetAlpha(opacity);
            _buffer.Clear();
        }

        internal void Commit()
        {
            if (_mesh == null) _mesh = new Mesh { name = "Skill tree geometry", hideFlags = HideFlags.DontSave };
            _buffer.FillMesh(_mesh);
            SetVerticesDirty();
        }

        protected override void UpdateGeometry() => canvasRenderer.SetMesh(_mesh);

        protected override void OnDestroy()
        {
            _buffer.Dispose();
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }
            base.OnDestroy();
        }
    }
}
