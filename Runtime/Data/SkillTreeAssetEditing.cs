using System.Collections.Generic;

namespace Game.SkillTree.Data
{
    public static class SkillTreeAssetEditing
    {
        private static ISkillTreeAssetEditor _editor;

        public static bool Available => _editor != null;

        public static void Register(ISkillTreeAssetEditor editor) => _editor = editor;
        public static SkillNodeConfig CreateNode(SkillTreeConfig tree, SkillTreeGraph graph)
        {
            return _editor?.CreateNode(tree, graph);
        }

        public static bool OpenNode(SkillNodeConfig node) => _editor?.OpenNode(node) ?? false;
        public static bool OpenProperties(SkillTreeConfig tree, int nodeId, SkillTreeGraph graph) =>
            _editor?.OpenProperties(tree, nodeId, graph) ?? false;
        public static bool OpenArrangement(SkillTreeConfig tree, SkillTreeGraph graph) =>
            _editor?.OpenArrangement(tree, graph) ?? false;
        public static void CloseContext(SkillTreeGraph graph) => _editor?.CloseContext(graph);
        public static bool IsPreviewing(SkillTreeGraph graph) => _editor?.IsPreviewing(graph) ?? false;
        public static bool SaveLayout(SkillTreeLayoutConfig layout, IReadOnlyList<SkillTreeNode> nodes,
            string undoName = "Edit skill tree layout") => _editor?.SaveLayout(layout, nodes, undoName) ?? false;
        public static bool UndoLastEdit() => _editor?.UndoLastEdit() ?? false;
        public static bool RedoLastEdit() => _editor?.RedoLastEdit() ?? false;
    }
}
