namespace Game.SkillTree
{
    /// <summary>
    /// Logical editing modifiers. Pointer, scroll and navigation events are provided by uGUI's EventSystem.
    /// </summary>
    public interface ISkillTreeModifiers
    {
        bool MoveSubtree { get; }
        bool ToggleAdditionalParent { get; }
    }
}
