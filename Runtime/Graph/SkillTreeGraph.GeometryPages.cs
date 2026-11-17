using System.Collections.Generic;
using UnityEngine;

namespace Game.SkillTree
{
    public sealed partial class SkillTreeGraph
    {
        private readonly List<SkillTreeGeometryPage> _geometryPages = new();
        private readonly List<SkillTreeGeometryPage> _connectionPages = new();
        private SkillTreeGeometryPage _currentPage;
        private SkillTreeGeometryPage _currentConnectionPage;
        private int _usedPages;
        private int _usedConnectionPages;
        private bool _geometryDirty = true;
        private bool _connectionsDirty = true;

        public override void SetVerticesDirty()
        {
            base.SetVerticesDirty();
            _geometryDirty = true;
            _connectionsDirty = true;
        }

        public override void SetMaterialDirty()
        {
            base.SetMaterialDirty();
            _geometryDirty = true;
            _connectionsDirty = true;
        }

        // Changes to node UI need not redraw the background or tessellate Bezier edges.
        private void SetNodeVisualsDirty(bool connectionsChanged = false)
        {
            _geometryDirty = true;
            _connectionsDirty |= connectionsChanged;
        }

        private void PrepareDirtyGeometry()
        {
            // Factories can replace or disable Graphics, which must happen before
            // either Canvas rebuild loop starts.
            if (_geometryDirty && IsActive()) PrepareGeometry();
            if (_purchaseEffectsDirty && IsActive()) PreparePurchaseEffects();
        }

        private void PrepareGeometry()
        {
            _geometryDirty = false;
            _pixels = new SkillTreeStroke.PixelSpace(this);
            _pixelScale = _pixels.Scale;
            _usedPages = 0;
            _currentPage = null;
            if (_connectionsDirty) PrepareConnections();
            DrawNodeDecorations();
            RefreshNodeDecorations();
            DrawPurchaseGlows();
            RefreshNodeIcons();
            DrawRevealEffects();
            if (Editable && _selectedId != 0)
                DrawConnectionHandles(GeometryFor(128, SkillTreeGeometryPage.LayerKind.Overlay));
            _currentPage?.Commit();
            _currentPage = null;

            int sibling = 0;
            for (int index = 0; index < _usedConnectionPages; index++)
                _connectionPages[index].transform.SetSiblingIndex(sibling++);
            for (int index = 0; index < _usedPages; index++)
                if (!_geometryPages[index].Overlay) _geometryPages[index].transform.SetSiblingIndex(sibling++);
            OrderNodeDecorations(ref sibling);
            OrderNodeIcons(ref sibling);
            for (int index = 0; index < _usedPages; index++)
                if (_geometryPages[index].Overlay) _geometryPages[index].transform.SetSiblingIndex(sibling++);
            for (int index = _usedPages; index < _geometryPages.Count; index++)
                _geometryPages[index].gameObject.SetActive(false);
            _purchaseEffectsDirty = true;
        }

        private void PrepareConnections()
        {
            _connectionsDirty = false;
            _usedConnectionPages = 0;
            _currentConnectionPage = null;
            DrawConnections();
            _currentConnectionPage?.Commit();
            _currentConnectionPage = null;
            for (int index = _usedConnectionPages; index < _connectionPages.Count; index++)
                _connectionPages[index].gameObject.SetActive(false);
        }

        private UnityEngine.UI.VertexHelper ConnectionGeometryFor(int vertices)
        {
            if (_currentConnectionPage == null ||
                _currentConnectionPage.Buffer.currentVertCount + vertices > GeometryBudget)
            {
                _currentConnectionPage?.Commit();
                if (_usedConnectionPages == _connectionPages.Count)
                {
                    var child = new GameObject("Connections", typeof(RectTransform), typeof(SkillTreeGeometryPage));
                    child.hideFlags = HideFlags.DontSave;
                    child.transform.SetParent(transform, false);
                    var page = child.GetComponent<SkillTreeGeometryPage>();
                    page.raycastTarget = false;
                    _connectionPages.Add(page);
                }
                _currentConnectionPage = _connectionPages[_usedConnectionPages++];
                var rect = _currentConnectionPage.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = rectTransform.pivot;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                _currentConnectionPage.material = material;
                _currentConnectionPage.maskable = maskable;
                _currentConnectionPage.gameObject.SetActive(true);
                _currentConnectionPage.Begin(SkillTreeGeometryPage.LayerKind.Base, 1f);
            }
            return _currentConnectionPage.Buffer;
        }

        private UnityEngine.UI.VertexHelper GeometryFor(int vertices,
            SkillTreeGeometryPage.LayerKind layer = SkillTreeGeometryPage.LayerKind.Base)
        {
            if (_currentPage == null || _currentPage.Layer != layer ||
                _currentPage.Buffer.currentVertCount + vertices > GeometryBudget)
            {
                _currentPage?.Commit();
                if (_usedPages == _geometryPages.Count)
                {
                    var child = new GameObject("Geometry", typeof(RectTransform), typeof(SkillTreeGeometryPage));
                    child.hideFlags = HideFlags.DontSave;
                    child.transform.SetParent(transform, false);
                    var page = child.GetComponent<SkillTreeGeometryPage>();
                    page.raycastTarget = false;
                    _geometryPages.Add(page);
                }
                _currentPage = _geometryPages[_usedPages++];
                var rect = _currentPage.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = rectTransform.pivot;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                _currentPage.material = material;
                _currentPage.maskable = maskable;
                _currentPage.gameObject.SetActive(true);
                _currentPage.Begin(layer, layer == SkillTreeGeometryPage.LayerKind.PurchaseGlow ? _purchaseGlowOpacity : 1f);
            }
            return _currentPage.Buffer;
        }

        private void HideGeneratedGraphics()
        {
            foreach (var page in _connectionPages)
                if (page != null) page.gameObject.SetActive(false);
            foreach (var page in _geometryPages)
                if (page != null) page.gameObject.SetActive(false);
            foreach (var icon in _nodeIcons.Values)
                if (icon.Graphic != null) icon.Graphic.gameObject.SetActive(false);
            foreach (var decoration in _nodeDecorations.Values)
                decoration.SetActive(false);
        }
    }
}
