using System.Collections.Generic;

namespace Game.SkillTree.Data
{
    public interface ISkillTreeAssetEditor
    {
        SkillNodeConfig CreateNode(SkillTreeConfig tree, SkillTreeGraph graph);
        bool OpenNode(SkillNodeConfig node);
        bool OpenProperties(SkillTreeConfig tree, int nodeId, SkillTreeGraph graph);
        bool OpenArrangement(SkillTreeConfig tree, SkillTreeGraph graph);
        void CloseContext(SkillTreeGraph graph);
        bool IsPreviewing(SkillTreeGraph graph);
        bool SaveLayout(SkillTreeLayoutConfig layout, IReadOnlyList<SkillTreeNode> nodes, string undoName);
        bool UndoLastEdit();
        bool RedoLastEdit();
    }
}
